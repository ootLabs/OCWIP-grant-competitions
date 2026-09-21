using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Versioning of the form definition, over real HTTP and a real PostgreSQL
/// (T-25).
///
/// One sentence is under test in every case here: publishing ADDS a version.
/// What an applicant already has in front of them never changes underneath
/// them, and what was submitted three years ago can still be rendered against
/// the exact structure it was filled against.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FormDefinitionVersioningTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public FormDefinitionVersioningTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Publishing_twice_leaves_two_versions_and_the_first_one_untouched()
    {
        // Arrange
        var client = await OperatorAsync();
        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        var first = await PublishAsync(client, competition.Id, Sample("tytul"));
        var second = await PublishAsync(client, competition.Id, Sample("nowy_tytul"));

        // Assert
        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(2, second.VersionNumber);

        // Two rows and not one edited row. This is the card in a single
        // assertion: the identifier of the first version still resolves, so
        // everything pointing at it still has something to point at.
        Assert.NotEqual(first.Id, second.Id);

        var versions = await ListAsync(client, competition.Id);
        Assert.Equal([1, 2], versions.Select(version => version.VersionNumber));

        var stored = await GetAsync(client, competition.Id, versionNumber: 1);
        Assert.Equal(first.Id, stored.Id);
        Assert.Contains("tytul", FieldKeys(stored.Definition));
        Assert.DoesNotContain("nowy_tytul", FieldKeys(stored.Definition));
    }

    [RequiresDatabaseFact]
    public async Task The_newest_version_is_the_one_in_force_for_the_competition()
    {
        // Arrange
        var client = await OperatorAsync();
        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        var first = await PublishAsync(client, competition.Id, Sample("wersja_jeden"));
        var second = await PublishAsync(client, competition.Id, Sample("wersja_dwa"));

        // Assert
        // What the competition hands to the NEXT applicant moves. Which is the
        // half of the rule that is allowed to move.
        Assert.True(second.IsCurrent);

        var versions = await ListAsync(client, competition.Id);
        Assert.Equal(
            [false, true],
            versions.Select(version => version.IsCurrent));

        var refreshed = await client.GetFromJsonAsync<CompetitionResponse>(
            $"/competitions/{competition.Id}");

        Assert.Equal(second.Id, refreshed!.FormDefinitionId);
        Assert.NotEqual(first.Id, refreshed.FormDefinitionId);
    }

    [RequiresDatabaseFact]
    public async Task A_draft_application_keeps_its_version_when_a_new_one_is_published()
    {
        // Arrange
        var client = await OperatorAsync();
        var competition = await CompetitionTestHost.CreateAsync(client);

        var first = await PublishAsync(client, competition.Id, Sample("stare_pole"));

        // An application filled in against version 1, as T-29 will create it.
        // Written through the context because the application endpoints do not
        // exist yet; the row, its foreign key and the rule under test do.
        var applicationId = await SeedDraftApplicationAsync(
            competition.Id, first.Id);

        // Act
        var second = await PublishAsync(client, competition.Id, Sample("nowe_pole"));

        // Assert
        await using var context = _database.CreateContext();

        var application = await context.Applications
            .AsNoTracking()
            .Include(x => x.FormDefinition)
            .SingleAsync(x => x.Id == applicationId);

        // Not raised to the new version in flight, which would make the fields
        // the applicant had already filled in disappear from under them.
        Assert.Equal(first.Id, application.FormDefinitionId);
        Assert.NotEqual(second.Id, application.FormDefinitionId);
        Assert.Equal(1, application.FormDefinition.VersionNumber);

        // And it renders against ITS version: the document behind the
        // application is still the one it was filled against.
        Assert.Contains("stare_pole", FieldKeys(application.FormDefinition.Definition));
        Assert.DoesNotContain(
            "nowe_pole", FieldKeys(application.FormDefinition.Definition));
    }

    [RequiresDatabaseFact]
    public async Task Version_numbers_are_unique_inside_one_competition_and_start_again_in_the_next()
    {
        // Arrange
        var client = await OperatorAsync();
        var first = await CompetitionTestHost.CreateAsync(client);
        var second = await CompetitionTestHost.CreateAsync(client);

        // Act
        await PublishAsync(client, first.Id, Sample("a"));
        await PublishAsync(client, first.Id, Sample("b"));
        var other = await PublishAsync(client, second.Id, Sample("c"));

        // Assert
        // Unique per competition, not globally: the second competition starts
        // its own count at 1.
        Assert.Equal(1, other.VersionNumber);

        await using var context = _database.CreateContext();

        var numbers = await context.FormDefinitions
            .AsNoTracking()
            .Where(x => x.CompetitionId == first.Id)
            .Select(x => x.VersionNumber)
            .ToListAsync();

        Assert.Equal([1, 2], numbers.Order());
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [RequiresDatabaseFact]
    public async Task Two_publications_at_once_never_produce_two_rows_with_one_number()
    {
        // Arrange
        var client = await OperatorAsync();
        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        // The service reads the highest number with a SELECT, and a SELECT
        // loses this race. What must not happen is a 500 or a duplicate: the
        // unique index answers, and the answer has to be something the caller
        // can retry.
        var responses = await Task.WhenAll(
            PostAsync(client, competition.Id, Sample("rownolegle_a")),
            PostAsync(client, competition.Id, Sample("rownolegle_b")));

        // Assert
        foreach (var response in responses)
        {
            Assert.Contains(
                response.StatusCode,
                new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        }

        await using var context = _database.CreateContext();

        var numbers = await context.FormDefinitions
            .AsNoTracking()
            .Where(x => x.CompetitionId == competition.Id)
            .Select(x => x.VersionNumber)
            .ToListAsync();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    private async Task<HttpClient> OperatorAsync()
    {
        var (host, _) = CompetitionTestHost.Create(_factory, _database);

        return await CompetitionTestHost.SignedInAs(host, Role.Operator);
    }

    /// <summary>
    /// The smallest legal document carrying one recognisable field, so a test
    /// can tell two versions apart by what is inside them.
    /// </summary>
    private static JsonElement Sample(string fieldKey) =>
        FormDefinitionSamples.WithFields(
            // A text field carries its character limit, so the smallest legal
            // document is this one and not a field with nothing on it.
            FormDefinitionSamples.Field(fieldKey, "shortText", "\"maxLength\": 200"));

    private static IReadOnlyList<string> FieldKeys(JsonElement definition) =>
        definition
            .GetProperty("sections")
            .EnumerateArray()
            .SelectMany(section => section.GetProperty("fields").EnumerateArray())
            .Select(field => field.GetProperty("key").GetString()!)
            .ToList();

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        Guid competitionId,
        JsonElement definition) =>
        client.PostAsJsonAsync(
            $"/competitions/{competitionId}/form-definitions",
            new FormDefinitionRequest(definition));

    private static async Task<FormDefinitionResponse> PublishAsync(
        HttpClient client,
        Guid competitionId,
        JsonElement definition)
    {
        var response = await PostAsync(client, competitionId, definition);

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<FormDefinitionResponse>())!;
    }

    private static async Task<IReadOnlyList<FormDefinitionSummaryResponse>> ListAsync(
        HttpClient client,
        Guid competitionId) =>
        (await client.GetFromJsonAsync<List<FormDefinitionSummaryResponse>>(
            $"/competitions/{competitionId}/form-definitions"))!;

    private static async Task<FormDefinitionResponse> GetAsync(
        HttpClient client,
        Guid competitionId,
        int versionNumber) =>
        (await client.GetFromJsonAsync<FormDefinitionResponse>(
            $"/competitions/{competitionId}/form-definitions/{versionNumber}"))!;

    private async Task<Guid> SeedDraftApplicationAsync(
        Guid competitionId,
        Guid formDefinitionId)
    {
        await using var context = _database.CreateContext();

        var entity = TestEntity.New($"Podmiot {Guid.NewGuid():N}");
        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        var application = new Application
        {
            CompetitionId = competitionId,
            EntityId = entity.Id,
            FormDefinitionId = formDefinitionId,
            Status = ApplicationStatus.Draft,
            Answers = JsonDocument.Parse("""{"stare_pole":"wpisane"}""")
                .RootElement
                .Clone(),
        };

        context.Applications.Add(application);
        await context.SaveChangesAsync();

        return application.Id;
    }
}
