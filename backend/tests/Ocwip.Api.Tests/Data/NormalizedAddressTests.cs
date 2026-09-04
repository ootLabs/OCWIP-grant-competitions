using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Pins PostgreSQL's upper() against Identity's own normalizer.
///
/// Two different functions produce the value uniqueness stands on. Identity
/// normalizes in .NET when an account goes through UserManager, and so does
/// anything calling Data/EmailNormalizer.cs; the T-12.0 migration and
/// scripts/seed.py do it in SQL with upper(). If they disagree about an address,
/// a seeded account exists that UserManager cannot find and grant-role reports
/// as unknown, the unique index accepts a second registration of the same
/// address, and nothing anywhere fails loudly. This test is the only thing that
/// would notice.
///
/// Compared against the normalizer rather than against ToUpperInvariant on
/// purpose: ToUpperInvariant is a re-implementation of what Identity does, and a
/// test that pins SQL to a copy proves nothing about the original.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NormalizedAddressTests
{
    private readonly PostgresDatabaseFixture _database;

    public NormalizedAddressTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    // ASCII first, then addresses that actually exercise the two
    // implementations. An all ASCII list cannot fail: upper() and .NET agree on
    // a-z by construction, so the divergence this test exists to catch could
    // only ever appear above it.
    [RequiresDatabaseTheory]
    [InlineData("adam@example.org")]
    [InlineData("Adam.Testowy@Example.ORG")]
    [InlineData("ADAM@EXAMPLE.ORG")]
    [InlineData("a.b-c+tag@sub.example.org")]
    [InlineData("lukasz.wisniewski@example.org")]
    [InlineData("łukasz.wiśniewski@example.org")]
    [InlineData("ĆMA.ŻÓŁW@example.org")]
    [InlineData("zażółć.gęślą.jaźń@example.org")]
    public async Task Postgres_upper_agrees_with_identitys_normalizer(string address)
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act
        var fromDatabase = await UpperAsync(context, address);

        // Assert
        Assert.Equal(EmailNormalizer.Normalize(address), fromDatabase);
    }

    [RequiresDatabaseFact]
    public async Task The_sharp_s_is_the_one_address_the_two_disagree_about()
    {
        // Arrange
        // A documented limit, not an accepted bug, and it is asserted so it
        // cannot be discovered by an account nobody can sign into. PostgreSQL
        // maps the sharp s to U+1E9E, .NET's invariant upper casing leaves it
        // alone, so an address containing it gets one normalized value from
        // scripts/seed.py and another from UserManager.
        //
        // The consequence is bounded to those two SQL writers: an address that
        // registers through the API is normalized in .NET at both ends and is
        // found again. Widening either side is a schema decision (a citext
        // column, or normalizing in the application only), so it belongs to
        // whoever needs a German address, with this test as the evidence.
        //
        // If this test ever fails because the two now AGREE, that is good news:
        // delete it and move the address into the theory above.
        const string address = "hans.strauß@example.de";

        await using var context = _database.CreateContext();

        // Act
        var fromDatabase = await UpperAsync(context, address);

        // Assert
        Assert.NotEqual(EmailNormalizer.Normalize(address), fromDatabase);
        Assert.Equal("HANS.STRAUẞ@EXAMPLE.DE", fromDatabase);
    }

    private static async Task<string> UpperAsync(AppDbContext context, string address) =>
        await context.Database
            .SqlQuery<string>($"SELECT upper({address}) AS \"Value\"")
            .SingleAsync();
}
