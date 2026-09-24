namespace Ocwip.Api.Services;

/// <summary>
/// Where the bytes of an uploaded attachment actually live (T-32), kept apart
/// from IAttachmentService so the database row and the stored file are two
/// separate concerns: one is metadata behind EF, the other is a stream behind
/// a path nobody outside this type constructs.
/// </summary>
internal interface IAttachmentStorage
{
    /// <summary>
    /// Writes the stream to a new, unguessable location and returns the
    /// opaque path that Attachment.StoragePath stores. Never derived from the
    /// original file name: a name coming from the applicant is not a safe
    /// path component (see Models/Attachment.cs).
    /// </summary>
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a previously saved path for reading. The caller owns disposing
    /// the returned stream.
    /// </summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
}
