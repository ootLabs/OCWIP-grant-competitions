using System.Text.Json;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// A submitted version of a report kept when it is sent back for
    /// correction (S-35), the counterpart of <see cref="ApplicationVersion"/>.
    /// The report row holds the version being worked on, and its answers are
    /// overwritten in place, so what the organiser accepted, and what they
    /// questioned in it, would otherwise be gone by the time the grant is
    /// settled.
    ///
    /// Written once, never updated. Version 1 is the first submission.
    /// </summary>
    public class ReportVersion
    {
        public Guid Id { get; set; }

        public Guid ReportId { get; set; }

        public int VersionNumber { get; set; }

        /// <summary>The version of the report form this one was filled in on.</summary>
        public Guid FormDefinitionId { get; set; }

        /// <summary>
        /// Sensitive Information: the answers as submitted, with the answers
        /// of sensitive fields encrypted inside the document, under this
        /// table's own purpose (T-47a).
        /// </summary>
        public JsonElement Answers { get; set; }

        /// <summary>Sensitive Information: the values taken from the application, encrypted like the answers.</summary>
        public JsonElement Prefill { get; set; }

        /// <summary>
        /// What the operator did not accept in this version, with the reason.
        /// Kept with the version it was written against: a correction may
        /// change the very amounts it judged.
        /// </summary>
        public JsonElement CostReview { get; set; }

        public DateTimeOffset SubmittedAt { get; set; }

        /// <summary>When it was sent back, which is when it stopped being the current version.</summary>
        public DateTimeOffset SupersededAt { get; set; }
    }
}
