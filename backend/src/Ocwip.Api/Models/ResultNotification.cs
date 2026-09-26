namespace Ocwip.Api.Models
{
    /// <summary>
    /// One result mail to send (T-43), written in the same transaction that
    /// approves the results: the list of who is owed a mail exists as soon as
    /// the results do, so a sending run that stops halfway is resumed from
    /// the rows still unsent, and nobody gets a second "you were funded".
    ///
    /// No address is stored here: the recipient is read at sending time from
    /// the account that submitted the application, so this table copies no
    /// personal data.
    /// </summary>
    public class ResultNotification : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid CompetitionId { get; set; }

        /// <summary>Unique: one result mail per application, ever.</summary>
        public Guid ApplicationId { get; set; }

        /// <summary>The result the mail announces, as approved (Funded, Reserve or Rejected).</summary>
        public ApplicationStatus Result { get; set; }

        /// <summary>
        /// When a sending run took this row. A run takes a row only when it
        /// is unclaimed or the claim is stale, so two runs at once do not
        /// both send it.
        /// </summary>
        public DateTimeOffset? ClaimedAt { get; set; }

        /// <summary>When the mail was handed to the sender; null while owed.</summary>
        public DateTimeOffset? SentAt { get; set; }

        public int Attempts { get; set; }

        /// <summary>Why the last attempt failed, for the operator; never a mail body.</summary>
        public string? LastError { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
