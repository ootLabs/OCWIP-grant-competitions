namespace Ocwip.Api.Models
{
    /// <summary>
    /// One attachment a competition asks applicants for (step 1.5 of the
    /// announcement wizard, docs/runbook/pola.md).
    ///
    /// A setting of the competition, not a file: the applicant's uploaded file
    /// is Attachment.cs and belongs to an application. This row says what is
    /// wanted, in what formats and how strongly.
    ///
    /// The template file the applicant downloads (a document of up to 10 MB in
    /// the report) is deliberately missing: storing files is T-32, and a column
    /// pointing at a store that does not exist yet would be a guess about that
    /// store. It arrives as a nullable foreign key on this table, which is the
    /// reason the attachments are a table and not JSON on the competition.
    /// </summary>
    public class CompetitionAttachment : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        /// <summary>What the applicant sees as the name of the attachment.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// What exactly is supposed to be attached. Optional, because a title
        /// like "CIT za 2025" carries its own explanation.
        /// </summary>
        public string? Description { get; set; }

        public AttachmentRequirement Requirement { get; set; }

        /// <summary>
        /// The formats this one attachment may be handed in as. Stored as rows
        /// of text in a PostgreSQL array column, see the configuration: the set
        /// is small, always read whole and never joined against.
        /// </summary>
        public IReadOnlyList<AllowedFileFormat> AllowedFormats { get; set; } = [];

        /// <summary>
        /// Where this attachment sits in the list the applicant is shown.
        /// Explicit, because "the order they were added in" is not a thing a
        /// database promises and the operator does reorder them.
        /// </summary>
        public int Position { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
