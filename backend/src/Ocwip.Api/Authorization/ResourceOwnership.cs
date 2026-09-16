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
    public static bool BelongsTo(User caller, IEntityScoped resource)
    {
        // An account with no Podmiot owns nothing, said out loud rather than
        // left to the comparison below. Every account looks like this today,
        // because registration does not create a Podmiot yet (B-09), and the
        // resource side of the comparison is a non-nullable Guid: reading
        // .Value without this guard would throw on every one of them and turn
        // a plain refusal into a 500. Refusing is also the right answer on its
        // own terms, so the guard is not only a null check.
        if (caller.EntityId is null)
        {
            return false;
        }

        return caller.EntityId.Value == resource.EntityId;
    }
}
