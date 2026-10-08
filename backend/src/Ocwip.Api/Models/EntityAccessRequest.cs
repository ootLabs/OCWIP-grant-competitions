using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>On the wire as its name, like every status the front reads (ApplicationStatus).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<EntityAccessRequestStatus>))]
    public enum EntityAccessRequestStatus
    {
        Pending,
        Approved,
        Rejected,
    }

    /// <summary>
    /// A request to join a Podmiot card that already exists (T-93a, report
    /// step 2.2): somebody typed a NIP that is taken, and instead of a second
    /// card with the same NIP and maybe another bank account, they ask the
    /// founder for access.
    ///
    /// Unanswered for seven days it reaches the operators (EscalationAge), who
    /// approve or refuse after checking outside the system, by phone or from
    /// the register. That decision keeps the operator's account, the moment and
    /// a note on how it was checked: the report wants every such approval in
    /// the history with a name and a date. The report calls this person the
    /// OCWIP administrator; there is no such role (R-02), so it is the operator.
    ///
    /// Never deleted: a refused request is part of the card's history.
    /// </summary>
    public class EntityAccessRequest : IAuditedEntity
    {
        /// <summary>Report step 2.2: "bez odpowiedzi przez siedem dni".</summary>
        public static readonly TimeSpan EscalationAge = TimeSpan.FromDays(7);

        public Guid Id { get; set; }

        public Guid EntityId { get; set; }
        public Entity Entity { get; set; } = null!;

        public Guid RequesterId { get; set; }

        public EntityAccessRequestStatus Status { get; set; } = EntityAccessRequestStatus.Pending;

        public DateTimeOffset? DecidedAt { get; set; }
        public Guid? DecidedById { get; set; }

        /// <summary>True when an operator decided instead of the founder.</summary>
        public bool DecidedByOperator { get; set; }

        /// <summary>
        /// How the operator checked the request outside the system. Required
        /// with an operator's decision, absent with the founder's. May name a
        /// person, so it is kept out of logs like any other free text.
        /// </summary>
        public string? OperatorNote { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
