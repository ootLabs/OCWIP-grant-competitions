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
        // Matched literally, because the unique index on the address is case
        // sensitive too: two accounts differing only in case can legally
        // exist today, so matching loosely would either grant the role
        // against a spelling the login path treats as a different account,
        // or find two rows and throw.
        //
        // Normalizing the address is no longer an open question: T-12.0 owns
        // it and moves the unique index onto the normalized column. When that
        // lands, this lookup moves with it, because a literal match against a
        // case insensitively unique column reports "no such address" for an
        // account that exists.
        var user = await context.Users.SingleOrDefaultAsync(
            x => x.Email == request.Email,
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
