using AcxiomCRM.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class ReportPageViewModel
{
    public ReportTable Table { get; set; } = new();
    public ReportFilter Filter { get; set; } = new();
    public string Action { get; set; } = "";
    public string? StatusLabel { get; set; }
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<SelectListItem> Owners { get; set; } = new();
    public bool ShowOwner { get; set; } = true;
    public bool ShowGroupBy { get; set; }
    public bool ShowAuditFilters { get; set; }
    public bool ShowDates { get; set; } = true;
    public List<SelectListItem> Modules { get; set; } = new();
}

public record ReportLink(string Action, string Title, string Description, bool Allowed);
