using System.Text.Json;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// Definitions the contract tests are written against (T-24).
///
/// Two of them, on purpose. <see cref="AllFieldKinds"/> proves every kind from
/// docs/runbook/pola.md has a representation; <see cref="Budget"/> is the part
/// of the real 2026 application that decides whether the schema works at all:
/// a table the applicant adds rows to, a value computed per row, a sum over a
/// column, a grant computed as a difference (D11) and a ceiling stated so it
/// can be read backwards (D12).
/// </summary>
internal static class FormDefinitionSamples
{
    public static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>Wraps field definitions in the smallest legal document.</summary>
    public static JsonElement WithFields(params string[] fields) =>
        Parse(
            $$"""
            {
              "schemaVersion": 1,
              "sections": [
                {
                  "key": "sekcja",
                  "title": "Sekcja",
                  "fields": [{{string.Join(",", fields)}}]
                }
              ]
            }
            """);

    public static string Field(
        string key,
        string type,
        string extra = "",
        bool printed = true) =>
        $$"""
        {
          "key": "{{key}}",
          "type": "{{type}}",
          "label": "Etykieta {{key}}",
          "help": "Podpowiedź do pola {{key}}",
          "required": true,
          "printed": {{(printed ? "true" : "false")}}{{(extra.Length == 0 ? string.Empty : "," + extra)}}
        }
        """;

    public static JsonElement AllFieldKinds() =>
        WithFields(
            Field("tytul", "shortText", "\"maxLength\": 200"),
            Field("opis", "longText", "\"maxLength\": 5000, \"minLength\": 1000"),
            Field("uczestnicy", "number", "\"minValue\": 1"),
            Field("kwota", "amount", "\"minValue\": 0"),
            Field("udzial", "percent", "\"minValue\": 0, \"maxValue\": 100"),
            Field("data_konca", "date"),
            Field("termin_spotkania", "dateTime"),
            Field("wersja_papierowa", "yesNo"),
            Field(
                "forma_prawna",
                "singleChoice",
                """
                "options": [
                  { "value": "stowarzyszenie", "label": "Stowarzyszenie" },
                  { "value": "fundacja", "label": "Fundacja" },
                  { "value": "inna", "label": "Inna" }
                ]
                """),
            Field(
                "formaty",
                "multipleChoice",
                """
                "options": [
                  { "value": "pdf", "label": "PDF" },
                  { "value": "odt", "label": "ODT" }
                ]
                """),
            Field(
                "rezultaty",
                "repeatableTable",
                $$"""
                "table": {
                  "minRows": 1,
                  "columns": [
                    {{Field("rezultat", "shortText", "\"maxLength\": 200")}},
                    {{Field("wartosc_docelowa", "number")}}
                  ]
                }
                """),
            Field(
                "czlonkowie_grupy",
                "fixedTable",
                $$"""
                "table": {
                  "rows": [
                    { "key": "lider", "label": "Lider grupy" },
                    { "key": "czlonek_2", "label": "Członek drugi" },
                    { "key": "czlonek_3", "label": "Członek trzeci" }
                  ],
                  "columns": [
                    {{Field("imie_i_nazwisko", "shortText", "\"maxLength\": 200")}},
                    {{Field("telefon", "shortText", "\"maxLength\": 20")}}
                  ]
                }
                """),
            Field(
                "odpis_z_rejestru",
                "file",
                """
                "file": {
                  "allowedFormats": ["pdf", "jpg"],
                  "maxSizeMegabytes": 10
                }
                """),
            Field(
                "oswiadczenie_1",
                "statement",
                "\"statementText\": \"Oświadczam, że dane są zgodne ze stanem "
                + "faktycznym.\""),
            Field(
                "suma_uczestnikow",
                "calculated",
                """
                "calculation": {
                  "kind": "sum",
                  "operands": ["rezultaty.wartosc_docelowa"]
                }
                """));

