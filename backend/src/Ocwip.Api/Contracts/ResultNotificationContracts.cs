namespace Ocwip.Api.Contracts;

/// <summary>The three result mails of a competition as OCWIP wrote them (T-43), null for the default.</summary>
public sealed record ResultMessagesRequest(string? Funded, string? Reserve, string? Rejected);

public sealed record ResultMessagesResponse(string? Funded, string? Reserve, string? Rejected);

/// <summary>Where the result mails of a competition stand: owed, sent, and failed at the last try.</summary>
public sealed record ResultNotificationsResponse(int Total, int Sent, int Pending, int Failed, DateTimeOffset? LastSentAt);
