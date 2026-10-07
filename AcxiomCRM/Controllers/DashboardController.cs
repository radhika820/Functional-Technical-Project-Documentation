using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class DashboardController : CrmController
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IScopeService scope, IDashboardService dashboard) : base(scope) => _dashboard = dashboard;

    public async Task<IActionResult> Index([FromQuery] DashboardFilter filter) =>
        View(await _dashboard.GetAsync(filter));
}
