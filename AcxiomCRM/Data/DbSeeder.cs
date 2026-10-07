using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class SeedUser
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = AppRoles.SalesExecutive;
    public string? ManagerEmail { get; set; }
}

/// <summary>
/// Creates the schema (EnsureCreated), the three roles and - if configured - initial users and demo data.
/// Seed credentials come from configuration (appsettings.Development.json / environment variables), never from code.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.EnsureCreatedAsync();

        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        var seedUsers = cfg.GetSection("Seed:Users").Get<List<SeedUser>>() ?? new();
        foreach (var su in seedUsers.OrderBy(u => u.Role == AppRoles.SalesExecutive ? 1 : 0))
        {
            if (await users.FindByEmailAsync(su.Email) != null) continue;
            string? managerId = null;
            if (!string.IsNullOrWhiteSpace(su.ManagerEmail))
                managerId = (await users.FindByEmailAsync(su.ManagerEmail))?.Id;

            var user = new ApplicationUser
            {
                UserName = su.Email, Email = su.Email, EmailConfirmed = true, FullName = su.FullName,
                IsActive = true, ManagerId = managerId, CreatedDate = DateTime.UtcNow
            };
            var result = await users.CreateAsync(user, su.Password);
            if (!result.Succeeded)
            {
                log.LogWarning("Could not seed user {Email}: {Errors}", su.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }
            await users.AddToRoleAsync(user, su.Role);
        }

        if (cfg.GetValue<bool>("Seed:DemoData") && !await db.Customers.AnyAsync())
            await SeedDemoDataAsync(db, users);
    }

    private static async Task SeedDemoDataAsync(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        var sales = (await users.GetUsersInRoleAsync(AppRoles.SalesExecutive)).OrderBy(u => u.Email).ToList();
        var manager = (await users.GetUsersInRoleAsync(AppRoles.Manager)).FirstOrDefault();
        if (sales.Count == 0) return;
        string Owner(int i) => sales[i % sales.Count].Id;
        var now = DateTime.UtcNow;

        var customers = new[]
        {
            new Customer { CustomerName = "Anita Rao", CompanyName = "Rao Textiles", Email = "anita.rao@example.com", Phone = "9876500001", City = "Visakhapatnam", State = "Andhra Pradesh" },
            new Customer { CustomerName = "Vikram Mehta", CompanyName = "Mehta Logistics", Email = "vikram.mehta@example.com", Phone = "9876500002", City = "Mumbai", State = "Maharashtra" },
            new Customer { CustomerName = "Sunita Iyer", CompanyName = "Iyer Foods", Email = "sunita.iyer@example.com", Phone = "9876500003", City = "Chennai", State = "Tamil Nadu" },
            new Customer { CustomerName = "Rahul Khanna", CompanyName = "Khanna Motors", Email = "rahul.khanna@example.com", Phone = "9876500004", City = "Delhi", State = "Delhi" },
            new Customer { CustomerName = "Priya Nair", CompanyName = "Nair Healthcare", Email = "priya.nair@example.com", Phone = "9876500005", City = "Kochi", State = "Kerala" },
            new Customer { CustomerName = "Arjun Reddy", CompanyName = "Reddy Constructions", Email = "arjun.reddy@example.com", Phone = "9876500006", City = "Hyderabad", State = "Telangana" }
        };
        for (var i = 0; i < customers.Length; i++)
        {
            var c = customers[i];
            c.CustomerCode = "TMP-" + Guid.NewGuid().ToString("N")[..12];
            c.AssignedTo = Owner(i);
            c.Status = CustomerStatuses.Active;
            c.CreatedDate = now.AddDays(-40 + i * 6);
            c.CreatedBy = c.AssignedTo;
        }
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
        foreach (var c in customers) c.CustomerCode = $"CUST-{c.CustomerId:D5}";

        var leadSeeds = new (string Name, string Company, string Source, string Status, string Priority, decimal Value)[]
        {
            ("Meera Joshi", "Joshi Exports", "Website", LeadStatuses.New, "High", 250000),
            ("Karan Malhotra", "KM Retail", "Referral", LeadStatuses.Contacted, "Medium", 120000),
            ("Divya Menon", "Menon Pharma", "Trade Show", LeadStatuses.Qualified, "High", 540000),
            ("Sanjay Gupta", "Gupta Steel", "Cold Call", LeadStatuses.Unqualified, "Low", 40000),
            ("Neha Kapoor", "Kapoor Events", "Social Media", LeadStatuses.Lost, "Low", 60000),
            ("Rohit Das", "Das Agro", "Email Campaign", LeadStatuses.New, "Medium", 90000),
            ("Lakshmi Pillai", "Pillai Spices", "Partner", LeadStatuses.Qualified, "Medium", 310000),
            ("Imran Sheikh", "Sheikh Auto", "Website", LeadStatuses.Contacted, "High", 420000)
        };
        var leads = new List<Lead>();
        for (var i = 0; i < leadSeeds.Length; i++)
        {
            var s = leadSeeds[i];
            leads.Add(new Lead
            {
                LeadCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                LeadName = s.Name, CompanyName = s.Company,
                Email = s.Name.ToLowerInvariant().Replace(' ', '.') + "@example.com",
                Phone = "98765" + (10000 + i * 7).ToString(),
                Source = s.Source, Status = s.Status, Priority = s.Priority, ExpectedValue = s.Value,
                AssignedTo = Owner(i), CreatedDate = now.AddDays(-30 + i * 3), CreatedBy = Owner(i)
            });
        }
        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();
        foreach (var l in leads) l.LeadCode = $"LEAD-{l.LeadId:D5}";

        var today = DateTime.Today;
        var oppSeeds = new (string Name, int Cust, string Stage, decimal Amount, int Prob, int CloseOffset)[]
        {
            ("Rao Textiles ERP rollout", 0, OpportunityStages.Qualification, 450000, 20, 45),
            ("Mehta fleet tracking", 1, OpportunityStages.Proposal, 780000, 40, 30),
            ("Iyer Foods CRM licences", 2, OpportunityStages.Negotiation, 320000, 70, 15),
            ("Khanna dealer portal", 3, OpportunityStages.Won, 950000, 100, -10),
            ("Nair clinic analytics", 4, OpportunityStages.Won, 275000, 100, -35),
            ("Reddy site monitoring", 5, OpportunityStages.Lost, 410000, 0, -20),
            ("Rao Textiles support AMC", 0, OpportunityStages.Proposal, 150000, 50, 25),
            ("Mehta analytics add-on", 1, OpportunityStages.Qualification, 220000, 25, 60)
        };
        var opps = new List<Opportunity>();
        for (var i = 0; i < oppSeeds.Length; i++)
        {
            var s = oppSeeds[i];
            var c = customers[s.Cust];
            var active = OpportunityStages.IsActive(s.Stage);
            opps.Add(new Opportunity
            {
                OpportunityName = s.Name, CustomerId = c.CustomerId, Stage = s.Stage, Status = OpportunityStatuses.FromStage(s.Stage),
                Amount = s.Amount, Probability = s.Prob, ExpectedCloseDate = today.AddDays(s.CloseOffset),
                ClosedDate = active ? null : now.AddDays(s.CloseOffset), Source = "Seed", AssignedTo = c.AssignedTo,
                CreatedDate = now.AddDays(-50 + i * 4), CreatedBy = c.AssignedTo
            });
        }
        db.Opportunities.AddRange(opps);

        db.FollowUps.AddRange(
            new FollowUp { Subject = "Discovery call", CustomerId = customers[0].CustomerId, FollowUpDate = today.AddDays(1), FollowUpType = "Call", AssignedTo = customers[0].AssignedTo, CreatedBy = customers[0].AssignedTo },
            new FollowUp { Subject = "Proposal walkthrough", CustomerId = customers[1].CustomerId, FollowUpDate = today.AddDays(3), FollowUpType = "Meeting", AssignedTo = customers[1].AssignedTo, CreatedBy = customers[1].AssignedTo },
            new FollowUp { Subject = "Send revised quote", LeadId = leads[2].LeadId, FollowUpDate = today.AddDays(2), FollowUpType = "Email", AssignedTo = leads[2].AssignedTo, CreatedBy = leads[2].AssignedTo },
            // deliberately overdue so the overdue indicators can be demonstrated (new follow-ups cannot be back-dated)
            new FollowUp { Subject = "Contract clarification", CustomerId = customers[2].CustomerId, FollowUpDate = today.AddDays(-2), FollowUpType = "Call", AssignedTo = customers[2].AssignedTo, CreatedBy = customers[2].AssignedTo },
            new FollowUp { Subject = "Intro call", LeadId = leads[1].LeadId, FollowUpDate = today.AddDays(-5), FollowUpType = "Call", Status = FollowUpStatuses.Completed, CompletedDate = now.AddDays(-5), AssignedTo = leads[1].AssignedTo, CreatedBy = leads[1].AssignedTo });

        db.Activities.AddRange(
            new Activity { ActivityType = "Call", Subject = "Qualification call", Description = "Discussed requirements and budget.", ActivityDate = today.AddDays(-3), CustomerId = customers[0].CustomerId, Status = "Completed", AssignedTo = customers[0].AssignedTo },
            new Activity { ActivityType = "Meeting", Subject = "On-site demo", ActivityDate = today.AddDays(4), CustomerId = customers[1].CustomerId, Status = "Planned", AssignedTo = customers[1].AssignedTo },
            new Activity { ActivityType = "Email", Subject = "Pricing sheet shared", ActivityDate = today.AddDays(-1), LeadId = leads[6].LeadId, Status = "Completed", AssignedTo = leads[6].AssignedTo },
            new Activity { ActivityType = "Task", Subject = "Prepare contract draft", ActivityDate = today.AddDays(5), CustomerId = customers[2].CustomerId, Status = "Planned", AssignedTo = customers[2].AssignedTo });

        await db.SaveChangesAsync();
    }
}
