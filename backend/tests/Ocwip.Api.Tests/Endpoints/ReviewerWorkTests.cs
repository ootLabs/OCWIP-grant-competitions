using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-40: the expert's own list, over HTTP. Only assigned applications, only
/// the caller's own card, and the sums the report asks for.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReviewerWorkTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ReviewerWorkTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task An_expert_lists_only_what_is_assigned_to_them_with_their_own_card()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var assigned = await SubmittedAsync(host, competition.Id);
        var notAssigned = await SubmittedAsync(host, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        (await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/evaluation-cards/merit",
            new FormDefinitionRequest(EvaluationCardSamples.MeritCard()))).EnsureSuccessStatusCode();

        var (expert, expertId) = await SeedReviewerAsync(host);
        var (other, otherId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await AcceptDeclarationAsync(other, competition.Id);
        foreach (var reviewerId in new[] { expertId, otherId })
        {
            (await operatorClient.PostAsJsonAsync(
                $"/applications/{assigned}/assignments", new AssignReviewerRequest(reviewerId))).EnsureSuccessStatusCode();
        }

        // The other expert recommends 7000; that must not show on this list.
        var otherCard = (await (await other.PostAsync($"/applications/{assigned}/evaluations/merit", content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await other.PutAsJsonAsync($"/evaluations/{otherCard.Id}",
            new { answers = new JsonObject { ["kwota"] = 7000 } })).EnsureSuccessStatusCode();

        var own = (await (await expert.PostAsync($"/applications/{assigned}/evaluations/merit", content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await expert.PutAsJsonAsync($"/evaluations/{own.Id}",
            new { answers = new JsonObject { ["kwota"] = 5000 } })).EnsureSuccessStatusCode();

        var work = (await expert.GetFromJsonAsync<ReviewerWorkResponse>("/reviewer/applications"))!;

        var group = Assert.Single(work.Competitions);
        var row = Assert.Single(group.Applications);
        Assert.Equal(assigned, row.ApplicationId);
        Assert.NotEqual(notAssigned, row.ApplicationId);
        Assert.Equal(OwnCardStanding.Draft, row.Card);
        Assert.Equal(own.Id, row.EvaluationId);
        Assert.Equal(5000m, row.RecommendedGrant);
        Assert.Equal(5000m, group.RecommendedTotal);
    }

    [RequiresDatabaseFact]
    public async Task An_assigned_expert_opens_the_attachments_and_nobody_else_does()
    {
        // T-40: "podgląd pełnego wniosku wraz z załącznikami". The list of
        // attachments was already reachable through the application; the file
        // itself is authorized against the attachment, which the reviewer
        // rule did not know until this card.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\n%test\n"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "statut.pdf");
        var upload = await applicant.PostAsync($"/applications/{draft.Id}/attachments", content);
        upload.EnsureSuccessStatusCode();
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentResponse>())!;
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var (assigned, assignedId) = await SeedReviewerAsync(host);
        var (stranger, _) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(assigned, competition.Id);
        (await operatorClient.PostAsJsonAsync(
            $"/applications/{draft.Id}/assignments", new AssignReviewerRequest(assignedId))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await assigned.GetAsync($"/attachments/{attachment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/attachments/{attachment.Id}")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_list_belongs_to_experts_only()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);

        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/reviewer/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync("/reviewer/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/reviewer/applications")).StatusCode);
    }

    private async Task<Guid> SubmittedAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, Guid competitionId)
    {
        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competitionId);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();
        return draft.Id;
    }
}
