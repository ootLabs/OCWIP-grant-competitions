namespace Ocwip.Api.Models
{
    /// <summary>
    /// One reviewer assigned to one application (T-37). Many to many on
    /// purpose, not one to one: the card's own open questions (B-02, how many
    /// reviewers score one application) are unanswered, and narrowing this
    /// relation later is cheap while widening it later is not.
    ///
    /// This is the row EntityScopedHandler reads for
    /// <see cref="Role.Reviewer"/>: a reviewer sees an application if and only
    /// if an active row here pairs their account with it. Nothing else grants
    /// that role a look at an application's answers.
    /// </summary>
    public class ApplicationAssignment : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        /// <summary>
        /// The reviewer's account. Named ReviewerId rather than UserId because
        /// only an account with <see cref="Models.Role.Reviewer"/> may occupy
        /// this column; the service enforces that, the schema does not, for
        /// the same reason no schema constraint checks a user's role anywhere
        /// else in this model.
        /// </summary>
        public Guid ReviewerId { get; set; }
        public User Reviewer { get; set; } = null!;

        /// <summary>
        /// Soft delete flag, same pattern as every other relation in this
        /// model. Revoking an assignment (the card's "cofnięcie przypisania")
        /// never removes the row: it is the proof that a reviewer once had
        /// access, and retention is at least 5 years.
        ///
        /// One row per (ApplicationId, ReviewerId) pair, unique, so assigning
        /// again after a revoke reactivates this same row rather than
        /// inserting a second one. Two rows for the same pair would leave the
        /// question "which one is current" with no answer written anywhere.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }
    }
}
