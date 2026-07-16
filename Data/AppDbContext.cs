using Microsoft.EntityFrameworkCore;
using SFSWebForm.Models;

namespace SFSWebForm.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentEmail> IncidentEmails => Set<IncidentEmail>();
    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>()
            .HasMany(i => i.Emails)
            .WithOne(e => e.Incident)
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Incident>()
            .HasMany(i => i.Updates)
            .WithOne(u => u.Incident)
            .HasForeignKey(u => u.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IncidentEmail>()
            .Property(e => e.Type)
            .HasConversion<string>();

        modelBuilder.Entity<IncidentEmail>()
            .Property(e => e.Priority)
            .HasConversion<string>();

        modelBuilder.Entity<IncidentUpdate>()
            .Property(u => u.Status)
            .HasConversion<string>();

        modelBuilder.Entity<IncidentUpdate>()
            .Property(u => u.EntryType)
            .HasConversion<string>();

        // [Required] on NextExpectedUpdate is only meant to enforce the Add Update *form*
        // requiring a value — by EF Core convention it would otherwise also make the database
        // column NOT NULL, breaking every other place that logs an IncidentUpdate audit entry
        // (Send success/failure, priority/sender changes, resolution drafts, resolve, reopen),
        // none of which have a "next expected update" to set. Overriding it back to nullable here
        // keeps the column optional while [Required] still drives MVC validation on the form.
        modelBuilder.Entity<IncidentUpdate>()
            .Property(u => u.NextExpectedUpdate)
            .IsRequired(false);

        modelBuilder.Entity<Incident>()
            .Property(i => i.Status)
            .HasConversion<string>();
    }
}
