using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Admin;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>
/// The only path in this codebase that writes the role column (T-13.1), against
/// a real PostgreSQL because that is where the check constraint and the unique
/// address live.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GrantRoleCommandTests
{
    private readonly PostgresDatabaseFixture _database;

    public GrantRoleCommandTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    // Email carries a null forgiving ! at the call sites below. Identity
    // declares it as string? on IdentityUser, our schema requires it
    // (UserConfiguration.cs), and a seeded account has just been given one.
    private static string Email(string label) => $"{label}-{Guid.NewGuid():N}@example.org";

    private async Task<User> SeedAsync(
        string label,
        Role role = Role.Applicant,
        bool isActive = true)
    {
        await using var context = _database.CreateContext();
        var user = TestUser.New(Email(label), role);

        if (!isActive)
        {
            user.IsActive = false;
            user.DeactivatedAt = DateTimeOffset.UtcNow;
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_becomes_an_operator()
    {
        // Arrange
        var user = await SeedAsync("awans");

        await using var context = _database.CreateContext();

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(user.Email!, Role.Operator));

        // Assert
        Assert.Equal(GrantRoleOutcome.Granted, outcome);

        await using var reader = _database.CreateContext();
        var stored = await reader.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(Role.Operator, stored.Role);
    }

    [RequiresDatabaseFact]
    public async Task A_second_operator_is_granted_just_as_well()
    {
        // Arrange
        // The acceptance criterion about more than one operator, exercised
        // through the command rather than only through the schema: OCWIP runs
        // its competitions with more than one person, so the grant path must not
        // be the place that assumes a single one.
        var first = await SeedAsync("operator-pierwszy");
        var second = await SeedAsync("operator-drugi");

        await using var context = _database.CreateContext();

        // Act
        var firstOutcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(first.Email!, Role.Operator));
        var secondOutcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(second.Email!, Role.Operator));

        // Assert
        Assert.Equal(GrantRoleOutcome.Granted, firstOutcome);
        Assert.Equal(GrantRoleOutcome.Granted, secondOutcome);
    }

    [RequiresDatabaseFact]
    public async Task An_address_that_matches_nothing_changes_nothing()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var missing = Email("nie-istnieje");

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(missing, Role.Operator));

        // Assert
        // No account is created. The command grants a role, it is not a second
        // registration path that happens to produce an operator.
        Assert.Equal(GrantRoleOutcome.AccountNotFound, outcome);
        Assert.False(await context.Users.AnyAsync(x => x.Email == missing));
    }

    [RequiresDatabaseFact]
    public async Task An_address_in_any_case_matches_the_same_account()
    {
        // Arrange
        // This test used to assert the opposite, and the inversion is the point
        // of T-12.0: uniqueness moved onto the normalized address, so
        // "Adam@x.pl" and "adam@x.pl" are now ONE account. An admin typing the
        // address with a capital has to reach it, because being told that a real
        // address is unknown reads as a broken tool, not as a case mismatch.
        var user = await SeedAsync("wielkosc-liter");

        await using var context = _database.CreateContext();

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(user.Email!.ToUpperInvariant(), Role.Operator));

        // Assert
        Assert.Equal(GrantRoleOutcome.Granted, outcome);

        await using var reader = _database.CreateContext();
        var stored = await reader.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(Role.Operator, stored.Role);
    }

    [RequiresDatabaseFact]
    public async Task An_accent_typed_the_other_way_matches_the_same_account()
    {
        // Arrange
        // Unicode has two spellings for an accented letter: one code point, or
        // the plain letter followed by a combining accent. They are the same
        // address to a person and different strings to a database, which is why
        // Identity's normalizer runs string.Normalize() before upper casing.
        // Upper casing alone looks equivalent and is not: it would report this
        // account as unknown, and an admin told a real address does not exist
        // reasonably concludes the tool is broken.
        // Written as escapes on purpose: the two spellings look identical in an
        // editor, and a tool that normalizes this file would otherwise turn the
        // test into one that proves nothing.
        const string composed = "jos\u00e9";
        const string decomposed = "jose\u0301";

        var email = $"{composed}-{Guid.NewGuid():N}@example.org";
        var typedTheOtherWay = email.Replace(composed, decomposed, StringComparison.Ordinal);

        // The premise of the test, so a future .NET that folds these on its own
        // cannot make it pass without proving anything.
        Assert.NotEqual(email, typedTheOtherWay);

        await using (var setup = _database.CreateContext())
        {
            setup.Users.Add(TestUser.New(email));
            await setup.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(typedTheOtherWay, Role.Operator));

        // Assert
        Assert.Equal(GrantRoleOutcome.Granted, outcome);
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_account_is_not_promoted()
    {
        // Arrange
        // Rows are never deleted (retention is at least 5 years), so this state
        // is one the command will genuinely meet. A deactivated account is on no
        // list of active users, so promoting it would create a privileged
        // account nobody is looking at.
        var user = await SeedAsync("dezaktywowane", isActive: false);

        await using var context = _database.CreateContext();

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(user.Email!, Role.Operator));

        // Assert
        Assert.Equal(GrantRoleOutcome.AccountDeactivated, outcome);

        await using var reader = _database.CreateContext();
        var stored = await reader.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(Role.Applicant, stored.Role);
    }

    [RequiresDatabaseFact]
    public async Task Granting_a_role_the_account_already_holds_writes_nothing()
    {
        // Arrange
        var user = await SeedAsync("juz-operator", Role.Operator);

        await using var before = _database.CreateContext();
        var updatedAtBefore = (await before.Users.SingleAsync(x => x.Id == user.Id))
            .UpdatedAt;

        await using var context = _database.CreateContext();

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(user.Email!, Role.Operator));

        // Assert
        // A repeated run must not move updated_at, or the account reads as
        // touched by somebody at a moment when nothing happened to it.
        Assert.Equal(GrantRoleOutcome.AlreadyHeld, outcome);

        await using var reader = _database.CreateContext();
        var stored = await reader.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(Role.Operator, stored.Role);
        Assert.Equal(updatedAtBefore, stored.UpdatedAt);
    }

    [RequiresDatabaseFact]
    public async Task A_role_can_be_taken_away_the_same_way()
    {
        // Arrange
        // The card is about granting, but a grant path with no way back means
        // the only way to demote a former employee is a statement typed under
        // pressure. Same command, the other direction.
        var user = await SeedAsync("degradacja", Role.Operator);

        await using var context = _database.CreateContext();

        // Act
        var outcome = await GrantRoleCommand.ExecuteAsync(
            context,
            new GrantRoleRequest(user.Email!, Role.Applicant));

        // Assert
        Assert.Equal(GrantRoleOutcome.Granted, outcome);

        await using var reader = _database.CreateContext();
        var stored = await reader.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(Role.Applicant, stored.Role);
    }
}
