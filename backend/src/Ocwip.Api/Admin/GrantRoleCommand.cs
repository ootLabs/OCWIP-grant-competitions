using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;

namespace Ocwip.Api.Admin;

/// <summary>
/// What happened, so the caller decides on the exit code and the wording and
/// this type stays free of both.
/// </summary>
internal enum GrantRoleOutcome
{
    Granted,
    AlreadyHeld,
    AccountNotFound,
    AccountDeactivated,
}

/// <summary>
/// Grants a role to an existing account. This is the only path in the codebase
/// that writes the role column, and it is deliberately not reachable over HTTP:
/// an operator sees the personal data of every organisation, so a screen that
/// hands out the role is also a way to obtain it through a mistake in the
/// authorization rules (docs/reguly-biznesowe.md, docs/architektura.md).
///
/// Security rule 3 in AGENTS.md, do not reveal whether an account exists, does
/// NOT apply here and this comment exists so nobody later "fixes" it: the rule
/// protects registration, login and password reset against a stranger probing
/// addresses. The caller of this command already holds a shell in the backend
/// container and could read the users table directly, so answering "no such
/// address" costs nothing and saves an admin from believing the tool is broken.
/// </summary>
internal static class GrantRoleCommand
{
    public static async Task<GrantRoleOutcome> ExecuteAsync(
        AppDbContext context,
        GrantRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        // Matched on the NORMALIZED column, which is where uniqueness lives
        // since T-12.0. Matching the address as written would report "no such
        // address" for an account that exists whenever the caller typed a
        // different case than the person who registered, and an admin told
        // that a real address is unknown reasonably concludes the tool is
        // broken.
        //
        // Through Identity's own normalizer (Data/EmailNormalizer.cs) rather
        // than an upper casing that looks like it. The normalizer also runs
        // string.Normalize(), so an address typed with a decomposed accent is
        // stored under one string and would be searched for under another.
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        var user = await context.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return GrantRoleOutcome.AccountNotFound;
        }

        // A deactivated account is refused rather than silently promoted: it
        // does not appear on any list of active users, so granting it the
        // operator role would create a privileged account nobody is looking at.
        // Rows are never deleted here (retention is at least 5 years), so this
        // is a state the command will genuinely meet.
        if (!user.IsActive)
        {
            return GrantRoleOutcome.AccountDeactivated;
        }

        // No write when nothing changes, so a repeated run does not move
        // updated_at and make the account look like it was touched.
        if (user.Role == request.Role)
        {
            return GrantRoleOutcome.AlreadyHeld;
        }

        user.Role = request.Role;
        await context.SaveChangesAsync(cancellationToken);

        return GrantRoleOutcome.Granted;
    }
}
