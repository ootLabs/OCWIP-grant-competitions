using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Export;

namespace Ocwip.Api.Services.Ranking;

internal enum ResignationOutcome
{
    Succeeded,
    NotFound,

    /// <summary>Not in the state the action starts from: not funded for a resignation, not on the reserve list for a promotion.</summary>
    WrongStatus,

    /// <summary>The results are not approved yet: there is nothing to resign from.</summary>
    NotResolved,

    Invalid,
}

internal sealed record ResignationResult(
    ResignationOutcome Outcome,
    ResignationsResponse? Overview = null,
    IDictionary<string, string[]>? Errors = null);

internal interface IResignationService
{
    Task<ResignationResult> OverviewAsync(Guid competitionId, CancellationToken cancellationToken);

    Task<ResignationResult> ResignAsync(Guid applicationId, Guid operatorId, CancellationToken cancellationToken);

    Task<ResignationResult> PromoteAsync(Guid applicationId, Guid operatorId, PromotionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Resignation and the reserve list (T-109, regulations 2026): a contract not
/// signed within 14 days of the publication of the results means the grant
/// is given up, and the money goes to the next application on the reserve
/// list that meets the threshold.
///
/// The clock only reminds (ContractDeadlineJob); the operator confirms the
/// resignation, and the operator confirms the promotion, with the amount the
/// system proposes: the requested one, or what is left of the pool. Both are
/// status changes by conditional UPDATE, in the history, with a mail to the
/// applicant, and neither moves UpdatedAt, which the checksum of the
/// submitted application is computed from (D15), like every result write.
/// </summary>
internal sealed class ResignationService(AppDbContext context, TimeProvider time, IRankingService ranking, IEmailSender email)
    : IResignationService
{
    public static readonly TimeSpan ContractWindow = TimeSpan.FromDays(14);

    public static DateTimeOffset? DeadlineOf(Competition competition) =>
        competition.ResultsApprovedAt + ContractWindow;

    public async Task<ResignationResult> OverviewAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId && x.IsActive, cancellationToken);
        var list = await ranking.GetRankingAsync(competitionId, cancellationToken);
        if (competition is null || list.Ranking is null)
        {
            return new ResignationResult(ResignationOutcome.NotFound);
        }

        var rows = list.Ranking.Rows;
        var deadline = DeadlineOf(competition);
        var now = time.GetUtcNow();
        var free = competition.TotalPoolAmount is { } pool ? pool - list.Ranking.AwardedTotal : (decimal?)null;

        var unsigned = rows.Where(x => x.Status == ApplicationStatus.Funded)
            .Select(x => new UnsignedContract(x.ApplicationId, x.Number, x.EntityName, x.AwardedGrant, deadline is { } d && now >= d))
            .ToList();

        var next = rows.Where(x => x.Status == ApplicationStatus.Reserve)
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .Select(x => new ReserveCandidate(
                x.ApplicationId, x.Rank, x.Number, x.EntityName, x.RequestedGrant, Proposed(x.RequestedGrant, free)))
            .FirstOrDefault();

        return new ResignationResult(
            ResignationOutcome.Succeeded,
            new ResignationsResponse(
                competitionId, competition.ResultsApprovedAt, deadline, competition.TotalPoolAmount,
                list.Ranking.AwardedTotal, free, unsigned, next));
    }

    public async Task<ResignationResult> ResignAsync(Guid applicationId, Guid operatorId, CancellationToken cancellationToken)
    {
        var application = await LoadAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return new ResignationResult(ResignationOutcome.NotFound);
        }

        if (application.Competition.ResultsApprovedAt is null)
        {
            return new ResignationResult(ResignationOutcome.NotResolved);
        }

