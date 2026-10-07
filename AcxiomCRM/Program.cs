using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using AcxiomCRM.Controllers;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Consistent number/date parsing regardless of the server's regional settings (decimals use '.').
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var isDev = builder.Environment.IsDevelopment();

// ---------- Data ----------
var connectionString = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString));

// ---------- Identity: hashing, password policy, lockout, roles ----------
var sec = config.GetSection("Security");
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = sec.GetValue("MinPasswordLength", 8);
    o.Password.RequireDigit = sec.GetValue("RequireDigit", true);
    o.Password.RequireLowercase = sec.GetValue("RequireLowercase", true);
    o.Password.RequireUppercase = sec.GetValue("RequireUppercase", true);
    o.Password.RequireNonAlphanumeric = sec.GetValue("RequireNonAlphanumeric", true);
    o.Password.RequiredUniqueChars = 4;

    o.Lockout.AllowedForNewUsers = true;
    o.Lockout.MaxFailedAccessAttempts = sec.GetValue("MaxFailedAccessAttempts", 5);
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(sec.GetValue("LockoutMinutes", 15));

    o.User.RequireUniqueEmail = true;
    o.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.Cookie.Name = ".AcxiomCRM.Auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(sec.GetValue("SessionMinutes", 30));
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});
// Validate the security stamp on every request so deactivation / role change / password reset take effect immediately.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = ".AcxiomCRM.Antiforgery";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

// ---------- JWT bearer for the REST API ----------
var jwt = new JwtSettings
{
    Key = config["Jwt:Key"] ?? string.Empty,
    Issuer = config["Jwt:Issuer"] ?? "AcxiomCRM",
    Audience = config["Jwt:Audience"] ?? "AcxiomCRM.Api",
    ExpiryMinutes = config.GetValue("Jwt:ExpiryMinutes", 60)
};
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    if (!isDev) throw new InvalidOperationException("Configure Jwt:Key (min 32 chars) via environment variable Jwt__Key or a secret store.");
    jwt.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)); // dev only: tokens are invalidated on restart
}
builder.Services.AddSingleton(jwt);

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
        };
        o.Events = new JwtBearerEvents
        {
            // Reject tokens of deactivated/locked users or tokens issued before logout / password / role change.
            OnTokenValidated = async ctx =>
            {
                var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = id == null ? null : await users.FindByIdAsync(id);
                var stamp = ctx.Principal?.FindFirstValue(JwtService.SecurityStampClaim);
                if (user == null || !user.IsActive || await users.IsLockedOutAsync(user) || stamp != user.SecurityStamp)
                    ctx.Fail("The token is no longer valid.");
            }
        };
    });
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = sec.GetValue("AuthRequestsPerMinute", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- MVC ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); // anti-forgery on every state-changing MVC request
    o.Filters.Add<ScopeLoadFilter>();
});

// ---------- Application services ----------
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IScopeService, ScopeService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<IFollowUpService, FollowUpService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AcxiomCRM API", Version = "v1", Description = "Secured REST API (JWT bearer). Obtain a token from POST /api/auth/login." });
    c.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Paste the token returned by /api/auth/login."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseExceptionHandler("/Home/Error");           // never expose stack traces / DB internals to users
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
if (!isDev) app.UseHsts();
app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (isDev)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AcxiomCRM API v1"));
}

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();
