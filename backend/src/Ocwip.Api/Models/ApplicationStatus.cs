using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// The only two states an application has while the evaluation module does
    /// not exist. Accepted, rejected and everything else on a ranking list
    /// belongs to the review entity, which docs/model-danych.md deliberately
    /// does not build yet.
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
        Submitted
    }
}
