namespace Ocwip.Api.Models
{
    /// <summary>
    /// The state a competition is actually in right now, which is not always
    /// the state written in its column.
    ///
    /// The report is explicit that the intake opens and closes on its own, from
    /// the dates, and that nobody moves it. Two ways to honour that: a
    /// background job rewriting the column on a timer, or deriving the state at
    /// read time. This is the second one, for a reason that decides it: a timer
    /// leaves the column wrong between ticks, and the minute it is wrong in is
    /// the closing minute, which is the one minute decision D7 forbids being
    /// wrong about. There is also no scheduler in this stack to put a job in.
    ///
    /// So the column holds the last state somebody chose, and this class adds
    /// whatever the clock has done since. Everything that answers a question
    /// about a competition, including the operator's own transitions, asks
    /// here. T-21 builds "is this competition still taking applications" on
    /// top of this and not next to it.
    /// </summary>
    public static class CompetitionLifecycle
    {
        /// <summary>
        /// The stored state, advanced by every scheduled transition whose
        /// moment has already passed.
        ///
        /// A loop and not a single step: a competition created with both dates
        /// in the past and published today is, in the same instant, open and
        /// then closed, and answering "trwa nabór" for it would be a lie with a
        /// deadline attached to it.
        /// </summary>
        public static CompetitionStatus Effective(
            Competition competition,
            DateTimeOffset now)
        {
            var status = competition.Status;

            while (true)
            {
                var target = CompetitionStatusTransitions.ScheduledTarget(status);

                if (target is null)
                {
                    return status;
                }

                var moment = ScheduledMoment(competition, status);

                // Null means the clock has no date to act on. A continuous
                // intake reaches this with EndDate null, and it has to stay
                // open rather than be treated as a competition whose closing
                // date has already passed. Same trap T-21 is warned about.
                if (moment is null || now < moment.Value)
                {
                    return status;
                }

                status = target.Value;
            }
        }

        /// <summary>
        /// When the scheduled transition out of <paramref name="from"/> fires,
        /// or null when it never does for this competition.
        ///
        /// Comparison is "now is at or past this moment", not "past it": the
        /// competition closing at 12:00 is closed at 12:00:00, which is why
        /// both dates are truncated to a whole minute on the way in.
        /// </summary>
        private static DateTimeOffset? ScheduledMoment(
            Competition competition,
            CompetitionStatus from) => from switch
            {
                CompetitionStatus.Published => competition.StartDate,
                CompetitionStatus.OpenForApplications => competition.EndDate,
                _ => null,
            };

        /// <summary>
        /// Whether a competition in this state has a public address at all.
        ///
        /// A draft does not, and the wording matters: this is not a hidden
        /// link. The public read endpoints answer 404 for a draft, because 403
        /// on a guessed identifier confirms that the draft exists.
        /// </summary>
        public static bool IsPubliclyVisible(CompetitionStatus status) =>
            status is not CompetitionStatus.Draft;
    }
}
