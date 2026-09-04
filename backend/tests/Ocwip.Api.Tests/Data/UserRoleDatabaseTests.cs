using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// What the role column does on a real PostgreSQL (T-13.1).
///
/// These invariants cannot be tested against EF metadata, because the path they
/// protect is the one that never goes through EF: an operator role is granted by
/// a statement typed against the database.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserRoleDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public UserRoleDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    private static string Email(string label) => $"{label}-{Guid.NewGuid():N}@example.org";

    [RequiresDatabaseFact]
    public async Task An_insert_that_omits_the_role_lands_on_applicant()
    {
        // Arrange
        // The reason the column carries a store default at all: a seed, a psql
        // session or a migration never touches the change tracker, and an
        // account arriving without a role has to be the least privileged one,
        // not a NOT NULL error that the next person works around by picking a
        // role at random.
        var email = Email("bez-roli");

        await using var context = _database.CreateContext();

        // Act
        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO users
                (first_name, last_name, email, normalized_email,
                 password_hash, is_active)
            VALUES
                ('Adam', 'Testowy', {email}, upper({email}),
                 'placeholder-not-a-hash', true)
            """);

        // Assert
        var stored = await context.Users.SingleAsync(x => x.Email == email);
        Assert.Equal(Role.Applicant, stored.Role);
    }

    [RequiresDatabaseFact]
    public async Task A_role_outside_the_enum_is_refused()
    {
        // Arrange
        // The typo this constraint exists for. Granting the operator role by
        // hand is a supported path, and lower case would otherwise be accepted:
        // the account then holds a role no authorization rule matches, so it is
        // denied everything for a reason invisible in the row.
        await using var context = _database.CreateContext();
        var user = TestUser.New(Email("rola-mala-litera"));
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync(
                $"UPDATE users SET role = 'operator' WHERE id = {user.Id}"));

        // Assert
        Assert.Equal(PostgresAssert.CheckViolation, exception.SqlState);
        Assert.Equal("ck_users_role_is_known", exception.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Several_accounts_hold_the_operator_role_at_the_same_time()
    {
        // Arrange
        // docs/reguly-biznesowe.md: OCWIP runs its competitions with more than
        // one person. The model has to allow it, and this is the proof that
        // nothing in the schema quietly limits the role to one row.
        var label = $"operator-{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();
        context.Users.Add(TestUser.New($"{label}-1@example.org", Role.Operator));
        context.Users.Add(TestUser.New($"{label}-2@example.org", Role.Operator));

        // Act
        await context.SaveChangesAsync();

        // Assert
        // Scoped to the rows this test inserted: the database is shared across
        // the postgres collection.
        var operators = await context.Users
            .Where(x => x.Email!.StartsWith(label) && x.Role == Role.Operator)
            .CountAsync();

        Assert.Equal(2, operators);
    }
}
