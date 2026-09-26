using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The result mails (T-43), operator only: their text per competition, where
/// the sending stands, and the sending itself, safe to run again.
/// </summary>
public static class ResultNotificationEndpoints
{
    public static void MapResultNotificationEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/competitions/{competitionId:guid}/result-messages",
            async Task<Results<Ok<ResultMessagesResponse>, ProblemHttpResult>> (
            Guid competitionId, [FromServices] IResultNotificationService? mails, CancellationToken cancellationToken) =>
        {
            if (mails is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            return await mails.MessagesAsync(competitionId, cancellationToken) is { } messages
                ? TypedResults.Ok(messages)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("GetResultMessages")
            .WithSummary("The three result mails of a competition as OCWIP wrote them, null for the default text.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPut("/competitions/{competitionId:guid}/result-messages",
            async Task<Results<Ok<ResultMessagesResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid competitionId,
            ResultMessagesRequest request,
            [FromServices] IResultNotificationService? mails,
            CancellationToken cancellationToken) =>
        {
            if (mails is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var (messages, errors) = await mails.SaveMessagesAsync(competitionId, request, cancellationToken);
            return errors is not null ? TypedResults.ValidationProblem(errors)
                : messages is not null ? TypedResults.Ok(messages)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("SaveResultMessages")
            .WithSummary("Saves the three result mails of a competition.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/result-notifications",
            async Task<Results<Ok<ResultNotificationsResponse>, ProblemHttpResult>> (
            Guid competitionId, [FromServices] IResultNotificationService? mails, CancellationToken cancellationToken) =>
        {
            if (mails is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            return await mails.StateAsync(competitionId, cancellationToken) is { } state
                ? TypedResults.Ok(state)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("GetResultNotifications")
            .WithSummary("How many result mails are owed, sent and failed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competitions/{competitionId:guid}/result-notifications/send",
            async Task<Results<Ok<ResultNotificationsResponse>, ProblemHttpResult>> (
            Guid competitionId, [FromServices] IResultNotificationService? mails, CancellationToken cancellationToken) =>
        {
            if (mails is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            return await mails.SendPendingAsync(competitionId, cancellationToken) is { } state
                ? TypedResults.Ok(state)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("SendResultNotifications")
            .WithSummary("Sends every result mail still owed; safe to run again, a sent mail is never sent twice.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);
    }
}
