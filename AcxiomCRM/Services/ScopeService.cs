using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public record UserOption(string Id, string Name);

/// <summary>
/// Resolves the signed-in user's role and data scope (Admin = everything, Manager = self + team,
/// Sales Executive = own/assigned). All CRM queries go through this so scoping is enforced on the server.
/// </summary>
public interface IScopeService
{
    Task LoadAsync();
    string UserId { get; }
    string UserName { get; }
    string Role { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    bool IsSales { get; }
    /// <summary>Null means "all owners" (Admin).</summary>
    IReadOnlyCollection<string>? VisibleOwnerIds { get; }
    IReadOnlyList<UserOption> AssignableUsers { get; }
    bool CanAccess(string? ownerId);
    bool CanAssignTo(string? userId);
    IQueryable<Customer> Customers();
    IQueryable<Lead> Leads();
    IQueryable<Opportunity> Opportunities();
    IQueryable<FollowUp> FollowUps();
    IQueryable<Activity> Activities();
}

public class ScopeService : IScopeService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private bool _loaded;
    private List<string>? _visible;
    private List<UserOption> _assignable = new();

    public ScopeService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db; _http = http;
    }

    public string UserId { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsAdmin => Role == AppRoles.Admin;
    public bool IsManager => Role == AppRoles.Manager;
    public bool IsSales => Role == AppRoles.SalesExecutive;
    public IReadOnlyCollection<string>? VisibleOwnerIds => _visible;
    public IReadOnlyList<UserOption> AssignableUsers => _assignable;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;

        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        UserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        UserName = user.Identity!.Name ?? string.Empty;
        Role = user.IsInRole(AppRoles.Admin) ? AppRoles.Admin
             : user.IsInRole(AppRoles.Manager) ? AppRoles.Manager
             : user.IsInRole(AppRoles.SalesExecutive) ? AppRoles.SalesExecutive
             : string.Empty;

        if (IsAdmin)
        {
            _visible = null;
            _assignable = await _db.Users.AsNoTracking().Where(u => u.IsActive)
                .OrderBy(u => u.FullName).Select(u => new UserOption(u.Id, u.FullName)).ToListAsync();
        }
        else if (IsManager)
        {
            var team = await _db.Users.AsNoTracking().Where(u => u.ManagerId == UserId).Select(u => u.Id).ToListAsync();
            team.Add(UserId);
            _visible = team;
            _assignable = await _db.Users.AsNoTracking().Where(u => u.IsActive && team.Contains(u.Id))
                .OrderBy(u => u.FullName).Select(u => new UserOption(u.Id, u.FullName)).ToListAsync();
        }
        else
        {
            _visible = new List<string> { UserId };
            _assignable = await _db.Users.AsNoTracking().Where(u => u.Id == UserId)
                .Select(u => new UserOption(u.Id, u.FullName)).ToListAsync();
        }
    }

    public bool CanAccess(string? ownerId) =>
        !string.IsNullOrEmpty(Role) && (_visible == null || (ownerId != null && _visible.Contains(ownerId)));

    public bool CanAssignTo(string? userId) =>
        !string.IsNullOrEmpty(userId) && _assignable.Any(a => a.Id == userId);

    // Unauthenticated/role-less callers see nothing.
    private bool None => string.IsNullOrEmpty(Role);

    public IQueryable<Customer> Customers() =>
        None ? _db.Customers.Where(_ => false) : _visible == null ? _db.Customers : _db.Customers.Where(c => _visible.Contains(c.AssignedTo));

    public IQueryable<Lead> Leads() =>
        None ? _db.Leads.Where(_ => false) : _visible == null ? _db.Leads : _db.Leads.Where(c => _visible.Contains(c.AssignedTo));

    public IQueryable<Opportunity> Opportunities() =>
        None ? _db.Opportunities.Where(_ => false) : _visible == null ? _db.Opportunities : _db.Opportunities.Where(c => _visible.Contains(c.AssignedTo));

    public IQueryable<FollowUp> FollowUps() =>
        None ? _db.FollowUps.Where(_ => false) : _visible == null ? _db.FollowUps : _db.FollowUps.Where(c => _visible.Contains(c.AssignedTo));

    public IQueryable<Activity> Activities() =>
        None ? _db.Activities.Where(_ => false) : _visible == null ? _db.Activities : _db.Activities.Where(c => _visible.Contains(c.AssignedTo));
}