        var now = time.GetUtcNow();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Funded only: a signed contract is not given up by this path.
        var changed = await context.Applications
            .Where(x => x.Id == applicationId && x.IsActive && x.Status == ApplicationStatus.Funded)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Resigned), cancellationToken);
        if (changed != 1)
        {
            return new ResignationResult(ResignationOutcome.WrongStatus);
        }

        // A contract drafted and never signed is withdrawn with the grant.
        await context.Contracts
            .Where(x => x.ApplicationId == applicationId && x.IsActive && x.Status != ContractStatus.Signed)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);

        History(applicationId, ApplicationStatus.Funded, ApplicationStatus.Resigned, operatorId, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await MailAsync(application, $"Rezygnacja z dotacji: wniosek {application.Number}", $"""
            Umowa do wniosku {application.Number} w konkursie "{application.Competition.Title}" nie została podpisana w terminie 14 dni od ogłoszenia wyników.

            Zgodnie z regulaminem konkursu oznacza to rezygnację z przyznanej dotacji. Środki przechodzą na kolejny wniosek z listy rezerwowej.

            Jeśli to pomyłka, skontaktuj się z organizatorem konkursu.
            """, cancellationToken);

        return new ResignationResult(ResignationOutcome.Succeeded);
    }

    public async Task<ResignationResult> PromoteAsync(
        Guid applicationId, Guid operatorId, PromotionRequest request, CancellationToken cancellationToken)
    {
        var application = await LoadAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return new ResignationResult(ResignationOutcome.NotFound);
        }

        if (application.Competition.ResultsApprovedAt is null)
        {
            return new ResignationResult(ResignationOutcome.NotResolved);
        }

        if (request.AwardedGrant is not { } amount || amount <= 0m || decimal.Round(amount, 2) != amount)
        {
            return Invalid("Kwota dotacji musi być dodatnia, z dokładnością do grosza.");
        }

        var now = time.GetUtcNow();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // One promotion at a time per competition: two in the same moment
        // must not both fit into the same free money.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({"promotion:" + application.CompetitionId})::bigint)",
            cancellationToken);

        if (application.Competition.TotalPoolAmount is { } pool)
        {
            var granted = await context.Applications
                .Where(x => x.CompetitionId == application.CompetitionId && x.IsActive
                    && (x.Status == ApplicationStatus.Funded || x.Status == ApplicationStatus.ContractSigned || x.Status == ApplicationStatus.Settled))
                .SumAsync(x => x.AwardedGrant ?? 0m, cancellationToken);

            if (amount > pool - granted)
            {
                return Invalid($"W puli zostało {ApplicationListLabels.Amount(pool - granted)} zł. Kwota nie może być większa.");
            }
        }

        var changed = await context.Applications
            .Where(x => x.Id == applicationId && x.IsActive && x.Status == ApplicationStatus.Reserve)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, ApplicationStatus.Funded).SetProperty(x => x.AwardedGrant, amount),
                cancellationToken);
        if (changed != 1)
        {
            return new ResignationResult(ResignationOutcome.WrongStatus);
        }

        History(applicationId, ApplicationStatus.Reserve, ApplicationStatus.Funded, operatorId, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await MailAsync(application, $"Dofinansowanie z listy rezerwowej: wniosek {application.Number}", $"""
            Wniosek {application.Number} w konkursie "{application.Competition.Title}" był na liście rezerwowej i otrzymał dofinansowanie w kwocie {ApplicationListLabels.Amount(amount)} zł, ze środków zwolnionych po rezygnacji innego wnioskodawcy.

            Organizator przygotuje umowę. Szczegóły zobaczysz w systemie przy swoim wniosku.
            """, cancellationToken);

        return new ResignationResult(ResignationOutcome.Succeeded);
    }

    private static decimal? Proposed(decimal? requested, decimal? free) =>
        requested is not { } asked ? null
        : free is not { } left ? asked
        : Math.Max(0m, Math.Min(asked, left));

    private Task<Application?> LoadAsync(Guid applicationId, CancellationToken cancellationToken) =>
        context.Applications.AsNoTracking()
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);

    private void History(Guid applicationId, ApplicationStatus from, ApplicationStatus to, Guid operatorId, DateTimeOffset now) =>
        context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStatus = from,
            ToStatus = to,
            ChangedAt = now,
            ChangedByUserId = operatorId,
        });

    /// <summary>To the account that submitted the application, the one its other mails went to.</summary>
    private async Task MailAsync(Application application, string subject, string body, CancellationToken cancellationToken)
    {
        var to = await context.ApplicationStatusHistory.AsNoTracking()
            .Where(x => x.ApplicationId == application.Id && x.ToStatus == ApplicationStatus.Submitted)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => x.ChangedByUser.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(to))
        {
            await email.SendAsync(new EmailMessage(to, subject, body), cancellationToken);
        }
    }

    private static ResignationResult Invalid(string message) =>
        new(ResignationOutcome.Invalid, Errors: new Dictionary<string, string[]> { ["awardedGrant"] = [message] });
}
