using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data;

/// <summary>
/// Contains DbContext for Competitions and FormDefinitions.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
