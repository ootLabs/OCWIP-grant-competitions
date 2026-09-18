using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// What the percentage limits of step 1.4 are counted from
    /// (docs/runbook/pola.md). A competition setting rather than a constant,
    /// because the 2026 templates count from the grant and the switch has to
    /// survive a year in which they do not.
    ///
    /// Text on the wire and in the column, same reason as CompetitionStatus.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PercentageBasis>))]
    public enum PercentageBasis
    {
        /// <summary>"Kwota dotacji". The default the 2026 template writes.</summary>
        GrantAmount,

        /// <summary>"Całkowita wartość projektu".</summary>
        TotalProjectValue
    }
}
