using Microsoft.AspNetCore.Authorization;

namespace Ocwip.Api.Authorization;

/// <summary>
/// "You may see this particular resource" (T-13.2).
///
/// A role attribute cannot express this and the card says why: two applicants
/// hold the SAME role and different rights to the same application, so the
/// answer depends on the resource, not only on the claim. That is what makes
/// this a requirement with a handler rather than a policy over a role.
///
/// <paramref name="ExpertsToo"/> says whether an assigned expert's way in
/// counts (R-44). It does for reading the application being evaluated; it
/// never does for what only the applicant's organisation may do (saving,
/// submitting, attachments, the report, the shared cards), because an
/// applicant account appointed as an expert carries both role claims and
/// the role policy alone cannot tell the two apart.
/// </summary>
public sealed record EntityScopedRequirement(bool ExpertsToo) : IAuthorizationRequirement;
