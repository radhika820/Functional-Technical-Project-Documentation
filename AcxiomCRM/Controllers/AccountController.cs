using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers;

public class AccountController : CrmController
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public AccountController(IScopeService scope, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IAuditService audit, IConfiguration config) : base(scope)
    {
        _users = users; _signIn = signIn; _audit = audit; _config = config;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var email = Rules.NormalizeEmail(model.Email);
        var user = await _users.FindByEmailAsync(email);
        const string generic = "Invalid email or password.";

        if (user == null)
        {
            await _audit.LogAsync(AuditActions.FailedLogin, AuditEntities.User, null, result: "Failure",
                details: "Unknown account", userName: email);
            ModelState.AddModelError("", generic);
            return View(model);
        }

        var check = await _signIn.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

        if (check.IsLockedOut)
        {
            await _audit.LogAsync(AuditActions.Lockout, AuditEntities.User, user.Id, result: "Failure",
                details: "Account locked after repeated failed sign-in attempts", userId: user.Id, userName: user.UserName);
            ModelState.AddModelError("", "This account is temporarily locked because of repeated failed sign-in attempts. Please try again later or contact an administrator.");
            return View(model);
        }

        if (!check.Succeeded)
        {
            await _audit.LogAsync(AuditActions.FailedLogin, AuditEntities.User, user.Id, result: "Failure",
                details: "Invalid credentials", userId: user.Id, userName: user.UserName);
            ModelState.AddModelError("", generic);
            return View(model);
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync(AuditActions.FailedLogin, AuditEntities.User, user.Id, result: "Failure",
                details: "Inactive account", userId: user.Id, userName: user.UserName);
            ModelState.AddModelError("", "This account is inactive. Please contact an administrator.");
            return View(model);
        }

        await _signIn.SignInAsync(user, isPersistent: model.RememberMe);
        await _audit.LogAsync(AuditActions.Login, AuditEntities.User, user.Id, userId: user.Id, userName: user.UserName);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (!_config.GetValue("Features:AllowSelfRegistration", true)) return NotFound();
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new RegisterViewModel());
    }

    /// <summary>Self-registration always creates a Sales Executive; elevated roles are assigned only by an Admin.</summary>
    [HttpPost, AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!_config.GetValue("Features:AllowSelfRegistration", true)) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var email = Rules.NormalizeEmail(model.Email);
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = model.FullName.Trim(), IsActive = true, CreatedDate = DateTime.UtcNow
        };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
            {
                var field = e.Code.StartsWith("Password") ? nameof(model.Password) : e.Code.Contains("Email") || e.Code.Contains("UserName") ? nameof(model.Email) : "";
                ModelState.AddModelError(field, e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail" ? "An account with this email already exists." : e.Description);
            }
            return View(model);
        }

        await _users.AddToRoleAsync(user, AppRoles.SalesExecutive);
        await _audit.LogAsync(AuditActions.Register, AuditEntities.User, user.Id, userId: user.Id, userName: user.UserName);
        await _signIn.SignInAsync(user, isPersistent: false);
        await _audit.LogAsync(AuditActions.Login, AuditEntities.User, user.Id, userId: user.Id, userName: user.UserName);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync(AuditActions.Logout, AuditEntities.User, Scope.UserId);
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            await _audit.LogAsync(AuditActions.PasswordChange, AuditEntities.User, user.Id, result: "Failure",
                details: "Password change rejected");
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Code == "PasswordMismatch" ? nameof(model.CurrentPassword) : nameof(model.NewPassword), e.Description);
            return View(model);
        }

        await _signIn.RefreshSignInAsync(user);
        await _audit.LogAsync(AuditActions.PasswordChange, AuditEntities.User, user.Id, details: "Password changed by user");
        FlashSuccess("Your password was changed.");
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
