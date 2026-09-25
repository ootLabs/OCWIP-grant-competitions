using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// Where an application stands (docs/runbook/proces.md, "Stany wniosku").
    /// Draft and Submitted since T-29 and T-33; the three results since T-42,
    /// written together for the whole competition when the operator approves
    /// the results, never one by one (applicants must not learn them in the
    /// order somebody clicked). Contract signing, realisation and settlement
    /// belong to T-43 onward.
    ///
    /// Text on the wire, by attribute rather than by a serializer option, same
    /// reason as every other enum here (Models/Role.cs): a number crossing the
    /// wire is a number the front has to keep a second table for, and T-29 is
    /// the first card that puts this one on the wire at all.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ApplicationStatus>))]
    public enum ApplicationStatus
    {
        Draft,
        Submitted,

        /// <summary>
        /// Granted, contract not signed yet: "dofinansowany, umowa
        /// niepodpisana" of the report, the money reserved and no obligation
        /// yet. The awarded amount is on Application.AwardedGrant.
        /// </summary>
        Funded,

        /// <summary>
        /// Evaluated positively and not funded: first in line for money a
        /// resignation frees (regulations 2026, contract not signed in 14
        /// days). Working assumption ZR-09.
        /// </summary>
        Reserve,

        /// <summary>Negative formal evaluation, or below the merit threshold.</summary>
        Rejected
    }
}
