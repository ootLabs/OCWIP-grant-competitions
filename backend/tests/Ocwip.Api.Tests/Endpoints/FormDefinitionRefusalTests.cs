using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// What the versioning routes refuse (T-25), and the route that does not exist
/// at all.
///
/// The refusals matter more here than usual: publishing is the only write path
/// to a column that applications point at for five years, so every way of
/// reaching it with something wrong has to end somewhere other than in the
/// table.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FormDefinitionRefusalTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public FormDefinitionRefusalTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    [RequiresDatabaseTheory]
    [InlineData(Role.Applicant)]
    [InlineData(Role.Reviewer)]
    public async Task Every_form_version_route_is_refused_to_the_other_roles(Role role)
    {
        // Arrange
        var (host, _) = Host();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        var client = await CompetitionTestHost.SignedInAs(host, role);

        // Act
        var responses = new[]
        {
            await Publish(client, competition.Id, Sample("pole")),
            await client.GetAsync($"/competitions/{competition.Id}/form-definitions"),
            await client.GetAsync(
                $"/competitions/{competition.Id}/form-definitions/1"),
        };

        // Assert
        // 403 and not 401: the session is good, signing in again changes
        // nothing. The reading routes are refused too, because the form of a
        // competition that has not been announced is work in progress.
        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));

        await AssertNoVersionsAsync(competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task A_signed_out_caller_is_refused()
    {
        // Arrange
        var (host, _) = Host();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        var client = host.CreateClient();

        // Act
        var response = await Publish(client, competition.Id, Sample("pole"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoVersionsAsync(competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task A_definition_the_renderer_could_not_draw_is_refused_with_the_field_named()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        var unknownKind = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("dziwne_pole", "hologram"));

        // Act
        var response = await Publish(client, competition.Id, unknownKind);

        // Assert
        // 400 and not 409: this one IS about the body, and the operator can
        // fix it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();

        // The path of the place in the document is the key, so the creator can
        // put the operator back on the field instead of on the form.
        var key = Assert.Single(problem!.Errors).Key;
        Assert.Contains("fields[0]", key);

        // The gate is the whole point of standing in front of the column: a
        // refused document leaves nothing behind.
        await AssertNoVersionsAsync(competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task A_body_with_no_document_in_it_is_refused_rather_than_stored()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        // No definition property at all, which arrives as JsonElement with
        // ValueKind.Undefined. Stored instead of refused it throws inside the
        // Npgsql serializer with a message naming no field, so this case has to
        // end as a 400 and not as a 500.
        var response = await client.PostAsJsonAsync(
            $"/competitions/{competition.Id}/form-definitions",
            new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoVersionsAsync(competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task An_unknown_competition_and_an_unknown_version_both_answer_404()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        var missing = Guid.NewGuid();

        // Act
        var responses = new[]
        {
            await Publish(client, missing, Sample("pole")),
            await client.GetAsync($"/competitions/{missing}/form-definitions"),
            await client.GetAsync($"/competitions/{missing}/form-definitions/1"),

            // The competition exists, the version does not: nothing was ever
            // published under it.
            await client.GetAsync(
                $"/competitions/{competition.Id}/form-definitions/7"),
        };

        // Assert
        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));

        // Same status, different sentence. An operator who mistyped the
        // competition must not be sent looking for a version of a form.
        Assert.Equal(
            FormDefinitionEndpoints.CompetitionNotFound,
            (await responses[1].Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);

        Assert.Equal(
            FormDefinitionEndpoints.NotFound,
            (await responses[3].Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [RequiresDatabaseFact]
    public async Task Editing_a_competition_without_naming_a_version_leaves_the_form_in_force()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        var published = await Publish(client, competition.Id, Sample("pole"));
        published.EnsureSuccessStatusCode();

        var version = (await published.Content
            .ReadFromJsonAsync<FormDefinitionResponse>())!;

        // Act
        // The wizard edits the dates and sends no form version, which is the
        // default shape of the request. Read as "take the form away" it would
        // un-publish the form of a competition mid intake, with a 200 and no
        // sign that anything happened.
        var edited = await client.PutAsJsonAsync(
            $"/competitions/{competition.Id}",
            CompetitionTestHost.Request(title: "Konkurs po edycji"));

        edited.EnsureSuccessStatusCode();

        // Assert
        var refreshed = (await edited.Content
            .ReadFromJsonAsync<CompetitionResponse>())!;

        Assert.Equal(version.Id, refreshed.FormDefinitionId);
    }

    [RequiresDatabaseFact]
    public async Task An_inactive_competition_takes_no_new_version()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        var deactivated = await client.DeleteAsync($"/competitions/{competition.Id}");
        deactivated.EnsureSuccessStatusCode();

        // Act
        var response = await Publish(client, competition.Id, Sample("pole"));

        // Assert
        // 409 and not 400: the body is fine, what is in the way is the state of
        // the competition. Same rule as editing it.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(FormDefinitionEndpoints.Inactive, problem!.Detail);

        await AssertNoVersionsAsync(competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task There_is_no_route_that_replaces_a_published_version()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        var published = await client.PostAsJsonAsync(
            $"/competitions/{competition.Id}/form-definitions",
            new FormDefinitionRequest(Sample("pole")));

        published.EnsureSuccessStatusCode();

        var version = $"/competitions/{competition.Id}/form-definitions/1";

        // Act
        var replace = await client.PutAsJsonAsync(
            version, new FormDefinitionRequest(Sample("podmienione")));

        var remove = await client.DeleteAsync(version);

        // Assert
        // The missing routes ARE the design, so they are pinned like a rule:
        // adding one later has to be a deliberate act that breaks this test,
        // not a convenience somebody maps on the way past.
        // Either answer is routing saying the same thing: 405 where a route
        // with that path exists under another method, 404 where none does.
        // What is asserted is that neither verb reaches anything.
        Assert.Contains(
            replace.StatusCode,
            new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });

        Assert.Contains(
            remove.StatusCode,
            new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });

        await using var context = _database.CreateContext();

        var stored = await context.FormDefinitions
            .AsNoTracking()
            .SingleAsync(x => x.CompetitionId == competition.Id);

        Assert.Contains(
            "pole",
            stored.Definition.GetRawText());
        Assert.DoesNotContain("podmienione", stored.Definition.GetRawText());
    }

    private static JsonElement Sample(string fieldKey) =>
        FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(fieldKey, "shortText", "\"maxLength\": 200"));

    private static Task<HttpResponseMessage> Publish(
        HttpClient client,
        Guid competitionId,
        JsonElement definition) =>
        client.PostAsJsonAsync(
            $"/competitions/{competitionId}/form-definitions",
            new FormDefinitionRequest(definition));

    private async Task AssertNoVersionsAsync(Guid competitionId)
    {
        await using var context = _database.CreateContext();

        Assert.False(
            await context.FormDefinitions
                .AsNoTracking()
                .AnyAsync(x => x.CompetitionId == competitionId));
    }
}
