namespace Ocwip.Api.Models
{
    /// <summary>
    /// One row per status change of an application (T-33): who changed it,
    /// when, and the transition itself, from status to status.
    ///
    /// Append-only. Nothing here is ever updated once written: a later
    /// transition is a new row, never a rewrite of this one, because this
    /// table is the proof that a submission happened at a given instant, not
    /// a mutable "current status" column with a memory attached. The current
    /// status still lives on Application.Status; this table exists precisely
    /// so that column is never the only place the history lived.
    ///
    /// FromStatus and ToStatus are not narrowed to the one transition this
    /// card writes (Draft to Submitted). R-03, the "return for correction"
    /// workflow, is explicitly out of scope for T-33, but a future
    /// Submitted-to-Draft row belongs in this same table rather than a second
    /// one invented later.
    /// </summary>
    public class ApplicationStatusHistory : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        public ApplicationStatus FromStatus { get; set; }

        public ApplicationStatus ToStatus { get; set; }

        /// <summary>
        /// When the transition happened, in UTC. Distinct from CreatedAt
        /// below in the same way Application.SubmittedAt is distinct from
        /// Application.CreatedAt: this one carries domain meaning ("when the
        /// applicant submitted") and is read by that name, rather than
        /// borrowing the audit stamp that happens to carry the same instant
        /// today but is not guaranteed to forever, for example if this row is
        /// ever written by a backfill.
        /// </summary>
        public DateTimeOffset ChangedAt { get; set; }

        /// <summary>
        /// The account that made the change. For the one transition this
        /// card writes it is always the submitting applicant, but a future
        /// operator-initiated transition (R-03) would point at an operator's
        /// account instead, which is why this is a plain foreign key to
        /// Users rather than "the owning entity's account".
        /// </summary>
        public Guid ChangedByUserId { get; set; }
        public User ChangedByUser { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
