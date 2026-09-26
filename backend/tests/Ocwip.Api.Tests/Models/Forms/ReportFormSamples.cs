using System.Text.Json;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// An application with a budget table and the report that takes its values
/// back ("było i jest", T-50a): the title read only, each budget row with its
/// planned value read only next to the executed one and the difference.
/// </summary>
public static class ReportFormSamples
{
    public static JsonElement Application() =>
        WithFields(
            Field("opis", "shortText", "\"maxLength\": 500"),
            Field(
                "budzet_a",
                "repeatableTable",
                $$"""
                "table": {
                  "minRows": 1,
                  "columns": [
                    {{Field("nazwa", "shortText", "\"maxLength\": 500")}},
                    {{Field("liczba", "number", "\"minValue\": 0")}},
                    {{Field("cena", "amount", "\"minValue\": 0")}},
                    {{Field("wartosc", "calculated", "\"calculation\": { \"kind\": \"product\", \"operands\": [\"liczba\", \"cena\"] }")}}
                  ]
                }
                """));

    public const string ApplicationAnswers = """
        {"opis":"Ławki w parku","budzet_a":[{"nazwa":"Deski","liczba":10,"cena":150},{"nazwa":"Farba","liczba":2,"cena":50}]}
        """;

    public static JsonElement Report() =>
        WithFields(
            Field("tytul", "shortText", "\"maxLength\": 500, \"readOnly\": true, \"prefillFrom\": \"opis\"")
                .Replace("\"required\": true", "\"required\": false"),
            Field("przebieg", "longText", "\"maxLength\": 5000"),
            Field(
                "budzet",
                "repeatableTable",
                $$"""
                "prefillFrom": "budzet_a",
                "table": {
                  "minRows": 1,
                  "columns": [
                    {{Optional(Field("pozycja", "shortText", "\"maxLength\": 500, \"readOnly\": true, \"prefillFrom\": \"nazwa\""))}},
                    {{Optional(Field("planowana", "amount", "\"minValue\": 0, \"readOnly\": true, \"prefillFrom\": \"wartosc\""))}},
                    {{Field("wykonana", "amount", "\"minValue\": 0")}},
                    {{Field("roznica", "calculated", "\"calculation\": { \"kind\": \"difference\", \"operands\": [\"wykonana\", \"planowana\"] }")}}
                  ]
                }
                """));

    /// <summary>A read only field may not be required (the applicant could not fill it in).</summary>
    private static string Optional(string field) => field.Replace("\"required\": true", "\"required\": false");
}
