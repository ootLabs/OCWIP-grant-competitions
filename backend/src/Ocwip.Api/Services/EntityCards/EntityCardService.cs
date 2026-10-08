using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Authorization;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.EntityCards;

public enum EntityCardOutcome
{
    Succeeded,
    /// <summary>No such card among the caller's: it does not exist, or the caller is not a member. The two answer alike.</summary>
    NotFound,
    /// <summary>
    /// Another active card already has this NIP (T-93a, report step 2.2): the
    /// caller asks to join it instead of founding a duplicate.
    /// </summary>
    NipTaken,
    Invalid,
}

public sealed record EntityCardResult(
    EntityCardOutcome Outcome,
    EntityCardResponse? Card = null,
    IDictionary<string, string[]>? Errors = null);

public interface IEntityCardService
{
    /// <summary>Every card the caller acts for, by name.</summary>
    Task<IReadOnlyList<EntityCardSummary>> ListAsync(ClaimsPrincipal caller, CancellationToken cancellationToken);

    Task<EntityCardResult> GetAsync(ClaimsPrincipal caller, Guid entityId, CancellationToken cancellationToken);

    Task<EntityCardResult> CreateAsync(ClaimsPrincipal caller, EntityCardData card, CancellationToken cancellationToken);

    Task<EntityCardResult> UpdateAsync(ClaimsPrincipal caller, Guid entityId, EntityCardData card, CancellationToken cancellationToken);
}

/// <summary>
/// The Podmiot cards the caller acts for (T-93, T-93a): founded at the first
/// application, corrected by any member from then on. Membership is the only
/// way in, read through <see cref="ResourceOwnership"/>; a card the caller is
/// not a member of answers exactly like one that does not exist.
///
/// A correction never touches a submitted application, which reads its own
/// copy (Application.EntitySnapshot).
/// </summary>
internal sealed class EntityCardService(AppDbContext context, UserManager<User> userManager) : IEntityCardService
{
    public async Task<IReadOnlyList<EntityCardSummary>> ListAsync(
        ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return [];
        }

        return await context.EntityMembers.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.IsActive && x.Entity.IsActive)
            .OrderBy(x => x.Entity.Name)
            .Select(x => new EntityCardSummary(x.EntityId, x.Entity.Type, x.Entity.Name, x.IsFounder, x.Entity.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<EntityCardResult> GetAsync(
        ClaimsPrincipal caller, Guid entityId, CancellationToken cancellationToken)
    {
        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return new EntityCardResult(EntityCardOutcome.NotFound);
        }

        var entity = await MemberEntityAsync(user.Id, entityId, tracked: false, cancellationToken);
        return entity is null
            ? new EntityCardResult(EntityCardOutcome.NotFound)
            : await SuccessAsync(entity, user.Id, cancellationToken);
    }

    public async Task<EntityCardResult> CreateAsync(
        ClaimsPrincipal caller, EntityCardData card, CancellationToken cancellationToken)
    {
        var check = EntityCardValidator.Validate(card);
        if (!check.IsValid)
        {
            return new EntityCardResult(EntityCardOutcome.Invalid, Errors: check.Problems);
        }

        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return new EntityCardResult(EntityCardOutcome.NotFound);
        }

        // Asked first so the answer is the kind one ("already registered,
        // ask to join"); ux_entities_nip_active is what holds when two
        // people found the same organisation in the same moment.
        if (await NipTakenAsync(check.Card!.Nip, except: null, cancellationToken))
        {
            return new EntityCardResult(EntityCardOutcome.NipTaken);
        }

        // The card and its founder in ONE SaveChanges: a card nobody is a
        // member of could never be reached again.
        var entity = new Entity();
        EntitySnapshots.Apply(entity, check.Card!);
        context.Entities.Add(entity);
        context.EntityMembers.Add(new EntityMember { Entity = entity, UserId = user.Id, IsFounder = true });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsNipTaken(exception))
        {
            return new EntityCardResult(EntityCardOutcome.NipTaken);
        }

        await context.Entry(entity).ReloadAsync(cancellationToken);
        return await SuccessAsync(entity, user.Id, cancellationToken);
    }

    public async Task<EntityCardResult> UpdateAsync(
        ClaimsPrincipal caller, Guid entityId, EntityCardData card, CancellationToken cancellationToken)
    {
        if (await userManager.GetUserAsync(caller) is not { } user)
        {
            return new EntityCardResult(EntityCardOutcome.NotFound);
        }

        var entity = await MemberEntityAsync(user.Id, entityId, tracked: true, cancellationToken);
        if (entity is null)
        {
            return new EntityCardResult(EntityCardOutcome.NotFound);
        }

        var check = EntityCardValidator.Validate(card);
        if (!check.IsValid)
        {
            return new EntityCardResult(EntityCardOutcome.Invalid, Errors: check.Problems);
        }

        // A correction must not walk this card onto another card's NIP.
        if (await NipTakenAsync(check.Card!.Nip, except: entity.Id, cancellationToken))
        {
            return new EntityCardResult(EntityCardOutcome.NipTaken);
        }

        // The type is free to change (T-94): every submitted application
        // froze the kind it was submitted as (applications.applicant_type),
        // and that is what its evaluation reads, not this card.
        EntitySnapshots.Apply(entity, check.Card!);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsNipTaken(exception))
        {
            return new EntityCardResult(EntityCardOutcome.NipTaken);
        }

        await context.Entry(entity).ReloadAsync(cancellationToken);
        return await SuccessAsync(entity, user.Id, cancellationToken);
    }

    private async Task<Entity?> MemberEntityAsync(
        Guid userId, Guid entityId, bool tracked, CancellationToken cancellationToken)
    {
        if (!await ResourceOwnership.ActsForAsync(context, userId, entityId, cancellationToken))
        {
            return null;
        }

        var entities = tracked ? context.Entities : context.Entities.AsNoTracking();
        return await entities.SingleOrDefaultAsync(x => x.Id == entityId && x.IsActive, cancellationToken);
    }

    private Task<bool> NipTakenAsync(string? nip, Guid? except, CancellationToken cancellationToken) =>
        nip is null
            ? Task.FromResult(false)
            : context.Entities.AnyAsync(x => x.Nip == nip && x.IsActive && x.Id != except, cancellationToken);

    private static bool IsNipTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_entities_nip_active",
        };

    private async Task<EntityCardResult> SuccessAsync(Entity entity, Guid callerId, CancellationToken cancellationToken)
    {
        var members = await context.EntityMembers.AsNoTracking()
            .Where(x => x.EntityId == entity.Id && x.IsActive)
            .Join(context.Users, member => member.UserId, account => account.Id,
                (member, account) => new { member, account })
            .OrderByDescending(x => x.member.IsFounder)
            .ThenBy(x => x.member.CreatedAt)
            .Select(x => new
            {
                x.member.UserId,
                Response = new EntityMemberResponse(
                    x.account.FirstName, x.account.LastName, x.member.IsFounder, x.member.CreatedAt),
            })
            .ToListAsync(cancellationToken);

        var isFounder = members.Any(x => x.UserId == callerId && x.Response.IsFounder);

        return new EntityCardResult(
            EntityCardOutcome.Succeeded,
            new EntityCardResponse(
                entity.Id,
                entity.UpdatedAt,
                EntitySnapshots.ToData(entity),
                isFounder,
                members.Select(x => x.Response).ToList()));
    }
}
