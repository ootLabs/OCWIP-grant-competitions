using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// What the operator reads about the applications that reached a
/// competition (T-35): the list, one submitted offer, and the list as a
/// spreadsheet or a PDF. Reading only; nothing here changes an application.
/// </summary>
internal interface IApplicationListService
{
    /// <summary>Null when there is no such competition.</summary>
    Task<ApplicationListResponse?> ListAsync(
        Guid competitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Null when the application does not exist, belongs to another
    /// competition, is still a draft or was deactivated: none of those is a
    /// submitted offer of this competition, and telling them apart would
    /// only show an operator what an applicant has been drafting.
    /// </summary>
    Task<SubmittedApplicationResponse?> GetSubmittedAsync(
        Guid competitionId, Guid applicationId, CancellationToken cancellationToken);
}
