namespace Ocwip.Api.Models
{
    /// <summary>
    /// A row of a competition's lists (required attachments, contacts, cost
    /// categories) that an edit takes off the list without deleting it
    /// (T-101): retention is at least 5 years (AGENTS.md, security rule 5),
    /// and an uploaded attachment points at the requirement it answers.
    /// </summary>
    public interface IRetainedRow
    {
        bool IsActive { get; set; }

        DateTimeOffset? DeactivatedAt { get; set; }
    }
}
