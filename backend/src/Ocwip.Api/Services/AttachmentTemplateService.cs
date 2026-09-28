using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

internal enum AttachmentTemplateOutcome
{
    Succeeded,
    NotFound,
    EmptyFile,
    FileTooLarge,
    UnsupportedFormat,
}

internal sealed record AttachmentTemplateResult(
    AttachmentTemplateOutcome Outcome,
    AttachmentTemplateResponse? Template = null,
    string? Message = null);

internal interface IAttachmentTemplateService
{
    Task<AttachmentTemplateResult> UploadAsync(Guid requirementId, string fileName, Stream content, CancellationToken cancellationToken);

    Task<AttachmentTemplateResult> WithdrawAsync(Guid requirementId, CancellationToken cancellationToken);

    Task<AttachmentDownload?> DownloadPublicAsync(Guid requirementId, CancellationToken cancellationToken);
}

/// <summary>
/// Templates of attachment requirements (T-102, R-30). The operator uploads
/// a file to a requirement, replaces it, withdraws it; anybody downloads the
/// one in force from a public competition, without signing in.
///
/// Through the same storage (IAttachmentStorage) and the same rules as an
/// applicant's attachment (T-32): the format is decided by the bytes, never by
/// the declared type or the name alone, and the limit is the product's
/// 10 MB for one file. Replacing and withdrawing only mark rows inactive; no
/// bytes are removed.
/// </summary>
internal sealed class AttachmentTemplateService(AppDbContext context, IAttachmentStorage storage, TimeProvider time)
    : IAttachmentTemplateService
{
    public const long MaxSizeInBytes = Competition.DefaultMaxAttachmentSizeInBytes;

    public async Task<AttachmentTemplateResult> UploadAsync(
        Guid requirementId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (!await RequirementExistsAsync(requirementId, cancellationToken))
        {
            return new AttachmentTemplateResult(AttachmentTemplateOutcome.NotFound);
        }

        var (refusal, buffer, format) = await ReadAsync(content, fileName, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }

        var path = await storage.SaveAsync(buffer!, cancellationToken);
        var template = new AttachmentTemplate
        {
            Id = Guid.NewGuid(),
            CompetitionAttachmentId = requirementId,
            FileName = AttachmentService.SafeFileName(fileName),
            Format = format,
            SizeInBytes = buffer!.Length,
            StoragePath = path,
        };

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await DeactivateAsync(requirementId, cancellationToken);
        context.AttachmentTemplates.Add(template);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two replacements in the same moment: the other one is in force.
            return new AttachmentTemplateResult(AttachmentTemplateOutcome.Succeeded, Response(template));
        }

        await transaction.CommitAsync(cancellationToken);
        return new AttachmentTemplateResult(AttachmentTemplateOutcome.Succeeded, Response(template));
    }

    public async Task<AttachmentTemplateResult> WithdrawAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        if (!await RequirementExistsAsync(requirementId, cancellationToken))
        {
            return new AttachmentTemplateResult(AttachmentTemplateOutcome.NotFound);
        }

        await DeactivateAsync(requirementId, cancellationToken);
        return new AttachmentTemplateResult(AttachmentTemplateOutcome.Succeeded);
    }

    /// <summary>The template in force, only while its competition is public and the requirement on its list.</summary>
    public async Task<AttachmentDownload?> DownloadPublicAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var template = await context.AttachmentTemplates.AsNoTracking()
            .Where(x => x.CompetitionAttachmentId == requirementId && x.IsActive
                && x.CompetitionAttachment.IsActive && x.CompetitionAttachment.Competition.IsActive)
            .Select(x => new { x.StoragePath, x.FileName, x.Format, x.CompetitionAttachment.Competition.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (template is null || !CompetitionLifecycle.IsPubliclyVisible(template.Status))
        {
            return null;
        }

        return new AttachmentDownload(
            await storage.OpenReadAsync(template.StoragePath, cancellationToken),
            template.FileName,
            AttachmentFormatDetector.CanonicalContentType(template.Format));
    }

    private Task<bool> RequirementExistsAsync(Guid requirementId, CancellationToken cancellationToken) =>
        context.CompetitionAttachments.AnyAsync(
            x => x.Id == requirementId && x.IsActive && x.Competition.IsActive, cancellationToken);

    private Task<int> DeactivateAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return context.AttachmentTemplates
            .Where(x => x.CompetitionAttachmentId == requirementId && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.DeactivatedAt, now), cancellationToken);
    }

    /// <summary>Up to one byte past the limit, then the format from the first bytes, as for an attachment.</summary>
    private static async Task<(AttachmentTemplateResult? Refusal, MemoryStream? Buffer, AllowedFileFormat Format)> ReadAsync(
        Stream content, string fileName, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxSizeInBytes)
            {
                return (new AttachmentTemplateResult(
                    AttachmentTemplateOutcome.FileTooLarge,
                    Message: $"Plik przekracza dopuszczalny rozmiar {MaxSizeInBytes / (1024 * 1024)} MB."), null, default);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            return (new AttachmentTemplateResult(AttachmentTemplateOutcome.EmptyFile), null, default);
        }

        buffer.Position = 0;
        var header = new byte[Math.Min(AttachmentFormatDetector.RequiredHeaderBytes, buffer.Length)];
        _ = await buffer.ReadAsync(header, cancellationToken);
        buffer.Position = 0;

        return AttachmentFormatDetector.TryDetect(header, fileName, out var format)
            ? (null, buffer, format)
            : (new AttachmentTemplateResult(AttachmentTemplateOutcome.UnsupportedFormat), null, default);
    }

    private static AttachmentTemplateResponse Response(AttachmentTemplate x) => new(x.FileName, x.Format, x.SizeInBytes);
}
