using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The EF model exactly as the application builds it, without touching a
/// database.
///
/// Configuring a bare ModelBuilder with a single IEntityTypeConfiguration is not
/// the same model: it has no snake_case convention, so it cannot see a wrong
/// table name, and no ConfigureConventions, so it cannot see the UTC converter.
/// Those are the two things review caught, so the tests read the real model.
///
/// IDesignTimeModel, not DbContext.Model: the runtime model is read optimized
/// and drops check constraints, column comments and maximum lengths, because
/// queries never need them. The design time model is the one migrations are
/// generated from, which is exactly the thing under test.
/// </summary>
internal static class TestModel
{
    private static readonly Lazy<IModel> Instance = new(Build);

    public static IModel Model => Instance.Value;

    public static IEntityType EntityType<TEntity>() =>
        Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException(
            $"{typeof(TEntity).Name} is not part of the model.");

    private static IModel Build()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        // Never opened: building the model does not connect. The address is
        // deliberately not a real one so a leaking test fails loudly.
        options.UseOcwipPostgres(
            "Host=model.invalid;Database=ocwip_model_only;Username=ocwip;Password=ocwip");

        using var context = new AppDbContext(options.Options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
