using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Reads the same set of accounts CompetitionService.FindContactsAsync
/// validates a contact against: role Operator, IsActive. A picker offering an
/// account that the save would then refuse is worse than no picker at all.
/// </summary>
internal sealed class OperatorDirectoryService : IOperatorDirectoryService
{
    private readonly AppDbContext _context;

    public OperatorDirectoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OperatorAccountResponse>> ListOperatorsAsync(
        CancellationToken cancellationToken) =>
        await _context.Users
            .Where(user => user.Role == Role.Operator && user.IsActive)
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new OperatorAccountResponse(
                user.Id, user.FirstName, user.LastName, user.Email!))
            .ToListAsync(cancellationToken);
}
