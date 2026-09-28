using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Documents;

namespace Ocwip.Api.Services;

internal enum CompetitionCopyOutcome
{
    Created,
    NotFound,
    Invalid,
    NumberTaken,
}

internal sealed record CompetitionCopyResult(
    CompetitionCopyOutcome Outcome,
    CompetitionResponse? Competition = null,
    IDictionary<string, string[]>? Errors = null);

internal interface ICompetitionCopyService
{
    Task<CompetitionCopyResult> CopyAsync(Guid sourceId, CompetitionCopyRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// "Skopiuj konkurs" (T-98, R-11): a draft that carries the previous edition
/// over, so the operator's second intake starts from the first one instead
/// of from an empty wizard.
///
/// Carried: the settings (a CompetitionRequest built from the source and run
/// through the same validator and CompetitionService.CreateAsync as the
/// wizard), the evaluation settings, the result mails, the lists (attachment
/// requirements, contacts still active, cost categories, as new rows), and
/// every document in force: the application form, both cards, the report
/// form, each as version 1 of the new competition through
/// FormDefinitionService, and the contract template through ContractService.
/// New rows everywhere, so a change in the copy never reaches the original.
///
/// Not carried: the number and the dates (the request gives new ones), the
/// project period, the paper deadline and the retention date, which are the
/// edition's own, nor anything that happened in the source: applications,
/// assignments, evaluations, results. All in one transaction: a half copied
/// competition would pass for a complete one.
/// </summary>
internal sealed class CompetitionCopyService(AppDbContext context, TimeProvider time) : ICompetitionCopyService
{
    public async Task<CompetitionCopyResult> CopyAsync(Guid sourceId, CompetitionCopyRequest request, CancellationToken cancellationToken)
    {
        var source = await context.Competitions.AsNoTracking()
            .Include(x => x.Attachments)
            .Include(x => x.CostCategories)
            .Include(x => x.Contacts).ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == sourceId && x.IsActive, cancellationToken);

        if (source is null)
        {
            return new CompetitionCopyResult(CompetitionCopyOutcome.NotFound);
        }

        if (request.StartDate is null)
        {
            return new CompetitionCopyResult(
                CompetitionCopyOutcome.Invalid,
                Errors: new Dictionary<string, string[]> { ["startDate"] = ["Podaj datę rozpoczęcia naboru w nowym konkursie."] });
        }

        var settings = CompetitionRequestValidator.Trim(SettingsOf(source, request));
        var problems = CompetitionRequestValidator.Validate(settings);
        if (problems.Count > 0)
        {
            return new CompetitionCopyResult(CompetitionCopyOutcome.Invalid, Errors: problems);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var created = await new CompetitionService(context, time).CreateAsync(settings, cancellationToken);
        if (created.Outcome is CompetitionOutcome.NumberTaken)
        {
            return new CompetitionCopyResult(CompetitionCopyOutcome.NumberTaken);
        }

        if (created.Outcome is not CompetitionOutcome.Succeeded)
        {
            throw new InvalidOperationException($"Copying competition {sourceId} failed with {created.Outcome}.");
        }

        var copyId = created.Competition!.Id;
        var copy = await context.Competitions.SingleAsync(x => x.Id == copyId, cancellationToken);

        // What the wizard does not carry: evaluation settings (T-39) and the
        // result mails (T-43).
        copy.EvaluatorsPerApplication = source.EvaluatorsPerApplication;
        copy.ScoreAggregation = source.ScoreAggregation;
        copy.MeritThreshold = source.MeritThreshold;
        copy.ThresholdIncludesStrategic = source.ThresholdIncludesStrategic;
        copy.DivergenceThresholdPercent = source.DivergenceThresholdPercent;
        copy.ResultEmailFunded = source.ResultEmailFunded;
        copy.ResultEmailReserve = source.ResultEmailReserve;
        copy.ResultEmailRejected = source.ResultEmailRejected;
        await context.SaveChangesAsync(cancellationToken);

        var forms = new FormDefinitionService(context);
        foreach (var (purpose, id) in new[]
        {
            (FormPurpose.Application, source.FormDefinitionId),
            (FormPurpose.FormalEvaluation, source.FormalCardDefinitionId),
            (FormPurpose.MeritEvaluation, source.MeritCardDefinitionId),
            (FormPurpose.Report, source.ReportFormDefinitionId),
        })
        {
            if (id is not { } definitionId)
            {
                continue;
            }

            var definition = await context.FormDefinitions.AsNoTracking().SingleAsync(x => x.Id == definitionId, cancellationToken);
            var published = await forms.PublishAsync(copyId, purpose, new FormDefinitionRequest(definition.Definition), cancellationToken);
            if (published.Outcome is not FormDefinitionOutcome.Succeeded)
            {
                // The source's version no longer passes today's contract: the
                // copy is refused whole, naming the part, rather than made
                // without it and passed off as complete.
                return new CompetitionCopyResult(
                    CompetitionCopyOutcome.Invalid,
                    Errors: new Dictionary<string, string[]>
                    {
                        ["source"] = [$"{PartName(purpose)} konkursu źródłowego nie przechodzi dzisiejszego kontraktu formularza. Opublikuj w nim poprawioną wersję i skopiuj ponownie."],
                    });
            }
        }

        var template = await context.DocumentTemplates.AsNoTracking()
            .Where(x => x.CompetitionId == sourceId && x.Kind == DocumentKind.Contract)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (template is not null)
        {
            await new ContractService(context, time).PublishTemplateAsync(copyId, template.Body, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        context.ChangeTracker.Clear();
        var response = await new CompetitionService(context, time).GetAsync(copyId, cancellationToken);
        return new CompetitionCopyResult(CompetitionCopyOutcome.Created, response.Competition);
    }

    /// <summary>The source's settings with the new number, title and dates; nothing of the edition itself.</summary>
    private static CompetitionRequest SettingsOf(Competition source, CompetitionCopyRequest request) =>
        new(
            request.Number ?? string.Empty,
            string.IsNullOrWhiteSpace(request.Title) ? source.Title : request.Title,
            source.Description,
            request.StartDate!.Value,
            request.IsContinuousIntake ? null : request.EndDate,
            request.IsContinuousIntake,
            source.MaxGrantAmount,
            FormDefinitionId: null,
            ExpectedResults: source.ExpectedResults,
            RulesUrl: source.RulesUrl,
            SubmissionNotice: source.SubmissionNotice,
            SubmissionEmailBody: source.SubmissionEmailBody,
            RequiresPaperSubmission: source.RequiresPaperSubmission,
            PaperSubmissionDeadline: null,
            PaperSubmissionAddress: source.PaperSubmissionAddress,
            ProjectStartDate: null,
            ProjectEndDate: null,
            TotalPoolAmount: source.TotalPoolAmount,
            MinGrantAmount: source.MinGrantAmount,
            MaxIndirectCostPercent: source.MaxIndirectCostPercent,
            MaxInstitutionalDevelopmentPercent: source.MaxInstitutionalDevelopmentPercent,
            PercentageBasis: source.PercentageBasis,
            MaxAverageAnnualRevenue: source.MaxAverageAnnualRevenue,
            PersonalDataProcessedUntil: null,
            CostCategories: [.. source.CostCategories.Where(x => x.IsActive).OrderBy(x => x.Position).Select(x => x.Category)],
            MaxAttachmentSizeInBytes: source.MaxAttachmentSizeInBytes,
            MaxApplicationSizeInBytes: source.MaxApplicationSizeInBytes,
            Attachments: [.. source.Attachments.Where(x => x.IsActive).OrderBy(x => x.Position)
                .Select(x => new CompetitionAttachmentRequest(x.Title, x.Description, x.Requirement, x.AllowedFormats))],
            // Only people still on the staff: a contact who has left would make
            // the copy fail on a name nobody asked for.
            ContactUserIds: [.. source.Contacts.Where(x => x.IsActive && x.User.IsActive).OrderBy(x => x.Position).Select(x => x.UserId)]);

    private static string PartName(FormPurpose purpose) => purpose switch
    {
        FormPurpose.FormalEvaluation => "Karta oceny formalnej",
        FormPurpose.MeritEvaluation => "Karta oceny merytorycznej",
        FormPurpose.Report => "Wzór sprawozdania",
        _ => "Formularz wniosku",
    };
}
