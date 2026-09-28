namespace Ocwip.Api.Models
{
    /// <summary>
    /// One "zwrot do poprawy" (T-103, R-03, RD10): the operator sends a
    /// submitted application back with the sections the applicant may change,
    /// what to correct and by when. Open until the applicant submits again
    /// (<see cref="ResolvedAt"/>); at most one open return per application,
    /// which the Returned status and a filtered unique index both hold.
    ///
    /// Kept after it is resolved, like every row here: it is the record of
    /// what OCWIP asked for and when.
    /// </summary>
    public class ApplicationReturn : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        /// <summary>The keys of the form sections unlocked for the correction, in form order.</summary>
        public List<string> Sections { get; set; } = [];

        /// <summary>Whether the applicant may also add or replace attachments.</summary>
        public bool UnlocksAttachments { get; set; }

        /// <summary>What to correct, in the operator's words; goes into the mail as it is.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The last moment a correction is accepted, a whole minute in UTC,
        /// also after the intake has closed (CompetitionIntake.ForCorrection).
        /// </summary>
        public DateTimeOffset Deadline { get; set; }

        public Guid ReturnedByUserId { get; set; }

        public DateTimeOffset ReturnedAt { get; set; }

        /// <summary>When the applicant submitted the correction. Null while the return is open.</summary>
        public DateTimeOffset? ResolvedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
