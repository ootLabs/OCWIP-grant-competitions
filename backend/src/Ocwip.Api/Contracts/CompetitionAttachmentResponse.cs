using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One attachment a competition asks for, as everybody reads it. Public too:
/// an applicant has to know what to prepare before they log in, and none of
/// these fields is anybody's personal data.
/// </summary>
public sealed record CompetitionAttachmentResponse(
    Guid Id,
    string Title,
    string? Description,
    AttachmentRequirement Requirement,
    IReadOnlyList<AllowedFileFormat> AllowedFormats,
    AttachmentTemplateResponse? Template = null);

/// <summary>
/// The template in force of a requirement (T-102): what the download link on
/// the competition page says. Downloaded anonymously from
/// GET /public/attachment-templates/{requirementId} once the competition is public.
/// </summary>
public sealed record AttachmentTemplateResponse(string FileName, AllowedFileFormat Format, long SizeInBytes);
