namespace Ocwip.Api.Models
{
    /// <summary>
    /// Who is allowed to cause a transition.
    /// </summary>
    public enum TransitionTrigger
    {
        /// <summary>
        /// An operator asks for it, deliberately, through the API.
        /// </summary>
        Operator,

        /// <summary>
        /// The clock causes it, from the dates on the competition. Nobody
        /// presses anything, and no endpoint accepts it: the report is explicit
        /// that these happen on their own.
        /// </summary>
        Schedule,

        /// <summary>
        /// Approving the results (T-97), and only that: the approval writes
        /// every application's result and resolves the competition in one
        /// transaction, so the status endpoint cannot resolve a competition
        /// whose results nobody approved.
        /// </summary>
        ResultsApproval
    }

    /// <summary>
    /// One allowed pair, plus who may cause it.
    /// </summary>
    public readonly record struct CompetitionStatusTransition(
        CompetitionStatus From,
        CompetitionStatus To,
        TransitionTrigger Trigger);
}
