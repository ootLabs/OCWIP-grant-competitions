using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The three rows an application cannot exist without: a competition, one
/// version of its form definition, and the entity that files it.
/// </summary>
internal sealed record ApplicationChain(
    Guid CompetitionId,
    Guid FormDefinitionId,
    Guid EntityId);

/// <summary>
/// Seeds that chain, because an application has three foreign keys and
/// repeating the setup in every database test class is how the setups drift
/// apart.
/// </summary>
internal static class TestApplicationChain
{
    public static FormDefinition NewFormDefinition(
        Guid competitionId,
        int versionNumber = 1) =>
        new()
        {
            CompetitionId = competitionId,
            VersionNumber = versionNumber,
            // Clone detaches the element from the document, so it stays valid
            // after the document is disposed.
            Definition = JsonDocument
                .Parse("""{"sections":[]}""")
                .RootElement
                .Clone(),
        };

    public static async Task<ApplicationChain> SeedAsync(
        PostgresDatabaseFixture database,
        string label)
    {
        await using var context = database.CreateContext();

        var competition = TestCompetition.New($"Konkurs {label}");
        var entity = TestEntity.New($"Podmiot {label}");
        context.Competitions.Add(competition);
        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        var definition = NewFormDefinition(competition.Id);
        context.FormDefinitions.Add(definition);
        await context.SaveChangesAsync();

        return new ApplicationChain(competition.Id, definition.Id, entity.Id);
    }
}
