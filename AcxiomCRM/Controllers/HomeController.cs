using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class HomeController : CrmController
{
    private readonly ILogger<HomeController> _logger;
    public HomeController(IScopeService scope, ILogger<HomeController> logger) : base(scope) => _logger = logger;

    [AllowAnonymous]
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Login", "Account");

    /// <summary>Global search across the records the user is allowed to see.</summary>
    public async Task<IActionResult> Search(string? q)
    {
        var vm = new SearchViewModel { Q = (q ?? "").Trim() };
        if (vm.Q.Length >= 2)
        {
            var t = vm.Q;
            vm.Customers = await Scope.Customers().AsNoTracking()
                .Where(c => c.CustomerName.Contains(t) || c.Email.Contains(t) || c.Phone.Contains(t) || c.CustomerCode.Contains(t)
                            || (c.CompanyName != null && c.CompanyName.Contains(t)))
                .OrderBy(c => c.CustomerName).Take(10).ToListAsync();
            vm.Leads = await Scope.Leads().AsNoTracking()
                .Where(l => l.LeadName.Contains(t) || l.Email.Contains(t) || l.Phone.Contains(t) || l.LeadCode.Contains(t)
                            || (l.CompanyName != null && l.CompanyName.Contains(t)))
                .OrderBy(l => l.LeadName).Take(10).ToListAsync();
            vm.Opportunities = await Scope.Opportunities().AsNoTracking().Include(o => o.Customer)
                .Where(o => o.OpportunityName.Contains(t) || o.Customer!.CustomerName.Contains(t))
                .OrderBy(o => o.OpportunityName).Take(10).ToListAsync();
        }
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error != null)
            _logger.LogError(feature.Error, "Unhandled exception for {Path}", feature.Path);

        if (feature?.Path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            return StatusCode(500, new ProblemDetails { Status = 500, Title = "An unexpected error occurred." });

        Response.StatusCode = 500;
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier, Code = 500 });
    }

    [AllowAnonymous]
    public IActionResult Status(int code)
    {
        var original = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath ?? "";
        if (original.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            var title = code switch { 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not found", 429 => "Too many requests", _ => "Request failed" };
            return new ObjectResult(new ProblemDetails { Status = code, Title = title }) { StatusCode = code };
        }
        return View("StatusCode", new ErrorViewModel { Code = code, RequestId = HttpContext.TraceIdentifier });
    }
}
