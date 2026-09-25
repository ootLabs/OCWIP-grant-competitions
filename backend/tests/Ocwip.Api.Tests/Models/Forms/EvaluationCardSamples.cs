using System.Text.Json;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// Two small evaluation cards shaped like the 2026 ones (T-38): enough of
/// each to exercise every rule, not the full text, which is T-38b's.
/// </summary>
public static class EvaluationCardSamples
{
    /// <summary>
    /// Three criteria: one for everybody, one asked of an organisation only
    /// (the income ceiling), one of a group under a patron only.
    /// </summary>
    public static JsonElement FormalCard() =>
        WithFields(
            Field("w_terminie", "yesNo", "\"role\": \"formalCriterion\""),
            Field("uzasadnienie_terminu", "longText", "\"maxLength\": 2000", printed: true)
                .Replace("\"required\": true", "\"required\": false"),
            Field(
                "przychod",
                "yesNo",
                "\"role\": \"formalCriterion\", \"appliesTo\": [\"Organisation\"]"),
            Field(
                "bez_funkcji_u_patrona",
                "yesNo",
                "\"role\": \"formalCriterion\", \"appliesTo\": [\"PatronInformalGroup\"]"));

    /// <summary>
    /// Two merit criteria (0-20 and 0-4), a merit sum, two strategic criteria
    /// worth a point each (one for informal groups only), their sum, and the
    /// recommended grant.
    /// </summary>
    public static JsonElement MeritCard() =>
        WithFields(
            Field("pomysl", "number", "\"minValue\": 0, \"maxValue\": 20"),
            Field("pomysl_uzasadnienie", "longText", "\"maxLength\": 3000"),
            Field("budzet", "number", "\"minValue\": 0, \"maxValue\": 4"),
            Field(
                "suma",
                "calculated",
                "\"calculation\": { \"kind\": \"sum\", \"operands\": [\"pomysl\", \"budzet\"] }, \"role\": \"meritScore\""),
            Field("biale_plamy", "yesNo", "\"points\": 1"),
            Field(
                "z_patronem",
                "yesNo",
                "\"points\": 1, \"appliesTo\": [\"InformalGroup\", \"PatronInformalGroup\"]"),
            Field(
                "strategiczne",
                "calculated",
                "\"calculation\": { \"kind\": \"sum\", \"operands\": [\"biale_plamy\", \"z_patronem\"] }, \"role\": \"strategicScore\""),
            Field("kwota", "amount", "\"role\": \"recommendedGrant\"")
                .Replace("\"required\": true", "\"required\": false"));
}
