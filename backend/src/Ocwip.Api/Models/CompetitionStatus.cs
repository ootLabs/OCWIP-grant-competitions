using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// The life of a competition, in the seven states the report names and in
    /// the order it names them (R-17 in docs/runbook/rozbieznosci.md).
    ///
    /// The cards speak of four. Seven is what the product actually needs,
    /// because "opublikowany" and "trwa nabór" are two different moments: from
    /// the first the competition is visible to a guest, from the second the
    /// "Wypelnij wniosek" button does anything. Collapsing them would leave no
    /// way to announce a competition before the intake opens, which is how
    /// every competition here is announced.
    ///
    /// Stored as text (CompetitionConfiguration), so the order below means
    /// nothing in the database and the two values added in T-20 cost no data
    /// migration. The names are the contract: renaming one is.
    ///
    /// On the wire it is text too, by attribute rather than by a serializer
    /// option, for the reason written out on Models/Role.cs.
    ///
    /// Who moves the competition between them is NOT expressed here but in
    /// CompetitionStatusTransitions, which is the single table of allowed
    /// pairs the card asks for.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CompetitionStatus>))]
    public enum CompetitionStatus
    {
        /// <summary>
        /// "Roboczy". Being written. Has no public address at all, which is a
        /// stronger statement than "the link is hidden": see
        /// CompetitionLifecycle.IsPubliclyVisible.
        /// </summary>
        Draft,

        /// <summary>
        /// "Opublikowany". Visible to a guest, but not yet taking applications.
        /// </summary>
        Published,

        /// <summary>
        /// "Trwa nabór". Visible and taking applications. Reached by the clock,
        /// never by a person.
        /// </summary>
        OpenForApplications,

        /// <summary>
        /// "Nabór zamknięty". Reached by the clock at the closing minute, or by
        /// the operator when the intake is continuous and therefore has no
        /// closing minute of its own.
        /// </summary>
        Closed,

        /// <summary>
        /// "Trwa ocena". The evaluation module is M5; this state exists so that
        /// the transition into it is already written down.
        /// </summary>
        UnderReview,

        /// <summary>"Rozstrzygnięty".</summary>
        Resolved,

        /// <summary>
        /// "Archiwalny". Still public, still readable, out of the current
        /// listing. Not the same thing as inactive: see Competition.IsActive.
        /// </summary>
        Archived
    }
}
