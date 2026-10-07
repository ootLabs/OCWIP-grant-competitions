using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Authorization;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.EntityCards;

public enum EntityAccessOutcome
{
    Succeeded,
    /// <summary>Not a valid NIP.</summary>
    InvalidNip,
    /// <summary>No card with this NIP, or no such request on a card the caller may decide for.</summary>
    NotFound,
    /// <summary>The caller already acts for this card.</summary>
    AlreadyMember,
    /// <summary>A member who did not found the card: only the founder decides (report decision 7).</summary>
    NotFounder,
    /// <summary>Somebody decided first.</summary>
    AlreadyDecided,
    /// <summary>An operator may step in only after seven days without an answer.</summary>
    NotEscalated,
    /// <summary>The operator did not say how the request was checked, or the founder sent a note.</summary>
    NoteMismatch,
}

public sealed record EntityAccessResult(EntityAccessOutcome Outcome, MyEntityAccessRequest? Request = null);

public interface IEntityAccessRequestService
{
    Task<EntityAccessResult> RequestAsync(ClaimsPrincipal caller, string nip, CancellationToken cancellationToken);

    Task<IReadOnlyList<MyEntityAccessRequest>> ListMineAsync(ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>Pending requests to a card the caller founded; an empty list with the reason when the caller may not see them.</summary>
    Task<(EntityAccessOutcome Outcome, IReadOnlyList<PendingEntityAccessRequest> Requests)> ListPendingAsync(
        ClaimsPrincipal caller, Guid entityId, CancellationToken cancellationToken);

    Task<EntityAccessOutcome> DecideAsFounderAsync(
        ClaimsPrincipal caller, Guid entityId, Guid requestId, EntityAccessDecisionBody decision, CancellationToken cancellationToken);

    Task<IReadOnlyList<EscalatedEntityAccessRequest>> ListEscalatedAsync(CancellationToken cancellationToken);

    Task<EntityAccessOutcome> DecideAsOperatorAsync(
        ClaimsPrincipal caller, Guid requestId, EntityAccessDecisionBody decision, CancellationToken cancellationToken);
}

/// <summary>
/// Joining a Podmiot card that already exists (T-93a, report step 2.2 and
/// decision 7). The founder approves; a request nobody answers for seven
/// days reaches the operators (EntityAccessEscalationJob), who decide after
/// checking outside the system and leave a note saying how.
///
/// Approval is one conditional UPDATE from Pending: the founder and an
/// operator clicking in the same moment produce one decision and one "somebody
/// decided first", never two memberships.
/// </summary>
internal sealed class EntityAccessRequestService(
    AppDbContext context,
    UserManager<User> userManager,
    TimeProvider time,
    IEmailSender email,
    IConfiguration configuration,
    ILogger<EntityAccessRequestService> logger) : IEntityAccessRequestService
{
    public async Task<EntityAccessResult> RequestAsync(
        ClaimsPrincipal caller, string nip, CancellationToken cancellationToken)
    {
        if (RegistryNumbers.Nip(nip) is not { } digits)
        {
            return new EntityAccessResult(EntityAccessOutcome.InvalidNip);
        }

        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return new EntityAccessResult(EntityAccessOutcome.NotFound);
        }

        var entity = await context.Entities.AsNoTracking()
            .Where(x => x.Nip == digits && x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return new EntityAccessResult(EntityAccessOutcome.NotFound);
        }

        if (await ResourceOwnership.ActsForAsync(context, user.Id, entity.Id, cancellationToken))
        {
            return new EntityAccessResult(EntityAccessOutcome.AlreadyMember);
        }

        // Asking twice is the same request: the founder hears once.
        var open = await context.EntityAccessRequests.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.EntityId == entity.Id && x.RequesterId == user.Id && x.Status == EntityAccessRequestStatus.Pending,
                cancellationToken);

        if (open is not null)
        {
            return new EntityAccessResult(EntityAccessOutcome.Succeeded, Mine(open, entity.Name));
        }

