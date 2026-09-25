using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// What one evaluation card says, read through the role markers of the card
/// version it was filled in on (T-38), with the same AnswerCalculator the
/// validation uses. Never stored: a saved sum next to the answers would be a
/// second fact about one thing, which D15 already refused once.
/// </summary>
/// <param name="FormalPassed">
/// True when every formal criterion asked of this applicant is met, false as
/// soon as one is not, null while any is still unanswered or the card has no
/// criteria (a merit card).
/// </param>
/// <param name="MeritScore">The merit sum, null on a card without one.</param>
/// <param name="StrategicScore">The strategic sum, null on a card without one.</param>
/// <param name="RecommendedGrant">
/// The amount the expert recommends, null when left empty: an empty optional
/// amount is not a recommendation of zero.
/// </param>
public sealed record EvaluationScores(
    bool? FormalPassed,
    decimal? MeritScore,
    decimal? StrategicScore,
    decimal? RecommendedGrant)
{
    public static EvaluationScores Read(FormDocument card, JsonElement answers, EntityType applicant)
    {
        var calculator = new AnswerCalculator(card, answers, applicant);
        var criteria = new List<JsonElement?>();
        decimal? merit = null;
        decimal? strategic = null;
        decimal? recommended = null;

        foreach (var section in card.Sections)
        {
            if (!calculator.IsVisible(section.VisibleWhen))
            {
                continue;
            }

            foreach (var field in section.Fields)
            {
                if (field.Role == FormFieldRole.None
                    || !calculator.IsVisible(field.VisibleWhen)
                    || !calculator.IsApplicable(field))
                {
                    continue;
                }

                switch (field.Role)
                {
                    case FormFieldRole.FormalCriterion:
                        criteria.Add(calculator.Answer(field.Key));
                        break;
                    case FormFieldRole.MeritScore:
                        merit = calculator.Value(field);
                        break;
                    case FormFieldRole.StrategicScore:
                        strategic = calculator.Value(field);
                        break;
                    case FormFieldRole.RecommendedGrant:
                        recommended = AnswerValues.IsEmpty(calculator.Answer(field.Key))
                            ? null
                            : calculator.Value(field);
                        break;
                }
            }
        }

        return new EvaluationScores(Formal(criteria), merit, strategic, recommended);
    }

    private static bool? Formal(IReadOnlyList<JsonElement?> criteria)
    {
        if (criteria.Count == 0)
        {
            return null;
        }

        if (criteria.Any(answer => answer is { ValueKind: JsonValueKind.False }))
        {
            return false;
        }

        return criteria.All(answer => answer is { ValueKind: JsonValueKind.True }) ? true : null;
    }
}
