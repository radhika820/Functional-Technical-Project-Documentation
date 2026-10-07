using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class OpportunitiesController : CrmController
{
    private readonly IOpportunityService _opps;
    private readonly ApplicationDbContext _db;

    public OpportunitiesController(IScopeService scope, IOpportunityService opps, ApplicationDbContext db) : base(scope)
    {
        _opps = opps; _db = db;
    }

    public async Task<IActionResult> Index([FromQuery] OpportunityListQuery query) =>
        View(new OpportunityListViewModel { Query = query, Data = await _opps.SearchAsync(query), Owners = OwnerItems(query.AssignedTo) });

    public async Task<IActionResult> Details(int id)
    {
        var res = await _opps.GetAsync(id);
        if (Reject(res) is { } r) return r;
        return View(new OpportunityDetailsViewModel
        {
            Opportunity = res.Value!,
            FollowUps = await Scope.FollowUps().AsNoTracking().Where(f => f.OpportunityId == id).OrderByDescending(f => f.FollowUpDate).Take(10).ToListAsync(),
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.Opportunity && a.RecordId == id.ToString())
                .OrderByDescending(a => a.CreatedDate).Take(15).ToListAsync()
        });
    }

    /// <summary>Sales pipeline board: opportunities grouped by stage with quick stage moves.</summary>
    public async Task<IActionResult> Pipeline()
    {
        var all = await Scope.Opportunities().AsNoTracking().Include(o => o.Customer).Include(o => o.AssignedUser)
            .OrderBy(o => o.ExpectedCloseDate).ToListAsync();
        var vm = new PipelineBoardViewModel();
        foreach (var stage in OpportunityStages.All)
        {
            var items = all.Where(o => o.Stage == stage).ToList();
            vm.Columns[stage] = OpportunityStages.IsActive(stage) ? items : items.OrderByDescending(o => o.ClosedDate).Take(10).ToList();
        }
        var open = all.Where(o => o.IsActive).ToList();
        vm.OpenAmount = open.Sum(o => o.Amount);
        vm.WeightedAmount = open.Sum(o => o.WeightedAmount);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> MoveStage(int id, string stage)
    {
        var res = await _opps.MoveStageAsync(id, stage);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess($"Opportunity moved to {stage}.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Stage could not be changed.");
        return RedirectToAction(nameof(Pipeline));
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        var vm = new OpportunityFormViewModel { AssignedTo = Scope.UserId, CustomerId = customerId ?? 0, LeadId = leadId };
        await Fill(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(OpportunityFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var res = await _opps.CreateAsync(model);
            if (res.Succeeded)
            {
                FlashSuccess("Opportunity created.");
                return RedirectToAction(nameof(Details), new { id = res.Value!.OpportunityId });
            }
            AddErrors(res);
        }
        await Fill(model);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var res = await _opps.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var o = res.Value!;
        var vm = new OpportunityFormViewModel
        {
            OpportunityId = o.OpportunityId, OpportunityName = o.OpportunityName, CustomerId = o.CustomerId, LeadId = o.LeadId,
            Amount = o.Amount, Stage = o.Stage, Probability = o.Probability, ExpectedCloseDate = o.ExpectedCloseDate,
            Source = o.Source, Notes = o.Notes, AssignedTo = o.AssignedTo
        };
        await Fill(vm);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, OpportunityFormViewModel model)
    {
        if (id != model.OpportunityId) return BadRequest();
        if (ModelState.IsValid)
        {
            var res = await _opps.UpdateAsync(id, model);
            if (Reject(res) is { } r) return r;
            if (res.Succeeded)
            {
                FlashSuccess("Opportunity updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(res);
        }
        await Fill(model);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var res = await _opps.DeleteAsync(id);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Opportunity deleted.");
        else
        {
            FlashError(res.Errors.FirstOrDefault().Message ?? "Opportunity could not be deleted.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task Fill(OpportunityFormViewModel vm)
    {
        vm.Owners = OwnerItems(vm.AssignedTo);
        vm.Customers = await CustomerItemsAsync(vm.CustomerId == 0 ? null : vm.CustomerId);
        vm.Leads = await LeadItemsAsync(vm.LeadId);
    }
}
