namespace AcxiomCRM.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";
    public const string AdminOrManager = Admin + "," + Manager;
    public static readonly string[] All = { Admin, Manager, SalesExecutive };
}

public static class CustomerStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public static readonly string[] All = { Active, Inactive };
}

public static class LeadStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string Unqualified = "Unqualified";
    public const string Converted = "Converted";
    public const string Lost = "Lost";
    public static readonly string[] All = { New, Contacted, Qualified, Unqualified, Converted, Lost };

    /// <summary>Configured lead workflow. Converted is reachable only through the conversion action.</summary>
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [New] = new[] { Contacted, Unqualified, Lost },
        [Contacted] = new[] { Qualified, Unqualified, Lost },
        [Qualified] = new[] { Contacted, Converted, Lost },
        [Unqualified] = new[] { New, Contacted },
        [Lost] = new[] { New },
        [Converted] = Array.Empty<string>()
    };

    public static bool IsValid(string? status) => status != null && All.Contains(status);

    public static bool CanTransition(string from, string to) =>
        from == to || (Transitions.TryGetValue(from, out var next) && next.Contains(to));

    /// <summary>Statuses a user may pick on the edit form (current + allowed next, excluding Converted).</summary>
    public static IEnumerable<string> SelectableFrom(string current) =>
        new[] { current }.Concat(Transitions.TryGetValue(current, out var n) ? n : Array.Empty<string>())
            .Where(s => s != Converted || current == Converted).Distinct();

    /// <summary>Open = not closed, lost or converted.</summary>
    public static bool IsOpen(string status) => status is New or Contacted or Qualified or Unqualified;
}

public static class LeadSources
{
    public static readonly string[] All =
        { "Website", "Referral", "Cold Call", "Email Campaign", "Social Media", "Trade Show", "Partner", "Other" };
}

public static class LeadPriorities
{
    public static readonly string[] All = { "Low", "Medium", "High" };
}

public static class OpportunityStages
{
    public const string Qualification = "Qualification";
    public const string Proposal = "Proposal";
    public const string Negotiation = "Negotiation";
    public const string Won = "Won";
    public const string Lost = "Lost";
    public static readonly string[] All = { Qualification, Proposal, Negotiation, Won, Lost };
    public static readonly string[] Active = { Qualification, Proposal, Negotiation };
    public static bool IsActive(string stage) => stage is Qualification or Proposal or Negotiation;
    public static bool IsValid(string? stage) => stage != null && All.Contains(stage);
}

public static class OpportunityStatuses
{
    public const string Open = "Open";
    public const string Won = "Won";
    public const string Lost = "Lost";
    public static string FromStage(string stage) => stage switch
    {
        OpportunityStages.Won => Won,
        OpportunityStages.Lost => Lost,
        _ => Open
    };
}

public static class FollowUpStatuses
{
    public const string Planned = "Planned";
    public const string Completed = "Completed";
    public const string Missed = "Missed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = { Planned, Completed, Missed, Cancelled };
}

public static class FollowUpTypes
{
    public static readonly string[] All = { "Call", "Meeting", "Email", "Visit" };
}

public static class ActivityTypes
{
    public static readonly string[] All = { "Call", "Meeting", "Email", "Task" };
}

public static class ActivityStatuses
{
    public static readonly string[] All = { "Planned", "Completed", "Cancelled" };
}

public static class AuditActions
{
    public const string Login = "Login";
    public const string FailedLogin = "FailedLogin";
    public const string Lockout = "Lockout";
    public const string Unlock = "Unlock";
    public const string Logout = "Logout";
    public const string Register = "Register";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Deactivate = "Deactivate";
    public const string Activate = "Activate";
    public const string RoleChange = "RoleChange";
    public const string StatusChange = "StatusChange";
    public const string Convert = "LeadConversion";
    public const string Complete = "Complete";
    public const string Reschedule = "Reschedule";
    public const string PasswordChange = "PasswordChange";
    public const string PasswordReset = "PasswordReset";
    public const string AccessDenied = "AccessDenied";
    public const string Export = "Export";
    public static readonly string[] All =
    {
        Login, FailedLogin, Lockout, Unlock, Logout, Register, Create, Update, Delete, Deactivate, Activate,
        RoleChange, StatusChange, Convert, Complete, Reschedule, PasswordChange, PasswordReset, AccessDenied, Export
    };
}

public static class AuditEntities
{
    public const string User = "User";
    public const string Customer = "Customer";
    public const string Lead = "Lead";
    public const string Opportunity = "Opportunity";
    public const string FollowUp = "FollowUp";
    public const string Activity = "Activity";
    public const string Report = "Report";
    public static readonly string[] All = { User, Customer, Lead, Opportunity, FollowUp, Activity, Report };
}

/// <summary>Project-wide validation rules (patterns, limits and messages) shared by UI, API and server.</summary>
public static class Rules
{
    public const string EmailPattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$";
    public const string EmailMessage = "Enter a valid email address.";

    /// <summary>10-digit Indian mobile number starting with 6-9.</summary>
    public const string PhonePattern = @"^[6-9][0-9]{9}$";
    public const string PhoneMessage = "Enter a valid phone number.";

    public const double MinAmount = 0.01;
    public const double MaxAmount = 9_999_999_999;
    public const double MaxLeadValue = 100_000_000;

    public const string AmountMessage = "Opportunity Amount must be greater than 0.";
    public const string ProbabilityMessage = "Probability must be between 0 and 100.";
    public const string CloseDateMessage = "Expected Close Date cannot be in the past.";
    public const string FollowUpDateMessage = "Follow-up date cannot be earlier than today.";

    public static string NormalizePhone(string? phone) => (phone ?? "").Trim();
    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
