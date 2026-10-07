using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

// ---------- Account ----------
public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.EmailMessage)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.EmailMessage)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm the password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password and confirmation do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Please confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Password and confirmation do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = "";
}

// ---------- Customers ----------
public class CustomerFormViewModel : CustomerInput
{
    public int CustomerId { get; set; }
    [BindNever] public List<SelectListItem>? Owners { get; set; }
}

public class CustomerListViewModel
{
    public PagedList<Customer> Data { get; set; } = new();
    public ListQuery Query { get; set; } = new();
    public List<SelectListItem> Owners { get; set; } = new();
}

public class CustomerDetailsViewModel
{
    public Customer Customer { get; set; } = null!;
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<Lead> Leads { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
    public List<AuditLog> History { get; set; } = new();
}

// ---------- Leads ----------
public class LeadFormViewModel : LeadInput
{
    public int LeadId { get; set; }
    [BindNever] public List<SelectListItem>? Owners { get; set; }
    [BindNever] public List<string>? StatusOptions { get; set; }
}

public class LeadListViewModel
{
    public PagedList<Lead> Data { get; set; } = new();
    public ListQuery Query { get; set; } = new();
    public List<SelectListItem> Owners { get; set; } = new();
}

public class LeadDetailsViewModel
{
    public Lead Lead { get; set; } = null!;
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
    public List<string> NextStatuses { get; set; } = new();
    public List<AuditLog> History { get; set; } = new();
}

public class ConvertLeadViewModel : ConvertLeadInput
{
    public int LeadId { get; set; }
    [BindNever] public Lead? Lead { get; set; }
    [BindNever] public Customer? ExistingCustomer { get; set; }
}

// ---------- Opportunities ----------
public class OpportunityFormViewModel : OpportunityInput
{
    public int OpportunityId { get; set; }
    [BindNever] public List<SelectListItem>? Owners { get; set; }
    [BindNever] public List<SelectListItem>? Customers { get; set; }
    [BindNever] public List<SelectListItem>? Leads { get; set; }
}

public class OpportunityListViewModel
{
    public PagedList<Opportunity> Data { get; set; } = new();
    public OpportunityListQuery Query { get; set; } = new();
    public List<SelectListItem> Owners { get; set; } = new();
}

public class OpportunityDetailsViewModel
{
    public Opportunity Opportunity { get; set; } = null!;
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<AuditLog> History { get; set; } = new();
}

public class PipelineBoardViewModel
{
    public Dictionary<string, List<Opportunity>> Columns { get; set; } = new();
    public decimal OpenAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

// ---------- Follow-ups ----------
public class FollowUpFormViewModel : FollowUpInput
{
    public int FollowUpId { get; set; }
    public bool IsNew { get; set; }
    [BindNever] public List<SelectListItem>? Owners { get; set; }
    [BindNever] public List<SelectListItem>? Customers { get; set; }
    [BindNever] public List<SelectListItem>? Leads { get; set; }
    [BindNever] public List<SelectListItem>? Opportunities { get; set; }
}

public class FollowUpListViewModel
{
    public PagedList<FollowUp> Data { get; set; } = new();
    public FollowUpListQuery Query { get; set; } = new();
    public List<SelectListItem> Owners { get; set; } = new();
    public bool PendingView { get; set; }
}

// ---------- Activities ----------
public class ActivityFormViewModel : ActivityInput
{
    public int ActivityId { get; set; }
    [BindNever] public List<SelectListItem>? Owners { get; set; }
    [BindNever] public List<SelectListItem>? Customers { get; set; }
    [BindNever] public List<SelectListItem>? Leads { get; set; }
}

public class ActivityListViewModel
{
    public PagedList<Activity> Data { get; set; } = new();
    public ActivityListQuery Query { get; set; } = new();
    public List<SelectListItem> Owners { get; set; } = new();
}

// ---------- Users ----------
public class UserCreateViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.EmailMessage)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = AppRoles.SalesExecutive;

    [Display(Name = "Reporting Manager")]
    public string? ManagerId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm the password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password and confirmation do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = "";

    [BindNever] public List<SelectListItem>? Managers { get; set; }
}

public class UserEditViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = "";

    [BindNever] public string Email { get; set; } = "";

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = AppRoles.SalesExecutive;

    [Display(Name = "Reporting Manager")]
    public string? ManagerId { get; set; }

    [BindNever] public List<SelectListItem>? Managers { get; set; }
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = "";
    [BindNever] public string Email { get; set; } = "";

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Please confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Password and confirmation do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = "";
}

public class UserRowViewModel
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class UserListViewModel
{
    public List<UserRowViewModel> Users { get; set; } = new();
    public string? Q { get; set; }
    public string? Role { get; set; }
    public bool CanAdminister { get; set; }
}

public class RoleRowViewModel
{
    public string Role { get; set; } = "";
    public string Scope { get; set; } = "";
    public int Users { get; set; }
    public int ActiveUsers { get; set; }
}

// ---------- Audit ----------
public class AuditListViewModel
{
    public PagedList<AuditLog> Data { get; set; } = new();
    public string? User { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? Result { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

// ---------- Misc ----------
public class SearchViewModel
{
    public string Q { get; set; } = "";
    public List<Customer> Customers { get; set; } = new();
    public List<Lead> Leads { get; set; } = new();
    public List<Opportunity> Opportunities { get; set; } = new();
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int Code { get; set; } = 500;
}
