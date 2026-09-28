namespace Ocwip.Api.Models
{
    /// <summary>
    /// One read of a resource holding personal data (T-47a, D8 on Trello,
    /// formerly T-82): who read what, and when. The answer OCWIP owes a person
    /// asking "who saw my data".
    ///
    /// Append-only, like ApplicationStatusHistory: a row is never updated or
    /// removed. What was read is named, not copied: the row holds the kind of
    /// resource and its id, never the data itself, so this table is not a
    /// second copy of what it guards.
    ///
    /// Written by Endpoints/PersonalDataReadFilter.cs on the endpoints that
    /// return such data, after a successful answer only.
    /// </summary>
    public class PersonalDataRead
    {
        public Guid Id { get; set; }

        /// <summary>The account that read. Always signed in: these endpoints refuse anyone else.</summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// What kind of resource: "application", "contract", "report",
        /// "attachment", "competition-applications" for an export.
        /// </summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>The id from the address: the application, contract, report, file or competition.</summary>
        public Guid ResourceId { get; set; }

        /// <summary>The route that answered, for example GET /contracts/{contractId}/pdf.</summary>
        public string Endpoint { get; set; } = string.Empty;

        public DateTimeOffset ReadAt { get; set; }
    }
}
