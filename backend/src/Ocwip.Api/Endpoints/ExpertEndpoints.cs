using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Experts;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// A competition's committee (R-44, report step 5.1): who is appointed,
/// appointing by address or inviting, and withdrawing. Operator only.
/// </summary>
public static class ExpertEndpoints
{
    internal const string CompetitionNotFound = "Nie ma takiego konkursu.";
    internal const string InvalidAddress = "Podaj poprawny adres e-mail.";
    internal const string NameRequired =
        "Nie ma konta z tym adresem. Podaj imię i nazwisko, a wyślemy zaproszenie do komisji.";
    internal const string NotAllowed =
        "Tego konta nie można powołać do komisji: to konto operatora albo konto wyłączone.";
    internal const string NotAppointed = "Ta osoba nie jest powołana do komisji tego konkursu.";
    internal const string StillAssigned =
        "Ta osoba ma przypisane wnioski w tym konkursie. Najpierw cofnij przydziały.";

    public static void MapExpertEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/competitions/{id:guid}/experts",
            async Task<Results<Ok<IReadOnlyList<CompetitionExpertResponse>>, ProblemHttpResult>> (
            Guid id,
            [FromServices] IExpertAppointmentService? experts,
            CancellationToken cancellationToken) =>
        {
            if (experts is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            return await experts.ListAsync(id, cancellationToken) is { } list
                ? TypedResults.Ok(list)
                : TypedResults.Problem(CompetitionNotFound, statusCode: 404);
        })
            .WithName("ListCompetitionExperts")
            .WithSummary("The committee of a competition: appointed experts with their assignments.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competitions/{id:guid}/experts",
            async Task<Results<Ok<CompetitionExpertResponse>, Created<CompetitionExpertResponse>, ProblemHttpResult>> (
            Guid id,
            AppointExpertRequest body,
            [FromServices] IExpertAppointmentService? experts,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (experts is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await experts.AppointAsync(context.User, id, body, cancellationToken);
            return result.Outcome switch
            {
                ExpertOutcome.Appointed => TypedResults.Ok(result.Expert!),
                ExpertOutcome.Invited => TypedResults.Created($"/competitions/{id}/experts", result.Expert!),
                ExpertOutcome.CompetitionNotFound => TypedResults.Problem(CompetitionNotFound, statusCode: 404),
                ExpertOutcome.NameRequired => TypedResults.Problem(NameRequired, statusCode: 404),
                ExpertOutcome.NotAllowed => TypedResults.Problem(NotAllowed, statusCode: 409),
                _ => TypedResults.Problem(InvalidAddress, statusCode: 400),
            };
        })
            .WithName("AppointCompetitionExpert")
            .WithSummary(
                "Appoints the account with this address to the committee; with a name and no "
                + "account, invites the person (201). Appointing twice changes nothing.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapDelete("/competitions/{id:guid}/experts/{userId:guid}",
            async Task<Results<NoContent, ProblemHttpResult>> (
            Guid id,
            Guid userId,
            [FromServices] IExpertAppointmentService? experts,
            CancellationToken cancellationToken) =>
        {
            if (experts is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await experts.WithdrawAsync(id, userId, cancellationToken);
            return result.Outcome switch
            {
                ExpertOutcome.Appointed => TypedResults.NoContent(),
                ExpertOutcome.StillAssigned => TypedResults.Problem(StillAssigned, statusCode: 409),
                _ => TypedResults.Problem(NotAppointed, statusCode: 404),
            };
        })
            .WithName("WithdrawCompetitionExpert")
            .WithSummary("Withdraws an appointment; refused while applications are assigned to the person.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }
}
