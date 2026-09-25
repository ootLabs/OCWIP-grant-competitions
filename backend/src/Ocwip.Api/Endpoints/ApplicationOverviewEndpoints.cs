using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// "Moje wnioski" (T-34), over HTTP: every application the caller's own
/// Podmiot has started or submitted, across every competition. Applicant
/// only, by role policy: the scope is decided from the caller's Podmiot, not
/// from an id in the route, so there is no per-resource check to run.
/// </summary>
public static class ApplicationOverviewEndpoints
{
    internal const string Unavailable =
        "Obsługa wniosków jest chwilowo niedostępna.";

    public static void MapApplicationOverviewEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);

        app.MapGet("/applications",
            async Task<Results<Ok<IReadOnlyList<ApplicationOverviewResponse>>, ProblemHttpResult>> (
            [FromServices] IApplicationOverviewService? overview,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (overview is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await overview.ListForCallerAsync(context.User, cancellationToken);

            return result.Outcome is ApplicationOverviewOutcome.Succeeded
                ? TypedResults.Ok(result.Applications!)
                : TypedResults.Problem(ApplicationEndpoints.NoEntity, statusCode: 403);
        })
            .WithName("ListMyApplications")
            .WithSummary(
                "Every application the caller's own Podmiot has started or "
                + "submitted, draft and submitted alike, newest first.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);
    }
}
