using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One attachment a competition asks for, as the operator sends it (T-20a,
/// step 1.5 of the wizard).
///
/// No identifier: the list arrives whole and replaces what was stored, because
/// the wizard edits it as a list. Matching rows by an id the client keeps would
/// mean the API trusts the client to remember which row is which, and the only
/// thing hanging off these rows so far is their own content. The template file
/// (T-32) will be the first thing that changes that.
/// </summary>
public sealed record CompetitionAttachmentRequest(
    string Title,
    string? Description,
    AttachmentRequirement Requirement,
    IReadOnlyList<AllowedFileFormat> AllowedFormats);
