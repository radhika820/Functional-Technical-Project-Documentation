using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class LeadsController : CrmController
{
    private readonly ILeadService _leads;
    private readonly ApplicationDbContext _db;

    public LeadsController(IScopeService scope, ILeadService leads, ApplicationDbContext db) : base(scope)
    {
        _leads = leads; _db = db;
    }

    public async Task<IActionResult> Index([FromQuery] ListQuery query) =>
        View(new LeadListViewModel { Query = query, Data = await _leads.SearchAsync(query), Owners = OwnerItems(query.AssignedTo) });

    public async Task<IActionResult> Details(int id)
    {
        var res = await _leads.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var l = res.Value!;
        return View(new LeadDetailsViewModel
        {
            Lead = l,
            NextStatuses = LeadStatuses.SelectableFrom(l.Status).Where(s => s != l.Status).ToList(),
            FollowUps = await Scope.FollowUps().AsNoTracking().Where(f => f.LeadId == id).OrderByDescending(f => f.FollowUpDate).Take(10).ToListAsync(),
            Activities = await Scope.Activities().AsNoTracking().Where(a => a.LeadId == id).OrderByDescending(a => a.ActivityDate).Take(10).ToListAsync(),
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.Lead && a.RecordId == id.ToString())
                .OrderByDescending(a => a.CreatedDate).Take(15).ToListAsync()
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new LeadFormViewModel
    {
        Owners = OwnerItems(Scope.UserId), AssignedTo = Scope.UserId,
        StatusOptions = LeadStatuses.All.Where(s => s != LeadStatuses.Converted).ToList()
    });

    [HttpPost]
    public async Task<IActionResult> Create(LeadFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var res = await _leads.CreateAsync(model);
            if (res.Succeeded)
            {
                FlashSuccess($"Lead {res.Value!.LeadCode} created.");
                return RedirectToAction(nameof(Details), new { id = res.Value.LeadId });
            }
            AddErrors(res);
        }
        model.Owners = OwnerItems(model.AssignedTo);
        model.StatusOptions = LeadStatuses.All.Where(s => s != LeadStatuses.Converted).ToList();
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var res = await _leads.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var l = res.Value!;
        if (l.Status == LeadStatuses.Converted)
        {
            FlashError("Converted leads are read-only.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return View("Form", new LeadFormViewModel
        {
            LeadId = l.LeadId, LeadName = l.LeadName, Email = l.Email, Phone = l.Phone, CompanyName = l.CompanyName, Source = l.Source,
            Status = l.Status, Priority = l.Priority, ExpectedValue = l.ExpectedValue, Notes = l.Notes, AssignedTo = l.AssignedTo,
            Owners = OwnerItems(l.AssignedTo), StatusOptions = LeadStatuses.SelectableFrom(l.Status).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, LeadFormViewModel model)
    {
        if (id != model.LeadId) return BadRequest();
        if (ModelState.IsValid)
        {
            var res = await _leads.UpdateAsync(id, model);
            if (Reject(res) is { } r) return r;
            if (res.Succeeded)
            {
                FlashSuccess("Lead updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(res);
        }
        var current = await _db.Leads.AsNoTracking().Where(l => l.LeadId == id).Select(l => l.Status).FirstOrDefaultAsync();
        model.Owners = OwnerItems(model.AssignedTo);
        model.StatusOptions = LeadStatuses.SelectableFrom(current ?? model.Status).ToList();
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, string status)
    {
        var res = await _leads.ChangeStatusAsync(id, status);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess($"Lead status changed to {status}.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Status could not be changed.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var res = await _leads.DeleteAsync(id);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Lead deleted.");
        else
        {
            FlashError(res.Errors.FirstOrDefault().Message ?? "Lead could not be deleted.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var res = await _leads.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var l = res.Value!;
        if (l.Status != LeadStatuses.Qualified)
        {
            FlashError("Only Qualified leads can be converted.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(await BuildConvertModel(l, new ConvertLeadViewModel
        {
            LeadId = id, OpportunityName = (l.CompanyName ?? l.LeadName) + " - Opportunity", Amount = l.ExpectedValue
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Convert(int id, ConvertLeadViewModel model)
    {
        if (id != model.LeadId) return BadRequest();
        if (ModelState.IsValid)
        {
            var res = await _leads.ConvertAsync(id, model);
            if (Reject(res) is { } r) return r;
            if (res.Succeeded)
            {
                var v = res.Value!;
                FlashSuccess($"Lead converted. {(v.CustomerWasExisting ? "Linked to existing customer" : "Customer created:")} {v.Customer.CustomerCode}; opportunity #{v.Opportunity.OpportunityId} created.");
                return RedirectToAction("Details", "Customers", new { id = v.Customer.CustomerId });
            }
            AddErrors(res);
        }
        var lead = await _leads.GetAsync(id);
        if (Reject(lead) is { } rr) return rr;
        return View(await BuildConvertModel(lead.Value!, model));
    }

    private async Task<ConvertLeadViewModel> BuildConvertModel(Lead lead, ConvertLeadViewModel model)
    {
        model.Lead = lead;
        var email = Rules.NormalizeEmail(lead.Email);
        var phone = Rules.NormalizePhone(lead.Phone);
        model.ExistingCustomer = await Scope.Customers().AsNoTracking().FirstOrDefaultAsync(c => c.Email == email || c.Phone == phone);
        return model;
    }
}
