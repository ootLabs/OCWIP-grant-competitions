using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Pins PostgreSQL's upper() against .NET's ToUpperInvariant.
///
/// Three places produce the normalized address that uniqueness stands on:
/// Identity's normalizer when an account goes through UserManager, upper() in
/// the T-12.0 migration and in scripts/seed.py, and ToUpperInvariant in
/// Admin/GrantRoleCommand.cs. If they disagree about an address, a seeded
/// account exists that UserManager cannot find and grant-role reports as
/// unknown, and nothing anywhere fails loudly. This test is the only thing that
/// would notice.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NormalizedAddressTests
{
    private readonly PostgresDatabaseFixture _database;

    public NormalizedAddressTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseTheory]
    [InlineData("adam@example.org")]
    [InlineData("Adam.Testowy@Example.ORG")]
    [InlineData("ADAM@EXAMPLE.ORG")]
    [InlineData("a.b-c+tag@sub.example.org")]
    public async Task Postgres_upper_agrees_with_ToUpperInvariant(string address)
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act
        var fromDatabase = await context.Database
            .SqlQuery<string>($"SELECT upper({address}) AS \"Value\"")
            .SingleAsync();

        // Assert
        Assert.Equal(address.ToUpperInvariant(), fromDatabase);
    }
}
