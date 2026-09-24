using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Uploading, replacing and downloading application attachments (T-32).
///
/// Every write goes through the same three checks CreateDraftAsync and
/// SaveDraftAsync make on the application itself (T-21's intake rule, draft
/// status, IsActive), because an attachment is part of the application it
/// belongs to and cannot outlive a rule the answers themselves obey. See
/// CompetitionIntake.cs, which names attachment upload as one of the three
/// places this comparison must not be written twice.
/// </summary>
internal sealed class AttachmentService : IAttachmentService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _time;
    private readonly IAttachmentStorage _storage;

    public AttachmentService(
        AppDbContext context, TimeProvider time, IAttachmentStorage storage)
    {
        _context = context;
        _time = time;
        _storage = storage;
    }

    public async Task<AttachmentResult> UploadAsync(
        Guid applicationId,
        string fileName,
        string declaredContentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .Include(x => x.Competition)
            .SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return new AttachmentResult(AttachmentOutcome.ApplicationNotFound);
        }

        var refusal = Refuse(application);

        if (refusal is not null)
        {
            return refusal;
        }

        var existingTotal = await _context.Attachments
            .Where(x => x.ApplicationId == applicationId && x.IsActive)
            .SumAsync(x => x.SizeInBytes, cancellationToken);

        var staged = await StageAsync(
            content, application.Competition, existingTotal, fileName, cancellationToken);

        if (staged.Result is not null)
        {
            return staged.Result;
        }

        var storagePath = await _storage.SaveAsync(staged.Buffer!, cancellationToken);

        var attachment = new Attachment
        {
            ApplicationId = applicationId,
            EntityId = application.EntityId,
            FileName = SafeFileName(fileName),
            ContentType = declaredContentType,
            Format = staged.Format,
            SizeInBytes = staged.Buffer!.Length,
            StoragePath = storagePath,
        };

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);

        return new AttachmentResult(AttachmentOutcome.Succeeded, ToResponse(attachment));
    }

    public async Task<AttachmentResult> ReplaceAsync(
        Guid attachmentId,
        string fileName,
        string declaredContentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Attachments
            .Include(x => x.Application)
                .ThenInclude(x => x.Competition)
            .SingleOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);

        if (existing is null)
        {
            return new AttachmentResult(AttachmentOutcome.NotFound);
        }

        var application = existing.Application;
        var refusal = Refuse(application);

        if (refusal is not null)
        {
            return refusal;
        }

        // The row being replaced is still active and still counts, so it is
        // excluded from the running total: it is about to stop being active
        // in the very same write, not stack on top of its own replacement.
        var existingTotal = await _context.Attachments
            .Where(x =>
                x.ApplicationId == existing.ApplicationId
                && x.IsActive
                && x.Id != attachmentId)
            .SumAsync(x => x.SizeInBytes, cancellationToken);

        var staged = await StageAsync(
            content, application.Competition, existingTotal, fileName, cancellationToken);

        if (staged.Result is not null)
        {
            return staged.Result;
        }

        var storagePath = await _storage.SaveAsync(staged.Buffer!, cancellationToken);

        var replacement = new Attachment
        {
            ApplicationId = existing.ApplicationId,
            EntityId = existing.EntityId,
            FileName = SafeFileName(fileName),
            ContentType = declaredContentType,
            Format = staged.Format,
            SizeInBytes = staged.Buffer!.Length,
            StoragePath = storagePath,
        };

        // Never deleted, never touched again: the old row keeps its own
        // bytes on disk exactly as they were (AGENTS.md rule 5, and the
        // card's "poprzedni nie znika twardo").
        existing.IsActive = false;
        existing.DeactivatedAt = _time.GetUtcNow();

        _context.Attachments.Add(replacement);
        await _context.SaveChangesAsync(cancellationToken);

        return new AttachmentResult(AttachmentOutcome.Succeeded, ToResponse(replacement));
    }

    public async Task<AttachmentDownload?> DownloadAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var attachment = await _context.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        var stream = await _storage.OpenReadAsync(attachment.StoragePath, cancellationToken);

        return new AttachmentDownload(
            stream,
            attachment.FileName,
            AttachmentFormatDetector.CanonicalContentType(attachment.Format));
    }

    public Task<Attachment?> FindForAuthorizationAsync(
        Guid id, CancellationToken cancellationToken) =>
        _context.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <summary>
    /// The three checks upload and replace share, ahead of anything specific
    /// to either one: intake closed, no longer a draft, deactivated by its own
    /// applicant. Null means neither happened and the caller may proceed.
    /// </summary>
    private AttachmentResult? Refuse(Application application)
    {
        if (application.Status is not ApplicationStatus.Draft)
        {
            return new AttachmentResult(AttachmentOutcome.AlreadySubmitted);
        }

        if (!application.IsActive)
        {
            return new AttachmentResult(AttachmentOutcome.Inactive);
        }

        var intake = CompetitionIntake.For(application.Competition, _time.GetUtcNow());

        return intake.AcceptsApplications
            ? null
            : new AttachmentResult(
                AttachmentOutcome.IntakeClosed,
                Message: CompetitionIntakeMessage.For(intake));
    }

    /// <summary>
    /// Reads the upload into memory up to one byte past the competition's
    /// per-file limit, so an oversized file is caught by what was actually
    /// read rather than by a Content-Length header the client is free to
    /// lie about, checks it is not empty, decides its real format from the
    /// bytes (never from the declared content type), and checks the running
    /// total for the application. A non-null Result on the way out means
    /// refuse and stop; the buffer and format are only meaningful when it is
    /// null.
    /// </summary>
    private static async Task<(
        AttachmentResult? Result,
        MemoryStream? Buffer,
        AllowedFileFormat Format)> StageAsync(
        Stream content,
        Competition competition,
        long existingTotal,
        string fileName,
        CancellationToken cancellationToken)
    {
        // Read in chunks and stop as soon as the running total is over the
        // limit, rather than buffering the whole body first: a forged
        // Content-Length is not something the client has to get right for
        // this check to hold, and memory used here is bounded by the limit
        // plus one chunk, not by whatever the request claims to be.
        var limit = competition.MaxAttachmentSizeInBytes;
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;

            if (total > limit)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            return (new AttachmentResult(AttachmentOutcome.EmptyFile), null, default);
        }

        if (total > limit)
        {
            return (
                new AttachmentResult(
                    AttachmentOutcome.FileTooLarge,
                    Message: SizeMessage(limit)),
                null,
                default);
        }

        if (existingTotal + buffer.Length > competition.MaxApplicationSizeInBytes)
        {
            return (
                new AttachmentResult(
                    AttachmentOutcome.ApplicationTooLarge,
                    Message: SizeMessage(competition.MaxApplicationSizeInBytes)),
                null,
                default);
        }

        buffer.Position = 0;
        var header = new byte[Math.Min(AttachmentFormatDetector.RequiredHeaderBytes, buffer.Length)];
        _ = await buffer.ReadAsync(header, cancellationToken);
        buffer.Position = 0;

        if (!AttachmentFormatDetector.TryDetect(header, fileName, out var format))
        {
            return (new AttachmentResult(AttachmentOutcome.UnsupportedFormat), null, default);
        }

        return (null, buffer, format);
    }

    private static string SizeMessage(long limitInBytes) =>
        $"Plik przekracza dopuszczalny rozmiar {limitInBytes / (1024 * 1024)} MB.";

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);

        return name.Length > 255 ? name[..255] : name;
    }

    private static AttachmentResponse ToResponse(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.ApplicationId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeInBytes,
            attachment.CreatedAt);
}
