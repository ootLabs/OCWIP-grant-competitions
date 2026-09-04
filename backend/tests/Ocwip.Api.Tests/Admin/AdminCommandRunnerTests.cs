using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Admin;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>
/// The console and exit code side of the administrative command (T-13.1).
///
/// All but the last need no database, and that is the point: a mistyped command
/// line has to be rejected before anything connects, and a missing connection
/// string has to say so rather than fail somewhere deeper. The last one is about
/// a write the database refuses, so it is the one that needs a real one.
/// </summary>
public sealed class AdminCommandRunnerTests
{
    private static IConfiguration EmptyConfiguration =>
        new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationFor(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
            })
            .Build();

    [Fact]
    public async Task A_broken_command_line_prints_the_reason_and_the_usage()
    {
        // Arrange
        await using var output = new StringWriter();

        // Act
        var exitCode = await AdminCommandRunner.RunAsync(
            ["grant-role", "--role", "Operator"],
            EmptyConfiguration,
            output);

        // Assert
        // Rejected without a connection string in sight, so the check cannot
        // depend on reaching a database.
        Assert.Equal(AdminCommandRunner.Failure, exitCode);

        var text = output.ToString();
        Assert.Contains("--email is missing", text);
        Assert.Contains("grant-role --email", text);
    }

    [Fact]
    public async Task A_missing_connection_string_is_reported_not_guessed()
    {
        // Arrange
        await using var output = new StringWriter();

        // Act
        var exitCode = await AdminCommandRunner.RunAsync(
            ["grant-role", "--email", "adam@example.org", "--role", "Operator"],
            EmptyConfiguration,
            output);

        // Assert
        // Same reason as in AppDbContextFactory: "db" is the compose service
        // name in half the projects on one laptop, so a guessed address would
        // let this command grant the operator role in somebody else's database
        // and report success.
        Assert.Equal(AdminCommandRunner.Failure, exitCode);
        Assert.Contains("ConnectionStrings__Postgres", output.ToString());
    }

    [Fact]
    public async Task An_unreachable_database_fails_with_a_code_not_a_stack_trace()
    {
        // Arrange
        // .invalid never resolves (RFC 2606), so this fails fast without a
        // database and without waiting on a real network timeout.
        var configuration = ConfigurationFor(
            "Host=nosuchhost.invalid;Port=5432;Database=ocwip;"
            + "Username=ocwip;Password=ocwip;Timeout=2");

        await using var output = new StringWriter();

        // Act
        var exitCode = await AdminCommandRunner.RunAsync(
            ["grant-role", "--email", "adam@example.org", "--role", "Operator"],
            configuration,
            output);

        // Assert
        // Without the catch this exits 134 with an unhandled exception, and 134
        // is not the Failure a wrapping script tests for. The same path covers a
        // database whose migrations have not been applied, which is reachable
        // because this command runs before ApplyPendingMigrations.
        Assert.Equal(AdminCommandRunner.Failure, exitCode);
        Assert.Contains("No role was granted", output.ToString());
        Assert.DoesNotContain("   at ", output.ToString());
    }

    [Fact]
    public async Task A_mistyped_verb_is_refused_instead_of_starting_a_server()
    {
        // Arrange
        await using var output = new StringWriter();

        // Act
        var exitCode = await AdminCommandRunner.RunAsync(
            ["grant_role", "--email", "adam@example.org", "--role", "Operator"],
            EmptyConfiguration,
            output);

        // Assert
        // Program.cs routes every verb here, so this message is reachable in
        // production and not only from a test. Before that, an underscore
        // instead of a dash booted a second api process in the container.
        Assert.Equal(AdminCommandRunner.Failure, exitCode);
        Assert.Contains("Unknown command", output.ToString());
    }

    [Fact]
    public async Task An_unknown_verb_never_reaches_a_database()
    {
        // Arrange
        await using var output = new StringWriter();

        // Act
        var exitCode = await AdminCommandRunner.RunAsync(
            ["drop-everything"],
            EmptyConfiguration,
            output);

        // Assert
        Assert.Equal(AdminCommandRunner.Failure, exitCode);
        Assert.Contains("Unknown command", output.ToString());
    }

    [RequiresDatabaseFact]
    public async Task An_update_that_reaches_no_row_fails_with_a_code_not_a_stack_trace()
    {
        // Arrange
        // ConcurrencyStamp is a concurrency token since T-12.0
        // (Data/Configurations/UserConfiguration.cs), so an update that matches
        // no row surfaces as DbUpdateConcurrencyException instead of as a
        // silently ignored write. Review caught that the catch here did not
        // cover it: DbUpdateException derives from neither DbException nor
        // InvalidOperationException, so the command exited 134 with a stack
        // trace, and 134 is not the Failure a wrapping script tests for.
        //
        // The zero rows come from a rule on the table rather than from a real
        // race, because this command reads and writes inside a single call and
        // nothing outside it can slip in between. DO INSTEAD NOTHING is the
        // narrowest way to produce exactly the condition EF reacts to, in a
        // database created for this test and dropped with it.
        await using var database = await ThrowawayDatabase.CreateAsync("grant_konflikt");

        var email = $"konflikt-{Guid.NewGuid():N}@example.org";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres(database.ConnectionString);

        await using (var setup = new AppDbContext(options.Options))
        {
            await setup.Database.MigrateAsync();
            setup.Users.Add(TestUser.New(email));
            await setup.SaveChangesAsync();

            await setup.Database.ExecuteSqlRawAsync(
                "CREATE RULE users_refuse_update AS ON UPDATE TO users "
                + "DO INSTEAD NOTHING");
        }

        await using var output = new StringWriter();

        // Act
        var exitCode = await AdminCommandRunner.RunAsync(
            ["grant-role", "--email", email, "--role", nameof(Role.Operator)],
            ConfigurationFor(database.ConnectionString),
            output);

        // Assert
        Assert.Equal(AdminCommandRunner.Failure, exitCode);

        // The message says nothing was written and the command can be repeated,
        // because EF's own text counts affected rows and leaves an admin with
        // no idea whether the role was granted.
        var text = output.ToString();
        Assert.Contains("No role was granted", text);
        Assert.Contains("Run the command again", text);
        Assert.DoesNotContain("   at ", text);
    }
}
