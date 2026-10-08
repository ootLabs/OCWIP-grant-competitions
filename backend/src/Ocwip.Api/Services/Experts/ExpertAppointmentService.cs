using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Authorization;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Experts;

public enum ExpertOutcome
{
    /// <summary>An existing account was appointed, or already was.</summary>
    Appointed,
    /// <summary>No account had this address: one was made and invited.</summary>
    Invited,
    CompetitionNotFound,
    InvalidAddress,
    /// <summary>No account with this address and no name to invite it with.</summary>
    NameRequired,
    /// <summary>An operator's account, or a deactivated one: not an expert.</summary>
    NotAllowed,
    NotAppointed,
    /// <summary>Withdrawal refused: applications of this competition are still assigned to them.</summary>
    StillAssigned,
}

public sealed record ExpertResult(ExpertOutcome Outcome, CompetitionExpertResponse? Expert = null);

public interface IExpertAppointmentService
{
    Task<IReadOnlyList<CompetitionExpertResponse>?> ListAsync(Guid competitionId, CancellationToken cancellationToken);

    Task<ExpertResult> AppointAsync(
        ClaimsPrincipal caller, Guid competitionId, AppointExpertRequest request, CancellationToken cancellationToken);

    Task<ExpertResult> WithdrawAsync(Guid competitionId, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// The committee of one competition (R-44, report step 5.1): the operator
/// finds a person by address and appoints them, or invites somebody without
/// an account. Any applicant account may be appointed, which is how the chair
/// of a foundation evaluates in a competition their foundation does not enter;
/// the conflict of interest is refused per application, at assignment.
/// </summary>
internal sealed class ExpertAppointmentService(
    AppDbContext context,
    UserManager<User> userManager,
    IEmailSender email,
    IConfiguration configuration,
    ILogger<ExpertAppointmentService> logger) : IExpertAppointmentService
{
    public async Task<IReadOnlyList<CompetitionExpertResponse>?> ListAsync(
        Guid competitionId, CancellationToken cancellationToken)
    {
        if (!await context.Competitions.AnyAsync(x => x.Id == competitionId, cancellationToken))
        {
            return null;
        }

        return await context.CompetitionExperts.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.IsActive)
            .OrderBy(x => x.User.LastName).ThenBy(x => x.User.FirstName)
            .Select(x => new CompetitionExpertResponse(
                x.UserId,
                x.User.FirstName,
                x.User.LastName,
                x.User.Email ?? string.Empty,
                x.CreatedAt,
                !x.User.EmailConfirmed,
                context.ApplicationAssignments.Count(
                    a => a.ReviewerId == x.UserId && a.IsActive && a.Application.CompetitionId == competitionId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpertResult> AppointAsync(
        ClaimsPrincipal caller, Guid competitionId, AppointExpertRequest request, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .Where(x => x.Id == competitionId)
            .Select(x => new { x.Id, x.Title })
            .SingleOrDefaultAsync(cancellationToken);

        if (competition is null)
        {
            return new ExpertResult(ExpertOutcome.CompetitionNotFound);
        }

        var address = request.Email?.Trim();
        if (!AccountInput.IsAddress(address) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(address))
        {
            return new ExpertResult(ExpertOutcome.InvalidAddress);
        }

        var operatorId = userManager.GetUserId(caller) is { } raw && Guid.TryParse(raw, out var parsed) ? parsed : (Guid?)null;
        var person = await userManager.FindByEmailAsync(address);
        var invited = false;

        if (person is null)
        {
            var first = request.FirstName?.Trim();
            var last = request.LastName?.Trim();
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last))
            {
                return new ExpertResult(ExpertOutcome.NameRequired);
            }

            // A random password nobody is told: the schema wants a hash, and
            // the owner of the address sets their own through the invitation
            // link, which also confirms the address (PasswordResetService).
            person = new User
            {
                Email = address,
                UserName = address,
                FirstName = first,
                LastName = last,
                Role = Role.Applicant,
                EmailConfirmed = false,
            };

            var created = await userManager.CreateAsync(person, UnknownPassword());
            if (!created.Succeeded)
            {
                logger.LogWarning("Expert invitation for competition {CompetitionId}: the account was not created", competitionId);
                return new ExpertResult(ExpertOutcome.InvalidAddress);
            }

            invited = true;
        }
        else if (!ExpertAppointments.MayBeAppointed(person))
        {
            return new ExpertResult(ExpertOutcome.NotAllowed);
        }

        var appointed = await ExpertAppointments.IsAppointedAsync(context, person.Id, competitionId, cancellationToken);
        if (!appointed)
        {
            context.CompetitionExperts.Add(new CompetitionExpert
            {
                CompetitionId = competitionId,
                UserId = person.Id,
                AppointedById = operatorId,
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
            {
                SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
            })
            {
                // Two clicks in the same moment: the other one appointed them.
                appointed = true;
            }
        }

        if (!appointed)
        {
            await NotifyAsync(person, competition.Title, invited, cancellationToken);
        }

        var expert = (await ListAsync(competitionId, cancellationToken))!.Single(x => x.UserId == person.Id);
        return new ExpertResult(invited ? ExpertOutcome.Invited : ExpertOutcome.Appointed, expert);
    }

    public async Task<ExpertResult> WithdrawAsync(Guid competitionId, Guid userId, CancellationToken cancellationToken)
    {
        var appointment = await context.CompetitionExperts
            .SingleOrDefaultAsync(x => x.CompetitionId == competitionId && x.UserId == userId && x.IsActive, cancellationToken);

        if (appointment is null)
        {
            return new ExpertResult(ExpertOutcome.NotAppointed);
        }

        // Withdrawing somebody with applications still assigned would leave
        // those applications with an expert who can no longer open them.
        // The operator takes the assignments off first, which is a decision
        // about each application (T-37).
        var assigned = await context.ApplicationAssignments.AnyAsync(
            a => a.ReviewerId == userId && a.IsActive && a.Application.CompetitionId == competitionId,
            cancellationToken);

        if (assigned)
        {
            return new ExpertResult(ExpertOutcome.StillAssigned);
        }

        appointment.IsActive = false;
        appointment.DeactivatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new ExpertResult(ExpertOutcome.Appointed);
    }

    /// <summary>Long, random and thrown away: it only has to pass the password policy.</summary>
    private static string UnknownPassword() =>
        $"Aa1-{Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))}";

    private async Task NotifyAsync(User person, string competitionTitle, bool invited, CancellationToken cancellationToken)
    {
        var baseUrl = (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
            ? configured
            : "http://localhost:3000").TrimEnd('/');

        string body;
        if (invited)
        {
            var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                await userManager.GeneratePasswordResetTokenAsync(person)));
            var link = $"{baseUrl}/reset-password?userId={person.Id}&token={Uri.EscapeDataString(token)}";

            body = $"""
                Operator OCWIP powołał Cię do komisji oceniającej wnioski w konkursie "{competitionTitle}".

                Konto w systemie zostało założone na ten adres. Ustaw hasło, żeby się zalogować:
                {link}

                Po zalogowaniu wnioski do oceny znajdziesz w panelu eksperta. Przed pierwszą oceną potwierdzisz deklarację bezstronności.
                """;
        }
        else
        {
            body = $"""
                Operator OCWIP powołał Cię do komisji oceniającej wnioski w konkursie "{competitionTitle}".

                Wnioski do oceny znajdziesz w panelu eksperta: {baseUrl}/panel/reviewer
                Przed pierwszą oceną potwierdzisz deklarację bezstronności.
                """;
        }

        try
        {
            await email.SendAsync(new EmailMessage(person.Email!, $"Powołanie do komisji: {competitionTitle}", body), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The appointment stands; the operator sees "zaproszenie wysłane"
            // only on the list, and can tell the person by other means.
            logger.LogWarning(exception, "Expert appointment mail for user {UserId} was not sent", person.Id);
        }
    }
}
