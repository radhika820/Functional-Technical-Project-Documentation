using System.Security.Claims;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AcxiomCRM.Services;

public class JwtSettings
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "AcxiomCRM";
    public string Audience { get; set; } = "AcxiomCRM.Api";
    public int ExpiryMinutes { get; set; } = 60;
}

public interface IJwtService
{
    Task<LoginResponse> CreateTokenAsync(ApplicationUser user);
}

public class JwtService : IJwtService
{
    public const string SecurityStampClaim = "sstamp";
    private readonly JwtSettings _settings;
    private readonly UserManager<ApplicationUser> _users;

    public JwtService(JwtSettings settings, UserManager<ApplicationUser> users)
    {
        _settings = settings; _users = users;
    }

    public async Task<LoginResponse> CreateTokenAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "";
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new Claim(ClaimTypes.Role, role),
            new Claim(SecurityStampClaim, user.SecurityStamp ?? "")
        });

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_settings.Key)), SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expires,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Role = role
        };
    }
}
