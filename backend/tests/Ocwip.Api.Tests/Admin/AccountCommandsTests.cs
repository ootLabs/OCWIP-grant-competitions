using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Admin;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>deactivate-account and list-accounts (T-104), on a real database.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountCommandsTests(PostgresDatabaseFixture database)
{
    private IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = database.ConnectionString })
        .Build();

    private async Task<(int Exit, string Output)> RunAsync(params string[] args)
    {
        await using var output = new StringWriter();
        var exit = await AdminCommandRunner.RunAsync(args, Configuration, output);
        return (exit, output.ToString());
    }

    private async Task<User> AccountAsync(Role role)
    {
        await using var context = database.CreateContext();
        var user = TestUser.New($"konto-{Guid.NewGuid():N}@example.org", role);
        user.SecurityStamp = "stary-znacznik";
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [RequiresDatabaseFact]
    public async Task Deactivation_marks_the_account_inactive_and_ends_its_sessions()
    {
        var user = await AccountAsync(Role.Reviewer);

        var (exit, output) = await RunAsync(AccountCommands.DeactivateVerb, "--email", user.Email!.ToUpperInvariant());

        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains("Deactivated", output);
        await using var context = database.CreateContext();
        var stored = await context.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id);
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.DeactivatedAt);
        Assert.NotEqual("stary-znacznik", stored.SecurityStamp);

        var again = await RunAsync(AccountCommands.DeactivateVerb, "--email", user.Email!);
        Assert.Equal(AdminCommandRunner.Success, again.Exit);
        Assert.Contains("already inactive", again.Output);
    }

    [RequiresDatabaseFact]
    public async Task An_unknown_address_or_a_missing_option_changes_nothing()
    {
        Assert.Equal(AdminCommandRunner.Failure, (await RunAsync(AccountCommands.DeactivateVerb, "--email", "nikt@example.org")).Exit);
        Assert.Equal(AdminCommandRunner.Failure, (await RunAsync(AccountCommands.DeactivateVerb)).Exit);
        Assert.Equal(AdminCommandRunner.Failure, (await RunAsync(AccountCommands.DeactivateVerb, "--role", "Operator")).Exit);
    }

    [RequiresDatabaseFact]
    public async Task The_list_shows_the_team_with_roles_and_never_an_applicant()
    {
        var operatorAccount = await AccountAsync(Role.Operator);
        var expert = await AccountAsync(Role.Reviewer);
        var applicant = await AccountAsync(Role.Applicant);

        var (exit, output) = await RunAsync(AccountCommands.ListVerb);

        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains($"Operator  active   {operatorAccount.Email}", output);
        Assert.Contains($"Reviewer  active   {expert.Email}", output);
        Assert.DoesNotContain(applicant.Email!, output);

        var experts = await RunAsync(AccountCommands.ListVerb, "--role", "reviewer");
        Assert.Contains(expert.Email!, experts.Output);
        Assert.DoesNotContain(operatorAccount.Email!, experts.Output);

        Assert.Equal(AdminCommandRunner.Failure, (await RunAsync(AccountCommands.ListVerb, "--role", "Applicant")).Exit);
    }
}
