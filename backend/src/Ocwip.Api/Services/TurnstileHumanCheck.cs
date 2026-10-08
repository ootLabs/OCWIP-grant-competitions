using System.Text.Json.Serialization;

namespace Ocwip.Api.Services;

/// <summary>
/// Asks Cloudflare whether a Turnstile token is good (siteverify). A token
/// is single use and lives five minutes, so a token copied out of one
/// browser is worth one request and no more.
///
/// The token itself never reaches the log: it is a credential for exactly
/// that one request, and the error codes say all there is to say.
/// </summary>
public sealed class TurnstileHumanCheck(
    HttpClient http,
    TurnstileOptions options,
    ILogger<TurnstileHumanCheck> logger) : IHumanCheck
{
    /// <summary>Cloudflare's own limit on a token's length.</summary>
    private const int MaxTokenLength = 2048;

    public async Task<HumanCheckOutcome> VerifyAsync(
        string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return HumanCheckOutcome.Refused;
        }

        var form = new Dictionary<string, string>
        {
            ["secret"] = options.SecretKey,
            ["response"] = token,
        };
        if (remoteIp is not null)
        {
            form["remoteip"] = remoteIp;
        }

        try
        {
            using var response = await http.PostAsync(
                "turnstile/v0/siteverify", new FormUrlEncodedContent(form), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile siteverify answered {Status}.", (int)response.StatusCode);
                return HumanCheckOutcome.Unavailable;
            }

            var answer = await response.Content.ReadFromJsonAsync<SiteverifyAnswer>(cancellationToken);
            if (answer is null)
            {
                return HumanCheckOutcome.Unavailable;
            }

            if (answer.Success)
            {
                return HumanCheckOutcome.Passed;
            }

            var codes = answer.ErrorCodes ?? [];

            // internal-error is Cloudflare's side, not the caller's.
            if (codes.Contains("internal-error"))
            {
                logger.LogWarning("Turnstile siteverify reported an internal error.");
                return HumanCheckOutcome.Unavailable;
            }

            logger.LogInformation(
                "Turnstile refused a token: {ErrorCodes}.", string.Join(", ", codes));
            return HumanCheckOutcome.Refused;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or System.Text.Json.JsonException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "Turnstile siteverify could not be reached.");
            return HumanCheckOutcome.Unavailable;
        }
    }

    private sealed class SiteverifyAnswer
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; init; } = [];
    }
}

/// <summary>The secret half of the Turnstile widget's key pair.</summary>
public sealed record TurnstileOptions(string SecretKey);
