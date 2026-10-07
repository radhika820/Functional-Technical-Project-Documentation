using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync(DashboardFilter filter);
}

/// <summary>All figures are computed on the server from the caller's authorized scope.</summary>
public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;

    public DashboardService(ApplicationDbContext db, IScopeService scope)
    {
        _db = db; _scope = scope;
    }

    public static (DateTime? fromUtc, DateTime? toUtc, string label) Resolve(DashboardFilter f)
    {
        var today = DateTime.Today;
        DateTime? from = null, to = null;
        string label = "All time";
        switch ((f.Range ?? "all").ToLowerInvariant())
        {
            case "today":
                from = today; to = today.AddDays(1); label = "Today"; break;
            case "week":
                from = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); to = from.Value.AddDays(7); label = "This week"; break;
            case "month":
                from = new DateTime(today.Year, today.Month, 1); to = from.Value.AddMonths(1); label = "This month"; break;
            case "custom":
                from = f.From?.Date; to = f.To?.Date.AddDays(1);
                label = $"{(from?.ToString("dd MMM yyyy") ?? "start")} - {(f.To?.ToString("dd MMM yyyy") ?? "today")}";
                break;
        }
        return (ToUtc(from), ToUtc(to), label);
    }

    private static DateTime? ToUtc(DateTime? local) =>
        local == null ? null : DateTime.SpecifyKind(local.Value, DateTimeKind.Local).ToUniversalTime();

    public async Task<DashboardViewModel> GetAsync(DashboardFilter filter)
    {
        var (fromUtc, toUtc, label) = Resolve(filter);
        var vm = new DashboardViewModel { Filter = filter, RangeLabel = label, Role = _scope.Role };

        var customers = _scope.Customers().AsNoTracking();
        var leads = _scope.Leads().AsNoTracking();
        var opps = _scope.Opportunities().AsNoTracking();
        if (fromUtc != null) { customers = customers.Where(c => c.CreatedDate >= fromUtc); leads = leads.Where(c => c.CreatedDate >= fromUtc); opps = opps.Where(c => c.CreatedDate >= fromUtc); }
        if (toUtc != null) { customers = customers.Where(c => c.CreatedDate < toUtc); leads = leads.Where(c => c.CreatedDate < toUtc); opps = opps.Where(c => c.CreatedDate < toUtc); }

        vm.TotalCustomers = await customers.CountAsync();
        vm.TotalLeads = await leads.CountAsync();
        vm.OpenLeads = await leads.CountAsync(l => l.Status == LeadStatuses.New || l.Status == LeadStatuses.Contacted
                                                   || l.Status == LeadStatuses.Qualified || l.Status == LeadStatuses.Unqualified);
        vm.TotalOpportunities = await opps.CountAsync();
        vm.OpenOpportunities = await opps.CountAsync(o => OpportunityStages.Active.Contains(o.Stage));
        vm.WonOpportunities = await opps.CountAsync(o => o.Stage == OpportunityStages.Won);
        vm.LostOpportunities = await opps.CountAsync(o => o.Stage == OpportunityStages.Lost);

        var open = opps.Where(o => OpportunityStages.Active.Contains(o.Stage) && o.Amount > 0);
        vm.TotalPipelineValue = await open.SumAsync(o => (decimal?)o.Amount) ?? 0m;
        vm.WeightedPipelineValue = await open.SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

        // Lead status chart
        var leadGroups = await leads.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        foreach (var s in LeadStatuses.All) vm.LeadStatusCounts[s] = leadGroups.FirstOrDefault(g => g.Key == s)?.Count ?? 0;

        // Opportunity pipeline chart
        var stageGroups = await opps.GroupBy(o => o.Stage)
            .Select(g => new { g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToListAsync();
        foreach (var s in OpportunityStages.All)
        {
            var g = stageGroups.FirstOrDefault(x => x.Key == s);
            vm.Pipeline.Add(new StageStat(s, g?.Count ?? 0, g?.Amount ?? 0m));
        }

        // Monthly sales/outcome totals: last 12 months, by closed date
        var today = DateTime.Today;
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var firstMonthUtc = DateTime.SpecifyKind(firstMonth, DateTimeKind.Local).ToUniversalTime();
        var closed = await _scope.Opportunities().AsNoTracking()
            .Where(o => (o.Stage == OpportunityStages.Won || o.Stage == OpportunityStages.Lost)
                        && o.ClosedDate != null && o.ClosedDate >= firstMonthUtc)
            .Select(o => new { o.Stage, o.Amount, o.ClosedDate })
            .ToListAsync();
        for (var i = 0; i < 12; i++)
        {
            var m = firstMonth.AddMonths(i);
            var inMonth = closed.Where(c => c.ClosedDate!.Value.ToLocalTime() is var d && d.Year == m.Year && d.Month == m.Month).ToList();
            vm.MonthlySales.Add(new MonthStat(m.ToString("MMM yyyy"),
                inMonth.Where(c => c.Stage == OpportunityStages.Won).Sum(c => c.Amount),
                inMonth.Where(c => c.Stage == OpportunityStages.Lost).Sum(c => c.Amount)));
        }

        // Follow-ups (live, not date-filtered)
        var planned = _scope.FollowUps().AsNoTracking().Where(f => f.Status == FollowUpStatuses.Planned);
        vm.PendingFollowUps = await planned.CountAsync();
        vm.OverdueFollowUps = await planned.CountAsync(f => f.FollowUpDate < today);
        var horizon = today.AddDays(7);
        vm.Upcoming = await planned.Where(f => f.FollowUpDate <= horizon)
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser)
            .OrderBy(f => f.FollowUpDate).Take(8).ToListAsync();

        if (_scope.IsAdmin)
        {
            var now = DateTimeOffset.UtcNow;
            var since = DateTime.UtcNow.AddHours(-24);
            vm.Admin = new AdminStats
            {
                TotalUsers = await _db.Users.CountAsync(),
                ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
                LockedUsers = await _db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now),
                AuditEvents24h = await _db.AuditLogs.CountAsync(a => a.CreatedDate >= since),
                FailedLogins24h = await _db.AuditLogs.CountAsync(a => a.Action == AuditActions.FailedLogin && a.CreatedDate >= since)
            };
        }
        return vm;
    }
}
