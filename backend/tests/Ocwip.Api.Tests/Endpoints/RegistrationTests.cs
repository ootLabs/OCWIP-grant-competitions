using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Registration database invariants verified against a real PostgreSQL database.
/// Guards the constraints, relationships, defaults, and column behavior that
/// must be enforced by the database rather than only by the API.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationTests
{
    private readonly PostgresDatabaseFixture _database;

    public RegistrationTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_user_gets_an_id_and_created_at()
    {
        // Arrange
        var email = $"user-{Guid.NewGuid()}@example.com";
        await using var context = _database.CreateContext();

        var user = TestUser.New(email);
        context.Users.Add(user);

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.NotEqual(default(DateTimeOffset), user.CreatedAt);
    }

    [RequiresDatabaseFact]
    public async Task Email_is_unique()
    {
        // Arrange
        var email = $"user-{Guid.NewGuid()}@example.com";

        await using var context = _database.CreateContext();

        var first = TestUser.New(email: email);
        var second = TestUser.New(email: email);

        context.Users.AddRange(first, second);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.NotNull(exception);
    }

    [RequiresDatabaseFact]
    public async Task An_insert_bypassing_ef_still_gets_an_id_and_created_at()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act
        // The account table keeps the name it already had rather than the
        // AspNetUsers Identity would have picked (see UserConfiguration.cs),
        // so every not null column without a database default has to be
        // supplied here, same as an EF driven insert would - is_active has
        // no store default (UserConfiguration.cs relies on the CLR property
        // initializer instead, which a raw insert bypasses), so it is spelled
        // out as true here. deactivated_at is left out instead of set to
        // NULL explicitly: its implicit NULL is the only value the check
        // constraint ck_users_deactivated_at_matches_is_active accepts
        // alongside is_active = true.
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "users"
                (email, password_hash, first_name, last_name, pesel, role,
                 email_confirmed, is_active,
                 phone_number_confirmed, two_factor_enabled, lockout_enabled,
                 access_failed_count)
            VALUES
                ('raw-user-one@example.com',
                 'test-password-hash',
                 'John',
                 'Smith',
                 '90010112345',
                 'Applicant',
                 false,
                 true,
                 false,
                 false,
                 false,
                 0),
                ('raw-user-two@example.com',
                 'test-password-hash',
                 'Jane',
                 'Smith',
                 '90020212345',
                 'Applicant',
                 false,
                 true,
                 false,
                 false,
                 false,
                 0)
            """);

        // Assert
        var rows = await context.Users
            .Where(x =>
                x.Email == "raw-user-one@example.com" ||
                x.Email == "raw-user-two@example.com")
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(Guid.Empty, rows.Select(x => x.Id));
        Assert.Equal(2, rows.Select(x => x.Id).Distinct().Count());

        Assert.All(
            rows,
            row => Assert.NotEqual(
                default(DateTimeOffset),
                row.CreatedAt));
    }

    [RequiresDatabaseFact]
    public async Task Password_hash_is_stored_instead_of_plain_text_password()
    {
        // Arrange
        var email = $"user-{Guid.NewGuid()}@example.com";
        await using var context = _database.CreateContext();

        var user = TestUser.New(email);
        context.Users.Add(user);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var storedPasswordHash = await context.Database
            .SqlQuery<string>(
                $"SELECT password_hash AS \"Value\" FROM \"users\" WHERE id = {user.Id}")
            .SingleAsync();

        Assert.False(string.IsNullOrWhiteSpace(storedPasswordHash));
        Assert.NotEqual("Password123!", storedPasswordHash);
    }
}
