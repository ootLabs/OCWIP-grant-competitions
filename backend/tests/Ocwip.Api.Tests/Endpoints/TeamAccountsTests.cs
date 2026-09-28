using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The OCWIP team for the operator (T-104): every operator and expert with
/// role and state, read only; and the mail an expert gets once per new
/// assignment.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TeamAccountsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    [RequiresDatabaseFact]
    public async Task The_team_list_has_operators_and_experts_with_their_roles_for_an_operator_only()
    {
        var (host, _) = CompetitionTestHost.Create(factory, database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var (_, expertId) = await SeedReviewerAsync(host);
        var (applicant, _, applicantEmail) = await SeedApplicantAsync(host, database);

        var team = (await operatorClient.GetFromJsonAsync<List<TeamAccountResponse>>("/accounts/team"))!;

        Assert.Contains(team, x => x.Id == expertId && x.Role == Role.Reviewer && x.IsActive);
        Assert.Contains(team, x => x.Role == Role.Operator);
        Assert.DoesNotContain(team, x => x.Email == applicantEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync("/accounts/team")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_expert_gets_one_mail_per_new_assignment()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (_, applicationId, _) = await EvaluationScene.SubmittedAsync(host, database, competition.Id);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var expertEmail = SessionTestHost.Email("ekspert-mail");
        var expert = await SessionTestHost.CreateAccountAsync(host, expertEmail, Role.Reviewer);

        var assign = $"/applications/{applicationId}/assignments";
        (await operatorClient.PostAsJsonAsync(assign, new AssignReviewerRequest(expert.Id))).EnsureSuccessStatusCode();
        (await operatorClient.PostAsJsonAsync(assign, new AssignReviewerRequest(expert.Id))).EnsureSuccessStatusCode();

        var mail = Assert.Single(emails.Sent, x => x.To == expertEmail);
        Assert.StartsWith("Nowy wniosek do oceny", mail.Subject);
        Assert.Contains("/panel/reviewer", mail.Body);
    }
}
