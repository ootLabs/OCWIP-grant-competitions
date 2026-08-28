using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Applications that satisfy every constraint.
///
/// Two factories rather than one with optional arguments, because the schema
/// pairs the status with both the submission date and the number: a single
/// factory would let a test build a shape the database refuses without meaning
/// to, and then the test would fail for a reason it never asked about.
/// </summary>
internal static class TestApplication
{
    public const string Answers = """{"strona-1":{"nazwa-zadania":"Test"}}""";

    public static Application Draft(
        ApplicationChain chain,
        string answers = Answers) =>
        new()
        {
            CompetitionId = chain.CompetitionId,
            EntityId = chain.EntityId,
            FormDefinitionId = chain.FormDefinitionId,
            Answers = Json(answers),
            Status = ApplicationStatus.Draft,
        };

    public static Application Submitted(
        ApplicationChain chain,
        string number,
        string answers = Answers) =>
        new()
        {
            CompetitionId = chain.CompetitionId,
            EntityId = chain.EntityId,
            FormDefinitionId = chain.FormDefinitionId,
            Answers = Json(answers),
            Status = ApplicationStatus.Submitted,
            SubmittedAt = new DateTimeOffset(2026, 9, 15, 10, 30, 0, TimeSpan.Zero),
            Number = number,
        };

    private static JsonElement Json(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}
