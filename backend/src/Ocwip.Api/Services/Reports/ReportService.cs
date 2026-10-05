using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Reports;

internal enum ReportOutcome
{
    Succeeded,
    Created,
    NotFound,

    /// <summary>Only a funded application reports; nothing else has a project to report on.</summary>
    NotFunded,

    /// <summary>The competition has no report form published yet.</summary>
    NoForm,

    /// <summary>Submitted or accepted: not editable until the operator sends it back.</summary>
    Frozen,

    /// <summary>The move asked for does not start from the state the report is in.</summary>
    WrongState,

    Invalid,
}

internal sealed record ReportResult(
    ReportOutcome Outcome,
    ReportResponse? Report = null,
    IDictionary<string, string[]>? Errors = null);

/// <summary>The report of a funded project (T-50a): start, fill in, submit, accept or send back.</summary>
internal interface IReportService
{
    Task<ReportResult> StartAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<ReportResult> GetAsync(Guid reportId, CancellationToken cancellationToken);

    Task<ReportResult> SaveAsync(Guid reportId, JsonElement answers, CancellationToken cancellationToken);

    Task<ReportResult> SubmitAsync(Guid reportId, Guid callerId, CancellationToken cancellationToken);

    Task<ReportResult> ReturnAsync(Guid reportId, Guid operatorId, string? reason, CancellationToken cancellationToken);

    Task<ReportResult> AcceptAsync(Guid reportId, Guid operatorId, CancellationToken cancellationToken);

    /// <summary>The operator's review of the budget costs (T-50b), only while the report is submitted.</summary>
    Task<ReportResult> ReviewCostsAsync(Guid reportId, IReadOnlyList<CostReviewItem> items, CancellationToken cancellationToken);

    /// <summary>Null for no such competition.</summary>
    Task<IReadOnlyList<ReportListItem>?> ListAsync(Guid competitionId, CancellationToken cancellationToken);

    Task<Report?> FindForAuthorizationAsync(Guid reportId, CancellationToken cancellationToken);
}

internal sealed partial class ReportService(AppDbContext context, TimeProvider time, IEmailSender email) : IReportService
{
    internal const int ReasonMaxLength = 2000;

