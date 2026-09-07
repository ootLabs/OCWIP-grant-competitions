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
        string? storagePath = null,
        long sizeInBytes = 1024) =>
        new()
        {
            ApplicationId = applicationId,
            FileName = "statut.pdf",
            ContentType = "application/pdf",
            SizeInBytes = sizeInBytes,
            StoragePath = storagePath ?? $"applications/{Guid.NewGuid():N}",
        };
}
