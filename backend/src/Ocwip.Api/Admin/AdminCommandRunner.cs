using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;

namespace Ocwip.Api.Admin;

/// <summary>
/// Wires the administrative command to the outside world: configuration in,
/// text and an exit code out. Parsing lives in <see cref="AdminCommandLine"/>
/// and the operation in <see cref="GrantRoleCommand"/>, so neither of those
/// needs a console.
///
/// The exit code matters as much as the message. A script wrapping this has no
/// other way to tell a granted role from an address that matched nothing.
/// </summary>
internal static class AdminCommandRunner
{
    public const int Success = 0;
    public const int Failure = 1;

    public static async Task<int> RunAsync(
        string[] args,
        IConfiguration configuration,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        var request = AdminCommandLine.ParseGrantRole(args, out var error);

        if (request is null)
        {
            await output.WriteLineAsync(error);
            await output.WriteLineAsync();
            await output.WriteLineAsync(AdminCommandLine.Usage);
            return Failure;
        }

        AppDbContext context;

        try
        {
            // The same connection string, from the same sources, that the API
            // and dotnet ef read. Create throws instead of guessing an address,
            // and that message is already the useful one, so it is printed
            // rather than wrapped in a stack trace nobody reads.
            context = AppDbContextFactory.Create(configuration);
        }
        catch (InvalidOperationException exception)
        {
            await output.WriteLineAsync(exception.Message);
            return Failure;
        }

        await using (context)
        {
            GrantRoleOutcome outcome;

            try
            {
                outcome = await GrantRoleCommand.ExecuteAsync(
                    context,
                    request,
                    cancellationToken);
            }
            // The account changed between the read and the write, so the
            // update matched no row. ConcurrencyStamp is a concurrency token
            // (Data/Configurations/UserConfiguration.cs), which is what makes
            // this reachable at all, and it does NOT arrive here as a
            // DbException or an InvalidOperationException: DbUpdateException
            // derives from neither, so the filter below would let it out as an
            // unhandled exception and the wrapping script would read 134
            // instead of Failure.
            //
            // Its own message rather than EF's, which counts affected rows and
            // says nothing an admin can act on. Nothing was granted and the
            // command is safe to repeat, so that is what it says.
            catch (DbUpdateConcurrencyException)
            {
                await output.WriteLineAsync(
                    "No role was granted: the account changed while the command "
                    + "was running, so nothing was written. Run the command "
                    + "again.");
                return Failure;
            }
            // An unreachable database, one whose migrations have not been
            // applied, or a write the database refuses, must not escape as an
            // unhandled exception. That exits with 134 and a stack trace, and
            // 134 is not the Failure this promises to a script reading the exit
            // code. EF wraps a transient connection failure in an
            // InvalidOperationException rather than letting the DbException
            // through, and a rejected write in a DbUpdateException, so all
            // three are caught; the missing connection string above is a
            // separate case, already handled.
            catch (Exception exception)
                when (exception is DbException
                    or InvalidOperationException
                    or DbUpdateException)
            {
                await output.WriteLineAsync(
                    "No role was granted: the database call failed. "
                    + exception.GetBaseException().Message);
                return Failure;
            }

            await output.WriteLineAsync(Describe(outcome, request));

            return outcome is GrantRoleOutcome.Granted or GrantRoleOutcome.AlreadyHeld
                ? Success
                : Failure;
        }
    }

    private static string Describe(GrantRoleOutcome outcome, GrantRoleRequest request) =>
        outcome switch
        {
            GrantRoleOutcome.Granted =>
                $"{request.Email} now holds the {request.Role} role.",
            GrantRoleOutcome.AlreadyHeld =>
                $"{request.Email} already held the {request.Role} role. Nothing changed.",
            GrantRoleOutcome.AccountNotFound =>
                $"No account with the address {request.Email}. The address is "
                + "matched without regard to case, so this is not a capital "
                + "letter typed differently.",
            GrantRoleOutcome.AccountDeactivated =>
                $"The account {request.Email} is deactivated, so no role was "
                + "granted. Reactivate it first.",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
}
