using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Pins the SQL normalization against Identity's own normalizer.
///
/// Two different functions produce the value uniqueness stands on. Identity
/// normalizes in .NET when an account goes through UserManager, and so does
/// anything calling Data/EmailNormalizer.cs; the T-12.0 migration and
/// scripts/seed.py do it in SQL. If they disagree about an address, a seeded
/// account exists that UserManager cannot find and grant-role reports as
/// unknown, the unique index accepts a second registration of the same address,
/// and nothing anywhere fails loudly. This test is the only thing that would
/// notice.
///
/// The SQL side is upper(normalize(address, NFC)) and BOTH calls are load
/// bearing. Identity's normalizer is string.Normalize() followed by
/// ToUpperInvariant(), and string.Normalize() defaults to NFC, so upper() on
/// its own agrees with .NET for every address whose accents happen to be
/// composed and parts ways with it on the ones that are not. Review caught that
/// omission; the decomposed rows in the theory below are what keeps it caught.
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
    //
    // The last rows carry COMBINING marks instead of precomposed letters: a
    // plain letter followed by U+0301, U+0307 or U+0328, which Unicode
    // considers equal to the single code point and a database does not. Three
    // different marks, because normalize() composes them from one table and a
    // gap in it would show on one letter and not on another.
    // Written as escapes on purpose, because the two spellings are identical in
    // an editor and a tool that normalizes this file would otherwise turn these
    // rows into copies of the ones above them.
    [RequiresDatabaseTheory]
    [InlineData("adam@example.org")]
    [InlineData("Adam.Testowy@Example.ORG")]
    [InlineData("ADAM@EXAMPLE.ORG")]
    [InlineData("a.b-c+tag@sub.example.org")]
    [InlineData("lukasz.wisniewski@example.org")]
    [InlineData("łukasz.wiśniewski@example.org")]
    [InlineData("ĆMA.ŻÓŁW@example.org")]
    [InlineData("zażółć.gęślą.jaźń@example.org")]
    [InlineData("wis\u006e\u0301iewski@example.org")]
    [InlineData("z\u0307o\u0301lty@example.org")]
    [InlineData("je\u0328czmien\u0301@example.org")]
    public async Task The_sql_normalization_agrees_with_identitys_normalizer(
        string address)
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act
        var fromDatabase = await NormalizeAsync(context, address);

        // Assert
        Assert.Equal(EmailNormalizer.Normalize(address), fromDatabase);
    }

    [RequiresDatabaseFact]
    public async Task Upper_casing_alone_is_what_review_caught()
    {
        // Arrange
        // The premise of the three decomposed rows above, asserted rather than
        // assumed. Uniqueness stands on this value, so a normalize() somebody
        // deletes as redundant has to fail as a test and not as an account
        // nobody can sign into: this is the shape that failure takes.
        const string decomposed = "wis\u006e\u0301iewski@example.org";

        await using var context = _database.CreateContext();

        // Act
        var withoutNormalize = await context.Database
            .SqlQuery<string>($"SELECT upper({decomposed}) AS \"Value\"")
            .SingleAsync();

        // Assert
        Assert.NotEqual(EmailNormalizer.Normalize(decomposed), withoutNormalize);
        Assert.Equal(
            EmailNormalizer.Normalize(decomposed),
            await NormalizeAsync(context, decomposed));
    }

    [RequiresDatabaseFact]
    public async Task The_sharp_s_is_the_one_address_the_two_still_disagree_about()
    {
        // Arrange
        // A documented limit, not an accepted bug, and it is asserted so it
        // cannot be discovered by an account nobody can sign into. PostgreSQL
        // maps the sharp s to U+1E9E, .NET's invariant upper casing leaves it
        // alone, so an address containing it gets one normalized value from
        // scripts/seed.py and another from UserManager. normalize() does not
        // close this one: the sharp s is a single code point in every
        // normalization form, so there is nothing for NFC to decompose.
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
        var fromDatabase = await NormalizeAsync(context, address);

        // Assert
        Assert.NotEqual(EmailNormalizer.Normalize(address), fromDatabase);
        Assert.Equal("HANS.STRAUẞ@EXAMPLE.DE", fromDatabase);
    }

    /// <summary>
    /// The expression the migration and scripts/seed.py write, spelled once so
    /// the tests cannot pin a different one than production uses.
    /// </summary>
    private static async Task<string> NormalizeAsync(
        AppDbContext context,
        string address) =>
        await context.Database
            .SqlQuery<string>(
                $"SELECT upper(normalize({address}, NFC)) AS \"Value\"")
            .SingleAsync();
}
