using Microsoft.Extensions.Configuration;
using Ocwip.Api.Admin;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>
/// The console and exit code side of the administrative command (T-13.1).
///
/// None of these needs a database, and that is the point: a mistyped command
/// line has to be rejected before anything connects, and a missing connection
/// string has to say so rather than fail somewhere deeper.
/// </summary>
public sealed class AdminCommandRunnerTests
{
    private static IConfiguration EmptyConfiguration =>
        new ConfigurationBuilder().Build();

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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] =
                    "Host=nosuchhost.invalid;Port=5432;Database=ocwip;"
                    + "Username=ocwip;Password=ocwip;Timeout=2",
            })
            .Build();

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
}
