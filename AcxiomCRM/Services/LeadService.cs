using System.Text.RegularExpressions;
using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public record ConversionResult(Lead Lead, Customer Customer, Opportunity Opportunity, bool CustomerWasExisting);

public interface ILeadService
{
    Task<PagedList<Lead>> SearchAsync(ListQuery q);
    Task<ServiceResult<Lead>> GetAsync(int id, bool track = false);
    Task<ServiceResult<Lead>> CreateAsync(LeadInput input);
    Task<ServiceResult<Lead>> UpdateAsync(int id, LeadInput input);
    Task<ServiceResult<Lead>> DeleteAsync(int id);
    Task<ServiceResult<Lead>> ChangeStatusAsync(int id, string status);
    Task<ServiceResult<ConversionResult>> ConvertAsync(int id, ConvertLeadInput input);
}

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;
    private readonly IAuditService _audit;

    public LeadService(ApplicationDbContext db, IScopeService scope, IAuditService audit)
    {
        _db = db; _scope = scope; _audit = audit;
    }

    public Task<PagedList<Lead>> SearchAsync(ListQuery q)
    {
        var query = _scope.Leads().AsNoTracking().Include(l => l.AssignedUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var t = q.Q.Trim();
            query = query.Where(l => l.LeadName.Contains(t) || l.LeadCode.Contains(t) || l.Email.Contains(t)
                                     || l.Phone.Contains(t) || (l.CompanyName != null && l.CompanyName.Contains(t)));
        }
        if (!string.IsNullOrWhiteSpace(q.Status)) query = query.Where(l => l.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.AssignedTo)) query = query.Where(l => l.AssignedTo == q.AssignedTo);

        var ordered = (q.Sort?.ToLowerInvariant()) switch
        {
            "code" => Sorting.By(query, l => l.LeadCode, q.Desc),
            "name" => Sorting.By(query, l => l.LeadName, q.Desc),
            "company" => Sorting.By(query, l => l.CompanyName, q.Desc),
            "source" => Sorting.By(query, l => l.Source, q.Desc),
            "status" => Sorting.By(query, l => l.Status, q.Desc),
            "priority" => Sorting.By(query, l => l.Priority, q.Desc),
            "value" => Sorting.By(query, l => l.ExpectedValue, q.Desc),
            "owner" => Sorting.By(query, l => l.AssignedUser!.FullName, q.Desc),
            "created" => Sorting.By(query, l => l.CreatedDate, q.Desc),
            _ => query.OrderByDescending(l => l.CreatedDate)
        };
        return PagedList<Lead>.CreateAsync(ordered.ThenBy(l => l.LeadId), q.Page, q.PageSize);
    }

    public async Task<ServiceResult<Lead>> GetAsync(int id, bool track = false)
    {
        var query = _db.Leads.Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer).AsQueryable();
        if (!track) query = query.AsNoTracking();
        var l = await query.FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return ServiceResult<Lead>.NotFound();
        if (!_scope.CanAccess(l.AssignedTo)) return ServiceResult<Lead>.Forbidden();
        return ServiceResult<Lead>.Ok(l);
    }

    public async Task<ServiceResult<Lead>> CreateAsync(LeadInput input)
    {
        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? _scope.UserId : input.AssignedTo!;
        var errors = Validate(input, assigned, null, null);
        if (errors.Count > 0) return ServiceResult<Lead>.Invalid(errors);

        var l = new Lead
        {
            LeadCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _scope.UserId
        };
        Apply(l, input, assigned);
        _db.Leads.Add(l);
        await _db.SaveChangesAsync();
        l.LeadCode = $"LEAD-{l.LeadId:D5}";
        await _db.SaveChangesAsync();

        await _db.Entry(l).Reference(x => x.AssignedUser).LoadAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Lead, l.LeadId.ToString(), null, AuditSnapshot.Of(l));
        return ServiceResult<Lead>.Ok(l);
    }

    public async Task<ServiceResult<Lead>> UpdateAsync(int id, LeadInput input)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var l = found.Value!;

        if (l.Status == LeadStatuses.Converted)
            return ServiceResult<Lead>.Invalid("", "Converted leads are read-only.");

        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? l.AssignedTo : input.AssignedTo!;
        var errors = Validate(input, assigned, l.Status, l.AssignedTo);
        if (errors.Count > 0) return ServiceResult<Lead>.Invalid(errors);

        var old = AuditSnapshot.Of(l);
        var oldStatus = l.Status;
        Apply(l, input, assigned);
        l.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _db.Entry(l).Reference(x => x.AssignedUser).LoadAsync();
        await _audit.LogAsync(AuditActions.Update, AuditEntities.Lead, id.ToString(), old, AuditSnapshot.Of(l));
        if (oldStatus != l.Status)
            await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Lead, id.ToString(), oldStatus, l.Status);
        return ServiceResult<Lead>.Ok(l);
    }

    public async Task<ServiceResult<Lead>> ChangeStatusAsync(int id, string status)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var l = found.Value!;

        if (!LeadStatuses.IsValid(status)) return ServiceResult<Lead>.Invalid("Status", "Select a valid lead status.");
        if (status == LeadStatuses.Converted) return ServiceResult<Lead>.Invalid("Status", "Use the Convert action to convert a lead.");
        if (!LeadStatuses.CanTransition(l.Status, status))
            return ServiceResult<Lead>.Invalid("Status", $"A lead cannot move from {l.Status} to {status}.");

        var old = l.Status;
        l.Status = status;
        l.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Lead, id.ToString(), old, status);
        return ServiceResult<Lead>.Ok(l);
    }

    public async Task<ServiceResult<Lead>> DeleteAsync(int id)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var l = found.Value!;

        if (l.Status == LeadStatuses.Converted)
            return ServiceResult<Lead>.Invalid("", "Converted leads cannot be deleted because they are part of the sales history.");
        if (await _db.FollowUps.AnyAsync(f => f.LeadId == id) || await _db.Activities.AnyAsync(a => a.LeadId == id))
            return ServiceResult<Lead>.Invalid("", "This lead has follow-ups or activities. Remove them first, or mark the lead as Lost instead.");

        var old = AuditSnapshot.Of(l);
        _db.Leads.Remove(l);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Lead, id.ToString(), old, null);
        return ServiceResult<Lead>.Ok(l);
    }

    /// <summary>Lead-to-Customer workflow: Qualified lead -> customer (new or existing) + opportunity, audited.</summary>
    public async Task<ServiceResult<ConversionResult>> ConvertAsync(int id, ConvertLeadInput input)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found.As<ConversionResult>();
        var lead = found.Value!;

        var errs = new List<(string, string)>();
        if (lead.Status != LeadStatuses.Qualified) errs.Add(("", "Only Qualified leads can be converted."));
        if (input.Amount <= 0) errs.Add(("Amount", Rules.AmountMessage));
        if (input.Probability < 0 || input.Probability > 100) errs.Add(("Probability", Rules.ProbabilityMessage));
        if (input.ExpectedCloseDate.Date < DateTime.Today) errs.Add(("ExpectedCloseDate", Rules.CloseDateMessage));
        if (string.IsNullOrWhiteSpace(input.OpportunityName)) errs.Add(("OpportunityName", "Opportunity Name is required."));

        var email = Rules.NormalizeEmail(lead.Email);
        var phone = Rules.NormalizePhone(lead.Phone);
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email || c.Phone == phone);
        if (existing != null && !_scope.CanAccess(existing.AssignedTo))
            errs.Add(("", "A customer with this email or phone already exists outside your scope. Ask your manager to link it."));
        if (existing != null && existing.Status != CustomerStatuses.Active)
            errs.Add(("", "The matching customer record is inactive. Reactivate it before converting this lead."));
        if (errs.Count > 0) return ServiceResult<ConversionResult>.Invalid(errs);

        await using var tx = await _db.Database.BeginTransactionAsync();
        var oldLead = AuditSnapshot.Of(lead);

        var customer = existing;
        var created = false;
        if (customer == null)
        {
            customer = new Customer
            {
                CustomerCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                CustomerName = lead.LeadName,
                Email = email,
                Phone = phone,
                CompanyName = lead.CompanyName,
                Status = CustomerStatuses.Active,
                Notes = $"Converted from lead {lead.LeadCode}.",
                AssignedTo = lead.AssignedTo,
                CreatedBy = _scope.UserId,
                CreatedDate = DateTime.UtcNow
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            customer.CustomerCode = $"CUST-{customer.CustomerId:D5}";
            created = true;
        }

        var opp = new Opportunity
        {
            OpportunityName = input.OpportunityName.Trim(),
            CustomerId = customer.CustomerId,
            LeadId = lead.LeadId,
            Amount = input.Amount,
            Stage = OpportunityStages.Qualification,
            Status = OpportunityStatuses.Open,
            Probability = input.Probability,
            ExpectedCloseDate = input.ExpectedCloseDate.Date,
            Source = lead.Source,
            AssignedTo = lead.AssignedTo,
            CreatedBy = _scope.UserId,
            CreatedDate = DateTime.UtcNow
        };
        _db.Opportunities.Add(opp);

        lead.Status = LeadStatuses.Converted;
        lead.ConvertedCustomerId = customer.CustomerId;
        lead.ConvertedDate = DateTime.UtcNow;
        lead.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        if (created)
            await _audit.LogAsync(AuditActions.Create, AuditEntities.Customer, customer.CustomerId.ToString(), null, AuditSnapshot.Of(customer),
                details: $"Created from lead {lead.LeadCode}");
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Opportunity, opp.OpportunityId.ToString(), null, AuditSnapshot.Of(opp),
            details: $"Created from lead {lead.LeadCode}");
        await _audit.LogAsync(AuditActions.Convert, AuditEntities.Lead, lead.LeadId.ToString(), oldLead, AuditSnapshot.Of(lead),
            details: $"Converted to customer {customer.CustomerCode} and opportunity #{opp.OpportunityId}");

        return ServiceResult<ConversionResult>.Ok(new ConversionResult(lead, customer, opp, !created));
    }

    private List<(string, string)> Validate(LeadInput i, string assigned, string? currentStatus, string? keepOwner)
    {
        var errs = new List<(string, string)>();
        var email = Rules.NormalizeEmail(i.Email);
        var phone = Rules.NormalizePhone(i.Phone);

        if (string.IsNullOrWhiteSpace(i.LeadName)) errs.Add(("LeadName", "Lead Name is required."));
        else if (i.LeadName.Trim().Length > 150) errs.Add(("LeadName", "Lead Name must be between 2 and 150 characters."));
        if (!Regex.IsMatch(email, Rules.EmailPattern) || email.Length > 150) errs.Add(("Email", Rules.EmailMessage));
        if (!Regex.IsMatch(phone, Rules.PhonePattern)) errs.Add(("Phone", Rules.PhoneMessage));
        if (i.ExpectedValue < 0 || i.ExpectedValue > (decimal)Rules.MaxLeadValue)
            errs.Add(("ExpectedValue", "Expected Value must be between 0 and 100,000,000."));
        if (!LeadStatuses.IsValid(i.Status)) errs.Add(("Status", "Select a valid lead status."));
        else if (i.Status == LeadStatuses.Converted) errs.Add(("Status", "Use the Convert action to convert a lead."));
        else if (currentStatus != null && !LeadStatuses.CanTransition(currentStatus, i.Status))
            errs.Add(("Status", $"A lead cannot move from {currentStatus} to {i.Status}."));
        if (!LeadSources.All.Contains(i.Source)) errs.Add(("Source", "Select a valid lead source."));
        if (!LeadPriorities.All.Contains(i.Priority)) errs.Add(("Priority", "Priority must be Low, Medium or High."));
        if (!_scope.CanAssignTo(assigned) && assigned != keepOwner)
            errs.Add(("AssignedTo", "You cannot assign records to the selected user."));
        return errs;
    }

    private static void Apply(Lead l, LeadInput i, string assigned)
    {
        l.LeadName = i.LeadName.Trim();
        l.Email = Rules.NormalizeEmail(i.Email);
        l.Phone = Rules.NormalizePhone(i.Phone);
        l.CompanyName = Text.Clean(i.CompanyName);
        l.Source = i.Source;
        l.Status = i.Status;
        l.Priority = i.Priority;
        l.ExpectedValue = i.ExpectedValue;
        l.Notes = Text.Clean(i.Notes);
        l.AssignedTo = assigned;
    }
}
