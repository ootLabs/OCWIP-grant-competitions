using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Ranking;

/// <summary>
/// Where the formal evaluation of one application stands (T-38, T-39), read
/// from the card's answers rather than from a stored verdict, the same way
/// <see cref="EvaluationScores"/> reads every other result.
///
/// One rule in one place: the ranking list and the operator's list of
/// applications (T-35) both show it, and two copies of this switch would
/// start disagreeing the first time one of them was corrected.
/// </summary>
internal static class FormalStandingReader
{
    /// <param name="cards">
    /// The card document per form version, as the caller already read them;
    /// a version missing or no longer passing the contract gate counts as a
    /// card still being filled in, not as a verdict.
    /// </param>
    public static FormalStanding Of(
        IReadOnlyDictionary<Guid, FormDocument?> cards, Evaluation? formal, EntityType applicant)
    {
        if (formal is null)
        {
            return FormalStanding.NotStarted;
        }

        if (formal.Status != EvaluationStatus.Finished)
        {
            return FormalStanding.InProgress;
        }

        var scores = cards.TryGetValue(formal.FormDefinitionId, out var card) && card is not null
            ? EvaluationScores.Read(card, formal.Answers, applicant)
            : null;

        return scores?.FormalPassed switch
        {
            true => FormalStanding.Passed,
            false => FormalStanding.Failed,
            _ => FormalStanding.InProgress,
        };
    }
}
