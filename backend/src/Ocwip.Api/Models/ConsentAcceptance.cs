namespace Ocwip.Api.Models
{
    /// <summary>
    /// One document an account accepted when it was created (T-107, R-19):
    /// the terms or the privacy notice, with the FULL text the person saw and
    /// the moment, the way an expert's impartiality declaration keeps its
    /// text (ReviewerDeclaration, T-40a). A later version of the document does
    /// not rewrite what was accepted. Written once, never updated.
    /// </summary>
    public class ConsentAcceptance
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        /// <summary>"terms" or "privacy" (Services/Consents/ConsentCatalog.cs).</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The hash the text had, which the registration named.</summary>
        public string Version { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset AcceptedAt { get; set; }
    }
}