        var request = new EntityAccessRequest { EntityId = entity.Id, RequesterId = user.Id };
        context.EntityAccessRequests.Add(request);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
        })
        {
            // A double click raced itself; the other insert is the request.
            var existing = await context.EntityAccessRequests.AsNoTracking().SingleAsync(
                x => x.EntityId == entity.Id && x.RequesterId == user.Id && x.Status == EntityAccessRequestStatus.Pending,
                cancellationToken);
            return new EntityAccessResult(EntityAccessOutcome.Succeeded, Mine(existing, entity.Name));
        }

        await context.Entry(request).ReloadAsync(cancellationToken);
        await NotifyFounderAsync(request, entity.Name, user, cancellationToken);

        return new EntityAccessResult(EntityAccessOutcome.Succeeded, Mine(request, entity.Name));
    }

    public async Task<IReadOnlyList<MyEntityAccessRequest>> ListMineAsync(
        ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return [];
        }

        return await context.EntityAccessRequests.AsNoTracking()
            .Where(x => x.RequesterId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MyEntityAccessRequest(x.Id, x.Entity.Name, x.Status, x.CreatedAt, x.DecidedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<(EntityAccessOutcome Outcome, IReadOnlyList<PendingEntityAccessRequest> Requests)> ListPendingAsync(
        ClaimsPrincipal caller, Guid entityId, CancellationToken cancellationToken)
    {
        var standing = await StandingAsync(caller, entityId, cancellationToken);
        if (standing.Outcome is not EntityAccessOutcome.Succeeded)
        {
            return (standing.Outcome, []);
        }

        var requests = await context.EntityAccessRequests.AsNoTracking()
            .Where(x => x.EntityId == entityId && x.Status == EntityAccessRequestStatus.Pending)
            .Join(context.Users, request => request.RequesterId, account => account.Id,
                (request, account) => new { request, account })
            .OrderBy(x => x.request.CreatedAt)
            .Select(x => new PendingEntityAccessRequest(
                x.request.Id, x.account.FirstName, x.account.LastName, x.account.Email!, x.request.CreatedAt))
            .ToListAsync(cancellationToken);

        return (EntityAccessOutcome.Succeeded, requests);
    }

    public async Task<EntityAccessOutcome> DecideAsFounderAsync(
        ClaimsPrincipal caller, Guid entityId, Guid requestId, EntityAccessDecisionBody decision,
        CancellationToken cancellationToken)
    {
        if (decision.Note is not null)
        {
            return EntityAccessOutcome.NoteMismatch;
        }

        var standing = await StandingAsync(caller, entityId, cancellationToken);
        if (standing.Outcome is not EntityAccessOutcome.Succeeded)
        {
            return standing.Outcome;
        }

        var request = await context.EntityAccessRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId && x.EntityId == entityId, cancellationToken);

        if (request is null)
        {
            return EntityAccessOutcome.NotFound;
        }

        return await DecideAsync(request, standing.UserId, byOperator: false, note: null, decision.Approve, cancellationToken);
    }

    public async Task<IReadOnlyList<EscalatedEntityAccessRequest>> ListEscalatedAsync(CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow() - EntityAccessRequest.EscalationAge;

        var rows = await (
            from request in context.EntityAccessRequests.AsNoTracking()
            where request.Status == EntityAccessRequestStatus.Pending && request.CreatedAt <= cutoff
            join requester in context.Users on request.RequesterId equals requester.Id
            let founder = context.EntityMembers
                .Where(m => m.EntityId == request.EntityId && m.IsFounder && m.IsActive)
                .Join(context.Users, m => m.UserId, u => u.Id, (m, u) => u)
                .FirstOrDefault()
            orderby request.CreatedAt
            select new EscalatedEntityAccessRequest(
                request.Id,
                request.Entity.Name,
                request.Entity.Nip,
                requester.FirstName,
                requester.LastName,
                requester.Email!,
                founder == null ? null : founder.FirstName,
                founder == null ? null : founder.LastName,
                founder == null ? null : founder.Email,
                request.CreatedAt))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<EntityAccessOutcome> DecideAsOperatorAsync(
        ClaimsPrincipal caller, Guid requestId, EntityAccessDecisionBody decision, CancellationToken cancellationToken)
    {
        var note = decision.Note?.Trim();
        if (string.IsNullOrEmpty(note) || note.Length > 1000)
        {
            return EntityAccessOutcome.NoteMismatch;
        }

        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return EntityAccessOutcome.NotFound;
        }

        var request = await context.EntityAccessRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null)
        {
            return EntityAccessOutcome.NotFound;
        }

        if (request.Status is not EntityAccessRequestStatus.Pending)
        {
            return EntityAccessOutcome.AlreadyDecided;
        }

        // Report step 2.2: the operator is the fallback for a founder who
        // does not answer, not a second way in for everybody from day one.
        if (request.CreatedAt > time.GetUtcNow() - EntityAccessRequest.EscalationAge)
        {
            return EntityAccessOutcome.NotEscalated;
        }

        return await DecideAsync(request, user.Id, byOperator: true, note, decision.Approve, cancellationToken);
    }

    private async Task<EntityAccessOutcome> DecideAsync(
        EntityAccessRequest request, Guid deciderId, bool byOperator, string? note, bool approve,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var status = approve ? EntityAccessRequestStatus.Approved : EntityAccessRequestStatus.Rejected;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var decided = await context.EntityAccessRequests
            .Where(x => x.Id == request.Id && x.Status == EntityAccessRequestStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, status)
                    .SetProperty(x => x.DecidedAt, now)
                    .SetProperty(x => x.DecidedById, deciderId)
                    .SetProperty(x => x.DecidedByOperator, byOperator)
                    .SetProperty(x => x.OperatorNote, note)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        if (decided != 1)
        {
            return EntityAccessOutcome.AlreadyDecided;
        }

        if (approve && !await ResourceOwnership.ActsForAsync(context, request.RequesterId, request.EntityId, cancellationToken))
        {
            context.EntityMembers.Add(new EntityMember
            {
                EntityId = request.EntityId,
                UserId = request.RequesterId,
                IsFounder = false,
                AccessRequestId = request.Id,
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await NotifyRequesterAsync(request, approve, cancellationToken);

        return EntityAccessOutcome.Succeeded;
    }

    /// <summary>Whether the caller founded this card: NotFound for a stranger, NotFounder for another member.</summary>
    private async Task<(EntityAccessOutcome Outcome, Guid UserId)> StandingAsync(
        ClaimsPrincipal caller, Guid entityId, CancellationToken cancellationToken)
    {
        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return (EntityAccessOutcome.NotFound, Guid.Empty);
        }

        var membership = await context.EntityMembers.AsNoTracking()
            .Where(x => x.EntityId == entityId && x.UserId == user.Id && x.IsActive && x.Entity.IsActive)
            .Select(x => new { x.IsFounder })
            .SingleOrDefaultAsync(cancellationToken);

        return membership switch
        {
            null => (EntityAccessOutcome.NotFound, user.Id),
            { IsFounder: false } => (EntityAccessOutcome.NotFounder, user.Id),
            _ => (EntityAccessOutcome.Succeeded, user.Id),
        };
    }

    private static MyEntityAccessRequest Mine(EntityAccessRequest request, string entityName) =>
        new(request.Id, entityName, request.Status, request.CreatedAt, request.DecidedAt);

    private string BaseUrl() =>
        (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
            ? configured
            : "http://localhost:3000").TrimEnd('/');

    // The request is saved before any mail: a relay that refuses does not
    // undo it, and the seven day escalation still reaches the operators.
    private async Task NotifyFounderAsync(
        EntityAccessRequest request, string entityName, User requester, CancellationToken cancellationToken)
    {
        var founder = await context.EntityMembers.AsNoTracking()
            .Where(x => x.EntityId == request.EntityId && x.IsFounder && x.IsActive)
            .Join(context.Users, m => m.UserId, u => u.Id, (m, u) => u)
            .Where(x => x.IsActive && x.EmailConfirmed && x.Email != null)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (founder is null)
        {
            return;
        }

        var body = $"""
            {requester.FirstName} {requester.LastName} ({requester.Email}) prosi o dostęp do karty "{entityName}".

            Osoba z dostępem do karty widzi wszystkie wnioski tej organizacji, także robocze, i może je składać. Zatwierdź prośbę tylko wtedy, gdy znasz tę osobę.

            Prośbę zatwierdzisz albo odrzucisz w panelu: {BaseUrl()}/panel/applicant/profile

            Jeśli nikt nie odpowie przez 7 dni, prośbę rozpatrzy operator OCWIP.
            """;

        await SendQuietlyAsync(
            new EmailMessage(founder, $"Prośba o dostęp do karty \"{entityName}\"", body), request.Id, cancellationToken);
    }

    private async Task NotifyRequesterAsync(EntityAccessRequest request, bool approved, CancellationToken cancellationToken)
    {
        var to = await context.Users.AsNoTracking()
            .Where(x => x.Id == request.RequesterId && x.IsActive && x.EmailConfirmed)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);
        var entityName = await context.Entities.AsNoTracking()
            .Where(x => x.Id == request.EntityId)
            .Select(x => x.Name)
            .SingleAsync(cancellationToken);

        if (string.IsNullOrEmpty(to))
        {
            return;
        }

        var body = approved
            ? $"""
                Masz dostęp do karty "{entityName}". Wnioski tej organizacji znajdziesz w panelu: {BaseUrl()}/panel/applicant
                """
            : $"""
                Prośba o dostęp do karty "{entityName}" została odrzucona. Jeśli to pomyłka, skontaktuj się z osobą, która prowadzi kartę w Twojej organizacji, albo z OCWIP.
                """;

        await SendQuietlyAsync(
            new EmailMessage(to, approved ? $"Dostęp do karty \"{entityName}\"" : $"Prośba o dostęp do karty \"{entityName}\" odrzucona", body),
            request.Id,
            cancellationToken);
    }

    private async Task SendQuietlyAsync(EmailMessage message, Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            await email.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The request id only: the addresses and names are personal data
            // (AGENTS.md, rule 4).
            logger.LogWarning(exception, "Access request {RequestId}: the mail was not sent", requestId);
        }
    }
}
