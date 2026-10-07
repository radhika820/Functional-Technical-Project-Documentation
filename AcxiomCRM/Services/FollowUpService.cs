using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IFollowUpService
{
    Task<PagedList<FollowUp>> SearchAsync(FollowUpListQuery q);
    Task<ServiceResult<FollowUp>> GetAsync(int id, bool track = false);
    Task<ServiceResult<FollowUp>> CreateAsync(FollowUpInput input);
    Task<ServiceResult<FollowUp>> UpdateAsync(int id, FollowUpInput input);
    Task<ServiceResult<FollowUp>> CompleteAsync(int id, string? remarks);
    Task<ServiceResult<FollowUp>> MarkMissedAsync(int id);
    Task<ServiceResult<FollowUp>> CancelAsync(int id);
    Task<ServiceResult<FollowUp>> RescheduleAsync(int id, DateTime newDate);
    Task<ServiceResult<FollowUp>> DeleteAsync(int id);
}

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;
    private readonly IAuditService _audit;

    public FollowUpService(ApplicationDbContext db, IScopeService scope, IAuditService audit)
    {
        _db = db; _scope = scope; _audit = audit;
    }

    public Task<PagedList<FollowUp>> SearchAsync(FollowUpListQuery q)
    {
        var query = _scope.FollowUps().AsNoTracking()
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity).Include(f => f.AssignedUser)
            .AsQueryable();

        if (q.PendingOnly) query = query.Where(f => f.Status == FollowUpStatuses.Planned);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var t = q.Q.Trim();
            query = query.Where(f => f.Subject.Contains(t) || (f.Customer != null && f.Customer.CustomerName.Contains(t))
                                     || (f.Lead != null && f.Lead.LeadName.Contains(t)));
        }
        if (q.Status == "Overdue")
        {
            var today = DateTime.Today;
            query = query.Where(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate < today);
        }
        else if (!string.IsNullOrWhiteSpace(q.Status)) query = query.Where(f => f.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Type)) query = query.Where(f => f.FollowUpType == q.Type);
        if (!string.IsNullOrWhiteSpace(q.AssignedTo)) query = query.Where(f => f.AssignedTo == q.AssignedTo);
        if (q.From != null) { var from = q.From.Value.Date; query = query.Where(f => f.FollowUpDate >= from); }
        if (q.To != null) { var to = q.To.Value.Date; query = query.Where(f => f.FollowUpDate <= to); }
        if (q.CustomerId != null) query = query.Where(f => f.CustomerId == q.CustomerId);
        if (q.LeadId != null) query = query.Where(f => f.LeadId == q.LeadId);
        if (q.OpportunityId != null) query = query.Where(f => f.OpportunityId == q.OpportunityId);

        var ordered = (q.Sort?.ToLowerInvariant()) switch
        {
            "date" => Sorting.By(query, f => f.FollowUpDate, q.Desc),
            "subject" => Sorting.By(query, f => f.Subject, q.Desc),
            "type" => Sorting.By(query, f => f.FollowUpType, q.Desc),
            "status" => Sorting.By(query, f => f.Status, q.Desc),
            "owner" => Sorting.By(query, f => f.AssignedUser!.FullName, q.Desc),
            _ => q.PendingOnly ? query.OrderBy(f => f.FollowUpDate) : query.OrderByDescending(f => f.FollowUpDate)
        };
        return PagedList<FollowUp>.CreateAsync(ordered.ThenBy(f => f.FollowUpId), q.Page, q.PageSize);
    }

    public async Task<ServiceResult<FollowUp>> GetAsync(int id, bool track = false)
    {
        var query = _db.FollowUps.Include(f => f.Customer).Include(f => f.Lead).Include(f => f.Opportunity).Include(f => f.AssignedUser)
            .AsQueryable();
        if (!track) query = query.AsNoTracking();
        var f = await query.FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return ServiceResult<FollowUp>.NotFound();
        if (!_scope.CanAccess(f.AssignedTo)) return ServiceResult<FollowUp>.Forbidden();
        return ServiceResult<FollowUp>.Ok(f);
    }

    public async Task<ServiceResult<FollowUp>> CreateAsync(FollowUpInput input)
    {
        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? _scope.UserId : input.AssignedTo!;
        var errors = await ValidateAsync(input, assigned, null, isNew: true);
        if (errors.Count > 0) return ServiceResult<FollowUp>.Invalid(errors);

        var f = new FollowUp { CreatedDate = DateTime.UtcNow, CreatedBy = _scope.UserId };
        Apply(f, input, assigned);
        f.Status = FollowUpStatuses.Planned;
        _db.FollowUps.Add(f);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditActions.Create, AuditEntities.FollowUp, f.FollowUpId.ToString(), null, AuditSnapshot.Of(f));
        return await GetAsync(f.FollowUpId);
    }

    public async Task<ServiceResult<FollowUp>> UpdateAsync(int id, FollowUpInput input)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var f = found.Value!;

        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? f.AssignedTo : input.AssignedTo!;
        var errors = await ValidateAsync(input, assigned, f.AssignedTo, isNew: false);
        if (errors.Count > 0) return ServiceResult<FollowUp>.Invalid(errors);

        var old = AuditSnapshot.Of(f);
        var oldDate = f.FollowUpDate;
        var oldStatus = f.Status;
        Apply(f, input, assigned);
        if (f.Status == FollowUpStatuses.Completed && oldStatus != FollowUpStatuses.Completed) f.CompletedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditActions.Update, AuditEntities.FollowUp, id.ToString(), old, AuditSnapshot.Of(f));
        if (f.Status == FollowUpStatuses.Planned && oldDate.Date != f.FollowUpDate.Date)
            await _audit.LogAsync(AuditActions.Reschedule, AuditEntities.FollowUp, id.ToString(),
                oldDate.ToString("yyyy-MM-dd"), f.FollowUpDate.ToString("yyyy-MM-dd"));
        if (oldStatus != f.Status)
            await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.FollowUp, id.ToString(), oldStatus, f.Status);
        return await GetAsync(id);
    }

    public async Task<ServiceResult<FollowUp>> CompleteAsync(int id, string? remarks)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var f = found.Value!;
        if (f.Status != FollowUpStatuses.Planned)
            return ServiceResult<FollowUp>.Invalid("", "Only planned follow-ups can be completed.");
        if (remarks != null && remarks.Length > 1000)
            return ServiceResult<FollowUp>.Invalid("Remarks", "Remarks cannot exceed 1000 characters.");

        var old = f.Status;
        f.Status = FollowUpStatuses.Completed;
        f.CompletedDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(remarks))
            f.Remarks = Truncate(string.IsNullOrEmpty(f.Remarks) ? remarks.Trim() : f.Remarks + "\n" + remarks.Trim(), 1000);

        // Workflow: update the related CRM record and keep an activity trail.
        string? leadNote = null;
        if (f.LeadId != null)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.LeadId == f.LeadId);
            if (lead != null && lead.Status == LeadStatuses.New)
            {
                lead.Status = LeadStatuses.Contacted;
                lead.ModifiedDate = DateTime.UtcNow;
                leadNote = lead.LeadId.ToString();
            }
        }
        _db.Activities.Add(new Activity
        {
            ActivityType = f.FollowUpType == "Visit" ? "Meeting" : f.FollowUpType,
            Subject = f.Subject,
            Description = Truncate(f.Remarks, 1000),
            ActivityDate = DateTime.Today,
            CustomerId = f.CustomerId,
            LeadId = f.LeadId,
            Status = "Completed",
            AssignedTo = f.AssignedTo,
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync(AuditActions.Complete, AuditEntities.FollowUp, id.ToString(), old, f.Status);
        if (leadNote != null)
            await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.Lead, leadNote, LeadStatuses.New, LeadStatuses.Contacted,
                details: "Auto-updated after follow-up completion");
        return ServiceResult<FollowUp>.Ok(f);
    }

    public async Task<ServiceResult<FollowUp>> MarkMissedAsync(int id)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var f = found.Value!;
        if (f.Status != FollowUpStatuses.Planned)
            return ServiceResult<FollowUp>.Invalid("", "Only planned follow-ups can be marked as missed.");
        if (f.FollowUpDate.Date > DateTime.Today)
            return ServiceResult<FollowUp>.Invalid("", "A follow-up scheduled for a future date cannot be marked as missed.");

        var old = f.Status;
        f.Status = FollowUpStatuses.Missed;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.FollowUp, id.ToString(), old, f.Status, details: "Marked missed");
        return ServiceResult<FollowUp>.Ok(f);
    }

    public async Task<ServiceResult<FollowUp>> CancelAsync(int id)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var f = found.Value!;
        if (f.Status != FollowUpStatuses.Planned)
            return ServiceResult<FollowUp>.Invalid("", "Only planned follow-ups can be cancelled.");

        var old = f.Status;
        f.Status = FollowUpStatuses.Cancelled;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.StatusChange, AuditEntities.FollowUp, id.ToString(), old, f.Status, details: "Cancelled");
        return ServiceResult<FollowUp>.Ok(f);
    }

    public async Task<ServiceResult<FollowUp>> RescheduleAsync(int id, DateTime newDate)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var f = found.Value!;
        if (f.Status != FollowUpStatuses.Planned)
            return ServiceResult<FollowUp>.Invalid("", "Only planned follow-ups can be rescheduled.");
        if (newDate.Date < DateTime.Today)
            return ServiceResult<FollowUp>.Invalid("FollowUpDate", Rules.FollowUpDateMessage);

        var old = f.FollowUpDate;
        f.FollowUpDate = newDate.Date;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Reschedule, AuditEntities.FollowUp, id.ToString(),
            old.ToString("yyyy-MM-dd"), f.FollowUpDate.ToString("yyyy-MM-dd"));
        return ServiceResult<FollowUp>.Ok(f);
    }

    public async Task<ServiceResult<FollowUp>> DeleteAsync(int id)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var f = found.Value!;
        var old = AuditSnapshot.Of(f);
        _db.FollowUps.Remove(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.FollowUp, id.ToString(), old, null);
        return ServiceResult<FollowUp>.Ok(f);
    }

    private async Task<List<(string, string)>> ValidateAsync(FollowUpInput i, string assigned, string? keepOwner, bool isNew)
    {
        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(i.Subject)) errs.Add(("Subject", "Subject is required."));
        if (!FollowUpStatuses.All.Contains(i.Status)) errs.Add(("Status", "Select a valid status."));
        if (!FollowUpTypes.All.Contains(i.FollowUpType)) errs.Add(("FollowUpType", "Select a valid follow-up type."));
        if (i.CustomerId == null && i.LeadId == null && i.OpportunityId == null)
            errs.Add(("CustomerId", "Select a customer, lead or opportunity for this follow-up."));

        // New follow-ups are always Planned (no historical-entry process is supported) and may not be dated in the past.
        if (isNew && i.Status != FollowUpStatuses.Planned)
            errs.Add(("Status", "New follow-ups must be created with the Planned status."));
        if (i.Status == FollowUpStatuses.Planned && i.FollowUpDate.Date < DateTime.Today)
            errs.Add(("FollowUpDate", Rules.FollowUpDateMessage));

        if (!_scope.CanAssignTo(assigned) && assigned != keepOwner)
            errs.Add(("AssignedTo", "You cannot assign records to the selected user."));

        if (i.CustomerId != null)
        {
            var owner = await _db.Customers.AsNoTracking().Where(c => c.CustomerId == i.CustomerId).Select(c => c.AssignedTo).FirstOrDefaultAsync();
            if (owner == null || !_scope.CanAccess(owner)) errs.Add(("CustomerId", "Select a valid customer."));
        }
        if (i.LeadId != null)
        {
            var owner = await _db.Leads.AsNoTracking().Where(l => l.LeadId == i.LeadId).Select(l => l.AssignedTo).FirstOrDefaultAsync();
            if (owner == null || !_scope.CanAccess(owner)) errs.Add(("LeadId", "Select a valid lead."));
        }
        if (i.OpportunityId != null)
        {
            var owner = await _db.Opportunities.AsNoTracking().Where(o => o.OpportunityId == i.OpportunityId).Select(o => o.AssignedTo).FirstOrDefaultAsync();
            if (owner == null || !_scope.CanAccess(owner)) errs.Add(("OpportunityId", "Select a valid opportunity."));
        }
        return errs;
    }

    private static void Apply(FollowUp f, FollowUpInput i, string assigned)
    {
        f.Subject = i.Subject.Trim();
        f.CustomerId = i.CustomerId;
        f.LeadId = i.LeadId;
        f.OpportunityId = i.OpportunityId;
        f.FollowUpDate = i.FollowUpDate.Date;
        f.FollowUpType = i.FollowUpType;
        f.Status = i.Status;
        f.Remarks = Text.Clean(i.Remarks);
        f.AssignedTo = assigned;
    }

    private static string? Truncate(string? s, int max) => s is null ? null : (s.Length <= max ? s : s[..max]);
}
