using System.Text.RegularExpressions;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface ICustomerService
{
    Task<PagedList<Customer>> SearchAsync(ListQuery q);
    Task<ServiceResult<Customer>> GetAsync(int id, bool track = false);
    Task<ServiceResult<Customer>> CreateAsync(CustomerInput input);
    Task<ServiceResult<Customer>> UpdateAsync(int id, CustomerInput input);
    Task<ServiceResult<Customer>> DeactivateAsync(int id);
}

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;
    private readonly IAuditService _audit;

    public CustomerService(ApplicationDbContext db, IScopeService scope, IAuditService audit)
    {
        _db = db; _scope = scope; _audit = audit;
    }

    public Task<PagedList<Customer>> SearchAsync(ListQuery q)
    {
        var query = _scope.Customers().AsNoTracking().Include(c => c.AssignedUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var t = q.Q.Trim();
            query = query.Where(c => c.CustomerName.Contains(t) || c.Email.Contains(t) || c.Phone.Contains(t)
                                     || c.CustomerCode.Contains(t) || (c.CompanyName != null && c.CompanyName.Contains(t)));
        }
        if (!string.IsNullOrWhiteSpace(q.Status)) query = query.Where(c => c.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.AssignedTo)) query = query.Where(c => c.AssignedTo == q.AssignedTo);

        var ordered = (q.Sort?.ToLowerInvariant()) switch
        {
            "code" => Sorting.By(query, c => c.CustomerCode, q.Desc),
            "name" => Sorting.By(query, c => c.CustomerName, q.Desc),
            "email" => Sorting.By(query, c => c.Email, q.Desc),
            "company" => Sorting.By(query, c => c.CompanyName, q.Desc),
            "status" => Sorting.By(query, c => c.Status, q.Desc),
            "owner" => Sorting.By(query, c => c.AssignedUser!.FullName, q.Desc),
            "created" => Sorting.By(query, c => c.CreatedDate, q.Desc),
            _ => query.OrderByDescending(c => c.CreatedDate)
        };
        return PagedList<Customer>.CreateAsync(ordered.ThenBy(c => c.CustomerId), q.Page, q.PageSize);
    }

    public async Task<ServiceResult<Customer>> GetAsync(int id, bool track = false)
    {
        var query = _db.Customers.Include(c => c.AssignedUser).AsQueryable();
        if (!track) query = query.AsNoTracking();
        var c = await query.FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return ServiceResult<Customer>.NotFound();
        if (!_scope.CanAccess(c.AssignedTo)) return ServiceResult<Customer>.Forbidden();
        return ServiceResult<Customer>.Ok(c);
    }

    public async Task<ServiceResult<Customer>> CreateAsync(CustomerInput input)
    {
        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? _scope.UserId : input.AssignedTo!;
        var errors = await ValidateAsync(input, assigned, 0);
        if (errors.Count > 0) return ServiceResult<Customer>.Invalid(errors);

        var c = new Customer
        {
            CustomerCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _scope.UserId
        };
        Apply(c, input, assigned);

        _db.Customers.Add(c);
        try
        {
            await _db.SaveChangesAsync();
            c.CustomerCode = $"CUST-{c.CustomerId:D5}";
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(c).State = EntityState.Detached;
            return ServiceResult<Customer>.Invalid("Email", "A customer with this email or phone already exists.");
        }

        await _db.Entry(c).Reference(x => x.AssignedUser).LoadAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Customer, c.CustomerId.ToString(), null, AuditSnapshot.Of(c));
        return ServiceResult<Customer>.Ok(c);
    }

    public async Task<ServiceResult<Customer>> UpdateAsync(int id, CustomerInput input)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var c = found.Value!;

        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? c.AssignedTo : input.AssignedTo!;
        // keep the current owner even if the actor could not normally assign to them (e.g. manager editing a team record)
        var errors = await ValidateAsync(input, assigned, id, keepOwner: c.AssignedTo);
        if (errors.Count > 0) return ServiceResult<Customer>.Invalid(errors);

        var oldSnap = AuditSnapshot.Of(c);
        var oldStatus = c.Status;
        Apply(c, input, assigned);
        c.ModifiedDate = DateTime.UtcNow;
        c.ModifiedBy = _scope.UserId;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return ServiceResult<Customer>.Invalid("Email", "A customer with this email or phone already exists."); }

        await _db.Entry(c).Reference(x => x.AssignedUser).LoadAsync();
        await _audit.LogAsync(AuditActions.Update, AuditEntities.Customer, id.ToString(), oldSnap, AuditSnapshot.Of(c));
        if (oldStatus != c.Status)
            await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Customer, id.ToString(), oldStatus, c.Status);
        return ServiceResult<Customer>.Ok(c);
    }

    public async Task<ServiceResult<Customer>> DeactivateAsync(int id)
    {
        if (_scope.IsSales) return ServiceResult<Customer>.Forbidden(); // only Admin/Manager may deactivate customers

        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var c = found.Value!;

        var hasOpen = await _db.Opportunities.AnyAsync(o => o.CustomerId == id && OpportunityStages.Active.Contains(o.Stage));
        if (hasOpen)
            return ServiceResult<Customer>.Invalid("", "This customer has open opportunities and cannot be deactivated. Close or reassign them first.");

        var old = AuditSnapshot.Of(c);
        c.Status = CustomerStatuses.Inactive;
        c.ModifiedDate = DateTime.UtcNow;
        c.ModifiedBy = _scope.UserId;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Deactivate, AuditEntities.Customer, id.ToString(), old, AuditSnapshot.Of(c));
        return ServiceResult<Customer>.Ok(c);
    }

    private async Task<List<(string, string)>> ValidateAsync(CustomerInput i, string assigned, int excludeId, string? keepOwner = null)
    {
        var errs = new List<(string, string)>();
        var email = Rules.NormalizeEmail(i.Email);
        var phone = Rules.NormalizePhone(i.Phone);
        var name = (i.CustomerName ?? "").Trim();
        var company = Text.Clean(i.CompanyName);

        if (name.Length < 2 || name.Length > 150) errs.Add(("CustomerName", "Customer Name must be between 2 and 150 characters."));
        if (!Regex.IsMatch(email, Rules.EmailPattern) || email.Length > 150) errs.Add(("Email", Rules.EmailMessage));
        if (!Regex.IsMatch(phone, Rules.PhonePattern)) errs.Add(("Phone", Rules.PhoneMessage));
        if (!CustomerStatuses.All.Contains(i.Status)) errs.Add(("Status", "Status must be Active or Inactive."));
        if (!_scope.CanAssignTo(assigned) && assigned != keepOwner)
            errs.Add(("AssignedTo", "You cannot assign records to the selected user."));
        if (errs.Count > 0) return errs;

        if (await _db.Customers.AnyAsync(c => c.Email == email && c.CustomerId != excludeId))
            errs.Add(("Email", "A customer with this email already exists."));
        if (await _db.Customers.AnyAsync(c => c.Phone == phone && c.CustomerId != excludeId))
            errs.Add(("Phone", "A customer with this phone number already exists."));
        if (await _db.Customers.AnyAsync(c => c.CustomerName == name && c.CompanyName == company && c.CustomerId != excludeId))
            errs.Add(("CustomerName", "A customer with the same name and company already exists."));
        return errs;
    }

    private static void Apply(Customer c, CustomerInput i, string assigned)
    {
        c.CustomerName = i.CustomerName.Trim();
        c.Email = Rules.NormalizeEmail(i.Email);
        c.Phone = Rules.NormalizePhone(i.Phone);
        c.CompanyName = Text.Clean(i.CompanyName);
        c.Address = Text.Clean(i.Address);
        c.City = Text.Clean(i.City);
        c.State = Text.Clean(i.State);
        c.Status = i.Status;
        c.Notes = Text.Clean(i.Notes);
        c.AssignedTo = assigned;
    }
}
