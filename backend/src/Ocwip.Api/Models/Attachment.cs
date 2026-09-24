using Ocwip.Api.Authorization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// Metadata of a file attached to an application, plus where the bytes
    /// live. Uploading, size limits, the format allow list and permission
    /// checked downloads are card T-32.
    ///
    /// <see cref="IEntityScoped"/> is implemented directly here, not read
    /// through <see cref="Application"/>, exactly as
    /// Authorization/IEntityScoped.cs said this row would: the download
    /// endpoint (T-32) needs "whose is this" from the attachment alone,
    /// without a join, the same one query per resource shape every other
    /// scoped endpoint follows.
    /// </summary>
    public class Attachment : IAuditedEntity, IEntityScoped
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        /// <summary>
        /// Copied from Application.EntityId at upload time, not read through
        /// the navigation property. The application it belongs to never
        /// changes once created, so the copy cannot drift, and it is the
        /// column ResourceOwnership.cs and EntityScopedHandler.cs read.
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// The name the applicant uploaded the file under, kept so the operator
        /// downloads something recognisable. Never used to build a path: a name
        /// coming from outside is not a safe path component.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Declared MIME type, kept as metadata only. Declared, not verified:
        /// the format allow list check (T-32, AttachmentFormatDetector) reads
        /// the file's own byte signature, never this column, and a download
        /// answers with the signature's canonical content type, not this one.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// The format AttachmentFormatDetector actually found in the bytes,
        /// one of the eight the competition allows. Stored so a download does
        /// not have to re-open the file to answer its own Content-Type
        /// header, and so a future reader never has to trust ContentType for
        /// anything that matters.
        /// </summary>
        public AllowedFileFormat Format { get; set; }

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
