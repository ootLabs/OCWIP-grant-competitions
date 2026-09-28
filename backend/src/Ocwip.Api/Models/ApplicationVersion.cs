using System.Text.Json;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// A submitted version of an application kept when it is sent back for
    /// correction (T-103). The application row holds the version being worked
    /// on and, once submitted again, the new one; the answers are overwritten
    /// in place, so the version the operator evaluated, and whose checksum the
    /// applicant's confirmation carries, would otherwise be gone.
    ///
    /// Written once, never updated. Version 1 is the first submission.
    /// </summary>
    public class ApplicationVersion
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }

        public int VersionNumber { get; set; }

        public Guid FormDefinitionId { get; set; }

        /// <summary>
        /// Sensitive Information: the answers as submitted, with the answers
        /// of sensitive fields encrypted inside the document like
        /// applications.answers (T-47a).
        /// </summary>
        public JsonElement Answers { get; set; }

        /// <summary>Sensitive Information: the card as submitted, encrypted like applications.entity_snapshot.</summary>
        public JsonElement? EntitySnapshot { get; set; }

        /// <summary>The checksum the confirmation of this version carried (D15).</summary>
        public string Checksum { get; set; } = string.Empty;

        public DateTimeOffset SubmittedAt { get; set; }

        /// <summary>When it was sent back, which is when it stopped being the current version.</summary>
        public DateTimeOffset SupersededAt { get; set; }
    }
}
