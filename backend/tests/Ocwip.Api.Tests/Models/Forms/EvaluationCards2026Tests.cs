using System.Text.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The two cards of the Kierunek NOWE FIO 2026 competition (T-38b), read from
/// backend/seed/evaluation-cards, the files scripts/seed.py publishes. A card
/// that stops passing the contract fails here, not in somebody's seed run.
/// </summary>
public sealed class EvaluationCards2026Tests
{
    private static JsonElement Card(string name)
    {
        // Walked up from the test binaries: /src in the container, the
        // repository's backend directory in CI.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "seed", "evaluation-cards", $"{name}-2026.json");

            if (File.Exists(path))
            {
                return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
            }
        }

        throw new FileNotFoundException($"seed/evaluation-cards/{name}-2026.json not found above {AppContext.BaseDirectory}");
    }

    private static FormDocument Formal()
    {
        var result = FormSchemaValidator.Validate(Card("formal"), FormPurpose.FormalEvaluation);
        Assert.Empty(result.Errors);
        return result.Document!;
    }

    private static FormDocument Merit()
    {
        var result = FormSchemaValidator.Validate(Card("merit"), FormPurpose.MeritEvaluation);
        Assert.Empty(result.Errors);
        return result.Document!;
    }

    private static readonly Dictionary<string, decimal?> NoBases = new();

    [Fact]
    public void The_formal_card_has_the_eight_criteria_of_the_paper_two_of_them_for_organisations_only()
    {
        var criteria = Formal().Sections.SelectMany(section => section.Fields)
            .Where(field => field.Role == FormFieldRole.FormalCriterion)
            .ToList();

        Assert.Equal(8, criteria.Count);
        Assert.Equal(
            ["przychod_do_50000", "rejestracja_do_60_miesiecy"],
            criteria.Where(field => field.AppliesTo is not null).Select(field => field.Key));
        Assert.All(
            criteria.Where(field => field.AppliesTo is not null),
            field => Assert.Equal([EntityType.Organisation], field.AppliesTo));
    }

    [Fact]
    public void An_informal_group_finishes_the_formal_card_without_the_organisation_criteria()
    {
        var answers = new JsonObject
        {
            ["zlozony_w_terminie"] = true,
            ["uprawniony_wnioskodawca"] = true,
            ["siedziba_w_wojewodztwie"] = true,
            ["dzialania_w_wojewodztwie"] = true,
            ["dzialania_w_terminie"] = true,
            ["kwota_do_7000"] = true,
        };
        var element = AnswerSamples.Element(answers);

        var group = AnswerValidator.Validate(Formal(), element, NoBases, AnswerStrictness.Submission, EntityType.InformalGroup);
        var organisation = AnswerValidator.Validate(Formal(), element, NoBases, AnswerStrictness.Submission, EntityType.Organisation);

        Assert.Empty(group.Errors);
        Assert.True(EvaluationScores.Read(Formal(), element, EntityType.InformalGroup).FormalPassed);
        Assert.Equal(
            ["przychod_do_50000", "rejestracja_do_60_miesiecy"],
            organisation.Errors.Select(error => error.Key).Order());
    }

    [Fact]
    public void Full_marks_on_the_merit_card_are_fifty_and_the_strategic_points_follow_the_applicant()
    {
        var answers = AnswerSamples.Element(new JsonObject
        {
            ["pomysl_i_cel"] = 20,
            ["rezultaty"] = 16,
            ["promocja"] = 10,
            ["budzet"] = 4,
            ["biale_plamy"] = true,
            ["grupa_z_patronem"] = true,
            ["bez_wsparcia_nowefio"] = true,
            ["proponowana_kwota"] = 7000,
        });

        var organisation = EvaluationScores.Read(Merit(), answers, EntityType.Organisation);
        var patronGroup = EvaluationScores.Read(Merit(), answers, EntityType.PatronInformalGroup);

        Assert.Equal(50m, organisation.MeritScore);
        // Białe plamy for everybody, plus one criterion each kind is asked.
        Assert.Equal(2m, organisation.StrategicScore);
        Assert.Equal(2m, patronGroup.StrategicScore);
        Assert.Equal(7000m, organisation.RecommendedGrant);
    }

    [Fact]
    public void A_merit_score_above_the_paper_scale_is_refused()
    {
        var answers = AnswerSamples.Element(new JsonObject { ["budzet"] = 5 });

        var result = AnswerValidator.Validate(Merit(), answers, NoBases, AnswerStrictness.Submission, EntityType.Organisation);

        Assert.Contains(result.Errors, error => error.Key == "budzet");
    }
}
