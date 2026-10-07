using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class ActivitiesController : CrmController
{
    private readonly IActivityService _activities;
    public ActivitiesController(IScopeService scope, IActivityService activities) : base(scope) => _activities = activities;

    public async Task<IActionResult> Index([FromQuery] ActivityListQuery query) =>
        View(new ActivityListViewModel { Query = query, Data = await _activities.SearchAsync(query), Owners = OwnerItems(query.AssignedTo) });

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        var vm = new ActivityFormViewModel { AssignedTo = Scope.UserId, CustomerId = customerId, LeadId = leadId };
        await Fill(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ActivityFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var res = await _activities.CreateAsync(model);
            if (res.Succeeded)
            {
                FlashSuccess("Activity saved.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(res);
        }
        await Fill(model);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var res = await _activities.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var a = res.Value!;
        var vm = new ActivityFormViewModel
        {
            ActivityId = a.ActivityId, ActivityType = a.ActivityType, Subject = a.Subject, Description = a.Description,
            ActivityDate = a.ActivityDate, CustomerId = a.CustomerId, LeadId = a.LeadId, Status = a.Status, AssignedTo = a.AssignedTo
        };
        await Fill(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ActivityFormViewModel model)
    {
        if (id != model.ActivityId) return BadRequest();
        if (ModelState.IsValid)
        {
            var res = await _activities.UpdateAsync(id, model);
            if (Reject(res) is { } r) return r;
            if (res.Succeeded)
            {
                FlashSuccess("Activity updated.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(res);
        }
        await Fill(model);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var res = await _activities.DeleteAsync(id);
        if (Reject(res) is { } r) return r;
        FlashSuccess("Activity deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task Fill(ActivityFormViewModel vm)
    {
        vm.Owners = OwnerItems(vm.AssignedTo);
        vm.Customers = await CustomerItemsAsync(vm.CustomerId, activeOnly: false);
        vm.Leads = await LeadItemsAsync(vm.LeadId);
    }
}
