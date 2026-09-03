using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data.Configurations;
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
        // The identity user table keeps its ASP.NET Core Identity name
        // (AspNetUsers), so every not null column without a database default
        // has to be supplied here, same as an EF driven insert would.
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetUsers"
                (email, password_hash, first_name, last_name, pesel, role,
                 is_verified, deactivated_at, email_confirmed,
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
                 '0001-01-01 00:00:00+00',
                 false,
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
                 '0001-01-01 00:00:00+00',
                 false,
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
                $"SELECT password_hash AS \"Value\" FROM \"AspNetUsers\" WHERE id = {user.Id}")
            .SingleAsync();

        Assert.False(string.IsNullOrWhiteSpace(storedPasswordHash));
        Assert.NotEqual("Password123!", storedPasswordHash);
    }
}
