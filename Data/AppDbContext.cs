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

        modelBuilder.Entity<IncidentUpdate>()
            .Property(u => u.Status)
            .HasConversion<string>();

        modelBuilder.Entity<IncidentUpdate>()
            .Property(u => u.EntryType)
            .HasConversion<string>();

        modelBuilder.Entity<Incident>()
            .Property(i => i.Status)
            .HasConversion<string>();
    }
}
