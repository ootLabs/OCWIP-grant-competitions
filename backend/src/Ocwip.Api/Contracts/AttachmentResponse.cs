namespace Ocwip.Api.Contracts;

/// <summary>
/// One attachment's metadata (T-32). Never the bytes themselves: those come
/// back from GET /attachments/{id} as a file download, not as JSON.
/// </summary>
public sealed record AttachmentResponse(
    Guid Id,
    Guid ApplicationId,
    string FileName,
    string ContentType,
    long SizeInBytes,
    DateTimeOffset CreatedAt);
