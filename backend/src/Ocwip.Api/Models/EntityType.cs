using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// The three kinds of applicant ("rodzaj wnioskodawcy"). Text on the
    /// wire for the reason written on ApplicationStatus: T-35 is the first
    /// card to put this one on the wire, and the list filters by it.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<EntityType>))]
    public enum EntityType
    {
        InformalGroup,
        PatronInformalGroup,
        Organisation
    }
}
