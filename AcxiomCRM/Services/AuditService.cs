using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null,
        string? newValue = null, string result = "Success", string? details = null,
        string? userId = null, string? userName = null);
}

/// <summary>Writes append-only audit entries. Callers must never pass passwords, hashes or reset tokens.</summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger)
    {
        _db = db; _http = http; _logger = logger;
    }

    public async Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null,
        string? newValue = null, string result = "Success", string? details = null,
        string? userId = null, string? userName = null)
    {
        var ctx = _http.HttpContext;
        var principal = ctx?.User;
        userId ??= principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        userName ??= principal?.Identity?.Name;

        var entry = new AuditLog
        {
            UserId = userId,
            UserName = Trunc(userName, 256),
            Action = action,
            EntityName = entityName,
            RecordId = Trunc(recordId, 50),
            OldValue = oldValue,
            NewValue = newValue,
            Result = result,
            Details = Trunc(details, 500),
            CreatedDate = DateTime.UtcNow,
            IpAddress = Trunc(ctx?.Connection.RemoteIpAddress?.ToString(), 64)
        };

        _db.AuditLogs.Add(entry);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Auditing must not break the user's request, but the failure must be visible to operators.
            _db.Entry(entry).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            _logger.LogError(ex, "Failed to write audit entry {Action} {Entity} {RecordId}", action, entityName, recordId);
        }
    }

    private static string? Trunc(string? s, int max) => s is null ? null : (s.Length <= max ? s : s[..max]);
}
