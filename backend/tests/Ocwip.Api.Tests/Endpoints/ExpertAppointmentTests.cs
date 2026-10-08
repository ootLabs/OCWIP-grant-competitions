using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The committee of a competition (R-44, report step 5.1 and the roles
/// table): the operator appoints by address, any applicant account may be an
/// expert in a competition, never of its own organisation's application, and
/// somebody without an account is invited.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExpertAppointmentTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private sealed record Scene(
        WebApplicationFactory<Program> Host,
        RecordingEmailSender Emails,
        HttpClient Operator,
        Guid CompetitionOne,
        Guid CompetitionTwo,
        ApplicationResponse OtherInOne,
        HttpClient Chair,
        string ChairEmail,
        Guid ChairId,
        ApplicationResponse ChairsOwnInTwo);

    /// <summary>
    /// Two competitions. Another organisation applies in the first; the
    /// chair of a foundation applies in the second with her own card.
    /// </summary>
    private async Task<Scene> SceneAsync()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        var one = await PublishedCompetitionWithFormAsync(host);
        var two = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (other, _, _) = await SeedApplicantAsync(host, database);
        var otherInOne = await SubmittedAsync(other, one.Id);

        var (chair, chairEntity, chairEmail) = await SeedApplicantAsync(host, database);
        var chairsOwn = await SubmittedAsync(chair, two.Id);
        await using var context = database.CreateContext();
        var chairId = await TestMembership.FounderOfAsync(context, chairEntity);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        return new Scene(host, emails, operatorClient, one.Id, two.Id, otherInOne, chair, chairEmail, chairId, chairsOwn);
    }

    private static async Task<ApplicationResponse> SubmittedAsync(HttpClient applicant, Guid competitionId)
    {
        var draft = await CreateAsync(applicant, competitionId);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        var submit = await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null);
        submit.EnsureSuccessStatusCode();
        return (await submit.Content.ReadFromJsonAsync<ApplicationResponse>())!;
    }

    private static Task<HttpResponseMessage> AppointAsync(HttpClient client, Guid competitionId, AppointExpertRequest body) =>
        client.PostAsJsonAsync($"/competitions/{competitionId}/experts", body);

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid applicationId, Guid reviewerId) =>
        client.PostAsJsonAsync($"/applications/{applicationId}/assignments", new AssignReviewerRequest(reviewerId));

    [RequiresDatabaseFact]
    public async Task An_applicant_appointed_to_another_competition_evaluates_there_and_keeps_her_own_application()
    {
        var scene = await SceneAsync();

        // Not appointed yet: no assignment, no expert's panel.
        Assert.Equal(HttpStatusCode.NotFound, (await AssignAsync(scene.Operator, scene.OtherInOne.Id, scene.ChairId)).StatusCode);
        Assert.False((await scene.Chair.GetFromJsonAsync<CurrentUserResponse>("/me"))!.IsExpert);

        var appointed = await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest($"  {scene.ChairEmail.ToUpperInvariant()} "));
        Assert.Equal(HttpStatusCode.OK, appointed.StatusCode);
        Assert.Equal(scene.ChairId, (await appointed.Content.ReadFromJsonAsync<CompetitionExpertResponse>())!.UserId);
        Assert.Single(scene.Emails.Sent, x => x.To == scene.ChairEmail && x.Subject.StartsWith("Powołanie do komisji", StringComparison.Ordinal));

        // Appointing twice changes nothing and sends nothing.
        Assert.Equal(HttpStatusCode.OK, (await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(scene.ChairEmail))).StatusCode);
        Assert.Single(scene.Emails.Sent, x => x.To == scene.ChairEmail && x.Subject.StartsWith("Powołanie do komisji", StringComparison.Ordinal));

        // The claim is rebuilt when the session is next validated, which the
        // frozen test clock never triggers; a new sign in does the same.
        Assert.True((await scene.Chair.GetFromJsonAsync<CurrentUserResponse>("/me"))!.IsExpert);
        scene = scene with { Chair = await LoginAsync(scene.Host, scene.ChairEmail) };
        await AcceptDeclarationAsync(scene.Chair, scene.CompetitionOne);
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(scene.Operator, scene.OtherInOne.Id, scene.ChairId)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await scene.Chair.GetAsync($"/applications/{scene.OtherInOne.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scene.Chair.GetAsync($"/applications/{scene.ChairsOwnInTwo.Id}")).StatusCode);
        Assert.Contains(await ListMineAsync(scene.Chair), x => x.Id == scene.ChairsOwnInTwo.Id);

        var committee = (await scene.Operator.GetFromJsonAsync<List<CompetitionExpertResponse>>($"/competitions/{scene.CompetitionOne}/experts"))!;
        Assert.Equal(1, Assert.Single(committee, x => x.UserId == scene.ChairId).Assigned);
    }

    [RequiresDatabaseFact]
    public async Task Nobody_evaluates_an_application_of_an_organisation_they_act_for()
    {
        var scene = await SceneAsync();

        (await AppointAsync(scene.Operator, scene.CompetitionTwo, new AppointExpertRequest(scene.ChairEmail))).EnsureSuccessStatusCode();
        var own = await AssignAsync(scene.Operator, scene.ChairsOwnInTwo.Id, scene.ChairId);

        Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
        await using var context = database.CreateContext();
        Assert.False(await context.ApplicationAssignments.AnyAsync(x => x.ReviewerId == scene.ChairId && x.ApplicationId == scene.ChairsOwnInTwo.Id));
    }

    [RequiresDatabaseFact]
    public async Task Joining_the_applicant_s_card_after_the_assignment_ends_the_evaluation()
    {
        var scene = await SceneAsync();
        (await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(scene.ChairEmail))).EnsureSuccessStatusCode();
        scene = scene with { Chair = await LoginAsync(scene.Host, scene.ChairEmail) };
        await AcceptDeclarationAsync(scene.Chair, scene.CompetitionOne);
        (await AssignAsync(scene.Operator, scene.OtherInOne.Id, scene.ChairId)).EnsureSuccessStatusCode();
        Assert.Contains(
            (await scene.Chair.GetFromJsonAsync<ReviewerWorkResponse>("/reviewer/applications"))!.Competitions.SelectMany(x => x.Applications),
            x => x.ApplicationId == scene.OtherInOne.Id);

        // She joins the other organisation's card afterwards (T-93a).
        await using (var context = database.CreateContext())
        {
            var entityId = await context.Applications.Where(x => x.Id == scene.OtherInOne.Id).Select(x => x.EntityId).SingleAsync();
            await TestMembership.GrantAsync(context, entityId, scene.ChairId);
        }

        var start = await scene.Chair.PostAsync($"/applications/{scene.OtherInOne.Id}/evaluations/merit", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
        Assert.DoesNotContain(
            (await scene.Chair.GetFromJsonAsync<ReviewerWorkResponse>("/reviewer/applications"))!.Competitions.SelectMany(x => x.Applications),
            x => x.ApplicationId == scene.OtherInOne.Id);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_declares_only_where_she_is_on_the_committee()
    {
        var scene = await SceneAsync();
        (await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(scene.ChairEmail))).EnsureSuccessStatusCode();
        scene = scene with { Chair = await LoginAsync(scene.Host, scene.ChairEmail) };

        var elsewhere = await scene.Chair.PostAsJsonAsync(
            $"/reviewer/competitions/{scene.CompetitionTwo}/declaration", new DeclarationDecisionRequest(true, null));

        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
        await using var context = database.CreateContext();
        Assert.False(await context.ReviewerDeclarations.AnyAsync(x => x.ReviewerId == scene.ChairId && x.CompetitionId == scene.CompetitionTwo));
    }

    [RequiresDatabaseFact]
    public async Task Somebody_without_an_account_is_invited_and_sets_a_password_through_the_link()
    {
        var scene = await SceneAsync();
        var address = SessionTestHost.Email("zaproszona-ekspertka");

        var unnamed = await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(address));
        Assert.Equal(HttpStatusCode.NotFound, unnamed.StatusCode);
        Assert.Contains("imię i nazwisko", await unnamed.Content.ReadAsStringAsync());

        var invited = await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(address, "Ewa", "Ekspercka"));
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        Assert.True((await invited.Content.ReadFromJsonAsync<CompetitionExpertResponse>())!.InvitationPending);

        var mail = Assert.Single(scene.Emails.Sent, x => x.To == address);
        var link = Regex.Match(mail.Body, @"reset-password\?userId=(?<id>[^&\s]+)&token=(?<token>\S+)");
        Assert.True(link.Success);

        var reset = await host(scene).PostAsJsonAsync("/reset-password", new ResetPasswordRequest(
            link.Groups["id"].Value, Uri.UnescapeDataString(link.Groups["token"].Value), SessionTestHost.Password));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // The link reached the address, so the address counts as confirmed.
        var expert = await LoginAsync(scene.Host, address);
        Assert.True((await expert.GetFromJsonAsync<CurrentUserResponse>("/me"))!.IsExpert);

        static HttpClient host(Scene scene) => scene.Host.CreateClient();
    }

    [RequiresDatabaseFact]
    public async Task An_operator_is_never_appointed_and_only_an_operator_appoints()
    {
        var scene = await SceneAsync();
        var operatorEmail = SessionTestHost.Email("operator-komisji");
        await SessionTestHost.CreateAccountAsync(scene.Host, operatorEmail, Role.Operator);

        Assert.Equal(HttpStatusCode.Conflict, (await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(operatorEmail))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AppointAsync(scene.Chair, scene.CompetitionOne, new AppointExpertRequest(scene.ChairEmail))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Chair.GetAsync($"/competitions/{scene.CompetitionOne}/experts")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Withdrawing_waits_for_the_assignments_and_then_closes_the_door()
    {
        var scene = await SceneAsync();
        (await AppointAsync(scene.Operator, scene.CompetitionOne, new AppointExpertRequest(scene.ChairEmail))).EnsureSuccessStatusCode();
        scene = scene with { Chair = await LoginAsync(scene.Host, scene.ChairEmail) };
        await AcceptDeclarationAsync(scene.Chair, scene.CompetitionOne);
        (await AssignAsync(scene.Operator, scene.OtherInOne.Id, scene.ChairId)).EnsureSuccessStatusCode();

        var withdraw = () => scene.Operator.DeleteAsync($"/competitions/{scene.CompetitionOne}/experts/{scene.ChairId}");
        Assert.Equal(HttpStatusCode.Conflict, (await withdraw()).StatusCode);

        (await scene.Operator.DeleteAsync($"/applications/{scene.OtherInOne.Id}/assignments/{scene.ChairId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await withdraw()).StatusCode);

        Assert.False((await scene.Chair.GetFromJsonAsync<CurrentUserResponse>("/me"))!.IsExpert);
        Assert.Equal(HttpStatusCode.Forbidden, (await scene.Chair.GetAsync($"/applications/{scene.OtherInOne.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scene.Chair.GetAsync($"/applications/{scene.ChairsOwnInTwo.Id}")).StatusCode);
    }
}
