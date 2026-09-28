using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Admin;

/// <summary>
/// The team's accounts from the server's shell (T-104), beside grant-role:
/// deactivate-account ends an account and every session it has, and
/// list-accounts shows the staff with their roles. Both run in the runtime
/// image as <c>dotnet Ocwip.Api.dll &lt;verb&gt;</c>, without the SDK, because
/// Program hands every verb to AdminCommandRunner before a web host exists.
///
/// Neither goes over HTTP, for the reason roles do not (docs/architektura.md,
/// "Rola operatora nadawana komendą"): a screen that changes accounts is a way
/// to obtain one through a mistake in the authorization rules.
/// </summary>
internal static class AccountCommands
{
    public const string DeactivateVerb = "deactivate-account";
    public const string ListVerb = "list-accounts";

    public const string Usage = """
        Usage:
          dotnet Ocwip.Api.dll deactivate-account --email <address>
          dotnet Ocwip.Api.dll list-accounts [--role <Operator|Reviewer>]

        deactivate-account marks the account inactive (nothing is deleted)
        and ends its sessions. list-accounts prints the OCWIP team: operators
        and experts, with their role and state; applicants are not listed.
        """;

    private static readonly Role[] Staff = [Role.Operator, Role.Reviewer];

    public static async Task<int> RunAsync(string[] args, IConfiguration configuration, TextWriter output, CancellationToken cancellationToken)
    {
        var options = Options(args, out var error);
        if (options is null)
        {
            await output.WriteLineAsync(error);
            await output.WriteLineAsync();
            await output.WriteLineAsync(Usage);
            return AdminCommandRunner.Failure;
        }

        AppDbContext context;
        try
        {
            context = AppDbContextFactory.Create(configuration);
        }
        catch (InvalidOperationException exception)
        {
            await output.WriteLineAsync(exception.Message);
            return AdminCommandRunner.Failure;
        }

        await using (context)
        {
            try
            {
                return args[0] == DeactivateVerb
                    ? await DeactivateAsync(context, options, output, cancellationToken)
                    : await ListAsync(context, options, output, cancellationToken);
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException or DbUpdateException)
            {
                await output.WriteLineAsync("The database call failed; nothing was changed. " + exception.GetBaseException().Message);
                return AdminCommandRunner.Failure;
            }
        }
    }

    private static async Task<int> DeactivateAsync(
        AppDbContext context, IReadOnlyDictionary<string, string> options, TextWriter output, CancellationToken cancellationToken)
    {
        if (!options.TryGetValue("--email", out var email))
        {
            await output.WriteLineAsync("--email is missing.");
            return AdminCommandRunner.Failure;
        }

        var normalized = EmailNormalizer.Normalize(email);
        var user = await context.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken);
        if (user is null)
        {
            await output.WriteLineAsync($"No account has the address {email}. Nothing was changed.");
            return AdminCommandRunner.Failure;
        }

        if (!user.IsActive)
        {
            await output.WriteLineAsync($"The account {email} is already inactive. Nothing was changed.");
            return AdminCommandRunner.Success;
        }

        user.IsActive = false;
        user.DeactivatedAt = DateTimeOffset.UtcNow;

        // A new stamp ends every session the account has: the cookie is
        // checked against it (docs/architektura.md, the logout that ends the
        // session on the server).
        user.SecurityStamp = Guid.NewGuid().ToString();
        await context.SaveChangesAsync(cancellationToken);

        await output.WriteLineAsync($"Deactivated {email} ({user.Role}); its sessions are ended. The account and its records stay.");
        return AdminCommandRunner.Success;
    }

    private static async Task<int> ListAsync(
        AppDbContext context, IReadOnlyDictionary<string, string> options, TextWriter output, CancellationToken cancellationToken)
    {
        var roles = Staff;
        if (options.TryGetValue("--role", out var role))
        {
            var match = Staff.Where(x => string.Equals(x.ToString(), role, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (match.Length == 0)
            {
                await output.WriteLineAsync($"--role must be {string.Join(" or ", Staff)}; applicants are not listed here.");
                return AdminCommandRunner.Failure;
            }

            roles = match;
        }

        var accounts = await context.Users.AsNoTracking()
            .Where(x => roles.Contains(x.Role))
            .OrderBy(x => x.Role).ThenBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new { x.Email, x.FirstName, x.LastName, x.Role, x.IsActive })
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            await output.WriteLineAsync(
                $"{account.Role,-9} {(account.IsActive ? "active  " : "inactive")} {account.Email} ({account.FirstName} {account.LastName})".TrimEnd());
        }

        await output.WriteLineAsync($"{accounts.Count} accounts.");
        return AdminCommandRunner.Success;
    }

    /// <summary>--name value pairs after the verb; null with the reason when they do not parse.</summary>
    private static Dictionary<string, string>? Options(string[] args, out string error)
    {
        var allowed = args[0] == DeactivateVerb ? new[] { "--email" } : new[] { "--role" };
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];
            if (!allowed.Contains(option))
            {
                error = $"Unknown option {option}.";
                return null;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"{option} has no value.";
                return null;
            }

            if (!options.TryAdd(option, args[index + 1]))
            {
                error = $"{option} was given twice.";
                return null;
            }
        }

        error = string.Empty;
        return options;
    }
}
