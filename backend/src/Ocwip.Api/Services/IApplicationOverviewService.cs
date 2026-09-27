using System.Security.Claims;
using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// How a request for "Moje wnioski" ended. One value per thing the caller
/// can do about it, the same shape as <see cref="ApplicationOutcome"/> in
/// IApplicationService.cs, kept separate because this read does not touch a
/// single resource an authorization policy could load first: it decides its
/// own scope from the caller's Podmiot.
/// </summary>
internal enum ApplicationOverviewOutcome
{
    /// <summary>Also for an account with no Podmiot yet, whose list is empty (T-93).</summary>
    Succeeded,
}

internal sealed record ApplicationOverviewResult(
    ApplicationOverviewOutcome Outcome,
    IReadOnlyList<ApplicationOverviewResponse>? Applications = null);

/// <summary>
/// The caller's own applications across every competition (T-34), draft and
/// submitted alike, for the "Moje wnioski" screen. Reading one application in
/// full, saving it or submitting it stays with IApplicationService and
/// IApplicationSubmissionService: this interface only answers "which ones are
/// mine".
/// </summary>
internal interface IApplicationOverviewService
{
    Task<ApplicationOverviewResult> ListForCallerAsync(
        ClaimsPrincipal caller, CancellationToken cancellationToken);
}
