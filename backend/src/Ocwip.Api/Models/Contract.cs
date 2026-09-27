using System.Text.Json;
using System.Text.Json.Serialization;
using Ocwip.Api.Authorization;

namespace Ocwip.Api.Models
{
    /// <summary>What a document template is the template of (T-45).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DocumentKind>))]
    public enum DocumentKind
    {
        /// <summary>"Umowa o dofinansowanie". Later: the committee's minutes and attendance list (report step 5.6).</summary>
        Contract,
    }

    /// <summary>
    /// A document template of a competition (T-45, model T-45.0): fixed text
    /// with {{placeholders}}, versioned and never changed once published,
    /// like a form. A contract keeps the version it was drawn up on.
    /// </summary>
    public class DocumentTemplate : IAuditedEntity
    {
        public Guid Id { get; set; }
        public Guid CompetitionId { get; set; }
        public DocumentKind Kind { get; set; }
        public int VersionNumber { get; set; }

        /// <summary>The text with {{placeholders}}; a line break is a line break in the document.</summary>
        public string Body { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ContractStatus>))]
    public enum ContractStatus
    {
        /// <summary>Drawn up: the operator fills in the values and prints it.</summary>
        Draft,

        /// <summary>The operator entered the date it was signed; the values are frozen.</summary>
        Signed,
    }

    /// <summary>
    /// The contract of a funded application (T-45). The text is never stored:
    /// it is the template version filled in with the application's data and
    /// the values the operator typed, whenever it is printed. IEntityScoped
    /// with a copy of the application's EntityId, so the applicant can read
    /// their own contract and an expert none.
    /// </summary>
    public class Contract : IAuditedEntity, IEntityScoped
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;
        public Guid CompetitionId { get; set; }
        public Guid EntityId { get; set; }

        public Guid TemplateId { get; set; }
        public DocumentTemplate Template { get; set; } = null!;

        /// <summary>
        /// The values of the placeholders the operator fills in (a bank
        /// account, the people who sign): an object of strings. Holds personal
        /// data, and for an informal group the PESEL numbers of its members
        /// (card T-45): sensitive, to be encrypted by T-47, and contracts do not
        /// go to production before it.
        /// </summary>
        public JsonElement Values { get; set; }

        public ContractStatus Status { get; set; } = ContractStatus.Draft;

        /// <summary>The day it was signed, as the operator entered it; null until then.</summary>
        public DateOnly? SignedOn { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
