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
        Schedule
    }

    /// <summary>
    /// One allowed pair, plus who may cause it.
    /// </summary>
    public readonly record struct CompetitionStatusTransition(
        CompetitionStatus From,
        CompetitionStatus To,
        TransitionTrigger Trigger);
}
