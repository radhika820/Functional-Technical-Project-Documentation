using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using AcxiomCRM.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Controllers;

public class ReportsController : CrmController
{
    private readonly IReportService _reports;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public ReportsController(IScopeService scope, IReportService reports, IAuditService audit, IConfiguration config) : base(scope)
    {
        _reports = reports; _audit = audit; _config = config;
    }

    public IActionResult Index() => View(new[]
    {
        new ReportLink("Customers", "Customer Report", "Customer list, status, owner and creation date.", true),
        new ReportLink("Leads", "Lead Report", "Lead source, status, owner and conversion information.", true),
        new ReportLink("FollowUps", "Follow-Up Report", "Planned, completed, missed and overdue follow-ups.", true),
        new ReportLink("Opportunities", "Opportunity Report", "Stage, amount, probability and expected close date.", true),
        new ReportLink("Pipeline", "Pipeline Report", "Stage-wise and owner-wise pipeline amount.", true),
        new ReportLink("Conversion", "Sales / Conversion Report", "Lead conversion and opportunity outcome metrics.", true),
        new ReportLink("UserActivity", "User Activity Report", "User actions and activity counts.", !Scope.IsSales),
        new ReportLink("Audit", "Audit Report", "Security and business audit events.", AuditAllowed)
    });

    public Task<IActionResult> Customers([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.CustomersAsync, "Customers", "Status", CustomerStatuses.All);

    public Task<IActionResult> Leads([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.LeadsAsync, "Leads", "Status", LeadStatuses.All);

    public Task<IActionResult> FollowUps([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.FollowUpsAsync, "FollowUps", "Status", FollowUpStatuses.All.Append("Overdue"));

    public Task<IActionResult> Opportunities([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.OpportunitiesAsync, "Opportunities", "Stage", OpportunityStages.All);

    public Task<IActionResult> Pipeline([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.PipelineAsync, "Pipeline", null, Array.Empty<string>(), groupBy: true);

    public Task<IActionResult> Conversion([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.ConversionAsync, "Conversion", null, Array.Empty<string>());

    [Authorize(Roles = AppRoles.AdminOrManager)]
    public Task<IActionResult> UserActivity([FromQuery] ReportFilter f, bool export = false) =>
        Run(f, export, _reports.UserActivityAsync, "UserActivity", null, Array.Empty<string>());

    public Task<IActionResult> Audit([FromQuery] ReportFilter f, bool export = false)
    {
        if (!AuditAllowed) return Task.FromResult<IActionResult>(Forbid());
        return Run(f, export, _reports.AuditAsync, "Audit", "Action", AuditActions.All, auditFilters: true, showOwner: false);
    }

    private bool AuditAllowed => Scope.IsAdmin || (Scope.IsManager && _config.GetValue("Features:ManagerCanViewAudit", false));

    private async Task<IActionResult> Run(ReportFilter f, bool export, Func<ReportFilter, Task<ReportTable>> build, string action,
        string? statusLabel, IEnumerable<string> statuses, bool groupBy = false, bool auditFilters = false, bool showOwner = true)
    {
        var table = await build(f);
        if (export)
        {
            await _audit.LogAsync(AuditActions.Export, AuditEntities.Report, null, details: $"{table.Title} exported ({table.Rows.Count} rows)");
            return File(table.ToCsv(), "text/csv", $"{action.ToLowerInvariant()}-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }
        table.Apply(f);
        return View("Table", new ReportPageViewModel
        {
            Table = table, Filter = f, Action = action, StatusLabel = statusLabel,
            StatusOptions = Items(statuses, f.Status), Owners = OwnerItems(f.OwnerId), ShowGroupBy = groupBy,
            ShowAuditFilters = auditFilters, ShowOwner = showOwner && !Scope.IsSales,
            Modules = Items(AuditEntities.All, f.Module)
        });
    }
}

