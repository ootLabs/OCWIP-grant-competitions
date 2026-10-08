using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;

namespace Ocwip.Api.Authorization;

/// <summary>
/// The one and only answer to "does this resource belong to this caller"
/// (T-13.2).
///
/// Access goes with the organisation, not with the person (T-93a, report
/// step 2.2 and decision 7): an account acts for every Podmiot card it is an
/// active member of, and sees everything of those cards, drafts included.
/// T-93 kept this in one place so that the change from a column on the
/// account to a membership table would touch this file and nothing else;
/// every service asks here instead of querying entity_members itself.
/// </summary>
internal static class ResourceOwnership
{
    /// <summary>
    /// The cards the account acts for, as a query to compose into a filter
    /// ("applications of my cards") without loading the ids first.
    /// </summary>
    public static IQueryable<Guid> EntityIdsOf(AppDbContext context, Guid userId) =>
        context.EntityMembers
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => x.EntityId);

    /// <summary>The same set, loaded, for a check repeated over a list.</summary>
    public static async Task<IReadOnlySet<Guid>> EntityIdsOfAsync(
        AppDbContext context, Guid userId, CancellationToken cancellationToken) =>
        await EntityIdsOf(context, userId).ToHashSetAsync(cancellationToken);

    public static Task<bool> ActsForAsync(
        AppDbContext context, Guid userId, Guid entityId, CancellationToken cancellationToken) =>
        EntityIdsOf(context, userId).AnyAsync(x => x == entityId, cancellationToken);

    /// <summary>
    /// An account with no card owns nothing, said out loud by the empty set:
    /// a new account has none until its first application (T-93).
    /// </summary>
    public static bool BelongsTo(IReadOnlySet<Guid> callerEntityIds, IEntityScoped resource) =>
        callerEntityIds.Contains(resource.EntityId);
}
