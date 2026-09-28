using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Authorization;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.EntityCards;

public enum EntityCardOutcome
{
    Succeeded,
    /// <summary>The caller has no card yet: GET and PUT answer 404, and the front offers the empty one.</summary>
    NotFound,
    /// <summary>POST when the caller already has a card, including a second POST racing the first.</summary>
    AlreadyExists,
    Invalid,
}

public sealed record EntityCardResult(
    EntityCardOutcome Outcome,
    EntityCardResponse? Card = null,
    IDictionary<string, string[]>? Errors = null);

public interface IEntityCardService
{
    Task<EntityCardResult> GetAsync(ClaimsPrincipal caller, CancellationToken cancellationToken);

    Task<EntityCardResult> CreateAsync(ClaimsPrincipal caller, EntityCardData card, CancellationToken cancellationToken);

    Task<EntityCardResult> UpdateAsync(ClaimsPrincipal caller, EntityCardData card, CancellationToken cancellationToken);
}

/// <summary>
/// The caller's own Podmiot card (T-93): created at the first application,
/// corrected from then on. Always the caller's own, found through
/// <see cref="ResourceOwnership.EntityIdOf"/>: no route carries an entity id,
/// so there is no other organisation's card to ask for.
///
/// A correction never touches a submitted application, which reads its own
/// copy (Application.EntitySnapshot).
/// </summary>
internal sealed class EntityCardService(AppDbContext context, UserManager<User> userManager) : IEntityCardService
{
    public async Task<EntityCardResult> GetAsync(ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var entity = await OwnEntityAsync(caller, tracked: false, cancellationToken);

        return entity is null
            ? new EntityCardResult(EntityCardOutcome.NotFound)
            : Success(entity);
    }

    public async Task<EntityCardResult> CreateAsync(
        ClaimsPrincipal caller, EntityCardData card, CancellationToken cancellationToken)
    {
        var check = EntityCardValidator.Validate(card);
        if (!check.IsValid)
        {
            return new EntityCardResult(EntityCardOutcome.Invalid, Errors: check.Problems);
        }

        // UserManager's store shares this scoped context, so the account it
        // finds is tracked here and the new Podmiot and the account pointing
        // at it go out in ONE SaveChanges below (not UserManager.UpdateAsync,
        // which would save on its own). The concurrency stamp is what refuses
        // the second of two POSTs racing each other: without it both would
        // insert a Podmiot, the last write would win and the first would be
        // left owned by nobody.
        var user = await userManager.GetUserAsync(caller);

        if (user is null || ResourceOwnership.EntityIdOf(user) is not null)
        {
            return new EntityCardResult(EntityCardOutcome.AlreadyExists);
        }

        var entity = new Entity();
        EntitySnapshots.Apply(entity, check.Card!);
        context.Entities.Add(entity);

        user.Entity = entity;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new EntityCardResult(EntityCardOutcome.AlreadyExists);
        }

        await context.Entry(entity).ReloadAsync(cancellationToken);
        return Success(entity);
    }

    public async Task<EntityCardResult> UpdateAsync(
        ClaimsPrincipal caller, EntityCardData card, CancellationToken cancellationToken)
    {
        var entity = await OwnEntityAsync(caller, tracked: true, cancellationToken);
        if (entity is null)
        {
            return new EntityCardResult(EntityCardOutcome.NotFound);
        }

        var check = EntityCardValidator.Validate(card);
        if (!check.IsValid)
        {
            return new EntityCardResult(EntityCardOutcome.Invalid, Errors: check.Problems);
        }

        // The type is free to change (T-94): every submitted application
        // froze the kind it was submitted as (applications.applicant_type),
        // and that is what its evaluation reads, not this card.
        EntitySnapshots.Apply(entity, check.Card!);
        await context.SaveChangesAsync(cancellationToken);
        await context.Entry(entity).ReloadAsync(cancellationToken);

        return Success(entity);
    }

    private async Task<Entity?> OwnEntityAsync(
        ClaimsPrincipal caller, bool tracked, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(caller);
        if (user is null || ResourceOwnership.EntityIdOf(user) is not { } entityId)
        {
            return null;
        }

        var entities = tracked ? context.Entities : context.Entities.AsNoTracking();
        return await entities.SingleOrDefaultAsync(x => x.Id == entityId && x.IsActive, cancellationToken);
    }

    private static EntityCardResult Success(Entity entity) =>
        new(
            EntityCardOutcome.Succeeded,
            new EntityCardResponse(entity.Id, entity.UpdatedAt, EntitySnapshots.ToData(entity)));
}
