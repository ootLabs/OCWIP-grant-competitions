using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Ocwip.Api.Data;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// What the T-12.0 migration does to data that already exists.
///
/// MigrationTests proves the chain applies, and it cannot prove any of this: it
/// migrates a database it just created, so every statement that touches rows
/// runs against an empty table. Every failure below is invisible there and
/// silent in production, which is exactly why each one gets a test.
/// </summary>
[Collection(Data.PostgresCollection.Name)]
public class IdentityMigrationTests
{
    /// <summary>
    /// The last migration before accounts moved onto Identity. Named rather
    /// than computed: this test is about that particular step, and a chain that
    /// grows past it must not quietly start testing something else.
    /// </summary>
    private const string BeforeIdentity = "20260902064108_AddUserRoleDefaultAndConstraint";

    [RequiresDatabaseFact]
    public async Task An_address_verified_before_identity_stays_confirmed()
    {
        // Arrange
        // is_verified and email_confirmed hold one fact, so the migration has to
        // carry the value over. Dropping the old column first would reset every
        // verified account to unverified, and T-12.3 gates sign in on a
        // confirmed address: those people would be locked out of accounts they
        // confirmed long ago, with nothing in any log to say why.
        await using var database = await ThrowawayDatabase.CreateAsync("mig_data");
        await using var context = CreateContext(database);

        await MigrateToAsync(context, BeforeIdentity);

        await InsertAccountAsync(context, "potwierdzony@example.org", isVerified: true);
        await InsertAccountAsync(context, "niepotwierdzony@example.org", isVerified: false);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        Assert.True(await EmailConfirmedAsync(context, "potwierdzony@example.org"));
        Assert.False(await EmailConfirmedAsync(context, "niepotwierdzony@example.org"));
    }

