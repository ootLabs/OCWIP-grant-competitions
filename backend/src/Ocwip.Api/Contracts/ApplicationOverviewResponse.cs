using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One of the caller's own applications, draft or submitted, as it appears
/// on "Moje wnioski" (T-34). Deliberately lighter than
/// <see cref="ApplicationResponse"/>: the list is what the applicant picks
/// an application from, not where they read its answers.
/// </summary>
/// <param name="LastSavedAt">
/// The application's own <c>UpdatedAt</c>, same meaning as on
/// <see cref="ApplicationResponse"/>.
/// </param>
public sealed record ApplicationOverviewResponse(
    Guid Id,
    Guid CompetitionId,
    string CompetitionNumber,
    string CompetitionTitle,
    ApplicationStatus Status,
    string? Number,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset LastSavedAt);
