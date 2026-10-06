using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Ocwip.Api.Data.Encryption;

/// <summary>
/// A file encrypted where it lies (S-38). The columns holding personal data
/// have been encrypted since T-47a, and the attachments were the one place
/// left where the same data sat in the clear: the volume, and the backup that
/// copies it, were the cheapest way to every statute, power of attorney and
/// register extract an applicant ever sent, with no key needed.
///
/// AES-GCM in chunks rather than over the whole file, because an attachment
/// is up to 25 MB and one-shot encryption would hold all of it, twice, in
/// memory for every upload at once. The chunks are bound together so that
/// cutting, reordering or swapping them is detected:
///
/// - the nonce of a chunk is the file's own random prefix plus its number, so
///   no two chunks of any file share one;
/// - the number goes into the associated data, so a reordered chunk fails;
/// - the file ends with a terminator chunk over no plaintext at all, so a
///   file cut short fails instead of reading as a shorter document;
/// - the name of the stored file is in the associated data of every chunk, so
///   one file's bytes cannot be served in place of another's.
/// </summary>
internal sealed class FileCipher(FieldCipher keys)
{
    /// <summary>Written at the start of an encrypted file, so a file written before S-38 is recognised and read as it is.</summary>
    public static readonly byte[] Magic = "OCWIPF1\n"u8.ToArray();

    public const string Purpose = "attachments.content";

    private const int ChunkBytes = 64 * 1024;
    private const int TagBytes = 16;
    private const int NonceBytes = 12;
    private const int PrefixBytes = 8;

    private readonly FieldCipher _keys = keys;

    /// <summary>The header: the magic, the key version, and the nonce prefix of this file.</summary>
    public static int HeaderBytes => Magic.Length + sizeof(int) + PrefixBytes;

    public async Task WriteAsync(Stream plain, Stream destination, string name, CancellationToken cancellationToken)
    {
        var version = _keys.CurrentVersion;
        var prefix = RandomNumberGenerator.GetBytes(PrefixBytes);

        var header = new byte[HeaderBytes];
        Magic.CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(Magic.Length), version);
        prefix.CopyTo(header, Magic.Length + sizeof(int));
        await destination.WriteAsync(header, cancellationToken);

        using var aes = new AesGcm(_keys.DeriveKey(version, Purpose), TagBytes);

        var plaintext = new byte[ChunkBytes];
        var sealedChunk = new byte[sizeof(int) + ChunkBytes + TagBytes];
        var number = 0;

        while (true)
        {
            var read = await ReadChunkAsync(plain, plaintext, cancellationToken);

            if (read == 0)
            {
                break;
            }

            var length = Seal(aes, plaintext.AsSpan(0, read), sealedChunk, prefix, name, number, final: false);
            await destination.WriteAsync(sealedChunk.AsMemory(0, length), cancellationToken);
            number++;
        }

        // The terminator: no plaintext, its own tag. A file cut short loses
        // this and fails to read, instead of reading as a shorter document.
        var end = Seal(aes, ReadOnlySpan<byte>.Empty, sealedChunk, prefix, name, number, final: true);
        await destination.WriteAsync(sealedChunk.AsMemory(0, end), cancellationToken);
    }

    /// <summary>
    /// The plaintext of a stored file. A file written before S-38 carries no
    /// magic and is handed back as it is, so the volume does not need
    /// rewriting before this can ship; reencrypt-data rewrites them.
    /// </summary>
    public async Task<Stream> ReadAsync(Stream stored, string name, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderBytes];

        if (await ReadChunkAsync(stored, header, cancellationToken) != HeaderBytes
            || !header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            stored.Position = 0;
            return stored;
        }

        var version = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(Magic.Length));
        var prefix = header.AsSpan(Magic.Length + sizeof(int), PrefixBytes).ToArray();

        // Decrypted into a temporary file rather than into memory: the same
        // 25 MB reason the encryption is chunked, and the download hands the
        // stream to Kestrel, which reads it long after this method returns.
        var plain = new FileStream(
            Path.GetTempFileName(),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            using var aes = new AesGcm(_keys.DeriveKey(version, Purpose), TagBytes);
            var sealedChunk = new byte[sizeof(int) + ChunkBytes + TagBytes];
            var plaintext = new byte[ChunkBytes];

            for (var number = 0; ; number++)
            {
                var length = await ReadLengthAsync(stored, sealedChunk, cancellationToken);
                var final = length == 0;

                Open(aes, sealedChunk, length, plaintext, prefix, name, number, final);

                if (final)
                {
                    break;
                }

                await plain.WriteAsync(plaintext.AsMemory(0, length), cancellationToken);
            }
        }
        catch
        {
            await plain.DisposeAsync();
            throw;
        }

        plain.Position = 0;
        return plain;
    }

    private static int Seal(
        AesGcm aes,
        ReadOnlySpan<byte> plaintext,
        Span<byte> destination,
        ReadOnlySpan<byte> prefix,
        string name,
        int number,
        bool final)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, plaintext.Length);

        aes.Encrypt(
            Nonce(prefix, number),
            plaintext,
            destination.Slice(sizeof(int), plaintext.Length),
            destination.Slice(sizeof(int) + plaintext.Length, TagBytes),
            AssociatedData(name, number, final));

        return sizeof(int) + plaintext.Length + TagBytes;
    }

    private static void Open(
        AesGcm aes,
        ReadOnlySpan<byte> sealedChunk,
        int length,
        Span<byte> plaintext,
        ReadOnlySpan<byte> prefix,
        string name,
        int number,
        bool final) =>
        aes.Decrypt(
            Nonce(prefix, number),
            sealedChunk.Slice(sizeof(int), length),
            sealedChunk.Slice(sizeof(int) + length, TagBytes),
            plaintext[..length],
            AssociatedData(name, number, final));

    private static byte[] Nonce(ReadOnlySpan<byte> prefix, int number)
    {
        var nonce = new byte[NonceBytes];
        prefix.CopyTo(nonce);
        BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(PrefixBytes), number);

        return nonce;
    }

    private static byte[] AssociatedData(string name, int number, bool final) =>
        Encoding.UTF8.GetBytes($"{Purpose}|{name}|{number}|{(final ? "end" : "chunk")}");

    /// <summary>Reads the length of the next chunk and the chunk itself; a stream that ends early is a cut file.</summary>
    private static async Task<int> ReadLengthAsync(Stream stored, byte[] sealedChunk, CancellationToken cancellationToken)
    {
        if (await ReadChunkAsync(stored, sealedChunk.AsMemory(0, sizeof(int)), cancellationToken) != sizeof(int))
        {
            throw new CryptographicException("The stored file ends before its last chunk.");
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(sealedChunk);

        if (length is < 0 or > ChunkBytes)
        {
            throw new CryptographicException("The stored file declares a chunk it cannot have.");
        }

        var expected = length + TagBytes;

        if (await ReadChunkAsync(stored, sealedChunk.AsMemory(sizeof(int), expected), cancellationToken) != expected)
        {
            throw new CryptographicException("The stored file ends in the middle of a chunk.");
        }

        return length;
    }

    private static async Task<int> ReadChunkAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await source.ReadAsync(buffer[total..], cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static Task<int> ReadChunkAsync(Stream source, byte[] buffer, CancellationToken cancellationToken) =>
        ReadChunkAsync(source, buffer.AsMemory(), cancellationToken);
}
