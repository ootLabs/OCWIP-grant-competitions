using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An attachment that satisfies every constraint. The storage path is a random
/// opaque value, both because the column is unique and because a guessable path
/// is the thing T-32 must not ship.
/// </summary>
internal static class TestAttachment
{
    public static Attachment New(
        Guid applicationId,
        Guid entityId,
        string? storagePath = null,
        long sizeInBytes = 1024) =>
        new()
        {
            ApplicationId = applicationId,
            EntityId = entityId,
            FileName = "statut.pdf",
            ContentType = "application/pdf",
            Format = AllowedFileFormat.Pdf,
            SizeInBytes = sizeInBytes,
            StoragePath = storagePath ?? $"applications/{Guid.NewGuid():N}",
        };
}