    [RequiresDatabaseFact]
    public async Task A_rollback_puts_the_confirmation_back_where_it_came_from()
    {
        // Arrange
        // Down() is documented as not fully reversible, and losing which
        // addresses were confirmed is not one of the parts that cannot be
        // reversed: is_verified comes back and takes the value over before the
        // Identity column is dropped. A rollback under pressure must not be the
        // moment everybody has to confirm their address again.
        await using var database = await ThrowawayDatabase.CreateAsync("mig_down");
        await using var context = CreateContext(database);

        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO users
                (first_name, last_name, email, normalized_email, password_hash,
                 email_confirmed, is_active)
            VALUES
                ('Adam', 'Testowy', 'wycofany@example.org',
                 upper(normalize('wycofany@example.org', NFC)),
                 'placeholder-not-a-hash', true, true)
            """);

        // Act
        await MigrateToAsync(context, BeforeIdentity);

        // Assert
        Assert.True(await ColumnExistsAsync(context, "is_verified"));
        Assert.False(await ColumnExistsAsync(context, "email_confirmed"));

        var verified = await context.Database
            .SqlQuery<bool>(
                $"""
                SELECT is_verified AS "Value" FROM users
                 WHERE email = 'wycofany@example.org'
                """)
            .SingleAsync();
        Assert.True(verified);
    }

    [RequiresDatabaseFact]
    public async Task Two_addresses_differing_only_in_case_stop_the_migration()
    {
        // Arrange
        // Uniqueness used to sit on the address as written, so this pair was two
        // legal accounts and is now one address. The migration cannot decide
        // which of them survives, so it has to stop with a message naming them.
        // Without the guard it stops anyway, on the unique index, with a bare
        // 23505 that names an index the operator has never seen.
        await using var database = await ThrowawayDatabase.CreateAsync("mig_case");
        await using var context = CreateContext(database);

        await MigrateToAsync(context, BeforeIdentity);

        await InsertAccountAsync(context, "Adam@example.org", isVerified: true);
        await InsertAccountAsync(context, "adam@example.org", isVerified: true);

        // Act
        var exception = await Record.ExceptionAsync(() => context.Database.MigrateAsync());

        // Assert
        Assert.NotNull(exception);
        var postgres = Assert.IsType<PostgresException>(exception.GetBaseException());
        Assert.Equal(PostgresAssert.RaisedException, postgres.SqlState);

        // The offending address, not just the fact that something collided: an
        // operator reading this has to know which rows to merge.
        Assert.Contains("adam@example.org", postgres.MessageText, StringComparison.Ordinal);

        // Nothing half applied. The guard runs before the first ALTER and the
        // whole migration is one transaction, so the database is still the one
        // that went in.
        Assert.True(await ColumnExistsAsync(context, "is_verified"));
        Assert.False(await ColumnExistsAsync(context, "email_confirmed"));
    }

    [RequiresDatabaseFact]
    public async Task An_accent_typed_the_other_way_is_normalized_the_way_identity_will_look_for_it()
    {
        // Arrange
        // Unicode has two spellings for an accented letter: one code point, or
        // the plain letter followed by a combining accent. The backfill has to
        // arrive at the value Identity's normalizer produces, and upper() alone
        // does not: it leaves the combining accent where it is, so the account
        // lands under a string UserManager never computes. Nothing fails at
        // migration time. What fails later is a sign in, a password reset and
        // grant-role, all reporting an address that plainly exists as unknown,
        // while the unique index happily takes the same address again in the
        // other spelling.
        //
        // Escapes, not the characters, because both spellings look identical in
        // an editor and a tool that normalizes this file would turn the test
        // into one that proves nothing.
        const string decomposed = "jose\u0301@example.org";

        await using var database = await ThrowawayDatabase.CreateAsync("mig_nfd");
        await using var context = CreateContext(database);

        await MigrateToAsync(context, BeforeIdentity);
        await InsertAccountAsync(context, decomposed, isVerified: true);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        var stored = await context.Database
            .SqlQuery<string>(
                $"""
                SELECT normalized_email AS "Value" FROM users
                 WHERE email = {decomposed}
                """)
            .SingleAsync();

        Assert.Equal(EmailNormalizer.Normalize(decomposed), stored);
    }

    [RequiresDatabaseFact]
    public async Task Two_addresses_differing_only_in_an_accent_stop_the_migration_too()
    {
        // Arrange
        // The guard has to group by the same expression the backfill writes.
        // These two rows are one address after normalization and were two legal
        // accounts before it, so the migration cannot decide which survives. A
        // guard that only upper cased would wave this pair through and let the
        // unique index reject it a few statements later, which is the bare 23505
        // with an unfamiliar index name that the guard exists to replace.
        const string composed = "jos\u00e9@example.org";
        const string decomposed = "jose\u0301@example.org";

        // The premise, so a future runtime that folds these on its own cannot
        // make this pass without proving anything.
        Assert.NotEqual(composed, decomposed);

        await using var database = await ThrowawayDatabase.CreateAsync("mig_akcent");
        await using var context = CreateContext(database);

        await MigrateToAsync(context, BeforeIdentity);

        await InsertAccountAsync(context, composed, isVerified: true);
        await InsertAccountAsync(context, decomposed, isVerified: true);

        // Act
        var exception = await Record.ExceptionAsync(() => context.Database.MigrateAsync());

        // Assert
        Assert.NotNull(exception);
        var postgres = Assert.IsType<PostgresException>(exception.GetBaseException());
        Assert.Equal(PostgresAssert.RaisedException, postgres.SqlState);

        // Nothing half applied: the guard runs before the first ALTER and the
        // whole migration is one transaction.
        Assert.True(await ColumnExistsAsync(context, "is_verified"));
        Assert.False(await ColumnExistsAsync(context, "email_confirmed"));
    }

    private static AppDbContext CreateContext(ThrowawayDatabase database)
    {
        // The same configuration the application and dotnet ef use.
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres(database.ConnectionString);
        return new AppDbContext(options.Options);
    }

    /// <summary>
    /// Through IMigrator, because Database.MigrateAsync only knows how to reach
    /// the end of the chain and these tests need the state before one step.
    /// </summary>
    private static Task MigrateToAsync(AppDbContext context, string targetMigration) =>
        context.GetService<IMigrator>().MigrateAsync(targetMigration);

    /// <summary>
    /// Raw SQL, because the EF model describes the schema AFTER the migration
    /// and these rows are written before it. Everything omitted here either has
    /// a store default or is nullable at this point in the chain.
    /// </summary>
    private static Task InsertAccountAsync(
        AppDbContext context,
        string email,
        bool isVerified) =>
        context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO users
                (first_name, last_name, email, password_hash, is_verified, is_active)
            VALUES
                ('Adam', 'Testowy', {email}, 'placeholder-not-a-hash',
                 {isVerified}, true)
            """);

    private static async Task<bool> EmailConfirmedAsync(AppDbContext context, string email) =>
        await context.Database
            .SqlQuery<bool>(
                $"""
                SELECT email_confirmed AS "Value" FROM users WHERE email = {email}
                """)
            .SingleAsync();

    private static async Task<bool> ColumnExistsAsync(AppDbContext context, string column) =>
        await context.Database
            .SqlQuery<bool>(
                $"""
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.columns
                     WHERE table_name = 'users' AND column_name = {column}
                ) AS "Value"
                """)
            .SingleAsync();
}
