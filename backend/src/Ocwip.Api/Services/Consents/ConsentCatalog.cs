using System.Security.Cryptography;
using System.Text;

namespace Ocwip.Api.Services.Consents;

/// <summary>One document a new account accepts: the terms, the privacy notice.</summary>
/// <param name="Version">The first 16 hex digits of the SHA-256 of the text: a changed text is a new version.</param>
public sealed record ConsentDocument(string Kind, string Title, string Version, string Text);

/// <summary>
/// The documents a registration accepts (T-107, R-19): the terms of the
/// service and the privacy notice, read from <c>seed/consents/*.md</c>, which
/// the runtime image carries. Their text is data, not code: replacing a file
/// replaces the document, and its version, a hash of the text, changes by
/// itself, so an acceptance of the old text never counts for the new one.
///
/// The first line of each file ("# ...") is the title shown above it. Read
/// once, at the first use; the files do not change while the process runs.
/// </summary>
public sealed class ConsentCatalog
{
    public const string Terms = "terms";
    public const string Privacy = "privacy";

    private readonly Lazy<IReadOnlyList<ConsentDocument>> _documents;

    public ConsentCatalog(IConfiguration configuration)
    {
        _documents = new Lazy<IReadOnlyList<ConsentDocument>>(() => Load(FindDirectory(configuration)));
    }

    public IReadOnlyList<ConsentDocument> Current => _documents.Value;

    /// <summary>Which documents in force the request did not accept, by title; empty when all were.</summary>
    public IReadOnlyList<string> Missing(IEnumerable<string>? acceptedVersions)
    {
        var accepted = new HashSet<string>(acceptedVersions ?? [], StringComparer.Ordinal);
        return [.. Current.Where(x => !accepted.Contains(x.Version)).Select(x => x.Title)];
    }

    private static IReadOnlyList<ConsentDocument> Load(string directory) =>
        [.. new[] { Terms, Privacy }.Select(kind =>
        {
            var text = File.ReadAllText(Path.Combine(directory, $"{kind}.md")).Replace("\r\n", "\n").Trim();
            var firstLine = text.Split('\n', 2)[0];
            var title = firstLine.StartsWith("# ", StringComparison.Ordinal) ? firstLine[2..].Trim() : kind;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
            return new ConsentDocument(kind, title, hash, text);
        })];

    /// <summary>Consents:Directory when set; otherwise seed/consents next to the binaries or above them.</summary>
    private static string FindDirectory(IConfiguration configuration)
    {
        if (configuration["Consents:Directory"] is { Length: > 0 } configured)
        {
            return configured;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "seed", "consents");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("seed/consents was not found; set Consents:Directory.");
    }
}