    /// <summary>
    /// Part III of the 2026 application, cut down to what the schema has to
    /// survive: cost table A, its per-row value, its sum, the applicant's own
    /// contribution, the grant as the difference (D11), cost table C and the
    /// share of indirect costs with its ceiling (D12).
    /// </summary>
    public static JsonElement Budget() =>
        WithFields(
            Field(
                "budzet_a",
                "repeatableTable",
                $$"""
                "table": {
                  "minRows": 1,
                  "columns": [
                    {{Field("nazwa", "shortText", "\"maxLength\": 500")}},
                    {{Field("jednostka", "shortText", "\"maxLength\": 50")}},
                    {{Field("liczba", "number", "\"minValue\": 0")}},
                    {{Field("cena", "amount", "\"minValue\": 0")}},
                    {{Field(
                        "wartosc",
                        "calculated",
                        """
                        "calculation": {
                          "kind": "product",
                          "operands": ["liczba", "cena"]
                        }
                        """)}}
                  ]
                }
                """),
            Field(
                "suma_a",
                "calculated",
                """
                "calculation": { "kind": "sum", "operands": ["budzet_a.wartosc"] }
                """),
            Field("wklad_wlasny", "amount", "\"minValue\": 0"),
            Field(
                "dotacja",
                "calculated",
                """
                "calculation": {
                  "kind": "difference",
                  "operands": ["suma_a", "wklad_wlasny"]
                },
                "limits": [
                  { "kind": "maxAmount", "basis": "competition.maxGrantAmount" }
                ]
                """),
            Field(
                "budzet_c",
                "repeatableTable",
                $$"""
                "table": {
                  "columns": [
                    {{Field("nazwa", "shortText", "\"maxLength\": 500")}},
                    {{Field("cena", "amount", "\"minValue\": 0")}}
                  ]
                }
                """),
            Field(
                "suma_c",
                "calculated",
                """
                "calculation": { "kind": "sum", "operands": ["budzet_c.cena"] },
                "limits": [
                  { "kind": "maxPercentOf", "percent": 10, "basis": "dotacja" }
                ]
                """),
            Field(
                "udzial_posrednich",
                "calculated",
                """
                "calculation": { "kind": "ratio", "operands": ["suma_c", "dotacja"] }
                """,
                printed: false));

    /// <summary>
    /// The three cost tables of the 2026 application (T-31): A without a
    /// percentage ceiling, B and C each held to a threshold that comes from
    /// the competition, not from the form, and the grant computed from all
    /// three minus the applicant's own contribution (D11).
    /// </summary>
    public static JsonElement BudgetWithThreeTables() =>
        WithFields(
            CostTable("budzet_a", "Koszty bezpośrednie"),
            Sum("suma_a", "budzet_a"),
            CostTable("budzet_b", "Koszty rozwoju instytucjonalnego"),
            Sum("suma_b", "budzet_b", "competition.maxInstitutionalDevelopmentPercent"),
            CostTable("budzet_c", "Koszty pośrednie"),
            Sum("suma_c", "budzet_c", "competition.maxIndirectCostPercent"),
            Field(
                "koszty_razem",
                "calculated",
                """
                "calculation": { "kind": "sum", "operands": ["suma_a", "suma_b", "suma_c"] }
                """),
            Field("wklad_wlasny", "amount", "\"minValue\": 0"),
            Field(
                "dotacja",
                "calculated",
                """
                "calculation": {
                  "kind": "difference",
                  "operands": ["koszty_razem", "wklad_wlasny"]
                },
                "limits": [
                  { "kind": "maxAmount", "basis": "competition.maxGrantAmount" }
                ]
                """));

    /// <summary>A cost table: name, units, unit price, value per row.</summary>
    private static string CostTable(string key, string label) =>
        $$"""
        {
          "key": "{{key}}",
          "type": "repeatableTable",
          "label": "{{label}}",
          "required": false,
          "printed": true,
          "table": {
            "columns": [
              {{Field("nazwa", "shortText", "\"maxLength\": 500")}},
              {{Field("liczba", "number", "\"minValue\": 0")}},
              {{Field("cena", "amount", "\"minValue\": 0")}},
              {{Field(
                  "wartosc",
                  "calculated",
                  """
                  "calculation": { "kind": "product", "operands": ["liczba", "cena"] }
                  """)}}
            ]
          }
        }
        """;

    /// <summary>
    /// The total of a cost table, optionally held to a share of the grant
    /// whose percentage is a competition setting.
    /// </summary>
    private static string Sum(string key, string table, string? percentFrom = null) =>
        Field(
            key,
            "calculated",
            $$"""
            "calculation": { "kind": "sum", "operands": ["{{table}}.wartosc"] }{{(percentFrom is null ? string.Empty : $$"""
            ,
            "limits": [
              { "kind": "maxPercentOf", "percentFrom": "{{percentFrom}}", "basis": "dotacja" }
            ]
            """)}}
            """);
}
