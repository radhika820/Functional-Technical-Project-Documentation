using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.Validation;

namespace AcxiomCRM.Dtos;

// Request models shared by the MVC forms (as base classes of the ViewModels) and the REST API.
// They are DTOs - EF entities are never bound or returned directly.

public class CustomerInput
{
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Customer Name must be between 2 and 150 characters.")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.EmailMessage)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    [Display(Name = "Phone (10-digit mobile)")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company")]
    public string? CompanyName { get; set; }

    [StringLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    public string? State { get; set; }

    [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Status must be Active or Inactive.")]
    public string Status { get; set; } = CustomerStatuses.Active;

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Assigned Sales Executive")]
    public string? AssignedTo { get; set; }
}

public class LeadInput
{
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Lead Name must be between 2 and 150 characters.")]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    [RegularExpression(Rules.EmailPattern, ErrorMessage = Rules.EmailMessage)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    [Display(Name = "Phone (10-digit mobile)")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Company Name cannot exceed 150 characters.")]
    [Display(Name = "Company")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Lead Source is required.")]
    [StringLength(50)]
    [Display(Name = "Source")]
    public string Source { get; set; } = "Website";

    [Required(ErrorMessage = "Lead Status is required.")]
    [StringLength(20)]
    public string Status { get; set; } = LeadStatuses.New;

    [Required(ErrorMessage = "Priority is required.")]
    [RegularExpression("^(Low|Medium|High)$", ErrorMessage = "Priority must be Low, Medium or High.")]
    public string Priority { get; set; } = "Medium";

    [Range(0, Rules.MaxLeadValue, ErrorMessage = "Expected Value must be between 0 and 100,000,000.")]
    [Display(Name = "Expected Value (INR)")]
    public decimal ExpectedValue { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Assigned Sales Executive")]
    public string? AssignedTo { get; set; }
}

public class ConvertLeadInput
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(Rules.MinAmount, Rules.MaxAmount, ErrorMessage = Rules.AmountMessage)]
    [Display(Name = "Amount (INR)")]
    public decimal Amount { get; set; }

    [Range(0, 100, ErrorMessage = Rules.ProbabilityMessage)]
    [Display(Name = "Probability (%)")]
    public int Probability { get; set; } = 20;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [NotPastDate(ErrorMessage = Rules.CloseDateMessage)]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);
}

public class OpportunityInput
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Customer is required.")]
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }

    [Display(Name = "Originating Lead")]
    public int? LeadId { get; set; }

    [Range(Rules.MinAmount, Rules.MaxAmount, ErrorMessage = Rules.AmountMessage)]
    [Display(Name = "Amount (INR)")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    public string Stage { get; set; } = OpportunityStages.Qualification;

    [Range(0, 100, ErrorMessage = Rules.ProbabilityMessage)]
    [Display(Name = "Probability (%)")]
    public int Probability { get; set; } = 20;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [NotPastDate(ErrorMessage = Rules.CloseDateMessage, ExemptProperty = nameof(Stage), ExemptValues = "Won,Lost")]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

    [StringLength(50)]
    public string? Source { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [Display(Name = "Owner")]
    public string? AssignedTo { get; set; }
}

public class FollowUpInput : IValidatableObject
{
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Opportunity")]
    public int? OpportunityId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [NotPastDate(ErrorMessage = Rules.FollowUpDateMessage, ExemptProperty = nameof(Status), ExemptValues = "Completed,Missed,Cancelled")]
    [Display(Name = "Follow-up Date")]
    public DateTime FollowUpDate { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "Follow-up Type is required.")]
    [Display(Name = "Type")]
    public string FollowUpType { get; set; } = "Call";

    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = FollowUpStatuses.Planned;

    [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }

    [Display(Name = "Assigned User")]
    public string? AssignedTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId is null && LeadId is null && OpportunityId is null)
            yield return new ValidationResult("Select a customer, lead or opportunity for this follow-up.", new[] { nameof(CustomerId) });
        if (!FollowUpStatuses.All.Contains(Status))
            yield return new ValidationResult("Select a valid status.", new[] { nameof(Status) });
        if (!FollowUpTypes.All.Contains(FollowUpType))
            yield return new ValidationResult("Select a valid follow-up type.", new[] { nameof(FollowUpType) });
    }
}

public class ActivityInput : IValidatableObject
{
    [Required(ErrorMessage = "Activity Type is required.")]
    [Display(Name = "Type")]
    public string ActivityType { get; set; } = "Call";

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
    public string Subject { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity Date is required.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Date")]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = "Planned";

    [Display(Name = "Assigned User")]
    public string? AssignedTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Models.ActivityTypes.All.Contains(ActivityType))
            yield return new ValidationResult("Select a valid activity type.", new[] { nameof(ActivityType) });
        if (!Models.ActivityStatuses.All.Contains(Status))
            yield return new ValidationResult("Select a valid status.", new[] { nameof(Status) });
    }
}

public class CompleteFollowUpInput
{
    [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
    public string? Remarks { get; set; }
}
