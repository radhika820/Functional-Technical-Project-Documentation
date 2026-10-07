using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

[Route("api/auth")]
public class AuthApiController : ApiBaseController
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;

    public AuthApiController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IJwtService jwt, IAuditService audit)
    {
        _users = users; _signIn = signIn; _jwt = jwt; _audit = audit;
    }

    /// <summary>Authenticate with email and password and receive a JWT bearer token.</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = Rules.NormalizeEmail(request.Email);
        var user = await _users.FindByEmailAsync(email);
        var unauthorized = Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid email or password." });

        if (user == null)
        {
            await _audit.LogAsync(AuditActions.FailedLogin, AuditEntities.User, null, result: "Failure", details: "API: unknown account", userName: email);
            return unauthorized;
        }

        var check = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            await _audit.LogAsync(AuditActions.Lockout, AuditEntities.User, user.Id, result: "Failure", details: "API: account locked",
                userId: user.Id, userName: user.UserName);
            return StatusCode(423, new ProblemDetails { Status = 423, Title = "Account temporarily locked. Try again later." });
        }
        if (!check.Succeeded || !user.IsActive)
        {
            await _audit.LogAsync(AuditActions.FailedLogin, AuditEntities.User, user.Id, result: "Failure",
                details: user.IsActive ? "API: invalid credentials" : "API: inactive account", userId: user.Id, userName: user.UserName);
            return unauthorized;
        }

        var response = await _jwt.CreateTokenAsync(user);
        await _audit.LogAsync(AuditActions.Login, AuditEntities.User, user.Id, details: "API login", userId: user.Id, userName: user.UserName);
        return Ok(response);
    }

    /// <summary>Terminate the session: all tokens issued before now stop working.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Logout()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();
        await _audit.LogAsync(AuditActions.Logout, AuditEntities.User, user.Id, details: "API logout");
        await _users.UpdateSecurityStampAsync(user);
        return NoContent();
    }
}
