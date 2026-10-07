using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public interface IReportService
{
    Task<ReportTable> CustomersAsync(ReportFilter f);
    Task<ReportTable> LeadsAsync(ReportFilter f);
    Task<ReportTable> FollowUpsAsync(ReportFilter f);
    Task<ReportTable> OpportunitiesAsync(ReportFilter f);
    Task<ReportTable> PipelineAsync(ReportFilter f);
    Task<ReportTable> ConversionAsync(ReportFilter f);
    Task<ReportTable> UserActivityAsync(ReportFilter f);
    Task<ReportTable> AuditAsync(ReportFilter f);
    Task<PipelineReportDto> PipelineDataAsync();
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IScopeService _scope;

    public ReportService(ApplicationDbContext db, IScopeService scope)
    {
        _db = db; _scope = scope;
    }

    private static DateTime? Utc(DateTime? local) =>
        local == null ? null : DateTime.SpecifyKind(local.Value.Date, DateTimeKind.Local).ToUniversalTime();

    private static DateTime? UtcEnd(DateTime? local) => Utc(local?.AddDays(1));

    public async Task<ReportTable> CustomersAsync(ReportFilter f)
    {
        var q = _scope.Customers().AsNoTracking().Include(c => c.AssignedUser).AsQueryable();
        var from = Utc(f.From); var to = UtcEnd(f.To);
        if (from != null) q = q.Where(c => c.CreatedDate >= from);
        if (to != null) q = q.Where(c => c.CreatedDate < to);
        if (!string.IsNullOrWhiteSpace(f.Status)) q = q.Where(c => c.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) q = q.Where(c => c.AssignedTo == f.OwnerId);
        var data = await q.OrderBy(c => c.CustomerName).ToListAsync();

        return new ReportTable
        {
            Key = "customers", Title = "Customer Report", Description = "Customer list with status, owner and creation date.",
            Columns = new[] { "Code", "Customer", "Company", "Email", "Phone", "Status", "Owner", "Created" },
            Rows = data.Select(c => new object?[] { c.CustomerCode, c.CustomerName, c.CompanyName, c.Email, c.Phone, c.Status,
                c.AssignedUser?.FullName, c.CreatedDate.ToLocalTime().Date }).ToList()
        };
    }

    public async Task<ReportTable> LeadsAsync(ReportFilter f)
    {
        var q = _scope.Leads().AsNoTracking().Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer).AsQueryable();
        var from = Utc(f.From); var to = UtcEnd(f.To);
        if (from != null) q = q.Where(l => l.CreatedDate >= from);
        if (to != null) q = q.Where(l => l.CreatedDate < to);
        if (!string.IsNullOrWhiteSpace(f.Status)) q = q.Where(l => l.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) q = q.Where(l => l.AssignedTo == f.OwnerId);
        var data = await q.OrderBy(l => l.LeadName).ToListAsync();

        return new ReportTable
        {
            Key = "leads", Title = "Lead Report", Description = "Lead source, status, owner and conversion information.",
            Columns = new[] { "Code", "Lead", "Source", "Status", "Priority", "Expected Value", "Owner", "Created", "Converted On", "Customer" },
            Rows = data.Select(l => new object?[] { l.LeadCode, l.LeadName, l.Source, l.Status, l.Priority, l.ExpectedValue,
                l.AssignedUser?.FullName, l.CreatedDate.ToLocalTime().Date, l.ConvertedDate?.ToLocalTime().Date,
                l.ConvertedCustomer?.CustomerCode }).ToList()
        };
    }

    public async Task<ReportTable> FollowUpsAsync(ReportFilter f)
    {
        var q = _scope.FollowUps().AsNoTracking().Include(x => x.AssignedUser).Include(x => x.Customer).Include(x => x.Lead)
            .Include(x => x.Opportunity).AsQueryable();
        var today = DateTime.Today;
        if (f.From != null) { var from = f.From.Value.Date; q = q.Where(x => x.FollowUpDate >= from); }
        if (f.To != null) { var to = f.To.Value.Date; q = q.Where(x => x.FollowUpDate <= to); }
        if (f.Status == "Overdue") q = q.Where(x => x.Status == FollowUpStatuses.Planned && x.FollowUpDate < today);
        else if (!string.IsNullOrWhiteSpace(f.Status)) q = q.Where(x => x.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) q = q.Where(x => x.AssignedTo == f.OwnerId);
        var data = await q.OrderBy(x => x.FollowUpDate).ToListAsync();

        var summary = new List<(string, string)>
        {
            ("Planned", data.Count(x => x.Status == FollowUpStatuses.Planned).ToString()),
            ("Completed", data.Count(x => x.Status == FollowUpStatuses.Completed).ToString()),
            ("Missed", data.Count(x => x.Status == FollowUpStatuses.Missed).ToString()),
            ("Overdue", data.Count(x => x.IsOverdue).ToString())
        };
        return new ReportTable
        {
            Key = "followups", Title = "Follow-Up Report", Description = "Planned, completed, missed and overdue activities.",
            Summary = summary,
            Columns = new[] { "Date", "Subject", "Type", "Status", "Overdue", "Related To", "Owner" },
            Rows = data.Select(x => new object?[] { x.FollowUpDate, x.Subject, x.FollowUpType, x.Status, x.IsOverdue,
                x.Customer?.CustomerName ?? x.Lead?.LeadName ?? x.Opportunity?.OpportunityName, x.AssignedUser?.FullName }).ToList()
        };
    }

    public async Task<ReportTable> OpportunitiesAsync(ReportFilter f)
    {
        var q = _scope.Opportunities().AsNoTracking().Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
        if (f.From != null) { var from = f.From.Value.Date; q = q.Where(o => o.ExpectedCloseDate >= from); }
        if (f.To != null) { var to = f.To.Value.Date; q = q.Where(o => o.ExpectedCloseDate <= to); }
        if (!string.IsNullOrWhiteSpace(f.Status)) q = q.Where(o => o.Stage == f.Status);
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) q = q.Where(o => o.AssignedTo == f.OwnerId);
        var data = await q.OrderBy(o => o.ExpectedCloseDate).ToListAsync();

        return new ReportTable
        {
            Key = "opportunities", Title = "Opportunity Report", Description = "Stage, amount, probability and expected close date.",
            Columns = new[] { "Opportunity", "Customer", "Stage", "Amount", "Probability %", "Weighted", "Expected Close", "Owner" },
            Rows = data.Select(o => new object?[] { o.OpportunityName, o.Customer?.CustomerName, o.Stage, o.Amount, o.Probability,
                o.WeightedAmount, o.ExpectedCloseDate, o.AssignedUser?.FullName }).ToList()
        };
    }

    public async Task<ReportTable> PipelineAsync(ReportFilter f)
    {
        var q = _scope.Opportunities().AsNoTracking().Include(o => o.AssignedUser).AsQueryable();
        if (f.From != null) { var from = f.From.Value.Date; q = q.Where(o => o.ExpectedCloseDate >= from); }
        if (f.To != null) { var to = f.To.Value.Date; q = q.Where(o => o.ExpectedCloseDate <= to); }
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) q = q.Where(o => o.AssignedTo == f.OwnerId);
        var data = await q.ToListAsync();
        var open = data.Where(o => o.IsActive).ToList();

        var summary = new List<(string, string)>
        {
            ("Open pipeline", AppFormat.Money(open.Sum(o => o.Amount))),
            ("Weighted pipeline", AppFormat.Money(open.Sum(o => o.WeightedAmount)))
        };

        if (string.Equals(f.GroupBy, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return new ReportTable
            {
                Key = "pipeline", Title = "Pipeline Report - by Owner", Description = "Open pipeline (Qualification to Negotiation) per owner.",
                Summary = summary,
                Columns = new[] { "Owner", "Open Opportunities", "Pipeline Amount", "Weighted Pipeline" },
                Rows = open.GroupBy(o => o.AssignedUser?.FullName ?? "(unassigned)")
                    .Select(g => new object?[] { g.Key, g.Count(), g.Sum(x => x.Amount), g.Sum(x => x.WeightedAmount) }).ToList()
            };
        }
        return new ReportTable
        {
            Key = "pipeline", Title = "Pipeline Report - by Stage", Description = "Opportunity count, amount and weighted amount per stage.",
            Summary = summary,
            Columns = new[] { "Stage", "Opportunities", "Pipeline Amount", "Weighted Amount" },
            Rows = OpportunityStages.All.Select(s =>
            {
                var items = data.Where(o => o.Stage == s).ToList();
                return new object?[] { s, items.Count, items.Sum(x => x.Amount), items.Sum(x => x.WeightedAmount) };
            }).ToList()
        };
    }

    public async Task<ReportTable> ConversionAsync(ReportFilter f)
    {
        var lq = _scope.Leads().AsNoTracking().Include(l => l.AssignedUser).AsQueryable();
        var oq = _scope.Opportunities().AsNoTracking().Include(o => o.AssignedUser).AsQueryable();
        var from = Utc(f.From); var to = UtcEnd(f.To);
        if (from != null) { lq = lq.Where(l => l.CreatedDate >= from); oq = oq.Where(o => o.CreatedDate >= from); }
        if (to != null) { lq = lq.Where(l => l.CreatedDate < to); oq = oq.Where(o => o.CreatedDate < to); }
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) { lq = lq.Where(l => l.AssignedTo == f.OwnerId); oq = oq.Where(o => o.AssignedTo == f.OwnerId); }
        var leads = await lq.ToListAsync();
        var opps = await oq.ToListAsync();

        static double Rate(int part, int whole) => whole == 0 ? 0 : Math.Round(part * 100.0 / whole, 1);
        var owners = leads.Select(l => (l.AssignedTo, Name: l.AssignedUser?.FullName ?? "(unassigned)"))
            .Concat(opps.Select(o => (o.AssignedTo, Name: o.AssignedUser?.FullName ?? "(unassigned)")))
            .DistinctBy(x => x.AssignedTo).ToList();

        var rows = owners.Select(ow =>
        {
            var ls = leads.Where(l => l.AssignedTo == ow.AssignedTo).ToList();
            var os = opps.Where(o => o.AssignedTo == ow.AssignedTo).ToList();
            var won = os.Where(o => o.Stage == OpportunityStages.Won).ToList();
            var lost = os.Count(o => o.Stage == OpportunityStages.Lost);
            return new object?[] { ow.Name, ls.Count, ls.Count(l => l.Status == LeadStatuses.Converted),
                ls.Count(l => l.Status != LeadStatuses.Converted), Rate(ls.Count(l => l.Status == LeadStatuses.Converted), ls.Count),
                won.Count, lost, won.Sum(o => o.Amount), Rate(won.Count, won.Count + lost) };
        }).ToList();

        var conv = leads.Count(l => l.Status == LeadStatuses.Converted);
        var wonAll = opps.Count(o => o.Stage == OpportunityStages.Won);
        var lostAll = opps.Count(o => o.Stage == OpportunityStages.Lost);
        return new ReportTable
        {
            Key = "conversion", Title = "Sales / Conversion Report", Description = "Leads converted vs not converted and opportunity outcomes per owner.",
            Summary = new List<(string, string)>
            {
                ("Leads", leads.Count.ToString()), ("Converted", conv.ToString()),
                ("Not converted", (leads.Count - conv).ToString()), ("Lead conversion", $"{Rate(conv, leads.Count)}%"),
                ("Won", wonAll.ToString()), ("Lost", lostAll.ToString()), ("Win rate", $"{Rate(wonAll, wonAll + lostAll)}%")
            },
            Columns = new[] { "Owner", "Leads", "Converted", "Not Converted", "Lead Conversion %", "Opps Won", "Opps Lost", "Won Amount", "Win Rate %" },
            Rows = rows
        };
    }

    public async Task<ReportTable> UserActivityAsync(ReportFilter f)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        var from = Utc(f.From); var to = UtcEnd(f.To);
        if (from != null) q = q.Where(a => a.CreatedDate >= from);
        if (to != null) q = q.Where(a => a.CreatedDate < to);
        if (_scope.VisibleOwnerIds != null) { var ids = _scope.VisibleOwnerIds; q = q.Where(a => a.UserId != null && ids.Contains(a.UserId)); }
        if (!string.IsNullOrWhiteSpace(f.OwnerId)) q = q.Where(a => a.UserId == f.OwnerId);

        var rows = await q.GroupBy(a => a.UserName ?? "(anonymous)").Select(g => new
        {
            User = g.Key,
            Total = g.Count(),
            Logins = g.Count(a => a.Action == AuditActions.Login),
            Failed = g.Count(a => a.Action == AuditActions.FailedLogin),
            Creates = g.Count(a => a.Action == AuditActions.Create),
            Updates = g.Count(a => a.Action == AuditActions.Update),
            Deletes = g.Count(a => a.Action == AuditActions.Delete),
            Last = g.Max(a => a.CreatedDate)
        }).ToListAsync();

        return new ReportTable
        {
            Key = "useractivity", Title = "User Activity Report", Description = "Action counts per user, derived from the audit log.",
            Columns = new[] { "User", "Total Actions", "Logins", "Failed Logins", "Creates", "Updates", "Deletes", "Last Activity" },
            Rows = rows.Select(r => new object?[] { r.User, r.Total, r.Logins, r.Failed, r.Creates, r.Updates, r.Deletes,
                r.Last.ToLocalTime().Date }).ToList()
        };
    }

    public async Task<ReportTable> AuditAsync(ReportFilter f)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        var from = Utc(f.From); var to = UtcEnd(f.To);
        if (from != null) q = q.Where(a => a.CreatedDate >= from);
        if (to != null) q = q.Where(a => a.CreatedDate < to);
        if (_scope.VisibleOwnerIds != null) { var ids = _scope.VisibleOwnerIds; q = q.Where(a => a.UserId != null && ids.Contains(a.UserId)); }
        if (!string.IsNullOrWhiteSpace(f.Status)) q = q.Where(a => a.Action == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Module)) q = q.Where(a => a.EntityName == f.Module);
        if (!string.IsNullOrWhiteSpace(f.Q)) { var t = f.Q.Trim(); q = q.Where(a => (a.UserName != null && a.UserName.Contains(t)) || (a.Details != null && a.Details.Contains(t))); }
        var data = await q.OrderByDescending(a => a.CreatedDate).Take(5000).ToListAsync();

        return new ReportTable
        {
            Key = "audit", Title = "Audit Report", Description = "Security and business audit events (most recent 5,000 matching entries).",
            Columns = new[] { "Time", "User", "Action", "Module", "Record", "Result", "IP Address", "Details" },
            Rows = data.Select(a => new object?[] { AppFormat.DateTimeLocal(a.CreatedDate), a.UserName, a.Action, a.EntityName,
                a.RecordId, a.Result, a.IpAddress, a.Details }).ToList()
        };
    }

    public async Task<PipelineReportDto> PipelineDataAsync()
    {
        var opps = await _scope.Opportunities().AsNoTracking().Include(o => o.AssignedUser).ToListAsync();
        var open = opps.Where(o => o.IsActive).ToList();
        return new PipelineReportDto
        {
            ByStage = OpportunityStages.All.Select(s =>
            {
                var items = opps.Where(o => o.Stage == s).ToList();
                return new PipelineRowDto { Group = s, Count = items.Count, Amount = items.Sum(x => x.Amount), WeightedAmount = items.Sum(x => x.WeightedAmount) };
            }).ToList(),
            ByOwner = open.GroupBy(o => o.AssignedUser?.FullName ?? "(unassigned)")
                .Select(g => new PipelineRowDto { Group = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount), WeightedAmount = g.Sum(x => x.WeightedAmount) })
                .ToList(),
            TotalOpenAmount = open.Sum(o => o.Amount),
            TotalWeightedAmount = open.Sum(o => o.WeightedAmount)
        };
    }
}
