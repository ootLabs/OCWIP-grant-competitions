using System.Text.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// Answers the T-30 tests check, written the way the renderer stores them
/// (frontend/lib/forms/answer-types.ts).
/// </summary>
internal static class AnswerSamples
{
    public static readonly IReadOnlyDictionary<string, decimal?> NoCompetitionSettings =
        new Dictionary<string, decimal?>();

    public static FormDocument Document(JsonElement definition)
    {
        var result = FormSchemaValidator.Validate(definition);

        return result.Document
            ?? throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Message)));
    }

    /// <summary>
    /// A complete, correct set of answers to
    /// <see cref="FormDefinitionSamples.AllFieldKinds"/>, as a node so a test
    /// can break exactly one thing in it.
    /// </summary>
    public static JsonObject CompleteAllFieldKinds() =>
        new()
        {
            ["tytul"] = "Warsztaty dla seniorów",
            ["opis"] = new string('a', 1000),
            ["uczestnicy"] = 12,
            ["kwota"] = 1500.5m,
            ["udzial"] = 20,
            ["data_konca"] = "2026-12-31",
            ["termin_spotkania"] = "2026-11-02T10:30",
            ["wersja_papierowa"] = false,
            ["forma_prawna"] = "fundacja",
            ["formaty"] = new JsonArray("pdf"),
            ["rezultaty"] = new JsonArray(
                new JsonObject { ["rezultat"] = "Spotkania", ["wartosc_docelowa"] = 10 }),
            ["czlonkowie_grupy"] = new JsonArray(
                Member("Anna Nowak"), Member("Jan Kowalski"), Member("Ewa Lis")),
            ["odpis_z_rejestru"] = new JsonObject
            {
                ["name"] = "odpis.pdf",
                ["sizeBytes"] = 204800,
            },
            ["oswiadczenie_1"] = true,
        };

    public static JsonElement Element(JsonNode node) =>
        JsonSerializer.SerializeToElement(node);

    public static JsonElement Parse(string json) => FormDefinitionSamples.Parse(json);

    public static AnswerValidationResult Validate(
        JsonElement definition,
        JsonNode answers,
        AnswerStrictness strictness,
        IReadOnlyDictionary<string, decimal?>? settings = null) =>
        AnswerValidator.Validate(
            Document(definition),
            Element(answers),
            settings ?? NoCompetitionSettings,
            strictness);

    public static string? MessageFor(AnswerValidationResult result, string key) =>
        result.Errors.SingleOrDefault(error => error.Key == key)?.Message;

    private static JsonObject Member(string name) =>
        new() { ["imie_i_nazwisko"] = name, ["telefon"] = "600100200" };
}
