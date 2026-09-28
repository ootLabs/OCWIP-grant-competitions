using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-108: the public results archive lists a competition only once its
/// results are approved, with the funded projects alone, and an informal
/// group under its own name, never its members' (RD3).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ResultsArchiveTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    [RequiresDatabaseFact]
    public async Task A_resolved_competition_shows_its_funded_projects_and_a_group_without_its_members()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (_, funded, _) = await SubmittedAsync(host, database, competition.Id);
        var (_, reserve, _) = await SubmittedAsync(host, database, competition.Id);
        var group = $"Grupa Sąsiedzka {Guid.NewGuid():N}";
        await using (var context = database.CreateContext())
        {
            var entityId = await context.Applications.Where(x => x.Id == funded).Select(x => x.EntityId).SingleAsync();
            var entity = await context.Entities.SingleAsync(x => x.Id == entityId);
            entity.Type = EntityType.InformalGroup;
            entity.Name = group;
            entity.Representatives = [new EntityRepresentative("Janina", "Kowalska", "lider grupy")];
            await context.SaveChangesAsync();
        }

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, funded, passed: true);
        await FormalAsync(operatorClient, reserve, passed: true);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);
        await ScoreAsync(operatorClient, expert, expertId, reserve, 12);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(6500m, null))).EnsureSuccessStatusCode();

        var anonymous = host.CreateClient();
        Assert.DoesNotContain(
            (await anonymous.GetFromJsonAsync<List<ResultsArchiveEntry>>("/public/results"))!,
            x => x.CompetitionId == competition.Id);

        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        var response = await anonymous.GetAsync("/public/results");
        var body = await response.Content.ReadAsStringAsync();
        var entry = Assert.Single((await response.Content.ReadFromJsonAsync<List<ResultsArchiveEntry>>())!,
            x => x.CompetitionId == competition.Id);

        var project = Assert.Single(entry.Projects);
        Assert.Equal((group, (decimal?)6500m), (project.EntityName, project.AwardedGrant));
        Assert.DoesNotContain("Kowalska", body);
        Assert.DoesNotContain("Janina", body);
    }
}
