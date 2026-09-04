using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data;

/// <summary>
/// The application's DbContext: accounts and entities, competitions and their
/// form definitions, applications and their attachments.
///
/// IdentityUserContext, NOT IdentityDbContext, and that is the reason
/// AspNetRoles and AspNetUserRoles do not exist in this schema. The role is a
/// column on the account (Models/Role.cs), so Identity's role tables would be a
/// second answer to "is this an operator", and two answers to that question is
/// one too many. See docs/architektura.md.
///
/// Users comes from the base class. Declaring it again here would hide the base
/// member, and TreatWarningsAsErrors turns that into a build failure rather than
/// a warning somebody scrolls past.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<User, Guid>(options)
{
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

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
        // First, so Identity's own configuration is in the model before
        // UserConfiguration overrides the parts of it we disagree with.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        // Identity names its tables explicitly, so the snake_case convention
        // leaves them alone and they would sit in this schema as
        // AspNetUserClaims next to form_definitions.
        //
        // Renamed here instead of in Data/Configurations/ because there is no
        // decision in it: these three tables are Identity's own, they are empty
        // today, and the only thing we have an opinion about is that one schema
        // reads in one casing. Why they exist at all, and why removing them
        // would cost more than keeping them, is in docs/model-danych.md.
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
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
