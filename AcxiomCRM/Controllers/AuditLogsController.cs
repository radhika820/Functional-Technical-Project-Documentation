using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

/// <summary>Read-only audit viewer. Admin: everything. Manager: team entries only, and only if enabled in configuration.</summary>
[Authorize(Roles = AppRoles.AdminOrManager)]
public class AuditLogsController : CrmController
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuditLogsController(IScopeService scope, ApplicationDbContext db, IConfiguration config) : base(scope)
    {
        _db = db; _config = config;
    }

    private bool Allowed => Scope.IsAdmin || (Scope.IsManager && _config.GetValue("Features:ManagerCanViewAudit", false));

    public async Task<IActionResult> Index(string? user, string? module, string? act, string? result,
        DateTime? from, DateTime? to, int page = 1)
    {
        if (!Allowed) return Forbid();

        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (Scope.VisibleOwnerIds != null) { var ids = Scope.VisibleOwnerIds; q = q.Where(a => a.UserId != null && ids.Contains(a.UserId)); }
        if (!string.IsNullOrWhiteSpace(user)) { var t = user.Trim(); q = q.Where(a => a.UserName != null && a.UserName.Contains(t)); }
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(a => a.EntityName == module);
        if (!string.IsNullOrWhiteSpace(act)) q = q.Where(a => a.Action == act);
        if (!string.IsNullOrWhiteSpace(result)) q = q.Where(a => a.Result == result);
        if (from != null) { var f = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local).ToUniversalTime(); q = q.Where(a => a.CreatedDate >= f); }
        if (to != null) { var t = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime(); q = q.Where(a => a.CreatedDate < t); }

        var data = await PagedList<AuditLog>.CreateAsync(q.OrderByDescending(a => a.CreatedDate).ThenByDescending(a => a.AuditLogId), page, 20);
        return View(new AuditListViewModel { Data = data, User = user, Module = module, Action = act, Result = result, From = from, To = to });
    }

    public async Task<IActionResult> Details(long id)
    {
        if (!Allowed) return Forbid();
        var entry = await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.AuditLogId == id);
        if (entry == null) return NotFound();
        if (Scope.VisibleOwnerIds != null && (entry.UserId == null || !Scope.VisibleOwnerIds.Contains(entry.UserId))) return Forbid();
        return View(entry);
    }
}
