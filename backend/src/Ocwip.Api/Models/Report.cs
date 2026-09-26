using System.Text.Json;
using System.Text.Json.Serialization;
using Ocwip.Api.Authorization;

namespace Ocwip.Api.Models
{
    /// <summary>Where a report stands (T-50a).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ReportStatus>))]
    public enum ReportStatus
    {
        Draft,
        Submitted,

        /// <summary>Sent back by the operator with a reason; editable again.</summary>
        Returned,

        Accepted,
    }

    /// <summary>
    /// The applicant's report on a funded project (T-50a, model T-50.0). A
    /// document of its own, next to the application rather than inside it:
    /// its own form version, answers, cycle and history.
    ///
    /// IEntityScoped with a copy of the application's EntityId, like an
    /// attachment: the operator reads every report, an applicant only their
    /// own, and an expert none (EntityScopedHandler grants a reviewer
    /// applications and attachments only).
    /// </summary>
    public class Report : IAuditedEntity, IEntityScoped
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        public Guid CompetitionId { get; set; }

        public Guid EntityId { get; set; }

        /// <summary>The report form version it was started on, never a newer one.</summary>
        public Guid FormDefinitionId { get; set; }
        public FormDefinition FormDefinition { get; set; } = null!;

        /// <summary>
        /// The applicant's answers. Personal data of the people named in the
        /// report (contact person, leader of the group): sensitive, in scope
        /// for encryption at rest with the application's answers (T-47).
        /// </summary>
        public JsonElement Answers { get; set; }

        /// <summary>
        /// The values taken from the application when the report was started
        /// ("było"), kept so that every save puts them back: the applicant
        /// cannot change what the application said, even with a hand made
        /// request.
        /// </summary>
        public JsonElement Prefill { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Draft;

        public DateTimeOffset? SubmittedAt { get; set; }

        /// <summary>Why the operator sent it back, shown to the applicant until the next submission.</summary>
        public string? ReturnReason { get; set; }

        public DateTimeOffset? AcceptedAt { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>One status change of a report (T-50a), append only like ApplicationStatusHistory.</summary>
    public class ReportStatusHistory : IAuditedEntity
    {
        public Guid Id { get; set; }
        public Guid ReportId { get; set; }
        public ReportStatus FromStatus { get; set; }
        public ReportStatus ToStatus { get; set; }
        public DateTimeOffset ChangedAt { get; set; }
        public Guid ChangedByUserId { get; set; }

        /// <summary>The reason given with a return, null otherwise.</summary>
        public string? Reason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
