using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public enum ResultStatus { Ok, NotFound, Forbidden, Invalid }

/// <summary>Outcome of a service call; carries field-level validation errors for both MVC and API callers.</summary>
public class ServiceResult<T>
{
    public ResultStatus Status { get; private set; } = ResultStatus.Ok;
    public T? Value { get; private set; }
    public List<(string Field, string Message)> Errors { get; } = new();
    public bool Succeeded => Status == ResultStatus.Ok;

    public static ServiceResult<T> Ok(T value) => new() { Value = value };
    public static ServiceResult<T> NotFound() => new() { Status = ResultStatus.NotFound };
    public static ServiceResult<T> Forbidden() => new() { Status = ResultStatus.Forbidden };
    public static ServiceResult<T> Invalid(string field, string message)
    {
        var r = new ServiceResult<T> { Status = ResultStatus.Invalid };
        r.Errors.Add((field, message));
        return r;
    }
    public static ServiceResult<T> Invalid(IEnumerable<(string Field, string Message)> errors)
    {
        var r = new ServiceResult<T> { Status = ResultStatus.Invalid };
        r.Errors.AddRange(errors);
        return r;
    }

    public void AddError(string field, string message)
    {
        Status = ResultStatus.Invalid;
        Errors.Add((field, message));
    }

    public ServiceResult<TOther> As<TOther>() => Status switch
    {
        ResultStatus.NotFound => ServiceResult<TOther>.NotFound(),
        ResultStatus.Forbidden => ServiceResult<TOther>.Forbidden(),
        _ => ServiceResult<TOther>.Invalid(Errors)
    };
}

public interface IPagedInfo
{
    int Page { get; }
    int PageSize { get; }
    int TotalCount { get; }
    int TotalPages { get; }
}

public class PagedList<T> : IPagedInfo
{
    public List<T> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> query, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedList<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}

/// <summary>Builds the JSON stored in AuditLog.OldValue/NewValue. Never includes password hashes or security stamps.</summary>
public static class AuditSnapshot
{
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PasswordResetToken", "Password", "ConfirmPassword"
    };

    public static string Of(object entity)
    {
        var dict = new SortedDictionary<string, object?>();
        foreach (var p in entity.GetType().GetProperties())
        {
            if (Skip.Contains(p.Name) || p.GetIndexParameters().Length > 0) continue;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!(t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateTimeOffset)))
                continue;
            dict[p.Name] = p.GetValue(entity);
        }
        return JsonSerializer.Serialize(dict);
    }
}

public static class Text
{
    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public static class AppFormat
{
    /// <summary>Indian digit grouping (1,00,000) without depending on ICU/culture data being installed.</summary>
    public static string Money(decimal value)
    {
        var neg = value < 0;
        var s = ((long)Math.Round(Math.Abs(value), MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        if (s.Length > 3)
        {
            var last3 = s[^3..];
            var rest = s[..^3];
            var groups = new List<string>();
            while (rest.Length > 2) { groups.Insert(0, rest[^2..]); rest = rest[..^2]; }
            if (rest.Length > 0) groups.Insert(0, rest);
            s = string.Join(",", groups) + "," + last3;
        }
        return (neg ? "-" : "") + "\u20B9" + s;
    }

    public static string Date(DateTime? value) => value?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "-";

    public static string DateTimeLocal(DateTime? utc) =>
        utc is null ? "-" : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
}
