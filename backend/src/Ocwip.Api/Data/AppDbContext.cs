using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

        // Identity points all three at the account with ON DELETE CASCADE, and
        // docs/model-danych.md rule 1 allows none anywhere: retention is at
        // least 5 years, so a DELETE that succeeds quietly is the failure mode
        // the rule exists to prevent. Emptiness today is not an argument for
        // leaving it, because a cascade only matters on the day somebody
        // deletes an account, which is the day nothing may be helping them.
        //
        // Through the metadata rather than HasMany().WithOne(): these
        // relationships carry no navigation on either side, so a fluent call
        // has nothing to match them by and would configure a SECOND foreign
        // key beside the one Identity already declared.
        RefuseToCascade(modelBuilder.Entity<IdentityUserClaim<Guid>>());
        RefuseToCascade(modelBuilder.Entity<IdentityUserLogin<Guid>>());
        RefuseToCascade(modelBuilder.Entity<IdentityUserToken<Guid>>());
    }

    private static void RefuseToCascade<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        foreach (var foreignKey in builder.Metadata.GetForeignKeys())
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
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
