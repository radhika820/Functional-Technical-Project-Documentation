using System.Linq.Expressions;

namespace AcxiomCRM.Services;

public class ListQuery
{
    public string? Q { get; set; }
    public string? Status { get; set; }
    public string? AssignedTo { get; set; }
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public bool Desc => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
}

public class OpportunityListQuery : ListQuery
{
    public string? Stage { get; set; }
}

public class FollowUpListQuery : ListQuery
{
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    public bool PendingOnly { get; set; }
}

public class ActivityListQuery : ListQuery
{
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

internal static class Sorting
{
    public static IOrderedQueryable<T> By<T, K>(IQueryable<T> q, Expression<Func<T, K>> key, bool desc) =>
        desc ? q.OrderByDescending(key) : q.OrderBy(key);
}
