using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Documents;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Contract templates and contracts over HTTP (T-45). The operator publishes
/// the template, draws up, fills in, prints and records the signing; the
/// applicant reads and prints their own contract. Reading goes through the
/// resource policy on the contract itself, which grants no expert anything.
/// </summary>
public static class ContractEndpoints
{
    internal const string Unavailable = "Umowy są chwilowo niedostępne.";
    internal const string NotFound = "Nie ma takiej umowy.";
    internal const string NotYours = "Nie masz dostępu do tej umowy.";
    internal const string NoTemplate = "Konkurs nie ma jeszcze wzoru umowy.";
    internal const string NotGranted = "Umowę sporządza się tylko dla dofinansowanego wniosku.";
    internal const string Signed = "Umowa jest podpisana. Jej wartości nie można już zmieniać.";
    internal const string NothingGranted = "Konkurs nie ma jeszcze dofinansowanych wniosków.";

    public static void MapContractEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/competitions/{competitionId:guid}/contract-template",
            async Task<Results<Created<DocumentTemplateResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid competitionId, DocumentTemplateRequest request, [FromServices] IContractService? contracts,
            CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await contracts.PublishTemplateAsync(competitionId, request.Body, cancellationToken);
            return result.Outcome switch
            {
                ContractOutcome.Created => TypedResults.Created($"/competitions/{competitionId}/contract-template", result.Template!),
                ContractOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                _ => TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404),
            };
        })
            .WithName("PublishContractTemplate")
            .WithSummary("Publishes the next version of the contract template; contracts drawn up keep their version.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/contract-template",
            async Task<Results<Ok<DocumentTemplateResponse>, ProblemHttpResult>> (
            Guid competitionId, [FromServices] IContractService? contracts, CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await contracts.TemplateAsync(competitionId, cancellationToken);
            return result.Template is { } template
                ? TypedResults.Ok(template)
                : TypedResults.Problem(NoTemplate, statusCode: 404);
        })
            .WithName("GetContractTemplate")
            .WithSummary("The contract template in force, with the placeholders it asks for.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competitions/{competitionId:guid}/contracts/bundle",
            async Task<Results<FileContentHttpResult, ProblemHttpResult>> (
            Guid competitionId, [FromServices] IContractBundleService? bundles, CancellationToken cancellationToken) =>
        {
            if (bundles is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await bundles.BuildAsync(competitionId, cancellationToken);
            return result.Outcome switch
            {
                ContractBundleOutcome.Succeeded => TypedResults.File(result.Zip!, "application/zip", result.FileName),
                ContractBundleOutcome.NoTemplate => TypedResults.Problem(NoTemplate, statusCode: 409),
                ContractBundleOutcome.NothingGranted => TypedResults.Problem(NothingGranted, statusCode: 409),
                _ => TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404),
            };
        })
            .WithName("BundleContracts")
            .WithSummary("Draws up the missing contracts of every granted application and returns the complete ones as one ZIP (T-45b); "
                + "a contract with a blank is named in braki.txt instead.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/applications/{applicationId:guid}/contract",
            async Task<Results<Created<ContractResponse>, Ok<ContractResponse>, ProblemHttpResult>> (
            Guid applicationId, [FromServices] IContractService? contracts, CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await contracts.StartAsync(applicationId, cancellationToken);
            return result.Outcome switch
            {
                ContractOutcome.Created => TypedResults.Created($"/contracts/{result.Contract!.Id}", result.Contract),
                ContractOutcome.Succeeded => TypedResults.Ok(result.Contract!),
                ContractOutcome.NotGranted => TypedResults.Problem(NotGranted, statusCode: 409),
                ContractOutcome.NoTemplate => TypedResults.Problem(NoTemplate, statusCode: 409),
                _ => TypedResults.Problem(EvaluationEndpoints.ApplicationNotFound, statusCode: 404),
            };
        })
            .WithName("DrawUpContract")
            .WithSummary("Draws up the contract of a funded application on the template in force, or hands back the one there.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/applications/{applicationId:guid}/contract",
            async Task<Results<Ok<ContractResponse>, ProblemHttpResult>> (
            Guid applicationId, HttpContext context, [FromServices] IContractService? contracts,
            [FromServices] IAuthorizationService authorization, CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            // Authorized on the contract, not the application: the
            // application's policy would let an assigned expert in too.
            var contract = await contracts.FindForApplicationAsync(applicationId, cancellationToken);
            if (contract is null)
            {
                return TypedResults.Problem(NotFound, statusCode: 404);
            }

            if (!(await authorization.AuthorizeAsync(context.User, contract, AuthorizationConfiguration.Names.OwnsResource)).Succeeded)
            {
                return TypedResults.Problem(NotYours, statusCode: 403);
            }

            var result = await contracts.GetAsync(contract.Id, cancellationToken);
            return TypedResults.Ok(result.Contract!);
        })
            .WithName("GetApplicationContract")
            // T-47a: who read this personal data, and when.
            .LogsPersonalDataRead("application-contract", "applicationId")
            .WithSummary("The contract of an application; the operator reads every one, an applicant only their own.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        app.MapPut("/contracts/{contractId:guid}/values",
            async Task<Results<Ok<ContractResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid contractId, ContractValuesRequest request, [FromServices] IContractService? contracts,
            CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            return Answer(await contracts.SaveValuesAsync(contractId, request.Values, cancellationToken));
        })
            .WithName("SaveContractValues")
            .WithSummary("The values of the blanks the template leaves to the operator, the whole set at once.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/contracts/{contractId:guid}/pdf",
            async Task<Results<FileContentHttpResult, ProblemHttpResult>> (
            Guid contractId, HttpContext context, [FromServices] IContractService? contracts,
            [FromServices] IAuthorizationService authorization, CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var contract = await contracts.FindForAuthorizationAsync(contractId, cancellationToken);
            if (contract is null)
            {
                return TypedResults.Problem(NotFound, statusCode: 404);
            }

            if (!(await authorization.AuthorizeAsync(context.User, contract, AuthorizationConfiguration.Names.OwnsResource)).Succeeded)
            {
                return TypedResults.Problem(NotYours, statusCode: 403);
            }

            var result = await contracts.PdfAsync(contractId, cancellationToken);
            return TypedResults.File(result.Pdf!, "application/pdf", result.FileName);
        })
            .WithName("DownloadContract")
            // T-47a: who read this personal data, and when.
            .LogsPersonalDataRead("contract", "contractId")
            .WithSummary("The contract as a PDF: the template filled in, a blank still to be typed printed as a dotted line.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        app.MapPost("/contracts/{contractId:guid}/sign",
            async Task<Results<Ok<ContractResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid contractId, SignContractRequest request, HttpContext context, [FromServices] IContractService? contracts,
            CancellationToken cancellationToken) =>
        {
            if (contracts is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var operatorId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Answer(await contracts.SignAsync(contractId, operatorId, request.SignedOn, cancellationToken));
        })
            .WithName("SignContract")
            .WithSummary("Records the day the contract was signed; every blank has to be filled in, the application becomes \"umowa podpisana\".")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);
    }

    private static Results<Ok<ContractResponse>, ValidationProblem, ProblemHttpResult> Answer(ContractResult result) =>
        result.Outcome switch
        {
            ContractOutcome.Succeeded => TypedResults.Ok(result.Contract!),
            ContractOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
            ContractOutcome.Signed => TypedResults.Problem(Signed, statusCode: 409),
            ContractOutcome.NotGranted => TypedResults.Problem(NotGranted, statusCode: 409),
            _ => TypedResults.Problem(NotFound, statusCode: 404),
        };
}
