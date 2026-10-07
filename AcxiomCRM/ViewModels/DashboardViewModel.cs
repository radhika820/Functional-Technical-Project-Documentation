using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardFilter
{
    /// <summary>all | today | week | month | custom</summary>
    public string Range { get; set; } = "all";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public record StageStat(string Stage, int Count, decimal Amount);
public record MonthStat(string Month, decimal Won, decimal Lost);

public class AdminStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int AuditEvents24h { get; set; }
    public int FailedLogins24h { get; set; }
}

public class DashboardViewModel
{
    public DashboardFilter Filter { get; set; } = new();
    public string RangeLabel { get; set; } = "All time";
    public string Role { get; set; } = "";

    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    public Dictionary<string, int> LeadStatusCounts { get; set; } = new();
    public List<StageStat> Pipeline { get; set; } = new();
    public List<MonthStat> MonthlySales { get; set; } = new();
    public List<FollowUp> Upcoming { get; set; } = new();
    public AdminStats? Admin { get; set; }
}
