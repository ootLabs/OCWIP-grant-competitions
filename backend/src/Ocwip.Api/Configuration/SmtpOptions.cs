namespace Ocwip.Api.Configuration;

/// <summary>
/// The mail relay (T-43a), from the environment: SMTP__HOST, SMTP__PORT and
/// the rest, see .env.example. No host means no relay, and the system keeps
/// logging mail instead of sending it (EmailSenderService).
/// </summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; set; }

    /// <summary>587 with STARTTLS is what relays expect from an application today.</summary>
    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? User { get; set; }

    /// <summary>A secret: set in the environment only, never logged.</summary>
    public string? Password { get; set; }

    /// <summary>The sender address; required once a host is set.</summary>
    public string? From { get; set; }

    public string FromName { get; set; } = "OCWIP";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
