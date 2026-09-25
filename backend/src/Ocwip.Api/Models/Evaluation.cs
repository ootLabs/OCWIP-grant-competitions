using System.Text.Json.Serialization;
using System.Text.Json;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// One evaluation card filled in by one person for one application (T-38).
    /// The structure of the card is a form definition with an evaluation
    /// purpose; this row holds the answers and who gave them. Scores are not
    /// stored: EvaluationScores reads them from the answers every time.
    /// </summary>
    public class Evaluation : IAuditedEntity
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Carried next to ApplicationId, like Application carries both
        /// competition and form version: both foreign keys are composite on it,
        /// so the card and the application cannot belong to two competitions.
        /// </summary>
        public Guid CompetitionId { get; set; }

        public Guid ApplicationId { get; set; }
        public Application Application { get; set; } = null!;

        /// <summary>
        /// The card version this evaluation was filled in on. Never changes,
        /// for the reason Application.FormDefinitionId never does: a card
        /// edited mid-evaluation must not reinterpret answers already given.
        /// </summary>
        public Guid FormDefinitionId { get; set; }

        public EvaluationStage Stage { get; set; }

        /// <summary>
        /// Who evaluates. Null only when AuthorName is set instead: decision 14
        /// of the report keeps the author apart from whoever types the card in,
        /// so a committee that met on paper can be entered by the operator.
        /// </summary>
        public Guid? AuthorUserId { get; set; }

        /// <summary>An author without an account, as written on the paper card.</summary>
        public string? AuthorName { get; set; }

        /// <summary>Who entered the card into the system; the author for their own.</summary>
        public Guid EnteredByUserId { get; set; }

        /// <summary>The answers in the shape of the form contract, like Application.Answers.</summary>
        public JsonElement Answers { get; set; }

        public EvaluationStatus Status { get; set; }

        /// <summary>When the stage was finished ("zapisz i zakończ etap"), null while a draft.</summary>
        public DateTimeOffset? FinishedAt { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeactivatedAt { get; set; }
    }

    /// <summary>The two stages of evaluation, never merged into one pass (report, 5.3 and 5.4).</summary>
    // Text on the wire, like every enum of this API: an ordinal would
    // reinterpret itself the day a member is inserted.
    [JsonConverter(typeof(JsonStringEnumConverter<EvaluationStage>))]
    public enum EvaluationStage
    {
        Formal,
        Merit,
    }

    /// <summary>"Zapisz" keeps a draft, "zapisz i zakończ etap" finishes it.</summary>
    // Text on the wire, like every enum of this API: an ordinal would
    // reinterpret itself the day a member is inserted.
    [JsonConverter(typeof(JsonStringEnumConverter<EvaluationStatus>))]
    public enum EvaluationStatus
    {
        Draft,
        Finished,
    }
}
