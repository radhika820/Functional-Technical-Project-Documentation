using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Customer
{
    public int CustomerId { get; set; }
    [MaxLength(20)] public string CustomerCode { get; set; } = string.Empty;
    [MaxLength(150)] public string CustomerName { get; set; } = string.Empty;
    [MaxLength(150)] public string Email { get; set; } = string.Empty;
    [MaxLength(15)] public string Phone { get; set; } = string.Empty;
    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    [MaxLength(20)] public string Status { get; set; } = CustomerStatuses.Active;
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    [MaxLength(450)] public string? ModifiedBy { get; set; }

    [MaxLength(450)] public string AssignedTo { get; set; } = string.Empty;
    public ApplicationUser? AssignedUser { get; set; }

    public List<Opportunity> Opportunities { get; set; } = new();
}

public class Lead
{
    public int LeadId { get; set; }
    [MaxLength(20)] public string LeadCode { get; set; } = string.Empty;
    [MaxLength(150)] public string LeadName { get; set; } = string.Empty;
    [MaxLength(150)] public string Email { get; set; } = string.Empty;
    [MaxLength(15)] public string Phone { get; set; } = string.Empty;
    [MaxLength(150)] public string? CompanyName { get; set; }
    [MaxLength(50)] public string Source { get; set; } = "Other";
    [MaxLength(20)] public string Status { get; set; } = LeadStatuses.New;
    [MaxLength(10)] public string Priority { get; set; } = "Medium";
    [Column(TypeName = "decimal(18,2)")] public decimal ExpectedValue { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    [MaxLength(450)] public string AssignedTo { get; set; } = string.Empty;
    public ApplicationUser? AssignedUser { get; set; }

    public int? ConvertedCustomerId { get; set; }
    public Customer? ConvertedCustomer { get; set; }
    public DateTime? ConvertedDate { get; set; }
}

public class Opportunity
{
    public int OpportunityId { get; set; }
    [MaxLength(150)] public string OpportunityName { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [MaxLength(20)] public string Stage { get; set; } = OpportunityStages.Qualification;
    public int Probability { get; set; }
    [Column(TypeName = "date")] public DateTime ExpectedCloseDate { get; set; }
    [MaxLength(10)] public string Status { get; set; } = OpportunityStatuses.Open;
    [MaxLength(50)] public string? Source { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    [MaxLength(450)] public string AssignedTo { get; set; } = string.Empty;
    public ApplicationUser? AssignedUser { get; set; }

    [NotMapped] public decimal WeightedAmount => Math.Round(Amount * Probability / 100m, 2);
    [NotMapped] public bool IsActive => OpportunityStages.IsActive(Stage);
}

public class FollowUp
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    [MaxLength(150)] public string Subject { get; set; } = string.Empty;
    [Column(TypeName = "date")] public DateTime FollowUpDate { get; set; }
    [MaxLength(20)] public string FollowUpType { get; set; } = "Call";
    [MaxLength(1000)] public string? Remarks { get; set; }
    [MaxLength(20)] public string Status { get; set; } = FollowUpStatuses.Planned;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? CreatedBy { get; set; }
    public DateTime? CompletedDate { get; set; }

    [MaxLength(450)] public string AssignedTo { get; set; } = string.Empty;
    public ApplicationUser? AssignedUser { get; set; }

    [NotMapped] public bool IsOverdue => Status == FollowUpStatuses.Planned && FollowUpDate.Date < DateTime.Today;
}

public class Activity
{
    public int ActivityId { get; set; }
    [MaxLength(20)] public string ActivityType { get; set; } = "Call";
    [MaxLength(150)] public string Subject { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Planned";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(450)] public string AssignedTo { get; set; } = string.Empty;
    public ApplicationUser? AssignedUser { get; set; }
}

/// <summary>Append-only audit trail. Rows are never updated or deleted by the application.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [MaxLength(256)] public string? UserName { get; set; }
    [MaxLength(50)] public string Action { get; set; } = string.Empty;
    [MaxLength(50)] public string EntityName { get; set; } = string.Empty;
    [MaxLength(50)] public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    [MaxLength(10)] public string Result { get; set; } = "Success";
    [MaxLength(500)] public string? Details { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? IpAddress { get; set; }
}
