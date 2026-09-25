using System.Text.Json.Serialization;
namespace Ocwip.Api.Models
{
    /// <summary>How the merit cards of one application combine into its score (T-39).</summary>
    // Text on the wire, like every enum of this API: an ordinal would
    // reinterpret itself the day a member is inserted.
    [JsonConverter(typeof(JsonStringEnumConverter<ScoreAggregation>))]
    public enum ScoreAggregation
    {
        /// <summary>The 2026 rule: two experts, up to 50 each, up to 100 together.</summary>
        Sum,

        /// <summary>The report's alternative, kept as a setting rather than a constant.</summary>
        Average,
    }
}
