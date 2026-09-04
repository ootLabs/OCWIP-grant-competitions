using Ocwip.Api.Admin;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>
/// Parsing of the administrative command line (T-13.1). No database and no
/// console here: everything that can be rejected before either is touched
/// should be, because the command runs against production data.
/// </summary>
public sealed class AdminCommandLineTests
{
    [Fact]
    public void No_arguments_is_not_an_admin_invocation()
    {
        // Assert
        // How the API starts in the container. If this ever returned true, the
        // service would try to grant a role instead of serving requests.
        Assert.False(AdminCommandLine.IsAdminInvocation([]));
    }

    [Theory]
    [InlineData("--urls")]
    [InlineData("--environment")]
    [InlineData("--role")]
    public void A_host_argument_is_not_an_admin_invocation(string argument)
    {
        // Assert
        // Only the first argument is examined, so nothing the web host is
        // normally given can be mistaken for the command.
        Assert.False(AdminCommandLine.IsAdminInvocation([argument, "value"]));
    }

    [Fact]
    public void The_verb_is_an_admin_invocation()
    {
        // Assert
        Assert.True(AdminCommandLine.IsAdminInvocation(["grant-role"]));
    }

    [Theory]
    [InlineData("grant_role")]
    [InlineData("grantrole")]
    [InlineData("grant-roles")]
    public void A_mistyped_verb_is_still_an_admin_invocation(string verb)
    {
        // Assert
        // Wider than "equals grant-role" on purpose. A typo matching nothing
        // here would fall through to WebApplication.CreateBuilder, and inside
        // the backend container that boots a SECOND api process: it takes the
        // exclusive lock on the migrations history, applies migrations and then
        // fights the running instance for port 8080. Somebody who mistyped a
        // role grant has to get an error, not a deployment.
        Assert.True(AdminCommandLine.IsAdminInvocation([verb, "--email", "a@b.pl"]));
    }

    [Theory]
    [InlineData("/urls")]
    [InlineData("-v")]
    public void A_setting_is_never_a_verb(string argument)
    {
        // Assert
        // Dash and slash both begin a configuration key, so neither can be a
        // command. Everything else is routed to the parser, which rejects the
        // verbs it does not know.
        Assert.False(AdminCommandLine.IsAdminInvocation([argument, "value"]));
    }

    [Fact]
    public void An_address_is_matched_with_its_whitespace_intact()
    {
        // Act
        var request = AdminCommandLine.ParseGrantRole(
            ["grant-role", "--email", " adam@example.org ", "--role", "Operator"],
            out _);

        // Assert
        // Not trimmed, because the address is matched literally against a case
        // sensitive unique index. Trimming would match a different row than the
        // characters the caller passed.
        Assert.NotNull(request);
        Assert.Equal(" adam@example.org ", request.Email);
    }

    [Fact]
    public void A_complete_command_line_parses()
    {
        // Act
        var request = AdminCommandLine.ParseGrantRole(
            ["grant-role", "--email", "adam@example.org", "--role", "Operator"],
            out var error);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(string.Empty, error);
        Assert.Equal("adam@example.org", request.Email);
        Assert.Equal(Role.Operator, request.Role);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("OPERATOR")]
    public void The_role_is_matched_without_regard_to_case(string role)
    {
        // Act
        var request = AdminCommandLine.ParseGrantRole(
            ["grant-role", "--email", "adam@example.org", "--role", role],
            out _);

        // Assert
        // Unlike the address. The address is data that has to match a row
        // exactly; the role is a name of one of three constants.
        Assert.NotNull(request);
        Assert.Equal(Role.Operator, request.Role);
    }

    [Fact]
    public void A_numeric_role_is_refused()
    {
        // Act
        var request = AdminCommandLine.ParseGrantRole(
            ["grant-role", "--email", "adam@example.org", "--role", "1"],
            out var error);

        // Assert
        // Enum.TryParse would have accepted this and handed out the Operator
        // role to somebody who typed a digit, which is why parsing goes by name.
        Assert.Null(request);
        Assert.Contains("Unknown role", error);
    }

    [Theory]
    // Nothing but the verb.
    [InlineData(new[] { "grant-role" }, "--email is missing")]
    // A role with no address: the address decides whose account is promoted.
    [InlineData(new[] { "grant-role", "--role", "Operator" }, "--email is missing")]
    [InlineData(new[] { "grant-role", "--email", "adam@example.org" }, "--role is missing")]
    [InlineData(new[] { "grant-role", "--email", "adam@example.org", "--role", "Root" }, "Unknown role")]
    [InlineData(new[] { "grant-role", "--mail", "adam@example.org" }, "Unknown option")]
    [InlineData(new[] { "grant-role", "--email" }, "has no value")]
    // A missed value: without the guard, --role would be read as the address.
    [InlineData(new[] { "grant-role", "--email", "--role", "Operator" }, "another option")]
    [InlineData(
        new[] { "grant-role", "--email", "a@example.org", "--email", "b@example.org", "--role", "Operator" },
        "was given twice")]
    public void A_broken_command_line_is_refused_with_a_reason(
        string[] args,
        string expected)
    {
        // Act
        var request = AdminCommandLine.ParseGrantRole(args, out var error);

        // Assert
        // Every one of these names the single thing that is wrong. A generic
        // "invalid arguments" would send the reader back to the usage text to
        // compare it word by word against what they typed.
        Assert.Null(request);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void An_unknown_verb_is_refused()
    {
        // Act
        var request = AdminCommandLine.ParseGrantRole(["revoke-role"], out var error);

        // Assert
        Assert.Null(request);
        Assert.Contains("grant-role", error);
    }
}
