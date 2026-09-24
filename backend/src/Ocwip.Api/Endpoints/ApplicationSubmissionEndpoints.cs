using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Submitting a draft application and downloading its confirmation PDF
/// (T-33), over HTTP.
///
/// Submission carries a role policy AND a resource policy, unlike the plain
/// resource routes in ApplicationEndpoints: an operator may read every
/// application (EntityScopedHandler), but submitting is the one act this
/// product has to be able to attribute to the applicant themselves ("dowód,
/// kto i kiedy złożył wniosek", docs/runbook/M4-wnioski.md), so an operator
/// hitting this route is refused before the row is even loaded, the same way
/// starting a draft is refused to anyone but an Applicant
/// (Endpoints/ApplicationEndpoints.cs). The confirmation PDF carries no such
/// restriction: it is a read, so it follows the plain load, ask, answer order
/// every other scoped resource in this product uses.
/// </summary>
public static class ApplicationSubmissionEndpoints
{
    internal const string Unavailable =
        "Obsługa wniosków jest chwilowo niedostępna.";

    internal const string NotFound = "Nie ma takiego wniosku.";

    internal const string Forbidden = "Nie masz dostępu do tego wniosku.";

    internal const string AccountNotFound =
        "Twoja sesja jest nieprawidłowa, zaloguj się ponownie.";

    internal const string AlreadySubmitted =
        "Ten wniosek został już złożony.";

    internal const string Inactive =
        "Ten wniosek został usunięty przez wnioskodawcę, więc nie można go "
        + "już złożyć.";

    internal const string NotSubmitted =
        "Ten wniosek nie został jeszcze złożony, więc nie ma czego "
        + "potwierdzać.";

    public static void MapApplicationSubmissionEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);

        app.MapPost("/applications/{id:guid}/submit",
            async Task<Results<Ok<ApplicationResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid id,
            [FromServices] IApplicationSubmissionService? submissions,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (submissions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                context, authorization, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            var result = await submissions.SubmitAsync(
                id, context.User, cancellationToken);

            if (result.Outcome is ApplicationSubmissionOutcome.AnswersRejected)
            {
                return TypedResults.ValidationProblem(result.Errors!);
            }

            return result.Outcome is ApplicationSubmissionOutcome.Succeeded
                ? TypedResults.Ok(result.Application!)
                : Failure(result.Outcome, result.Message);
        })
            .WithName("SubmitApplication")
            .WithSummary(
                "Submits a draft application: validates it at the "
                + "submission level (T-30), checks the intake window (T-21), "
                + "assigns the application number, freezes the answers, "
                + "writes a status history entry and sends the confirmation "
                + "e-mail. Irreversible.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/applications/{id:guid}/confirmation",
            async Task<Results<FileContentHttpResult, ProblemHttpResult>> (
            Guid id,
            [FromServices] IApplicationSubmissionService? submissions,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (submissions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                context, authorization, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            var result = await submissions.GetConfirmationPdfAsync(
                id, cancellationToken);

            if (result.Outcome is not ApplicationSubmissionOutcome.Succeeded)
            {
                return Failure(result.Outcome, message: null);
            }

            return TypedResults.File(
                result.Content!, "application/pdf", result.FileName);
        })
            .WithName("DownloadApplicationConfirmation")
            .WithSummary(
                "The confirmation PDF for a submitted application: number, "
                + "competition, form version, submission timestamp and "
                + "checksum. Same permission check as the application "
                + "itself.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }

    /// <summary>
    /// Load, then ask, then answer, the same order ApplicationEndpoints and
    /// AttachmentEndpoints use: a row that is not there is 404, a row that is
    /// there and is not the caller's is 403. Goes through IApplicationService
    /// rather than IApplicationSubmissionService for the lookup, because it is
    /// the exact same "bare resource for an authorization check" IApplicationService
    /// already exposes (FindForAuthorizationAsync), and repeating that query
    /// in a second service would be the same row read twice for no reason.
    /// </summary>
    private static async Task<ProblemHttpResult?> AuthorizeAsync(
        HttpContext context,
        IAuthorizationService authorization,
        Guid id,
        CancellationToken cancellationToken)
    {
        var applications = context.RequestServices
            .GetRequiredService<IApplicationService>();

        var resource = await applications.FindForAuthorizationAsync(
            id, cancellationToken);

        if (resource is null)
        {
            return TypedResults.Problem(NotFound, statusCode: 404);
        }

        var authorized = await authorization.AuthorizeAsync(
            context.User, resource, AuthorizationConfiguration.Names.OwnsResource);

        return authorized.Succeeded
            ? null
            : TypedResults.Problem(Forbidden, statusCode: 403);
    }

    private static ProblemHttpResult Failure(
        ApplicationSubmissionOutcome outcome, string? message) =>
        outcome switch
        {
            ApplicationSubmissionOutcome.NotFound =>
                TypedResults.Problem(NotFound, statusCode: 404),

            ApplicationSubmissionOutcome.AccountNotFound =>
                TypedResults.Problem(AccountNotFound, statusCode: 401),

            ApplicationSubmissionOutcome.AlreadySubmitted =>
                TypedResults.Problem(AlreadySubmitted, statusCode: 409),

            ApplicationSubmissionOutcome.Inactive =>
                TypedResults.Problem(Inactive, statusCode: 409),

            // The message names the moment that decided it (D12), so it is
            // carried on the result rather than one fixed string here.
            ApplicationSubmissionOutcome.IntakeClosed =>
                TypedResults.Problem(message!, statusCode: 409),

            ApplicationSubmissionOutcome.NotSubmitted =>
                TypedResults.Problem(NotSubmitted, statusCode: 409),

            // Succeeded never reaches here, and AnswersRejected is handled by
            // its own ValidationProblem branch at the call site: a new
            // outcome should arrive as a visible 500 rather than as a
            // silently successful answer.
            _ => throw new InvalidOperationException(
                $"Unhandled application submission outcome: {outcome}"),
        };
}
