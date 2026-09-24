namespace Ocwip.Api.Services;

/// <summary>
/// Local disk implementation of IAttachmentStorage (T-32). Registered
/// unconditionally in Program.cs, the same way TimeProvider is: it needs no
/// database, and an attachment upload failing to build the DI container on a
/// host without one would be a new way for /health to stop answering.
///
/// Every file lands directly under the root as a bare GUID, with no
/// subdirectories and no trace of the original name or extension: the path is
/// meant to be looked up by the database row that owns it, never guessed or
/// browsed, and a flat namespace is the simplest thing that satisfies that.
/// Nothing here ever deletes a file (AGENTS.md rule 5 and the card's own
/// "poprzedni nie znika twardo"): replacing an attachment writes a new one and
/// leaves the old bytes exactly where they were.
/// </summary>
internal sealed class AttachmentStorageService : IAttachmentStorage
{
    private readonly string _root;

    public AttachmentStorageService(IConfiguration configuration)
    {
        _root = configuration["Attachments:StoragePath"] is { Length: > 0 } configured
            ? configured
            : "/data/attachments";

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        var storagePath = Guid.NewGuid().ToString("N");
        var fullPath = Path.Combine(_root, storagePath);

        await using (var file = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return storagePath;
    }

    public Task<Stream> OpenReadAsync(
        string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_root, storagePath);

        // storagePath always comes from a database row this process wrote
        // itself (SaveAsync's return value), never from anything an HTTP
        // caller supplies directly, so there is nothing here to sanitise
        // against a traversal payload it will never receive.
        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult(stream);
    }
}
