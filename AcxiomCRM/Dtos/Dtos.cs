using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

// Response DTOs (no passwords, hashes, tokens or internal navigation graphs).

public class CustomerDto
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedDate { get; set; }

    public static CustomerDto From(Customer c) => new()
    {
        Id = c.CustomerId, CustomerCode = c.CustomerCode, CustomerName = c.CustomerName, Email = c.Email, Phone = c.Phone,
        CompanyName = c.CompanyName, Address = c.Address, City = c.City, State = c.State, Status = c.Status, Notes = c.Notes,
        AssignedTo = c.AssignedTo, AssignedToName = c.AssignedUser?.FullName, CreatedDate = c.CreatedDate
    };
}

public class LeadDto
{
    public int Id { get; set; }
    public string LeadCode { get; set; } = "";
    public string LeadName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? CompanyName { get; set; }
    public string Source { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public decimal ExpectedValue { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public DateTime CreatedDate { get; set; }

    public static LeadDto From(Lead l) => new()
    {
        Id = l.LeadId, LeadCode = l.LeadCode, LeadName = l.LeadName, Email = l.Email, Phone = l.Phone, CompanyName = l.CompanyName,
        Source = l.Source, Status = l.Status, Priority = l.Priority, ExpectedValue = l.ExpectedValue, Notes = l.Notes,
        AssignedTo = l.AssignedTo, AssignedToName = l.AssignedUser?.FullName, ConvertedCustomerId = l.ConvertedCustomerId,
        CreatedDate = l.CreatedDate
    };
}

public class OpportunityDto
{
    public int Id { get; set; }
    public string OpportunityName { get; set; } = "";
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public decimal Amount { get; set; }
    public string Stage { get; set; } = "";
    public int Probability { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = "";
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedDate { get; set; }

    public static OpportunityDto From(Opportunity o) => new()
    {
        Id = o.OpportunityId, OpportunityName = o.OpportunityName, CustomerId = o.CustomerId, CustomerName = o.Customer?.CustomerName,
        LeadId = o.LeadId, Amount = o.Amount, Stage = o.Stage, Probability = o.Probability, WeightedAmount = o.WeightedAmount,
        ExpectedCloseDate = o.ExpectedCloseDate, Status = o.Status, Source = o.Source, Notes = o.Notes, AssignedTo = o.AssignedTo,
        AssignedToName = o.AssignedUser?.FullName, CreatedDate = o.CreatedDate
    };
}

public class FollowUpDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string FollowUpType { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsOverdue { get; set; }
    public string? Remarks { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }

    public static FollowUpDto From(FollowUp f) => new()
    {
        Id = f.FollowUpId, Subject = f.Subject, CustomerId = f.CustomerId, LeadId = f.LeadId, OpportunityId = f.OpportunityId,
        FollowUpDate = f.FollowUpDate, FollowUpType = f.FollowUpType, Status = f.Status, IsOverdue = f.IsOverdue,
        Remarks = f.Remarks, AssignedTo = f.AssignedTo, AssignedToName = f.AssignedUser?.FullName
    };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
}

public class PipelineRowDto
{
    public string Group { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class PipelineReportDto
{
    public List<PipelineRowDto> ByStage { get; set; } = new();
    public List<PipelineRowDto> ByOwner { get; set; } = new();
    public decimal TotalOpenAmount { get; set; }
    public decimal TotalWeightedAmount { get; set; }
}
