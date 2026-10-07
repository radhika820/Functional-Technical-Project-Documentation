using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

/// <summary>Base for all authenticated MVC controllers: protected from anonymous access by default.</summary>
[Authorize]
public abstract class CrmController : Controller
{
    protected readonly IScopeService Scope;
    protected CrmController(IScopeService scope) => Scope = scope;

    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    protected void AddErrors<T>(ServiceResult<T> result)
    {
        foreach (var (field, message) in result.Errors) ModelState.AddModelError(field ?? "", message);
    }

    /// <summary>Maps NotFound/Forbidden service results to the proper MVC response.</summary>
    protected IActionResult? Reject<T>(ServiceResult<T> result) => result.Status switch
    {
        ResultStatus.NotFound => NotFound(),
        ResultStatus.Forbidden => Forbid(),
        _ => null
    };

    protected List<SelectListItem> OwnerItems(string? selected = null) =>
        Scope.AssignableUsers.Select(u => new SelectListItem(u.Name, u.Id, u.Id == selected)).ToList();

    protected static List<SelectListItem> Items(IEnumerable<string> values, string? selected = null) =>
        values.Select(v => new SelectListItem(v, v, v == selected)).ToList();

    protected async Task<List<SelectListItem>> CustomerItemsAsync(int? selected = null, bool activeOnly = true)
    {
        var rows = await Scope.Customers().AsNoTracking()
            .Where(c => !activeOnly || c.Status == CustomerStatuses.Active || c.CustomerId == selected)
            .OrderBy(c => c.CustomerName).Select(c => new { c.CustomerId, c.CustomerName, c.CompanyName }).ToListAsync();
        return rows.Select(c => new SelectListItem(
            c.CustomerName + (string.IsNullOrEmpty(c.CompanyName) ? "" : " - " + c.CompanyName),
            c.CustomerId.ToString(), c.CustomerId == selected)).ToList();
    }

    protected async Task<List<SelectListItem>> LeadItemsAsync(int? selected = null)
    {
        var rows = await Scope.Leads().AsNoTracking()
            .Where(l => l.Status != LeadStatuses.Lost || l.LeadId == selected)
            .OrderBy(l => l.LeadName).Select(l => new { l.LeadId, l.LeadName, l.LeadCode }).ToListAsync();
        return rows.Select(l => new SelectListItem($"{l.LeadName} ({l.LeadCode})", l.LeadId.ToString(), l.LeadId == selected)).ToList();
    }

    protected async Task<List<SelectListItem>> OpportunityItemsAsync(int? selected = null)
    {
        var rows = await Scope.Opportunities().AsNoTracking()
            .OrderBy(o => o.OpportunityName).Select(o => new { o.OpportunityId, o.OpportunityName }).ToListAsync();
        return rows.Select(o => new SelectListItem(o.OpportunityName, o.OpportunityId.ToString(), o.OpportunityId == selected)).ToList();
    }

    protected IActionResult RedirectBack(string? returnUrl, string action, string? controller = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(action, controller);
}
