using Ocwip.Api.Authorization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// The party that files an application: an organisation or an informal
    /// group. Not a login account, see <see cref="User"/>.
    ///
    /// One entity with a type column, not three tables. The three types differ
    /// in their DATA, not in the way they log in, so NIP and address are
    /// nullable and their requiredness follows the type. An entity with no NIP
    /// is not broken data, it is an informal group.
    /// </summary>
    public class Entity : IAuditedEntity, IEntityScoped
    {
        public Guid Id { get; set; }

        /// <summary>
        /// A Podmiot is its own owner, so the authorization handler can treat
        /// it like any other scoped resource (T-13.2) and the applicant's own
        /// card needs no second rule. Explicit, so this does not become a
        /// second public property saying what Id already says.
        /// </summary>
        Guid IEntityScoped.EntityId => Id;

        public EntityType Type { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// "Forma prawna" (T-93). Null for an informal group without a
        /// patron, which has no card (pola.md, type 3). Like every field of
        /// the card below, required for an organisation card and checked at
        /// the API edge, not by the schema (EntityCardValidator), for the
        /// reason Nip gives.
        /// </summary>
        public LegalForm? LegalForm { get; set; }

        /// <summary>The name of the legal form when <see cref="LegalForm"/> is Other.</summary>
        public string? LegalFormOther { get; set; }

        public EntityRegister? Register { get; set; }

        /// <summary>Ten digits in KRS, free text in any other register.</summary>
        public string? RegisterNumber { get; set; }

        /// <summary>
        /// Stored as plaintext by decision DZ-2 (docs/runbook/plan-v1.md):
        /// only organisations carry one, and an organisation's NIP is public
        /// in KRS. Kept on the list of sensitive fields all the same, because
        /// a natural person applying with a NIP would change that answer.
        ///
        /// Required for an organisation only. That rule is NOT a NOT NULL and
        /// not a check constraint either: an informal group has none, so the
        /// schema would have to know the type. Type dependent validation sits
        /// at the API edge, see docs/konwencje.md.
        /// </summary>
        public string? Nip { get; set; }

        /// <summary>
        /// Optional, 9 or 14 digits. "Do potwierdzenia" in pola.md: the 2026
        /// application template does not ask for it.
        /// </summary>
        public string? Regon { get; set; }

        /// <summary>
        /// Sensitive Information. "Adres siedziby". In scope for encryption
        /// at rest in T-47a: a small organisation is often registered at
        /// somebody's home.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Sensitive Information, for the reason on <see cref="Address"/>.
        /// Only when it differs from the registered address.
        /// </summary>
        public string? CorrespondenceAddress { get; set; }

        /// <summary>
        /// Sensitive Information: often a person's own number. In scope for
        /// encryption at rest in T-47a.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Sensitive Information: often a person's own address. In scope for
        /// encryption at rest in T-47a. Was contact_information, a single
        /// free text field, before the card had separate fields (T-93).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Sensitive Information: the account the grant is paid into, 26
        /// digits (NRB), stored without spaces. In scope for encryption at
        /// rest in T-47a.
        /// </summary>
        public string? BankAccount { get; set; }

        /// <summary>
        /// Sensitive Information: natural persons' names, see
        /// <see cref="EntityRepresentative"/>. At least one for an
        /// organisation card, none for an informal group.
        /// </summary>
        public List<EntityRepresentative> Representatives { get; set; } = [];

        /// <summary>
        /// Soft delete flag, see the same field on Competition.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }

        /// <summary>
        /// The account this entity belongs to. One to one today, and that is an
        /// assumption to confirm, see <see cref="User.EntityId"/>.
        /// </summary>
        public User? User { get; set; }

        public ICollection<Application> Applications { get; set; } = [];
    }
}
