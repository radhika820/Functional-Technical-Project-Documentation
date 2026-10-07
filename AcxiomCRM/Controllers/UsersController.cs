using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

/// <summary>User &amp; role administration. Admin = full; Manager = read-only view of their team (configurable).</summary>
[Authorize(Roles = AppRoles.AdminOrManager)]
public class UsersController : CrmController
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public UsersController(IScopeService scope, UserManager<ApplicationUser> users, ApplicationDbContext db,
        IAuditService audit, IConfiguration config) : base(scope)
    {
        _users = users; _db = db; _audit = audit; _config = config;
    }

    // ---------- list ----------
    public async Task<IActionResult> Index(string? q, string? role)
    {
        if (Scope.IsManager && !_config.GetValue("Features:ManagerCanViewUsers", true)) return Forbid();

        var query = _db.Users.AsNoTracking().Include(u => u.Manager).AsQueryable();
        if (Scope.VisibleOwnerIds != null) { var ids = Scope.VisibleOwnerIds; query = query.Where(u => ids.Contains(u.Id)); }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(u => u.FullName.Contains(t) || (u.Email != null && u.Email.Contains(t)));
        }
        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        var userIds = users.Select(u => u.Id).ToList();
        var roleMap = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id
                             where userIds.Contains(ur.UserId) select new { ur.UserId, Role = r.Name }).ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var rows = users.Select(u => new UserRowViewModel
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email ?? "", IsActive = u.IsActive,
            Role = roleMap.FirstOrDefault(r => r.UserId == u.Id)?.Role ?? "",
            ManagerName = u.Manager?.FullName, CreatedDate = u.CreatedDate,
            IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now, LockoutEnd = u.LockoutEnd, FailedAttempts = u.AccessFailedCount
        }).Where(r => string.IsNullOrEmpty(role) || r.Role == role).ToList();

        return View(new UserListViewModel { Users = rows, Q = q, Role = role, CanAdminister = Scope.IsAdmin });
    }

    // ---------- create ----------
    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Create() => View(new UserCreateViewModel { Managers = await ManagerItems() });

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!AppRoles.All.Contains(model.Role)) ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        var email = Rules.NormalizeEmail(model.Email);
        string? managerId = null;
        if (model.Role == AppRoles.SalesExecutive && !string.IsNullOrEmpty(model.ManagerId))
        {
            if (await IsManager(model.ManagerId)) managerId = model.ManagerId;
            else ModelState.AddModelError(nameof(model.ManagerId), "Select a valid manager.");
        }

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, FullName = model.FullName.Trim(),
                IsActive = model.IsActive, ManagerId = managerId, CreatedDate = DateTime.UtcNow
            };
            var result = await _users.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _users.AddToRoleAsync(user, model.Role);
                await _audit.LogAsync(AuditActions.Create, AuditEntities.User, user.Id, null, AuditSnapshot.Of(user),
                    details: $"User created with role {model.Role}");
                FlashSuccess($"User {email} created.");
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code.StartsWith("Password") ? nameof(model.Password) : nameof(model.Email),
                    e.Code is "DuplicateUserName" or "DuplicateEmail" ? "A user with this email already exists." : e.Description);
        }
        model.Managers = await ManagerItems(model.ManagerId);
        return View(model);
    }

    // ---------- edit ----------
    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        var role = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? AppRoles.SalesExecutive;
        return View(new UserEditViewModel
        {
            Id = user.Id, FullName = user.FullName, Email = user.Email ?? "", Role = role, ManagerId = user.ManagerId,
            Managers = await ManagerItems(user.ManagerId, excludeId: user.Id)
        });
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Edit(string id, UserEditViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        model.Email = user.Email ?? "";

        var oldRole = (await _users.GetRolesAsync(user)).FirstOrDefault() ?? "";
        if (!AppRoles.All.Contains(model.Role)) ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        if (oldRole == AppRoles.Admin && model.Role != AppRoles.Admin)
        {
            if (user.Id == Scope.UserId) ModelState.AddModelError(nameof(model.Role), "You cannot remove your own Admin role.");
            else if (await ActiveAdminCount(excluding: user.Id) == 0) ModelState.AddModelError(nameof(model.Role), "At least one active Admin is required.");
        }
        string? managerId = null;
        if (model.Role == AppRoles.SalesExecutive && !string.IsNullOrEmpty(model.ManagerId))
        {
            if (model.ManagerId != user.Id && await IsManager(model.ManagerId)) managerId = model.ManagerId;
            else ModelState.AddModelError(nameof(model.ManagerId), "Select a valid manager.");
        }

        if (ModelState.IsValid)
        {
            var old = AuditSnapshot.Of(user);
            user.FullName = model.FullName.Trim();
            user.ManagerId = managerId;
            var update = await _users.UpdateAsync(user);
            if (!update.Succeeded)
            {
                ModelState.AddModelError("", "The user could not be updated.");
            }
            else
            {
                if (oldRole != model.Role)
                {
                    if (!string.IsNullOrEmpty(oldRole)) await _users.RemoveFromRoleAsync(user, oldRole);
                    await _users.AddToRoleAsync(user, model.Role);
                    await _users.UpdateSecurityStampAsync(user); // existing sessions/tokens pick up the new role immediately
                    await _audit.LogAsync(AuditActions.RoleChange, AuditEntities.User, user.Id, oldRole, model.Role,
                        details: $"Role changed for {user.Email}");
                }
                await _audit.LogAsync(AuditActions.Update, AuditEntities.User, user.Id, old, AuditSnapshot.Of(user));
                FlashSuccess("User updated.");
                return RedirectToAction(nameof(Index));
            }
        }
        model.Managers = await ManagerItems(model.ManagerId, excludeId: user.Id);
        return View(model);
    }

    // ---------- activate / deactivate / unlock ----------
    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<IActionResult> SetActive(string id, bool active)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (!active)
        {
            if (user.Id == Scope.UserId) { FlashError("You cannot deactivate your own account."); return RedirectToAction(nameof(Index)); }
            if (await _users.IsInRoleAsync(user, AppRoles.Admin) && await ActiveAdminCount(excluding: user.Id) == 0)
            { FlashError("At least one active Admin is required."); return RedirectToAction(nameof(Index)); }
        }

        user.IsActive = active;
        await _users.UpdateAsync(user);
        await _users.UpdateSecurityStampAsync(user); // terminates existing sessions when deactivating
        await _audit.LogAsync(active ? AuditActions.Activate : AuditActions.Deactivate, AuditEntities.User, user.Id,
            (!active).ToString(), active.ToString(), details: $"{(active ? "Activated" : "Deactivated")} {user.Email}");
        FlashSuccess($"{user.Email} {(active ? "activated" : "deactivated")}.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync(AuditActions.Unlock, AuditEntities.User, user.Id, details: $"Account unlocked: {user.Email}");
        FlashSuccess($"{user.Email} unlocked.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- password reset (admin) ----------
    [Authorize(Roles = AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        return View(new ResetPasswordViewModel { Id = user.Id, Email = user.Email ?? "" });
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, ResetPasswordViewModel model)
    {
        if (id != model.Id) return BadRequest();
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();
        model.Email = user.Email ?? "";
        if (!ModelState.IsValid) return View(model);

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(nameof(model.NewPassword), e.Description);
            return View(model);
        }
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync(AuditActions.PasswordReset, AuditEntities.User, user.Id, details: $"Password reset by administrator for {user.Email}");
        FlashSuccess($"Password reset for {user.Email}.");
        return RedirectToAction(nameof(Index));
    }

    // ---------- roles & permissions ----------
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Roles()
    {
        var scopes = new Dictionary<string, string>
        {
            [AppRoles.Admin] = "Full application administration, users/roles, audit logs, configuration, all CRM records and reports.",
            [AppRoles.Manager] = "Team customers, leads, opportunities, follow-ups and management reports; no unrestricted security administration.",
            [AppRoles.SalesExecutive] = "Own/assigned customers, leads, opportunities, follow-ups and activities; own/assigned reports."
        };
        var rows = new List<RoleRowViewModel>();
        foreach (var role in AppRoles.All)
        {
            var members = await _users.GetUsersInRoleAsync(role);
            rows.Add(new RoleRowViewModel { Role = role, Scope = scopes[role], Users = members.Count, ActiveUsers = members.Count(m => m.IsActive) });
        }
        return View(rows);
    }

    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult Permissions() => View();

    // ---------- helpers ----------
    private async Task<bool> IsManager(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;
        var u = await _users.FindByIdAsync(userId);
        return u != null && u.IsActive && await _users.IsInRoleAsync(u, AppRoles.Manager);
    }

    private async Task<int> ActiveAdminCount(string excluding)
    {
        var admins = await _users.GetUsersInRoleAsync(AppRoles.Admin);
        return admins.Count(a => a.IsActive && a.Id != excluding);
    }

    private async Task<List<SelectListItem>> ManagerItems(string? selected = null, string? excludeId = null)
    {
        var managers = (await _users.GetUsersInRoleAsync(AppRoles.Manager)).Where(m => m.IsActive && m.Id != excludeId).OrderBy(m => m.FullName);
        return managers.Select(m => new SelectListItem(m.FullName, m.Id, m.Id == selected)).ToList();
    }
}
