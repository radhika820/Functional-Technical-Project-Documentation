using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class FollowUpsController : CrmController
{
    private readonly IFollowUpService _followUps;
    public FollowUpsController(IScopeService scope, IFollowUpService followUps) : base(scope) => _followUps = followUps;

    public async Task<IActionResult> Index([FromQuery] FollowUpListQuery query)
    {
        query.PendingOnly = false;
        return View(new FollowUpListViewModel { Query = query, Data = await _followUps.SearchAsync(query), Owners = OwnerItems(query.AssignedTo) });
    }

    /// <summary>Pending (planned) follow-ups, soonest first, with overdue items highlighted.</summary>
    public async Task<IActionResult> Pending([FromQuery] FollowUpListQuery query)
    {
        query.PendingOnly = true;
        if (query.Status != "Overdue") query.Status = null;
        return View("Index", new FollowUpListViewModel { Query = query, Data = await _followUps.SearchAsync(query), Owners = OwnerItems(query.AssignedTo), PendingView = true });
    }

    public async Task<IActionResult> Details(int id)
    {
        var res = await _followUps.GetAsync(id);
        if (Reject(res) is { } r) return r;
        return View(res.Value!);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        var vm = new FollowUpFormViewModel
        {
            IsNew = true, AssignedTo = Scope.UserId, Status = FollowUpStatuses.Planned,
            CustomerId = customerId, LeadId = leadId, OpportunityId = opportunityId
        };
        await Fill(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(FollowUpFormViewModel model)
    {
        model.IsNew = true;
        model.Status = FollowUpStatuses.Planned; // new follow-ups are always planned
        ModelState.Remove(nameof(model.Status));
        if (ModelState.IsValid)
        {
            var res = await _followUps.CreateAsync(model);
            if (res.Succeeded)
            {
                FlashSuccess("Follow-up scheduled.");
                return RedirectToAction(nameof(Pending));
            }
            AddErrors(res);
        }
        await Fill(model);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var res = await _followUps.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var f = res.Value!;
        var vm = new FollowUpFormViewModel
        {
            FollowUpId = f.FollowUpId, Subject = f.Subject, CustomerId = f.CustomerId, LeadId = f.LeadId, OpportunityId = f.OpportunityId,
            FollowUpDate = f.FollowUpDate, FollowUpType = f.FollowUpType, Status = f.Status, Remarks = f.Remarks, AssignedTo = f.AssignedTo
        };
        await Fill(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, FollowUpFormViewModel model)
    {
        if (id != model.FollowUpId) return BadRequest();
        if (ModelState.IsValid)
        {
            var res = await _followUps.UpdateAsync(id, model);
            if (Reject(res) is { } r) return r;
            if (res.Succeeded)
            {
                FlashSuccess("Follow-up updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(res);
        }
        await Fill(model);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Complete(int id, string? remarks, string? returnUrl)
    {
        var res = await _followUps.CompleteAsync(id, remarks);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Follow-up marked as completed.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Follow-up could not be completed.");
        return RedirectBack(returnUrl, nameof(Pending));
    }

    [HttpPost]
    public async Task<IActionResult> Missed(int id, string? returnUrl)
    {
        var res = await _followUps.MarkMissedAsync(id);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Follow-up marked as missed.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Follow-up could not be updated.");
        return RedirectBack(returnUrl, nameof(Pending));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? returnUrl)
    {
        var res = await _followUps.CancelAsync(id);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Follow-up cancelled.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Follow-up could not be cancelled.");
        return RedirectBack(returnUrl, nameof(Pending));
    }

    [HttpPost]
    public async Task<IActionResult> Reschedule(int id, DateTime date, string? returnUrl)
    {
        var res = await _followUps.RescheduleAsync(id, date);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Follow-up rescheduled.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Follow-up could not be rescheduled.");
        return RedirectBack(returnUrl, nameof(Pending));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var res = await _followUps.DeleteAsync(id);
        if (Reject(res) is { } r) return r;
        FlashSuccess("Follow-up deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task Fill(FollowUpFormViewModel vm)
    {
        vm.Owners = OwnerItems(vm.AssignedTo);
        vm.Customers = await CustomerItemsAsync(vm.CustomerId, activeOnly: false);
        vm.Leads = await LeadItemsAsync(vm.LeadId);
        vm.Opportunities = await OpportunityItemsAsync(vm.OpportunityId);
    }
}
