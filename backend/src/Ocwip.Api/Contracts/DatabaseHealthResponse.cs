namespace Ocwip.Api.Contracts;

/// <summary>The body of GET /health/db when the probe succeeds.</summary>
public record DatabaseHealthResponse(string Status, string Database);