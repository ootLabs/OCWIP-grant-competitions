using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <inheritdoc cref="IApplicationOverviewService"/>
internal sealed class ApplicationOverviewService : IApplicationOverviewService
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;

    public ApplicationOverviewService(AppDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<ApplicationOverviewResult> ListForCallerAsync(
        ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(caller);

        if (user?.EntityId is not { } entityId)
        {
            return new ApplicationOverviewResult(ApplicationOverviewOutcome.NoEntity);
        }

        // IsActive only: a deactivated draft is the applicant's own
        // DeactivateAsync (T-29), which the card promises will make it
        // disappear from exactly this list, not just stop it from being
        // edited.
        var applications = await _context.Applications
            .AsNoTracking()
            .Include(x => x.Competition)
            .Where(x => x.EntityId == entityId && x.IsActive)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new ApplicationOverviewResponse(
                x.Id,
                x.CompetitionId,
                x.Competition.Number,
                x.Competition.Title,
                x.Status,
                x.Number,
                x.SubmittedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new ApplicationOverviewResult(
            ApplicationOverviewOutcome.Succeeded, applications);
    }
}
