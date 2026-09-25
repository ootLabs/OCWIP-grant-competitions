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
/// T-41b: the operator shares the cards once for the whole competition; the
/// applicant then reads the finished cards of their own application with
/// nothing about who evaluated; nobody else reads them through this route.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CardSharingTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CardSharingTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task The_applicant_reads_the_finished_cards_only_after_sharing_and_never_who_wrote_them()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"projekt"}"""));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        foreach (var (stage, document) in new[]
            { ("formal", EvaluationCardSamples.FormalCard()), ("merit", EvaluationCardSamples.MeritCard()) })
        {
            (await operatorClient.PostAsJsonAsync(
                $"/competitions/{competition.Id}/evaluation-cards/{stage}",
                new FormDefinitionRequest(document))).EnsureSuccessStatusCode();
        }

        var (expert, expertId) = await SeedReviewerAsync(host);
        (await operatorClient.PostAsJsonAsync(
            $"/applications/{draft.Id}/assignments", new AssignReviewerRequest(expertId))).EnsureSuccessStatusCode();
        await AcceptDeclarationAsync(expert, competition.Id);
        var merit = (await (await expert.PostAsync($"/applications/{draft.Id}/evaluations/merit", content: null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<EvaluationResponse>())!;
        (await expert.PutAsJsonAsync($"/evaluations/{merit.Id}", new
        {
            answers = new JsonObject
            {
                ["pomysl"] = 15,
                ["pomysl_uzasadnienie"] = "Uzasadnienie eksperta.",
                ["budzet"] = 3,
                ["biale_plamy"] = true,
            },
        })).EnsureSuccessStatusCode();
        (await expert.PostAsync($"/evaluations/{merit.Id}/finish", content: null)).EnsureSuccessStatusCode();

        // A formal card still being filled in: never shown, shared or not.
        (await operatorClient.PostAsync($"/applications/{draft.Id}/evaluations/formal", content: null))
            .EnsureSuccessStatusCode();

        var cardsAddress = $"/applications/{draft.Id}/evaluation-cards";
        var before = (await applicant.GetFromJsonAsync<ApplicantEvaluationCards>(cardsAddress))!;
        Assert.False(before.Shared);
        Assert.Empty(before.Cards);

        var sharingAddress = $"/competitions/{competition.Id}/card-sharing";
        Assert.Equal(HttpStatusCode.Forbidden, (await expert.PostAsync(sharingAddress, content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsync(sharingAddress, content: null)).StatusCode);

        var touchedBefore = (await operatorClient.GetFromJsonAsync<CompetitionResponse>(
            $"/competitions/{competition.Id}"))!.UpdatedAt;

        var shared = await operatorClient.PostAsync(sharingAddress, content: null);
        Assert.Equal(HttpStatusCode.OK, shared.StatusCode);
        Assert.NotNull((await operatorClient.GetFromJsonAsync<CardSharingResponse>(sharingAddress))!.SharedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsync(sharingAddress, content: null)).StatusCode);

        // The change is a change of the competition like any other: its
        // audit timestamp moves, although the UPDATE skips SaveChanges.
        Assert.True((await operatorClient.GetFromJsonAsync<CompetitionResponse>(
            $"/competitions/{competition.Id}"))!.UpdatedAt > touchedBefore);

        var response = await applicant.GetAsync(cardsAddress);
        var raw = await response.Content.ReadAsStringAsync();
        var after = System.Text.Json.JsonSerializer.Deserialize<ApplicantEvaluationCards>(
            raw, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.True(after.Shared);
        var card = Assert.Single(after.Cards);
        Assert.Equal(EvaluationStage.Merit, card.Stage);
        Assert.Equal(18m, card.MeritScore);
        Assert.Contains("Uzasadnienie eksperta.", raw);

        // Nothing that points at the expert: no id, no account, no name.
        Assert.DoesNotContain(expertId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(merit.Id.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Testowa", raw);
        Assert.DoesNotContain("author", raw, StringComparison.OrdinalIgnoreCase);

        // The expert is signed in and assigned, and still does not read the
        // cards this way; neither does another applicant.
        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync(cardsAddress)).StatusCode);
        var (stranger, _, _) = await SeedApplicantAsync(host, _database);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(cardsAddress)).StatusCode);
    }
}
