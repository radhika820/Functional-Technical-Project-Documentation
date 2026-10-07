using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Manager).WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.CustomerCode).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();   // email uniqueness enforced in DB as well as in the service
            e.HasIndex(x => x.Phone).IsUnique();   // phone uniqueness
            e.HasIndex(x => x.AssignedTo);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Lead>(e =>
        {
            e.HasIndex(x => x.LeadCode).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AssignedTo);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ConvertedCustomer).WithMany().HasForeignKey(x => x.ConvertedCustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Opportunity>(e =>
        {
            e.HasIndex(x => x.Stage);
            e.HasIndex(x => x.AssignedTo);
            e.HasOne(x => x.Customer).WithMany(c => c.Opportunities).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<FollowUp>(e =>
        {
            e.HasIndex(x => new { x.Status, x.FollowUpDate });
            e.HasIndex(x => x.AssignedTo);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Activity>(e =>
        {
            e.HasIndex(x => x.AssignedTo);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.CreatedDate);
            e.HasIndex(x => new { x.EntityName, x.RecordId });
            e.HasIndex(x => x.UserId);
        });
    }

    // ---- Audit log is append-only: block any update/delete of existing audit rows ----
    private void GuardAuditLog()
    {
        var tampered = ChangeTracker.Entries<AuditLog>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (tampered)
            throw new InvalidOperationException("Audit log entries are append-only and cannot be modified or deleted.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditLog();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAuditLog();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
