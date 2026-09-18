namespace Ocwip.Api.Models
{
    /// <summary>
    /// Every legal move between competition states, as a table of pairs.
    ///
    /// The card asks for exactly this shape and R-17 says why: the report has
    /// three states the cards do not, and a lifecycle expressed as switch
    /// statements scattered through a service makes adding one a hunt. Here it
    /// is a row.
    ///
    /// The table is the only authority. There is no second place that knows
    /// "you cannot publish twice", and nothing outside this file compares two
    /// CompetitionStatus values to decide whether a move is allowed.
    /// </summary>
    public static class CompetitionStatusTransitions
    {
        /// <summary>
        /// Read it top to bottom and it is the sequence from the report:
        /// roboczy, opublikowany, trwa nabór, nabór zamknięty, trwa ocena,
        /// rozstrzygnięty, archiwalny.
        ///
        /// Two rows deserve a note.
        ///
        /// Published to OpenForApplications and OpenForApplications to Closed
        /// are the only Schedule rows, because the intake window is the only
        /// thing in this product that the calendar decides on its own.
        ///
        /// OpenForApplications to Closed also exists as an Operator row, and it
        /// is not a duplicate: a continuous intake has no closing date, so the
        /// clock will never move it, and without this row such a competition
        /// could never be closed at all. This is the "zamknięcie" the card
        /// lists in its scope.
        /// </summary>
        private static readonly CompetitionStatusTransition[] Table =
        [
            new(CompetitionStatus.Draft,
                CompetitionStatus.Published,
                TransitionTrigger.Operator),

            new(CompetitionStatus.Published,
                CompetitionStatus.OpenForApplications,
                TransitionTrigger.Schedule),

            new(CompetitionStatus.OpenForApplications,
                CompetitionStatus.Closed,
                TransitionTrigger.Schedule),

            new(CompetitionStatus.OpenForApplications,
                CompetitionStatus.Closed,
                TransitionTrigger.Operator),

            new(CompetitionStatus.Closed,
                CompetitionStatus.UnderReview,
                TransitionTrigger.Operator),

            new(CompetitionStatus.UnderReview,
                CompetitionStatus.Resolved,
                TransitionTrigger.Operator),

            new(CompetitionStatus.Resolved,
                CompetitionStatus.Archived,
                TransitionTrigger.Operator),
        ];

        public static IReadOnlyList<CompetitionStatusTransition> All => Table;

        /// <summary>
        /// May an operator move a competition from <paramref name="from"/> to
        /// <paramref name="to"/>?
        ///
        /// Deliberately not "is this pair in the table": a Schedule-only pair
        /// answers false here, so no endpoint can hand an operator a way to
        /// open an intake before its start date. There is one such pair today
        /// (Published to OpenForApplications) and forgetting this distinction
        /// would quietly make it operator-driven.
        /// </summary>
        public static bool AllowsOperator(
            CompetitionStatus from,
            CompetitionStatus to) =>
            Table.Any(transition =>
                transition.From == from
                && transition.To == to
                && transition.Trigger == TransitionTrigger.Operator);

        /// <summary>
        /// The states an operator may move to from here, in table order. The
        /// API hands this to the caller so a panel does not have to reimplement
        /// the table to decide which buttons to draw.
        /// </summary>
        public static IReadOnlyList<CompetitionStatus> OperatorTargets(
            CompetitionStatus from) =>
            Table
                .Where(transition =>
                    transition.From == from
                    && transition.Trigger == TransitionTrigger.Operator)
                .Select(transition => transition.To)
                .Distinct()
                .ToArray();

        /// <summary>
        /// The one state the clock can move this competition to next, or null
        /// when the clock has nothing to say about this state.
        /// </summary>
        public static CompetitionStatus? ScheduledTarget(CompetitionStatus from)
        {
            foreach (var transition in Table)
            {
                if (transition.From == from
                    && transition.Trigger == TransitionTrigger.Schedule)
                {
                    return transition.To;
                }
            }

            return null;
        }
    }
}
