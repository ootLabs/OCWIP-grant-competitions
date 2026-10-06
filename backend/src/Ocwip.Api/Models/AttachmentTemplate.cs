namespace Ocwip.Api.Models
{
    /// <summary>
    /// A file the organiser gives out with an attachment requirement (T-102,
    /// R-30): "wzór oświadczenia", "wzór harmonogramu", to download, fill in
    /// and upload back as the attachment. Public, like the competition page it
    /// is linked from, so it never holds personal data.
    ///
    /// One active per requirement. Replacing uploads a new row and makes the
    /// old one inactive; withdrawing only makes it inactive. The bytes stay
    /// in storage either way, the same rule as an applicant's attachment
    /// (T-32): nothing is removed.
    /// </summary>
    public class AttachmentTemplate : IRetainedRow
    {
        public Guid Id { get; set; }

        public Guid CompetitionAttachmentId { get; set; }
        public CompetitionAttachment CompetitionAttachment { get; set; } = null!;

        /// <summary>The name the file is downloaded under, cleaned like an attachment's.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Decided from the bytes, never from the declared type (T-32).</summary>
        public AllowedFileFormat Format { get; set; }

        public long SizeInBytes { get; set; }

        /// <summary>
        /// Where IAttachmentStorage put it; never shown, never guessable. The
        /// same volume and the same encryption as an applicant's attachment
        /// (S-38), so reencrypt-data has to rewrite these files too.
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTimeOffset? DeactivatedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
