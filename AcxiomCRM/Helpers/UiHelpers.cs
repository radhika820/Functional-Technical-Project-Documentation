using System.Text.Encodings.Web;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Helpers;

public static class Ui
{
    /// <summary>Bootstrap colour for a status/stage value.</summary>
    public static string Badge(string? value) => value switch
    {
        "Active" or "Won" or "Completed" or "Converted" or "Qualified" or "Success" or "Open" => "success",
        "Lost" or "Missed" or "Inactive" or "Failure" or "Unqualified" or "High" or "Overdue" => "danger",
        "Planned" or "Contacted" or "Negotiation" or "Medium" => "warning",
        "New" or "Proposal" or "Qualification" or "Low" => "info",
        _ => "secondary"
    };

    public static IHtmlContent StatusBadge(string? value) =>
        new HtmlString($"<span class=\"badge text-bg-{Badge(value)}\">{HtmlEncoder.Default.Encode(value ?? "-")}</span>");

    public static string Owner(ApplicationUser? u) => u?.FullName ?? "-";
}

public static class HtmlExtensions
{
    /// <summary>Sortable column header link that preserves the current filters.</summary>
    public static IHtmlContent SortLink(this IHtmlHelper html, string text, string key)
    {
        var req = html.ViewContext.HttpContext.Request;
        var currentSort = req.Query["sort"].ToString();
        var currentDir = req.Query["dir"].ToString();
        var active = string.Equals(currentSort, key, StringComparison.OrdinalIgnoreCase);
        var desc = string.Equals(currentDir, "desc", StringComparison.OrdinalIgnoreCase);
        var nextDir = active && !desc ? "desc" : "asc";

        var pairs = req.Query.Where(k => k.Key is not ("sort" or "dir" or "page"))
            .SelectMany(k => k.Value.Select(v => new KeyValuePair<string, string?>(k.Key, v)))
            .Append(new("sort", key)).Append(new("dir", nextDir)).ToList();
        var url = req.Path + QueryString.Create(pairs);

        var a = new TagBuilder("a");
        a.Attributes["href"] = url;
        a.AddCssClass("text-decoration-none text-nowrap");
        a.InnerHtml.Append(text);
        if (active) a.InnerHtml.AppendHtml(desc ? " <i class=\"bi bi-caret-down-fill\"></i>" : " <i class=\"bi bi-caret-up-fill\"></i>");
        return a;
    }

    public static string PageUrl(this IHtmlHelper html, int page)
    {
        var req = html.ViewContext.HttpContext.Request;
        var pairs = req.Query.Where(k => k.Key != "page")
            .SelectMany(k => k.Value.Select(v => new KeyValuePair<string, string?>(k.Key, v)))
            .Append(new("page", page.ToString())).ToList();
        return req.Path + QueryString.Create(pairs);
    }

    public static IHtmlContent Money(this IHtmlHelper html, decimal amount) => new HtmlString(HtmlEncoder.Default.Encode(AppFormat.Money(amount)));
}

public record FollowUpAlertsModel(int Overdue, int DueToday)
{
    public int Total => Overdue + DueToday;
}

/// <summary>Navbar notification bell: overdue and due-today follow-ups within the user's scope.</summary>
public class FollowUpAlertsViewComponent : ViewComponent
{
    private readonly IScopeService _scope;
    public FollowUpAlertsViewComponent(IScopeService scope) => _scope = scope;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true) return Content(string.Empty);
        await _scope.LoadAsync();
        var today = DateTime.Today;
        var planned = _scope.FollowUps().AsNoTracking().Where(f => f.Status == FollowUpStatuses.Planned);
        var overdue = await planned.CountAsync(f => f.FollowUpDate < today);
        var dueToday = await planned.CountAsync(f => f.FollowUpDate == today);
        return View(new FollowUpAlertsModel(overdue, dueToday));
    }
}

public static class CrmItems
{
    public static List<SelectListItem> From(IEnumerable<string> values, string? selected = null) =>
        values.Select(v => new SelectListItem(v, v, v == selected)).ToList();
}
