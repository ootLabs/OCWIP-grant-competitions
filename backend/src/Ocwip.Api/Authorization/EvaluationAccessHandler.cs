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
            //
            // An applicant account appointed to a committee is an expert the
            // same way (R-44); the appointment, the assignment and the
            // declaration (T-40a) are what count, not the role.
            case Role.Reviewer:
            case Role.Applicant:
                if (evaluation.Stage == EvaluationStage.Merit
                    && evaluation.AuthorUserId == user.Id
                    && await ExpertAppointments.MayEvaluateAsync(
                        dbContext, user.Id, evaluation.ApplicationId, CancellationToken.None))
                {
                    context.Succeed(requirement);
                }

                return;

            // Any role added later is refused until somebody writes its rule.
            default:
                return;
        }
    }
}
