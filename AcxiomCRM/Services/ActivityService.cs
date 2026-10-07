using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IActivityService
{
    Task<PagedList<Activity>> SearchAsync(ActivityListQuery q);
    Task<ServiceResult<Activity>> GetAsync(int id, bool track = false);
    Task<ServiceResult<Activity>> CreateAsync(ActivityInput input);
    Task<ServiceResult<Activity>> UpdateAsync(int id, ActivityInput input);
    Task<ServiceResult<Activity>> DeleteAsync(int id);
}

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;
    private readonly IAuditService _audit;

    public ActivityService(ApplicationDbContext db, IScopeService scope, IAuditService audit)
    {
        _db = db; _scope = scope; _audit = audit;
    }

    public Task<PagedList<Activity>> SearchAsync(ActivityListQuery q)
    {
        var query = _scope.Activities().AsNoTracking()
            .Include(a => a.Customer).Include(a => a.Lead).Include(a => a.AssignedUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var t = q.Q.Trim();
            query = query.Where(a => a.Subject.Contains(t) || (a.Description != null && a.Description.Contains(t)));
        }
        if (!string.IsNullOrWhiteSpace(q.Type)) query = query.Where(a => a.ActivityType == q.Type);
        if (!string.IsNullOrWhiteSpace(q.Status)) query = query.Where(a => a.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.AssignedTo)) query = query.Where(a => a.AssignedTo == q.AssignedTo);
        if (q.From != null) { var from = q.From.Value.Date; query = query.Where(a => a.ActivityDate >= from); }
        if (q.To != null) { var to = q.To.Value.Date.AddDays(1); query = query.Where(a => a.ActivityDate < to); }

        var ordered = (q.Sort?.ToLowerInvariant()) switch
        {
            "type" => Sorting.By(query, a => a.ActivityType, q.Desc),
            "subject" => Sorting.By(query, a => a.Subject, q.Desc),
            "date" => Sorting.By(query, a => a.ActivityDate, q.Desc),
            "status" => Sorting.By(query, a => a.Status, q.Desc),
            "owner" => Sorting.By(query, a => a.AssignedUser!.FullName, q.Desc),
            _ => query.OrderByDescending(a => a.ActivityDate)
        };
        return PagedList<Activity>.CreateAsync(ordered.ThenBy(a => a.ActivityId), q.Page, q.PageSize);
    }

    public async Task<ServiceResult<Activity>> GetAsync(int id, bool track = false)
    {
        var query = _db.Activities.Include(a => a.Customer).Include(a => a.Lead).Include(a => a.AssignedUser).AsQueryable();
        if (!track) query = query.AsNoTracking();
        var a = await query.FirstOrDefaultAsync(x => x.ActivityId == id);
        if (a == null) return ServiceResult<Activity>.NotFound();
        if (!_scope.CanAccess(a.AssignedTo)) return ServiceResult<Activity>.Forbidden();
        return ServiceResult<Activity>.Ok(a);
    }

    public async Task<ServiceResult<Activity>> CreateAsync(ActivityInput input)
    {
        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? _scope.UserId : input.AssignedTo!;
        var errors = await ValidateAsync(input, assigned, null);
        if (errors.Count > 0) return ServiceResult<Activity>.Invalid(errors);

        var a = new Activity { CreatedDate = DateTime.UtcNow };
        Apply(a, input, assigned);
        _db.Activities.Add(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Create, AuditEntities.Activity, a.ActivityId.ToString(), null, AuditSnapshot.Of(a));
        return await GetAsync(a.ActivityId);
    }

    public async Task<ServiceResult<Activity>> UpdateAsync(int id, ActivityInput input)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var a = found.Value!;

        var assigned = string.IsNullOrWhiteSpace(input.AssignedTo) ? a.AssignedTo : input.AssignedTo!;
        var errors = await ValidateAsync(input, assigned, a.AssignedTo);
        if (errors.Count > 0) return ServiceResult<Activity>.Invalid(errors);

        var old = AuditSnapshot.Of(a);
        Apply(a, input, assigned);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Update, AuditEntities.Activity, id.ToString(), old, AuditSnapshot.Of(a));
        return await GetAsync(id);
    }

    public async Task<ServiceResult<Activity>> DeleteAsync(int id)
    {
        var found = await GetAsync(id, track: true);
        if (!found.Succeeded) return found;
        var a = found.Value!;
        var old = AuditSnapshot.Of(a);
        _db.Activities.Remove(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.Delete, AuditEntities.Activity, id.ToString(), old, null);
        return ServiceResult<Activity>.Ok(a);
    }

    private async Task<List<(string, string)>> ValidateAsync(ActivityInput i, string assigned, string? keepOwner)
    {
        var errs = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(i.Subject)) errs.Add(("Subject", "Subject is required."));
        if (!ActivityTypes.All.Contains(i.ActivityType)) errs.Add(("ActivityType", "Select a valid activity type."));
        if (!ActivityStatuses.All.Contains(i.Status)) errs.Add(("Status", "Select a valid status."));
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
        return errs;
    }

    private static void Apply(Activity a, ActivityInput i, string assigned)
    {
        a.ActivityType = i.ActivityType;
        a.Subject = i.Subject.Trim();
        a.Description = Text.Clean(i.Description);
        a.ActivityDate = i.ActivityDate.Date;
        a.CustomerId = i.CustomerId;
        a.LeadId = i.LeadId;
        a.Status = i.Status;
        a.AssignedTo = assigned;
    }
}
