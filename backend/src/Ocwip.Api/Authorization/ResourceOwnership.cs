using Ocwip.Api.Models;

namespace Ocwip.Api.Authorization;

/// <summary>
/// The one and only answer to "does this resource belong to this caller"
/// (T-13.2).
///
/// It is a single method on purpose, and docs/runbook/rozbieznosci.md says why
/// under R-01: the report describes access attached to an ORGANISATION rather
/// than to a person, with several people sharing one Podmiot through a join
/// table. Today the schema wires one account to one Podmiot
/// (users.entity_id, unique), so the check is a comparison. After R-01 it
/// becomes a membership lookup. If the comparison were spelled out at every
/// call site, that change would be a hunt through the services; here it is
/// this method and nothing else, which is exactly what R-01 asks for when it
/// says not to scatter user.EntityId around.
/// </summary>
internal static class ResourceOwnership
{
    /// <summary>
    /// The Podmiot the caller acts for, or null when there is none. Every
    /// service that needs "the caller's own Podmiot" asks here rather than
    /// reading user.EntityId (T-93), so R-01 changes this method and nothing
    /// else.
    /// </summary>
    public static Guid? EntityIdOf(User caller) => caller.EntityId;

    public static bool BelongsTo(User caller, IEntityScoped resource)
    {
        // An account with no Podmiot owns nothing, said out loud by the
        // pattern rather than left to a comparison with a nullable: a new
        // account has none until its first application (T-93).
        return EntityIdOf(caller) is { } entityId && entityId == resource.EntityId;
    }
}
