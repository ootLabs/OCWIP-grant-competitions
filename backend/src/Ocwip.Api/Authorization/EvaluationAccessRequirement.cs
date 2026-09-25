using Microsoft.AspNetCore.Authorization;

namespace Ocwip.Api.Authorization;

/// <summary>
/// "You may read, or fill in, THIS evaluation" (T-38). Not the entity scoped
/// requirement: an evaluation belongs to no Podmiot the applicant could own,
/// and the applicant does not see it at all until the operator publishes the
/// cards (report, step 5.5, a later card).
/// </summary>
/// <param name="Write">Saving or finishing, as opposed to reading.</param>
public sealed record EvaluationAccessRequirement(bool Write) : IAuthorizationRequirement;
