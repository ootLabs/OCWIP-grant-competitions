using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Gives an account access to a Podmiot card the way the product does
/// (T-93a): the first account becomes the founder, every next one joins
/// through an approved request, because the schema refuses a member who is
/// neither (ck_entity_members_founder_has_no_request).
/// </summary>
internal static class TestMembership
{
    public static async Task GrantAsync(AppDbContext context, Guid entityId, Guid userId)
    {
        var hasFounder = await context.EntityMembers
            .AnyAsync(x => x.EntityId == entityId && x.IsFounder && x.IsActive);

        if (!hasFounder)
        {
            context.EntityMembers.Add(new EntityMember { EntityId = entityId, UserId = userId, IsFounder = true });
            await context.SaveChangesAsync();
            return;
        }

        var founderId = await context.EntityMembers
            .Where(x => x.EntityId == entityId && x.IsFounder && x.IsActive)
            .Select(x => x.UserId)
            .SingleAsync();

        var request = new EntityAccessRequest
        {
            EntityId = entityId,
            RequesterId = userId,
            Status = EntityAccessRequestStatus.Approved,
            DecidedAt = DateTimeOffset.UtcNow,
            DecidedById = founderId,
        };
        context.EntityAccessRequests.Add(request);
        await context.SaveChangesAsync();

        context.EntityMembers.Add(new EntityMember
        {
            EntityId = entityId,
            UserId = userId,
            IsFounder = false,
            AccessRequestId = request.Id,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>The founder of a card, for a test that needs "the applicant" of a scene.</summary>
    public static Task<Guid> FounderOfAsync(AppDbContext context, Guid entityId) =>
        context.EntityMembers
            .Where(x => x.EntityId == entityId && x.IsFounder && x.IsActive)
            .Select(x => x.UserId)
            .SingleAsync();
}
