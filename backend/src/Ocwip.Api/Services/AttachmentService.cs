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
        Guid? requirementId,
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

        var refusal = await RefuseAsync(application, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        // A requirement of THIS competition, still on its list: an id from
        // another competition is refused, so no file can answer a
        // requirement its application was never asked (T-101).
        CompetitionAttachment? requirement = null;
        if (requirementId is { } id)
        {
            requirement = await _context.CompetitionAttachments.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == id && x.CompetitionId == application.CompetitionId && x.IsActive, cancellationToken);
            if (requirement is null)
            {
                return new AttachmentResult(AttachmentOutcome.UnknownRequirement);
            }
        }

        var existingTotal = await _context.Attachments
            .Where(x => x.ApplicationId == applicationId && x.IsActive)
            .SumAsync(x => x.SizeInBytes, cancellationToken);

        var staged = await StageAsync(
            content, application.Competition, existingTotal, fileName, cancellationToken);

        if (staged.Result is not null)
        {
            // A file of no known format at all, uploaded against a known
            // requirement: say what THAT requirement takes rather than every
            // format the system accepts.
            return requirement is not null && staged.Result.Outcome is AttachmentOutcome.UnsupportedFormat
                ? new AttachmentResult(
                    AttachmentOutcome.FormatNotForRequirement,
                    Message: RequirementFormats(requirement))
                : staged.Result;
        }

        if (requirement is not null && !requirement.AllowedFormats.Contains(staged.Format))
        {
            return new AttachmentResult(
                AttachmentOutcome.FormatNotForRequirement,
                Message: RequirementFormats(requirement));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (await RecheckUnderLockAsync(application, replacing: null, staged.Buffer!.Length, cancellationToken) is { } late)
        {
            return late;
        }

        var storagePath = await _storage.SaveAsync(staged.Buffer!, cancellationToken);

        var attachment = BuildAttachment(
            applicationId, application.EntityId, fileName, declaredContentType, staged, storagePath);
        attachment.CompetitionAttachmentId = requirement?.Id;

        _context.Attachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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

        // A row a previous replace already deactivated is history, not the
        // current file: replacing it again would resurrect a row rule 5 says
        // stays exactly where that replace left it, and would let two rows
        // for the same upload end up active at once.
        if (!existing.IsActive)
        {
            return new AttachmentResult(AttachmentOutcome.AlreadyReplaced);
        }

        var application = existing.Application;
        var refusal = await RefuseAsync(application, cancellationToken);

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

        // The replacement answers the same requirement, so it has to be in a
        // format that requirement takes, like a first upload (T-101).
        var replacedRequirement = existing.CompetitionAttachmentId is { } requirementId
            ? await _context.CompetitionAttachments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == requirementId, cancellationToken)
            : null;

        if (staged.Result is not null)
        {
            return replacedRequirement is not null
                && staged.Result.Outcome is AttachmentOutcome.UnsupportedFormat
                ? new AttachmentResult(
                    AttachmentOutcome.FormatNotForRequirement,
                    Message: RequirementFormats(replacedRequirement))
                : staged.Result;
        }

        if (replacedRequirement is not null
            && !replacedRequirement.AllowedFormats.Contains(staged.Format))
        {
            return new AttachmentResult(
                AttachmentOutcome.FormatNotForRequirement,
                Message: RequirementFormats(replacedRequirement));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (await RecheckUnderLockAsync(application, replacing: attachmentId, staged.Buffer!.Length, cancellationToken) is { } late)
        {
            return late;
        }

        // Two replacements of the same file in the same moment: the second
        // finds it already replaced once it has the lock.
        await _context.Entry(existing).ReloadAsync(cancellationToken);
        if (!existing.IsActive)
        {
            return new AttachmentResult(AttachmentOutcome.AlreadyReplaced);
        }

        var storagePath = await _storage.SaveAsync(staged.Buffer!, cancellationToken);

        var replacement = BuildAttachment(
            existing.ApplicationId, existing.EntityId, fileName, declaredContentType,
            staged, storagePath);

        // Never deleted, never touched again: the old row keeps its own
        // bytes on disk exactly as they were (AGENTS.md rule 5, and the
        // card's "poprzedni nie znika twardo").
        existing.IsActive = false;
        existing.DeactivatedAt = _time.GetUtcNow();

        // The replacement answers the same requirement (T-101).
        replacement.CompetitionAttachmentId = existing.CompetitionAttachmentId;

        _context.Attachments.Add(replacement);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AttachmentResult(AttachmentOutcome.Succeeded, ToResponse(replacement));
    }

    public async Task<AttachmentDownload?> DownloadAsync(
        Guid id, CancellationToken cancellationToken)
    {
        // Only the version in force (S-22). The row and the bytes stay, the
        // retention rule says so, but a replaced file is in the product
        // nowhere: no list shows it, so whoever asks for it by id remembered
        // an identifier, and an assigned expert must not read a version the
        // applicant has withdrawn. AttachmentTemplateService.OpenAsync has
        // filtered the same way all along.
        var attachment = await _context.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);

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

    public async Task<IReadOnlyList<AttachmentResponse>> ListAsync(
        Guid applicationId, CancellationToken cancellationToken)
    {
        var attachments = await _context.Attachments
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(ToResponse).ToList();
    }

    /// <summary>
    /// The application row locked for the rest of the transaction, and what
    /// was checked on the copy read before the file was staged checked again:
    /// a submission committed while the file was being read makes the
    /// application no longer editable, and a parallel upload counts towards
    /// the total. The file is read before the lock, so the lock is held only
    /// for the checks and the write.
    /// </summary>
    private async Task<AttachmentResult?> RecheckUnderLockAsync(
        Application application, Guid? replacing, long size, CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM applications WHERE id = {application.Id} FOR UPDATE", cancellationToken);
        await _context.Entry(application).ReloadAsync(cancellationToken);

        if (await RefuseAsync(application, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var total = await _context.Attachments
            .Where(x => x.ApplicationId == application.Id && x.IsActive && (replacing == null || x.Id != replacing))
            .SumAsync(x => x.SizeInBytes, cancellationToken);

        return total + size > application.Competition.MaxApplicationSizeInBytes
            ? new AttachmentResult(
                AttachmentOutcome.ApplicationTooLarge,
                Message: SizeMessage(application.Competition.MaxApplicationSizeInBytes))
            : null;
    }

    /// <summary>
    /// The three checks upload and replace share, ahead of anything specific
    /// to either one: nothing to edit any more, deactivated by its own
    /// applicant, intake or correction window over (ApplicationEditWindow,
    /// T-103). A correction takes files only when its return unlocks them.
    /// Null means none of it happened and the caller may proceed.
    /// </summary>
    private async Task<AttachmentResult?> RefuseAsync(Application application, CancellationToken cancellationToken)
    {
        var window = await ApplicationEditWindow.ForAsync(_context, application, _time.GetUtcNow(), cancellationToken);

        if (window.State is EditWindowState.NotEditable || (window.IsCorrection && !window.UnlocksAttachments))
        {
            return new AttachmentResult(AttachmentOutcome.AlreadySubmitted);
        }

        if (!application.IsActive)
        {
            return new AttachmentResult(AttachmentOutcome.Inactive);
        }

        return window.State is EditWindowState.Closed
            ? new AttachmentResult(AttachmentOutcome.IntakeClosed, Message: window.Message)
            : null;
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

    /// <summary>
    /// What a requirement takes, named. Used both when a known format is not
    /// on the requirement's list and when the file is no known format at all:
    /// the generic sentence there named every format the SYSTEM accepts, so a
    /// PDF-only requirement invited the applicant to try DOCX or JPG next.
    /// </summary>
    private static string RequirementFormats(CompetitionAttachment requirement) =>
        $"Załącznik \"{requirement.Title}\" przyjmuje tylko: "
        + string.Join(", ", requirement.AllowedFormats.Select(format => format.ToString().ToUpperInvariant()))
        + ".";

    /// <summary>
    /// The row both UploadAsync and ReplaceAsync insert, written once so the
    /// two write paths cannot quietly drift apart on how a field is derived.
    /// </summary>
    private static Attachment BuildAttachment(
        Guid applicationId,
        Guid entityId,
        string fileName,
        string declaredContentType,
        (AttachmentResult? Result, MemoryStream? Buffer, AllowedFileFormat Format) staged,
        string storagePath) =>
        new()
        {
            ApplicationId = applicationId,
            EntityId = entityId,
            FileName = SafeFileName(fileName),
            ContentType = SafeContentType(declaredContentType),
            Format = staged.Format,
            SizeInBytes = staged.Buffer!.Length,
            StoragePath = storagePath,
        };

    private static string SizeMessage(long limitInBytes) =>
        limitInBytes < 1024 * 1024
            ? $"Plik przekracza dopuszczalny rozmiar {limitInBytes} B."
            : $"Plik przekracza dopuszczalny rozmiar {limitInBytes / (1024 * 1024)} MB.";

    internal static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);

        return name.Length > 255 ? name[..255] : name;
    }

    /// <summary>
    /// Bounded to the column width (AttachmentConfiguration.cs), the same
    /// reasoning as SafeFileName: a value this large cannot be a real MIME
    /// type and would otherwise fail SaveChangesAsync with an unhandled
    /// exception instead of the 400 every other rejection here produces.
    /// </summary>
    private static string SafeContentType(string contentType) =>
        contentType.Length > 255 ? contentType[..255] : contentType;

    private static AttachmentResponse ToResponse(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.ApplicationId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeInBytes,
            attachment.CreatedAt,
            attachment.CompetitionAttachmentId);
}
