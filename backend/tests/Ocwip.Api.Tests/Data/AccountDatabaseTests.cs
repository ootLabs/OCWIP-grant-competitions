using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Account and entity invariants on a real PostgreSQL. These two tables reached
/// the schema together with the application, so this class also carries the
/// regression for timestamps that used to be DateTime.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public AccountDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    private static string Email(string label) => $"{label}-{Guid.NewGuid():N}@example.org";

    [RequiresDatabaseFact]
    public async Task Two_accounts_on_one_email_address_are_refused()
    {
        // Arrange
        // Enforced here and not by a SELECT before the INSERT, which loses the
        // race against a second registration arriving at the same moment.
        var email = Email("duplikat");

        await using (var first = _database.CreateContext())
        {
            first.Users.Add(TestUser.New(email));
            await first.SaveChangesAsync();
        }

        await using var second = _database.CreateContext();
        second.Users.Add(TestUser.New(email));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_users_normalized_email", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Two_accounts_differing_only_in_case_are_refused()
    {
        // Arrange
        // The decision T-12.0 took and the schema now enforces: "Adam@x.pl" and
        // "adam@x.pl" are ONE account. Uniqueness used to sit on the address as
        // written, so both were accepted and a password reset had two rows to
        // choose between.
        var email = Email("wielkosc-liter");

        await using (var first = _database.CreateContext())
        {
            first.Users.Add(TestUser.New(email));
            await first.SaveChangesAsync();
        }

        await using var second = _database.CreateContext();
        second.Users.Add(TestUser.New(email.ToUpperInvariant()));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_users_normalized_email", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task An_insert_beside_ef_lands_on_the_identity_defaults()
    {
        // Arrange
        // scripts/seed.py inserts accounts with raw SQL, and so do the schema
        // tests, so every NOT NULL column Identity added has to carry a store
        // default or those inserts stop working. Those defaults are the reason
        // this test exists, not a tidy afterthought.
        //
        // normalized_email is listed on purpose and is the one new NOT NULL
        // column with NO default: uniqueness stands on it, and an account
        // without one would collide with nobody.
        var email = Email("obok-ef");

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
        Assert.False(stored.EmailConfirmed);
        Assert.True(stored.LockoutEnabled);
        Assert.Equal(0, stored.AccessFailedCount);
        Assert.Equal(Role.Applicant, stored.Role);
    }

    [RequiresDatabaseFact]
    public async Task An_account_written_with_lockout_off_keeps_it_off()
    {
        // Arrange
        // The store default on lockout_enabled is true, which is what the test
        // above wants, and it is also a trap: EF leaves a property out of the
        // INSERT while it still holds its sentinel, and a bool inherited from
        // IdentityUser starts at false. If the sentinel stayed there, false, the
        // only value worth writing, would be the one value EF drops, and this
        // row would come back with lockout ENABLED while the object that wrote
        // it said the opposite. T-12.3 may well turn lockout off for new
        // accounts, and it would be reading a column that disagrees with the
        // code. HasDefaultValue keeps the sentinel with the default, and this
        // test is what proves it end to end rather than in metadata.
        var email = Email("bez-blokady");
        var user = TestUser.New(email);
        user.LockoutEnabled = false;

        await using var context = _database.CreateContext();

        // Act
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        await using var reader = _database.CreateContext();
        var stored = await reader.Users.SingleAsync(x => x.Email == email);
        Assert.False(stored.LockoutEnabled);
    }

    [RequiresDatabaseFact]
    public async Task Two_accounts_on_one_entity_are_refused()
    {
        // Arrange
        // One to one, which docs/model-danych.md lists as an ASSUMPTION to
        // confirm: we do not know whether several people in one organisation
        // file applications from separate accounts. If the answer is yes, this
        // test is the one that has to change, deliberately.
        await using var setup = _database.CreateContext();
        var entity = TestEntity.New("Podmiot z dwoma kontami");
        setup.Entities.Add(entity);

        var firstUser = TestUser.New(Email("pierwszy"));
        firstUser.Entity = entity;
        setup.Users.Add(firstUser);
        await setup.SaveChangesAsync();

        await using var context = _database.CreateContext();
        var secondUser = TestUser.New(Email("drugi"));
        secondUser.EntityId = entity.Id;
        context.Users.Add(secondUser);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_users_entity_id", postgres.ConstraintName);
    }

    [RequiresDatabaseTheory]
    [InlineData(Role.Operator)]
    [InlineData(Role.Reviewer)]
    public async Task An_account_with_no_entity_is_allowed(Role role)
    {
        // Arrange
        // An operator and a reviewer work for OCWIP, they do not apply for a
        // grant, so the foreign key to the entity has to stay optional.
        await using var context = _database.CreateContext();
        var user = TestUser.New(Email("bez-podmiotu"), role);
        context.Users.Add(user);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var stored = await context.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Null(stored.EntityId);
        Assert.Equal(role, stored.Role);
    }

    [RequiresDatabaseFact]
    public async Task Deleting_an_entity_that_has_an_account_is_refused()
    {
        // Arrange
        // docs/model-danych.md rule 1, on the account side as well.
        await using var setup = _database.CreateContext();
        var entity = TestEntity.New("Podmiot z kontem");
        var user = TestUser.New(Email("z-podmiotem"));
        user.Entity = entity;
        setup.Users.Add(user);
        await setup.SaveChangesAsync();

        await using var context = _database.CreateContext();
        var stored = await context.Entities.SingleAsync(x => x.Id == entity.Id);
        context.Entities.Remove(stored);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(
            PostgresAssert.ForeignKeyViolation,
            PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task An_entity_with_no_nip_and_no_address_is_allowed()
    {
        // Arrange
        // An informal group. T-11.2 rejected NOT NULL on everything for exactly
        // this row: no NIP is not broken data, it is three natural persons.
        await using var context = _database.CreateContext();
        var entity = TestEntity.New("Grupa nieformalna");
        entity.Type = EntityType.InformalGroup;
        entity.Nip = null;
        entity.Address = null;
        context.Entities.Add(entity);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var stored = await context.Entities.SingleAsync(x => x.Id == entity.Id);
        Assert.Null(stored.Nip);
        Assert.Null(stored.Address);
        Assert.Equal(EntityType.InformalGroup, stored.Type);
    }

    [RequiresDatabaseFact]
    public async Task An_account_created_with_a_non_utc_offset_is_stored_in_utc()
    {
        // Arrange
        // A regression against the shape these classes had before they reached
        // the schema. With DateTime columns the offset would have been dropped
        // rather than converted, and Npgsql throws instead of converting a
        // DateTimeOffset carrying a non zero offset, so the first operator
        // saving from a Polish browser would have broken SaveChanges.
        var instant = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.FromHours(2));

        await using var writeContext = _database.CreateContext();
        var user = TestUser.New(Email("strefa-czasowa"));
        user.DeactivatedAt = instant;
        user.IsActive = false;
        writeContext.Users.Add(user);

        // Act
        await writeContext.SaveChangesAsync();

        // Assert
        await using var readContext = _database.CreateContext();
        var stored = await readContext.Users.SingleAsync(x => x.Id == user.Id);

        Assert.NotNull(stored.DeactivatedAt);
        Assert.Equal(TimeSpan.Zero, stored.DeactivatedAt.Value.Offset);
        Assert.Equal(instant.UtcDateTime, stored.DeactivatedAt.Value.UtcDateTime);
    }

    [RequiresDatabaseFact]
    public async Task An_account_marked_inactive_without_a_date_is_refused()
    {
        // Arrange
        // The soft delete pairing, on the accounts table too: is_active = false
        // with no date gives a row nobody can date.
        await using var context = _database.CreateContext();
        var user = TestUser.New(Email("bez-daty-dezaktywacji"));
        user.IsActive = false;
        context.Users.Add(user);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_users_deactivated_at_matches_is_active",
            postgres.ConstraintName);
    }
}
