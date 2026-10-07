using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

/// <summary>Application user built on ASP.NET Core Identity (password hashing, lockout and roles are framework-managed).</summary>
public class ApplicationUser : IdentityUser
{
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Reporting manager (for Sales Executives). Defines a Manager's "team".</summary>
    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }
}