    public async Task<ReportResult> StartAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await context.Applications
            .Include(x => x.Competition)
            .Include(x => x.Entity)
            .Include(x => x.FormDefinition)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);

        if (application is null)
        {
            return new ReportResult(ReportOutcome.NotFound);
        }

        if (await ActiveForAsync(applicationId, cancellationToken) is { } existing)
        {
            return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, existing.Id, cancellationToken));
        }

        if (!ApplicationStatuses.IsGranted(application.Status))
        {
            return new ReportResult(ReportOutcome.NotFunded);
        }

        if (application.Competition.ReportFormDefinitionId is not { } formId)
        {
            return new ReportResult(ReportOutcome.NoForm);
        }

        var form = await context.FormDefinitions.AsNoTracking().SingleAsync(x => x.Id == formId, cancellationToken);
        var reportForm = ReportReader.Document(form);
        var applicationForm = ReportReader.Document(application.FormDefinition);
        var prefill = ReportPrefill.Build(reportForm, applicationForm, application.Answers, application.KindOfApplicant);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            CompetitionId = application.CompetitionId,
            EntityId = application.EntityId,
            FormDefinitionId = form.Id,
            // The report starts as what the application said: editable
            // values taken from it are a starting point, read only ones stay.
            Answers = Protected(JsonSerializer.SerializeToElement(prefill), reportForm, applicationForm),
            Prefill = Protected(JsonSerializer.SerializeToElement(prefill), reportForm, applicationForm),
        };

        context.Reports.Add(report);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two tabs started it in the same moment; the other one won.
            context.ChangeTracker.Clear();
            var raced = await ActiveForAsync(applicationId, cancellationToken)
                ?? throw new InvalidOperationException($"No active report of {applicationId} after a unique violation.");
            return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, raced.Id, cancellationToken));
        }

        return new ReportResult(ReportOutcome.Created, await ReportReader.ResponseAsync(context, report.Id, cancellationToken));
    }

    public async Task<ReportResult> GetAsync(Guid reportId, CancellationToken cancellationToken) =>
        await ReportReader.ResponseAsync(context, reportId, cancellationToken) is { } response
            ? new ReportResult(ReportOutcome.Succeeded, response)
            : new ReportResult(ReportOutcome.NotFound);

    public async Task<ReportResult> SaveAsync(Guid reportId, JsonElement answers, CancellationToken cancellationToken)
    {
        if (answers.ValueKind != JsonValueKind.Object)
        {
            return new ReportResult(ReportOutcome.Invalid, Errors: new Dictionary<string, string[]>
            {
                ["answers"] = ["Odpowiedzi muszą być obiektem."],
            });
        }

        var (report, form, applicant) = await LoadAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new ReportResult(ReportOutcome.NotFound);
        }

        if (report.Status is not (ReportStatus.Draft or ReportStatus.Returned))
        {
            return new ReportResult(ReportOutcome.Frozen);
        }

        var merged = JsonSerializer.SerializeToElement(ReportPrefill.Apply(form!, answers, report.Prefill));
        var check = AnswerValidator.Validate(form!, merged, Bases, AnswerStrictness.Draft, applicant);
        if (!check.IsValid)
        {
            return new ReportResult(ReportOutcome.Invalid, Errors: check.ToProblemErrors());
        }

        // Encrypted in memory from here on; the response is read again below.
        report.Answers = Protected(merged, form, ReportReader.Document(report.Application.FormDefinition));
        if (!await TrySaveAsync(cancellationToken))
        {
            return new ReportResult(ReportOutcome.Frozen);
        }

        return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, report.Id, cancellationToken));
    }

    public async Task<ReportResult> SubmitAsync(Guid reportId, Guid callerId, CancellationToken cancellationToken)
    {
        var (report, form, applicant) = await LoadAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new ReportResult(ReportOutcome.NotFound);
        }

        if (report.Status is not (ReportStatus.Draft or ReportStatus.Returned))
        {
            return new ReportResult(ReportOutcome.Frozen);
        }

        var check = AnswerValidator.Validate(form!, report.Answers, Bases, AnswerStrictness.Submission, applicant);
        if (!check.IsValid)
        {
            return new ReportResult(ReportOutcome.Invalid, Errors: check.ToProblemErrors());
        }

        Move(report, ReportStatus.Submitted, callerId, reason: null);
        // A judgement on a row the applicant changed after a return no
        // longer judges that cost: it goes, and the operator looks again.
        report.CostReview = ReportSettlement.Write(
            ReportSettlement.Keep(form!, report.Answers, applicant, ReportSettlement.Read(report.CostReview)));
        report.SubmittedAt = time.GetUtcNow();
        report.ReturnReason = null;
        if (!await TrySaveAsync(cancellationToken))
        {
            return new ReportResult(ReportOutcome.Frozen);
        }

        return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, report.Id, cancellationToken));
    }

    public async Task<ReportResult> ReturnAsync(
        Guid reportId, Guid operatorId, string? reason, CancellationToken cancellationToken)
    {
        var text = reason?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > ReasonMaxLength)
        {
            return new ReportResult(ReportOutcome.Invalid, Errors: new Dictionary<string, string[]>
            {
                ["reason"] = [$"Podaj powód zwrotu, najwyżej {ReasonMaxLength} znaków: wnioskodawca go przeczyta."],
            });
        }

        var report = await context.Reports.FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, cancellationToken);
        if (report is null)
        {
            return new ReportResult(ReportOutcome.NotFound);
        }

        if (report.Status is not ReportStatus.Submitted)
        {
            return new ReportResult(ReportOutcome.WrongState);
        }

        Move(report, ReportStatus.Returned, operatorId, text);
        report.ReturnReason = text;
        if (!await TrySaveAsync(cancellationToken))
        {
            return new ReportResult(ReportOutcome.WrongState);
        }

        await NotifyReturnedAsync(report.Id, text, cancellationToken);

        return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, report.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<ReportListItem>?> ListAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        if (!await context.Competitions.AnyAsync(x => x.Id == competitionId, cancellationToken))
        {
            return null;
        }

        // The frozen name (S-06), so this screen says what the signed
        // documents say: the copy comes back with the row and the name is put
        // together once it is in memory, because EntitySnapshots.NameOf is
        // not something the provider can translate.
        var rows = await context.Reports.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.IsActive)
            .OrderBy(x => x.Application.Number)
            .Select(x => new
            {
                x.Id,
                x.ApplicationId,
                x.Application.Number,
                x.Application.EntitySnapshot,
                LiveName = x.Application.Entity.Name,
                x.Status,
                x.SubmittedAt,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new ReportListItem(
                x.Id,
                x.ApplicationId,
                x.Number,
                EntityCards.EntitySnapshots.NameOf(x.EntitySnapshot, x.LiveName),
                x.Status,
                x.SubmittedAt))
            .ToList();
    }

    public Task<Report?> FindForAuthorizationAsync(Guid reportId, CancellationToken cancellationToken) =>
        context.Reports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, cancellationToken);

    /// <summary>A report measures no limit against competition settings; the application did.</summary>
    private static readonly IReadOnlyDictionary<string, decimal?> Bases = new Dictionary<string, decimal?>();

    /// <summary>
    /// False when the report left the state it was read in before the save
    /// (the status is a concurrency token): the caller answers as if it had
    /// read the new state, and nothing of this save is kept.
    /// </summary>
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private void Move(Report report, ReportStatus to, Guid by, string? reason)
    {
        context.ReportStatusHistory.Add(new ReportStatusHistory
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            FromStatus = report.Status,
            ToStatus = to,
            ChangedAt = time.GetUtcNow(),
            ChangedByUserId = by,
            Reason = reason,
        });
        report.Status = to;
    }

    private Task<Report?> ActiveForAsync(Guid applicationId, CancellationToken cancellationToken) =>
        context.Reports.AsNoTracking().FirstOrDefaultAsync(x => x.ApplicationId == applicationId && x.IsActive, cancellationToken);

    /// <summary>The sensitive answers encrypted (T-47a), see SensitiveAnswers.ReportKeys.</summary>
    private static JsonElement Protected(JsonElement answers, FormDocument? report, FormDocument? application) =>
        SensitiveAnswers.Protect(answers, SensitiveAnswers.ReportKeys(report, application), SensitiveAnswers.ReportPurpose);

    private async Task<(Report? Report, FormDocument? Form, EntityType Applicant)> LoadAsync(
        Guid reportId, CancellationToken cancellationToken)
    {
        var report = await context.Reports
            .Include(x => x.FormDefinition)
            .Include(x => x.Application).ThenInclude(x => x.Entity)
            .Include(x => x.Application).ThenInclude(x => x.FormDefinition)
            .FirstOrDefaultAsync(x => x.Id == reportId && x.IsActive, cancellationToken);

        return report is null
            ? (null, null, default)
            : (report, ReportReader.Document(report.FormDefinition), report.Application.KindOfApplicant);
    }
}
