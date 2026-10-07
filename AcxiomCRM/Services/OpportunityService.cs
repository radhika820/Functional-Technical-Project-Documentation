using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IOpportunityService
{
    Task<PagedList<Opportunity>> SearchAsync(OpportunityListQuery q);
    Task<ServiceResult<Opportunity>> GetAsync(int id, bool track = false);
    Task<ServiceResult<Opportunity>> CreateAsync(OpportunityInput input);
    Task<ServiceResult<Opportunity>> UpdateAsync(int id, OpportunityInput input);
    Task<ServiceResult<Opportunity>> DeleteAsync(int id);
    Task<ServiceResult<Opportunity>> MoveStageAsync(int id, string stage);
}

public class OpportunityService : IOpportunityService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;
    private readonly IAuditService _audit;

    public OpportunityService(ApplicationDbContext db, IScopeService scope, IAuditService audit)
    {
        _db = db; _scope = scope; _audit = audit;
    }

    public Task<PagedList<Opportunity>> SearchAsync(OpportunityListQuery q)
    {
        var query = _scope.Opportunities().AsNoTracking().Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var t = q.Q.Trim();
            query = query.Where(o => o.OpportunityName.Contains(t) || o.Customer!.CustomerName.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(q.Stage)) query = query.Where(o => o.Stage == q.Stage);
        if (!string.IsNullOrWhiteSpace(q.Status)) query = query.Where(o => o.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.AssignedTo)) query = query.Where(o => o.AssignedTo == q.AssignedTo);

        var ordered = (q.Sort?.ToLowerInvariant()) switch
        {
            "name" => Sorting.By(query, o => o.OpportunityName, q.Desc),
            "customer" => Sorting.By(query, o => o.Customer!.CustomerName, q.Desc),
            "stage" => Sorting.By(query, o => o.Stage, q.Desc),
            "amount" => Sorting.By(query, o => o.Amount, q.Desc),
            "probability" => Sorting.By(query, o => o.Probability, q.Desc),
            "close" => Sorting.By(query, o => o.ExpectedCloseDate, q.Desc),
            "owner" => Sorting.By(query, o => o.AssignedUser!.FullName, q.Desc),
            _ => query.OrderByDescending(o => o.CreatedDate)
        };
        return PagedList<Opportunity>.CreateAsync(ordered.ThenBy(o => o.OpportunityId), q.Page, q.PageSize);
    }

    public async Task<ServiceResult<Opportunity>> GetAsync(int id, bool track = false)
    {
        var query = _db.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).Include(o => o.Lead).AsQueryable();
        if (!track) query = query.AsNoTracking();
        var o = await query.FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null) return ServiceResult<Opportunity>.NotFound();
        if (!_scope.CanAccess(o.AssignedTo)) return ServiceResult<Opportunity>.Forbidden();
        return ServiceResult<Opportunity>.Ok(o);
    }

    public async Task<ServiceResult<Opportunity>> CreateAsync(OpportunityInput input)
    {
        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? _scope.UserId : input.AssignedTo!;
        var (errors, customer) = await ValidateAsync(input, assigned, null, null);
        if (errors.Count > 0) return ServiceResult<Opportunity>.Invalid(errors);

        var o = new Opportunity { CreatedDate = DateTime.UtcNow, CreatedBy = _scope.UserId };
        Apply(o, input, assigned);
        o.Customer = customer;
        _db.Opportunities.Add(o);
        await _db.SaveChangesAsync();

        await _db.Entry(o).Reference(x => x.AssignedUser).LoadAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Opportunity, o.OpportunityId.ToString(), null, AuditSnapshot.Of(o));
        return ServiceResult<Opportunity>.Ok(o);
    }

    public async Task<ServiceResult<Opportunity>> UpdateAsync(int id, OpportunityInput input)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var o = found.Value!;

        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? o.AssignedTo : input.AssignedTo!;
        var (errors, customer) = await ValidateAsync(input, assigned, o.CustomerId, o.AssignedTo);
        if (errors.Count > 0) return ServiceResult<Opportunity>.Invalid(errors);

        var old = AuditSnapshot.Of(o);
        var oldStage = o.Stage;
        Apply(o, input, assigned);
        o.Customer = customer;
        o.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _db.Entry(o).Reference(x => x.AssignedUser).LoadAsync();
        await _audit.LogAsync(AuditActions.Update, AuditEntities.Opportunity, id.ToString(), old, AuditSnapshot.Of(o));
        if (oldStage != o.Stage)
            await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Opportunity, id.ToString(), oldStage, o.Stage, details: "Stage change");
        return ServiceResult<Opportunity>.Ok(o);
    }

    public async Task<ServiceResult<Opportunity>> MoveStageAsync(int id, string stage)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var o = found.Value!;

        if (!OpportunityStages.IsValid(stage)) return ServiceResult<Opportunity>.Invalid("Stage", "Select a valid stage.");
        if (!o.IsActive)
            return ServiceResult<Opportunity>.Invalid("Stage", "Closed (Won/Lost) opportunities cannot be moved. Edit the opportunity to reopen it.");

        var oldStage = o.Stage;
        var old = AuditSnapshot.Of(o);
        SetStage(o, stage, o.Probability);
        o.ModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Opportunity, id.ToString(), old, AuditSnapshot.Of(o),
            details: $"Stage {oldStage} -> {stage}");
        return ServiceResult<Opportunity>.Ok(o);
    }

    public async Task<ServiceResult<Opportunity>> DeleteAsync(int id)
    {
        if (_scope.IsSales) return ServiceResult<Opportunity>.Forbidden(); // sales pipeline records are deleted by Admin/Manager only

        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var o = found.Value!;

        if (await _db.FollowUps.AnyAsync(f => f.OpportunityId == id))
            return ServiceResult<Opportunity>.Invalid("", "This opportunity has follow-ups. Remove them first.");

        var old = AuditSnapshot.Of(o);
        _db.Opportunities.Remove(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Opportunity, id.ToString(), old, null);
        return ServiceResult<Opportunity>.Ok(o);
    }

    private async Task<(List<(string, string)> errors, Customer? customer)> ValidateAsync(
        OpportunityInput i, string assigned, int? currentCustomerId, string? keepOwner)
    {
        var errs = new List<(string, string)>();

        if (string.IsNullOrWhiteSpace(i.OpportunityName)) errs.Add(("OpportunityName", "Opportunity Name is required."));
        if (i.Amount <= 0 || i.Amount > (decimal)Rules.MaxAmount) errs.Add(("Amount", Rules.AmountMessage));
        if (i.Probability < 0 || i.Probability > 100) errs.Add(("Probability", Rules.ProbabilityMessage));
        if (!OpportunityStages.IsValid(i.Stage)) errs.Add(("Stage", "Select a valid stage."));
        else if (OpportunityStages.IsActive(i.Stage) && i.ExpectedCloseDate.Date < DateTime.Today)
            errs.Add(("ExpectedCloseDate", Rules.CloseDateMessage));
        if (!_scope.CanAssignTo(assigned) && assigned != keepOwner)
            errs.Add(("AssignedTo", "You cannot assign records to the selected user."));

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == i.CustomerId);
        if (customer == null) errs.Add(("CustomerId", "Select a valid customer."));
        else if (!_scope.CanAccess(customer.AssignedTo) && customer.CustomerId != currentCustomerId)
            errs.Add(("CustomerId", "You do not have access to the selected customer."));
        else if (customer.Status != CustomerStatuses.Active && customer.CustomerId != currentCustomerId)
            errs.Add(("CustomerId", "The selected customer is inactive."));

        if (i.LeadId != null)
        {
            var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == i.LeadId);
            if (lead == null || !_scope.CanAccess(lead.AssignedTo)) errs.Add(("LeadId", "Select a valid lead."));
        }
        return (errs, customer);
    }

    private static void Apply(Opportunity o, OpportunityInput i, string assigned)
    {
        o.OpportunityName = i.OpportunityName.Trim();
        o.CustomerId = i.CustomerId;
        o.LeadId = i.LeadId;
        o.Amount = i.Amount;
        o.ExpectedCloseDate = i.ExpectedCloseDate.Date;
        o.Source = Text.Clean(i.Source);
        o.Notes = Text.Clean(i.Notes);
        o.AssignedTo = assigned;
        SetStage(o, i.Stage, i.Probability);
    }

    /// <summary>Keeps Status/Probability/ClosedDate consistent with the stage (Won=100%, Lost=0%).</summary>
    private static void SetStage(Opportunity o, string stage, int probability)
    {
        o.Stage = stage;
        o.Status = OpportunityStatuses.FromStage(stage);
        o.Probability = stage == OpportunityStages.Won ? 100 : stage == OpportunityStages.Lost ? 0 : probability;
        o.ClosedDate = OpportunityStages.IsActive(stage) ? null : (o.ClosedDate ?? DateTime.UtcNow);
    }
}
