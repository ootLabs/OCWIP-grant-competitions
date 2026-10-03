namespace Ocwip.Api.Contracts;

/// <summary>POST /me/password (T-106): the current password and the new one.</summary>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>POST /me/email (T-106): the new address, confirmed by the current password.</summary>
public sealed record ChangeEmailRequest(string? NewEmail, string? CurrentPassword);

/// <summary>
/// POST /confirm-email-change (T-106): what the link in the mail to the new
/// address carries. The address itself is NOT here: it waits on the account
/// (User.PendingEmail), so it stays out of the link, the browser history and
/// every proxy log on the way (obserwacja 2).
/// </summary>
public sealed record ConfirmEmailChangeRequest(string? UserId, string? Token);
