using Ocwip.Api.Models;

namespace Ocwip.Api.Admin;

/// <summary>
/// What was asked for on the command line. Produced by
/// <see cref="AdminCommandLine"/>, carried out by <see cref="GrantRoleCommand"/>.
/// </summary>
internal sealed record GrantRoleRequest(string Email, Role Role);

/// <summary>
/// Parses the administrative command line. No console and no database: this
/// type only decides what was asked for, so it can be tested without either.
///
/// It runs before WebApplication.CreateBuilder, and that ordering is a
/// requirement rather than a preference: a command that grants a role has no
/// business building a web host, opening a listening socket or running startup
/// migrations on its way to a single UPDATE.
///
/// English, like the startup error in Data/AppDbContextFactory.cs and unlike the
/// product UI: the reader of these messages is whoever already has a shell in
/// the backend container.
/// </summary>
internal static class AdminCommandLine
{
    /// <summary>
    /// The only verb. Kebab case, like every other externally visible name in
    /// this repository (docs/konwencje.md).
    /// </summary>
    public const string GrantRoleVerb = "grant-role";

    private const string EmailOption = "--email";
    private const string RoleOption = "--role";

    public const string Usage = """
        Usage:
          dotnet run --project src/Ocwip.Api/Ocwip.Api.csproj --no-launch-profile \
            -- grant-role --email <address> --role <Applicant|Operator|Reviewer>

        Grants a role to an existing account. The address is matched literally
        and is case sensitive, because the unique index on it is. The command
        never creates an account and never reactivates a deactivated one.
        """;

    /// <summary>
    /// True when argv asks for an administrative command instead of the web
    /// host: a first argument that is a verb rather than a setting.
    ///
    /// Deliberately wider than "equals grant-role". Matching the verb exactly
    /// here would send a typo (grant_role, grantrole) on to the web host, which
    /// inside the backend container means a SECOND api process booting: it takes
    /// the exclusive lock on the migrations history, runs startup migrations and
    /// then fights the running instance for port 8080. Somebody who mistyped a
    /// role grant has to get an error, not a deployment. Anything that looks
    /// like a verb is routed here, and ParseGrantRole rejects the ones it does
    /// not know.
    ///
    /// Dash and slash both start a configuration key (--urls, /urls), so an
    /// argument beginning with either is a setting for the host and never a
    /// command.
    /// </summary>
    public static bool IsAdminInvocation(string[] args) =>
        args.Length > 0
        && !args[0].StartsWith('-')
        && !args[0].StartsWith('/');

    /// <summary>
    /// Returns the parsed request, or null with <paramref name="error"/> set to
    /// a message naming the single thing that is wrong.
    /// </summary>
    public static GrantRoleRequest? ParseGrantRole(string[] args, out string error)
    {
        // The verb itself, checked here rather than in IsAdminInvocation, which
        // routes every verb this way precisely so an unknown one reaches this
        // message instead of the web host.
        if (args.Length == 0 || args[0] != GrantRoleVerb)
        {
            error = $"Unknown command. The only one is {GrantRoleVerb}.";
            return null;
        }

        string? email = null;
        string? role = null;

        for (var index = 1; index < args.Length; index += 2)
        {
            var option = args[index];

            if (index + 1 >= args.Length)
            {
                error = $"{option} has no value.";
                return null;
            }

            var value = args[index + 1];

            // A missed value would otherwise be read as the next option, and the
            // complaint would then name an argument the caller did type.
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"{option} has no value: {value} is another option.";
                return null;
            }

            switch (option)
            {
                // Refused rather than overwritten. Last one wins would swallow a
                // mistyped address, and the address decides whose account gains
                // the privilege.
                case EmailOption when email is not null:
                case RoleOption when role is not null:
                    error = $"{option} was given twice.";
                    return null;
                case EmailOption:
                    email = value;
                    break;
                case RoleOption:
                    role = value;
                    break;
                default:
                    error = $"Unknown option {option}.";
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            error = $"{EmailOption} is missing.";
            return null;
        }

        if (role is null)
        {
            error = $"{RoleOption} is missing.";
            return null;
        }

        // Matched by name rather than with Enum.TryParse, which also accepts "1"
        // and would hand the Operator role to somebody who typed a digit.
        var match = Enum.GetNames<Role>()
            .SingleOrDefault(x =>
                string.Equals(x, role, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            error =
                $"Unknown role {role}. Available: "
                + $"{string.Join(", ", Enum.GetNames<Role>())}.";
            return null;
        }

        error = string.Empty;

        // Not trimmed. The address is matched literally against a case sensitive
        // unique index, and a Trim here would quietly break that both ways: an
        // account whose stored address really does carry a trailing space would
        // be unreachable, while a caller who typed one would match a different
        // row than the characters they passed. The shell already splits on
        // whitespace, so trimming buys nothing anyway.
        return new GrantRoleRequest(email, Enum.Parse<Role>(match));
    }
}
