using Microsoft.AspNetCore.Authorization;

namespace Ocwip.Api.Authorization;

/// <summary>
/// "You may see this particular resource" (T-13.2).
///
/// A role attribute cannot express this and the card says why: two applicants
/// hold the SAME role and different rights to the same application, so the
/// answer depends on the resource, not only on the claim. That is what makes
/// this a requirement with a handler rather than a policy over a role.
/// </summary>
public sealed class EntityScopedRequirement : IAuthorizationRequirement;
