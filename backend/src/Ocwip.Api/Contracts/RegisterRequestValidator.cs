using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Ocwip.Api.Contracts;

/// <summary>
/// Shape of the request, checked at the API edge (docs/konwencje.md) and
/// answered in Polish, because a rejected form is text a person reads.
///
/// The address check lives here because T-12.0 switched Identity's
/// AllowedUserNameCharacters off on purpose: that filter is meant for usernames
/// and, since UserName mirrors the address, it would end up deciding which
/// ADDRESSES may register, in English, against a rule nobody wrote down. So the
/// rule is written down here instead.
///
/// The password is only checked for presence. What makes a password acceptable
/// is one decision and it lives in Configuration/IdentityConfiguration.cs;
/// repeating the length here would be the same rule in two places.
/// </summary>
internal static class RegisterRequestValidator
{
    /// <summary>254 is the column width, see UserConfiguration.cs.</summary>
    private const int EmailLength = 254;

    /// <summary>100 on both name columns, see UserConfiguration.cs.</summary>
    private const int NameLength = 100;

    /// <summary>
    /// Stateless, so one instance is enough. See <see cref="IsAddress"/> for
    /// why Identity's own address check is repeated here.
    /// </summary>
    private static readonly EmailAddressAttribute IdentitysAddressRule = new();

    /// <summary>
    /// Whitespace around a pasted address or name is somebody's clipboard, not
    /// their intent, and it has to go before anything else reads the request.
    /// Refusing " adam@example.org" as malformed would be a message claiming
    /// the address is wrong when it is not, and keeping the space would be
    /// worse than rude: the normalized copy keeps it too, so one address would
    /// hold two accounts and the unique index would not notice.
    ///
    /// The password is left exactly as typed. A space at either end of a
    /// password is a character of the password.
    /// </summary>
    public static RegisterRequest Trim(RegisterRequest request) => request with
    {
        Email = Trimmed(request.Email),
        FirstName = Trimmed(request.FirstName),
        LastName = Trimmed(request.LastName),
    };

    public static Dictionary<string, string[]> Validate(RegisterRequest request)
    {
        var problems = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            problems["email"] = ["Adres e-mail jest wymagany."];
        }
        else if (request.Email.Length > EmailLength)
        {
            problems["email"] =
                [$"Adres e-mail nie może być dłuższy niż {EmailLength} znaków."];
        }
        else if (!IsAddress(request.Email))
        {
            problems["email"] = ["To nie jest poprawny adres e-mail."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            problems["password"] = ["Hasło jest wymagane."];
        }

        AddName(problems, "firstName", request.FirstName, "Imię");
        AddName(problems, "lastName", request.LastName, "Nazwisko");

        return problems;
    }

    /// <summary>
    /// The declared type says the value is never null, and JSON says otherwise:
    /// a body with "email": null deserializes straight through the annotation.
    /// </summary>
    private static string Trimmed(string? value) => value?.Trim() ?? string.Empty;

    private static void AddName(
        Dictionary<string, string[]> problems,
        string key,
        string value,
        string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems[key] = [$"{label} jest wymagane."];
        }
        else if (value.Length > NameLength)
        {
            problems[key] =
                [$"{label} nie może być dłuższe niż {NameLength} znaków."];
        }
    }

    /// <summary>
    /// MailAddress rather than a regular expression, because a hand written one
    /// either rejects legal addresses or accepts nonsense, and this one has to
    /// agree with what a mail server will accept in T-12.2.
    ///
    /// The round trip comparison is the part that matters: MailAddress happily
    /// parses "Adam Testowy &lt;adam@example.org&gt;" and exposes the address
    /// inside it, so without this check a display name would register an
    /// account under an address the person never typed.
    ///
    /// EmailAddressAttribute on top, and not as a belt: Identity's UserValidator
    /// runs exactly that check itself whenever RequireUniqueEmail is on, so an
    /// address this method accepts and that one refuses does not come back as
    /// the Polish message below. It comes back as Identity's English
    /// InvalidEmail, reported against the PASSWORD field, because that is the
    /// only field AccountEndpoints has left to report against by then.
    /// "a@b"@example.org is such an address: MailAddress parses and round trips
    /// it, EmailAddressAttribute counts two at signs and refuses it.
    /// </summary>
    private static bool IsAddress(string value) =>
        MailAddress.TryCreate(value, out var parsed)
        && string.Equals(parsed.Address, value, StringComparison.Ordinal)
        && IdentitysAddressRule.IsValid(value);
}
