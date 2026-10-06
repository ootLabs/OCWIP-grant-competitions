using Ocwip.Api.Data.Encryption;

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
            // Encrypted on the way down (S-38): a statute, a power of
            // attorney or a register extract is the same class of personal
            // data as the columns T-47a encrypts, and this volume is copied
            // into every backup.
            await new FileCipher(FieldEncryption.Cipher).WriteAsync(content, file, storagePath, cancellationToken);
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

        // A file written before S-38 has no header and comes back as it is,
        // so this ships without rewriting the volume first; reencrypt-data
        // rewrites those.
        return new FileCipher(FieldEncryption.Cipher).ReadAsync(stream, storagePath, cancellationToken);
    }

    public async Task<bool> RewriteAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_root, storagePath);

        if (!File.Exists(fullPath))
        {
            return false;
        }

        var cipher = new FileCipher(FieldEncryption.Cipher);

        // Already under the current key, so nothing to do. Without this check
        // every run of reencrypt-data would decrypt and re-encrypt the whole
        // volume again, and the command tells the operator to run it again
        // after any failure.
        await using (var header = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
        {
            if (await cipher.IsCurrentAsync(header, cancellationToken))
            {
                return false;
            }
        }

        // Into a neighbour first, then one atomic move: a crash halfway
        // leaves the file that was there, never half of two.
        var rewritten = fullPath + ".rewriting";

        try
        {
            await using (var plain = await OpenReadAsync(storagePath, cancellationToken))
            await using (var destination = new FileStream(
                rewritten, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await cipher.WriteAsync(plain, destination, storagePath, cancellationToken);
            }
        }
        catch
        {
            // A half written neighbour would otherwise sit on the volume for
            // good: nothing serves it, and nothing else would ever clean it.
            TryDelete(rewritten);
            throw;
        }

        File.Move(rewritten, fullPath, overwrite: true);

        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original failure is the one worth reporting.
        }
    }
}
