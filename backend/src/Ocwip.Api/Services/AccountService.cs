using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Registration. Deliberately the only write path that creates an account.
/// </summary>
internal sealed class AccountService(UserManager<User> userManager)
    : IAccountService
{
    /// <summary>
    /// Named by T-12.0 in UserConfiguration.cs, and asserted there by
    /// AccountConfigurationTests, so renaming the index fails that test and
    /// leads whoever renamed it here.
    /// </summary>
    private const string EmailIndex = "ix_users_normalized_email";

    private const string UniqueViolation = "23505";

    public async Task<RegistrationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // UserManager exposes no overload that takes a token, so this is the
        // only honest thing left to do with one: stop before the write rather
        // than accept a token and ignore it. A caller that gave up gets no
        // account created behind its back.
        cancellationToken.ThrowIfCancellationRequested();

        var user = new User
        {
            Email = request.Email,
            // Identity insists on a username and this product has no such
            // concept, so it mirrors the address (Models/User.cs). UserManager
            // computes both normalized columns from these two, which is why
            // nothing here normalizes anything by hand.
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        // Role and EmailConfirmed are left untouched on purpose. Applicant
        // comes from the entity and from the column default, false likewise,
        // and setting either here would be the same decision written a third
        // time. More importantly, no endpoint may ever choose a role: an
        // operator sees the personal data of every organisation, see
        // Models/Role.cs and docs/architektura.md.

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (IsAddressTaken(exception))
        {
            // RequireUniqueEmail made UserManager check for a duplicate with a
            // SELECT before this INSERT, and that check loses the race against
            // a second registration arriving in the same moment. The unique
            // index answered instead. Same outcome either way, which is the
            // point: the caller cannot tell the two apart, and neither can they
            // tell either from a success.
            return RegistrationResult.Accepted;
        }

        if (result.Succeeded)
        {
            return RegistrationResult.Accepted;
        }

        // Duplicate errors are DROPPED rather than reported. What survives is
        // about the request itself, not about who already holds an account, so
        // it is safe to hand back. If nothing survives, the only problem was
        // that the address is taken, and security rule 3 says that answer has
        // to be indistinguishable from success.
        var reportable = result.Errors
            .Where(error => !IsDuplicate(error))
            .Select(error => error.Description)
            .ToList();

        return reportable.Count == 0
            ? RegistrationResult.Accepted
            : RegistrationResult.Rejected(reportable);
    }

    /// <summary>
    /// Both codes, because UserName mirrors the address: a second registration
    /// on one address trips the e-mail rule and the username rule at once.
    /// </summary>
    private static bool IsDuplicate(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName);

    /// <summary>
    /// Narrow on purpose. Any other unique violation, the one to one index on
    /// entity_id for instance, is a real failure and must not be swallowed as
    /// "that address is taken".
    /// </summary>
    private static bool IsAddressTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == UniqueViolation
        && postgres.ConstraintName == EmailIndex;
}
