using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data;

/// <summary>
/// Contains DbContext for Competitions and FormDefinitions.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        // One decision for every timestamp in the model, not one per property:
        // docs/model-danych.md rule 2 says all timestamps are UTC, and Npgsql
        // throws instead of converting when an offset arrives.
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditedEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAuditedEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// The store default now() fires on INSERT only, so an updated row would
    /// keep reporting its creation instant. Both stamps come from the same clock
    /// so the two values are comparable; the store default stays as the backstop
    /// for inserts that never reach the change tracker.
    /// </summary>
    private void StampAuditedEntities()
    {
        var entries = ChangeTracker
            .Entries<IAuditedEntity>()
            .Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else
            {
                // Never rewritten by an update, whatever the caller passed in.
                entry.Property(x => x.CreatedAt).IsModified = false;
            }

            entry.Entity.UpdatedAt = now;
        }
    }
}
