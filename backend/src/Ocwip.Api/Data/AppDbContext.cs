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
}
