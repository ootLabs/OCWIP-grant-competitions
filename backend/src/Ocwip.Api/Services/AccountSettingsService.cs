using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

internal enum AccountSettingsOutcome
{
    Succeeded,
    Unauthorized,
    Invalid,

    /// <summary>The link of an address change did not work, for whatever reason.</summary>
    InvalidToken,
}

internal sealed record AccountSettingsResult(
    AccountSettingsOutcome Outcome,
    IDictionary<string, string[]>? Errors = null);

internal interface IAccountSettingsService
{
    Task<AccountSettingsResult> ChangePasswordAsync(ClaimsPrincipal caller, string? currentPassword, string? newPassword, CancellationToken cancellationToken);

    Task<AccountSettingsResult> RequestEmailChangeAsync(ClaimsPrincipal caller, string? newEmail, string? currentPassword, CancellationToken cancellationToken);

    Task<AccountSettingsResult> ConfirmEmailChangeAsync(string? userId, string? newEmail, string? token, CancellationToken cancellationToken);
}

/// <summary>
/// Changing one's own password and e-mail address after signing in (T-106,
/// R-08).
///
/// The password needs the old one; the change rotates the security stamp, so
/// every other session ends at its next request, and this one is signed in
/// again so the person who changed it stays in.
///
/// The address needs the password too, and a confirmation from the NEW
/// address: until the link in that mail is followed nothing changes, so a
/// typo cannot lock anybody out and nobody takes an address they cannot read.
/// The old address hears about the request at once, so the owner of a
/// hijacked session learns about it. Whether the new address already has an
/// account is never told (security rule 3): the answer is the same, and a
/// taken address simply gets no link.
/// </summary>
internal sealed class AccountSettingsService(
    UserManager<User> users,
    SignInManager<User> signIn,
    IEmailSender email,
    IConfiguration configuration) : IAccountSettingsService
{
    public async Task<AccountSettingsResult> ChangePasswordAsync(
        ClaimsPrincipal caller, string? currentPassword, string? newPassword, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(caller);
        if (user is null)
        {
            return new AccountSettingsResult(AccountSettingsOutcome.Unauthorized);
        }

        if (await RefusedPasswordAsync(user, currentPassword) is { } refusal)
        {
            return refusal;
        }

        var changed = await users.ChangePasswordAsync(user, currentPassword!, newPassword ?? string.Empty);
        if (!changed.Succeeded)
        {
            return new AccountSettingsResult(
                AccountSettingsOutcome.Invalid,
                new Dictionary<string, string[]> { ["newPassword"] = [.. changed.Errors.Select(x => x.Description)] });
        }

        await users.ResetAccessFailedCountAsync(user);

        // The stamp moved with the password, which ends every session; this
        // one gets a new cookie on the new stamp.
        await signIn.RefreshSignInAsync(user);

        if (!string.IsNullOrEmpty(user.Email))
        {
            await email.SendAsync(new EmailMessage(user.Email, "Hasło zostało zmienione", """
                Hasło do Twojego konta zostało właśnie zmienione. Pozostałe sesje zostały wylogowane.

                Jeśli to nie Ty, od razu ustaw nowe hasło przez "Nie pamiętasz hasła?" na stronie logowania.
                """), cancellationToken);
        }

        return new AccountSettingsResult(AccountSettingsOutcome.Succeeded);
    }

    public async Task<AccountSettingsResult> RequestEmailChangeAsync(
        ClaimsPrincipal caller, string? newEmail, string? currentPassword, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(caller);
        if (user is null)
        {
            return new AccountSettingsResult(AccountSettingsOutcome.Unauthorized);
        }

        var address = newEmail?.Trim() ?? string.Empty;
        if (address.Length > 254 || !RegisterRequestValidator.IsAddress(address))
        {
            return Invalid("newEmail", "Podaj poprawny adres e-mail.");
        }

        if (await RefusedPasswordAsync(user, currentPassword) is { } refusal)
        {
            return refusal;
        }

        var sameAsNow = string.Equals(EmailNormalizer.Normalize(address), user.NormalizedEmail, StringComparison.Ordinal);
        var taken = !sameAsNow && await users.FindByEmailAsync(address) is not null;

        // A taken address gets no link, and the caller gets the same answer
        // as for a free one: nothing here says which it was.
        if (!sameAsNow && !taken)
        {
            var token = await users.GenerateChangeEmailTokenAsync(user, address);
            var link = $"{BaseUrl()}/confirm-email-change?userId={user.Id}"
                + $"&email={Uri.EscapeDataString(address)}"
                + $"&token={Uri.EscapeDataString(WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)))}";

            await email.SendAsync(new EmailMessage(address, "Potwierdź nowy adres e-mail", $"""
                Poproszono o zmianę adresu e-mail konta na ten adres.

                Aby ją potwierdzić, otwórz link:

                {link}

                Do czasu potwierdzenia konto działa pod dotychczasowym adresem. Jeśli to nie Ty, zignoruj tę wiadomość.
                """), cancellationToken);
        }

        if (!sameAsNow && !string.IsNullOrEmpty(user.Email))
        {
            await email.SendAsync(new EmailMessage(user.Email, "Prośba o zmianę adresu e-mail", """
                Poproszono o zmianę adresu e-mail Twojego konta. Adres zmieni się dopiero po potwierdzeniu z nowej skrzynki.

                Jeśli to nie Ty, zmień hasło: ktoś może mieć dostęp do Twojej sesji.
                """), cancellationToken);
        }

        return new AccountSettingsResult(AccountSettingsOutcome.Succeeded);
    }

    public async Task<AccountSettingsResult> ConfirmEmailChangeAsync(
        string? userId, string? newEmail, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(newEmail) || string.IsNullOrEmpty(token))
        {
            return new AccountSettingsResult(AccountSettingsOutcome.InvalidToken);
        }

        var user = await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return new AccountSettingsResult(AccountSettingsOutcome.InvalidToken);
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return new AccountSettingsResult(AccountSettingsOutcome.InvalidToken);
        }

        var previous = user.Email;

        // Checks the token against this very address, moves the address and
        // its normalized form, confirms it and rotates the security stamp,
        // which ends every session: the next sign in uses the new address.
        var changed = await users.ChangeEmailAsync(user, newEmail, decoded);
        if (!changed.Succeeded)
        {
            // Wrong token, expired, used, or the address taken in the meantime:
            // one answer for all of them.
            return new AccountSettingsResult(AccountSettingsOutcome.InvalidToken);
        }

        // The account signs in by its address, which is also its user name.
        await users.SetUserNameAsync(user, newEmail);

        if (!string.IsNullOrEmpty(previous))
        {
            await email.SendAsync(new EmailMessage(previous, "Adres e-mail został zmieniony", """
                Adres e-mail Twojego konta został zmieniony. Ten adres nie jest już przypisany do konta.

                Jeśli to nie Ty, skontaktuj się z organizatorem konkursu.
                """), cancellationToken);
        }

        return new AccountSettingsResult(AccountSettingsOutcome.Succeeded);
    }

    /// <summary>
    /// The current password, counted like a sign in: a wrong one moves the
    /// lockout counter, and a locked out account is refused before the
    /// password is even looked at, which CheckPasswordAsync alone does not
    /// do. A stolen session is not a way to guess the password without limit.
    /// </summary>
    private async Task<AccountSettingsResult?> RefusedPasswordAsync(User user, string? currentPassword)
    {
        if (await users.IsLockedOutAsync(user))
        {
            return Invalid("currentPassword", "Za dużo błędnych prób. Spróbuj ponownie za kilka minut.");
        }

        if (string.IsNullOrEmpty(currentPassword) || !await users.CheckPasswordAsync(user, currentPassword))
        {
            await users.AccessFailedAsync(user);
            return Invalid("currentPassword", "Obecne hasło jest nieprawidłowe.");
        }

        return null;
    }

    private static AccountSettingsResult Invalid(string field, string message) =>
        new(AccountSettingsOutcome.Invalid, new Dictionary<string, string[]> { [field] = [message] });

    private string BaseUrl() =>
        (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured ? configured : "http://localhost:3000")
            .TrimEnd('/');
}
