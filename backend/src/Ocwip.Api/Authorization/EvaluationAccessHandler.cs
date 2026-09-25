using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Authorization;

/// <summary>
/// Who may read or fill in one evaluation (T-38), the whole role table in one
/// switch like EntityScopedHandler. Only ever calls Succeed, so a case not
/// written here is a refusal.
/// </summary>
internal sealed class EvaluationAccessHandler(UserManager<User> userManager, AppDbContext dbContext)
    : AuthorizationHandler<EvaluationAccessRequirement, Evaluation>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EvaluationAccessRequirement requirement,
        Evaluation evaluation)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null || !user.IsActive)
        {
            return;
        }

        switch (user.Role)
        {
            // The operator runs the evaluation, so reads every card, but fills
            // in only the formal one: merit points are the experts' (report,
            // 5.4). Entering a paper committee's card on its behalf is the
            // AuthorName path, which has no screen in the MVP.
            case Role.Operator:
                if (!requirement.Write || evaluation.Stage == EvaluationStage.Formal)
                {
                    context.Succeed(requirement);
                }

                return;

            // An expert sees their own card and nobody else's, not even
            // another expert's card of the same application: the two
            // evaluations are independent (regulamin 2026, "2 niezależnych
            // członków"). And only while still assigned: an operator who
            // withdraws the assignment withdraws the card with it (T-37).
            case Role.Reviewer:
                if (evaluation.Stage == EvaluationStage.Merit
                    && evaluation.AuthorUserId == user.Id
                    && await dbContext.ApplicationAssignments.AnyAsync(
                        a => a.ApplicationId == evaluation.ApplicationId
                            && a.ReviewerId == user.Id
                            && a.IsActive)
                    // T-40a: the declaration gates the card as it gates the application.
                    && await dbContext.ReviewerDeclarations.AnyAsync(
                        d => d.CompetitionId == evaluation.CompetitionId
                            && d.ReviewerId == user.Id
                            && d.Accepted
                            && d.IsActive))
                {
                    context.Succeed(requirement);
                }

                return;

            // The applicant, and any role added later, is refused.
            default:
                return;
        }
    }
}
