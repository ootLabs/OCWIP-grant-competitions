namespace Ocwip.Api.Models
{
    /// <summary>
    /// Metadata of a file attached to an application, plus where the bytes live.
    ///
    /// Only metadata at this stage. Uploading, size limits, the format allow
    /// list and permission checked downloads are card T-32, so nothing here
    /// reads or writes the file itself.
    /// </summary>
    public class Attachment : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        /// <summary>
        /// The name the applicant uploaded the file under, kept so the operator
        /// downloads something recognisable. Never used to build a path: a name
        /// coming from outside is not a safe path component.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Declared MIME type. Declared, not verified: whoever accepts the
        /// upload in T-32 owns checking that the bytes match, because a client
        /// controlled content type proves nothing.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        public long SizeInBytes { get; set; }

        /// <summary>
        /// Where the stored bytes live, opaque to the applicant and unique per
        /// row.
        ///
        /// Unique because two rows pointing at one blob turn deleting a file
        /// into a way of breaking another application's attachment. Opaque
        /// because an attachment is another organisation's document: a path
        /// anyone can guess is a leak, and the download has to pass the same
        /// permission check as the application itself (T-32).
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// Soft delete flag, see the same field on Competition.
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
