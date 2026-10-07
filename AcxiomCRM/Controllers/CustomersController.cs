using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

public class CustomersController : CrmController
{
    private readonly ICustomerService _customers;
    private readonly ApplicationDbContext _db;

    public CustomersController(IScopeService scope, ICustomerService customers, ApplicationDbContext db) : base(scope)
    {
        _customers = customers; _db = db;
    }

    public async Task<IActionResult> Index([FromQuery] ListQuery query)
    {
        var vm = new CustomerListViewModel
        {
            Query = query,
            Data = await _customers.SearchAsync(query),
            Owners = OwnerItems(query.AssignedTo)
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var res = await _customers.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var c = res.Value!;

        var vm = new CustomerDetailsViewModel
        {
            Customer = c,
            Opportunities = await Scope.Opportunities().AsNoTracking().Where(o => o.CustomerId == id)
                .OrderByDescending(o => o.CreatedDate).ToListAsync(),
            Leads = await Scope.Leads().AsNoTracking().Where(l => l.ConvertedCustomerId == id).ToListAsync(),
            FollowUps = await Scope.FollowUps().AsNoTracking().Where(f => f.CustomerId == id)
                .OrderByDescending(f => f.FollowUpDate).Take(10).ToListAsync(),
            Activities = await Scope.Activities().AsNoTracking().Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.ActivityDate).Take(10).ToListAsync(),
            History = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == AuditEntities.Customer && a.RecordId == id.ToString())
                .OrderByDescending(a => a.CreatedDate).Take(15).ToListAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Create() =>
        View("Form", new CustomerFormViewModel { Owners = OwnerItems(Scope.UserId), AssignedTo = Scope.UserId });

    [HttpPost]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var res = await _customers.CreateAsync(model);
            if (res.Succeeded)
            {
                FlashSuccess($"Customer {res.Value!.CustomerCode} created.");
                return RedirectToAction(nameof(Details), new { id = res.Value.CustomerId });
            }
            AddErrors(res);
        }
        model.Owners = OwnerItems(model.AssignedTo);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var res = await _customers.GetAsync(id);
        if (Reject(res) is { } r) return r;
        var c = res.Value!;
        var vm = new CustomerFormViewModel
        {
            CustomerId = c.CustomerId, CustomerName = c.CustomerName, Email = c.Email, Phone = c.Phone, CompanyName = c.CompanyName,
            Address = c.Address, City = c.City, State = c.State, Status = c.Status, Notes = c.Notes, AssignedTo = c.AssignedTo,
            Owners = OwnerItems(c.AssignedTo)
        };
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        if (id != model.CustomerId) return BadRequest();
        if (ModelState.IsValid)
        {
            var res = await _customers.UpdateAsync(id, model);
            if (Reject(res) is { } r) return r;
            if (res.Succeeded)
            {
                FlashSuccess("Customer updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(res);
        }
        model.Owners = OwnerItems(model.AssignedTo);
        return View("Form", model);
    }

    /// <summary>"Delete" deactivates the customer (history is preserved).</summary>
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var res = await _customers.DeactivateAsync(id);
        if (Reject(res) is { } r) return r;
        if (res.Succeeded) FlashSuccess("Customer deactivated.");
        else FlashError(res.Errors.FirstOrDefault().Message ?? "Customer could not be deactivated.");
        return RedirectToAction(nameof(Index));
    }
}
