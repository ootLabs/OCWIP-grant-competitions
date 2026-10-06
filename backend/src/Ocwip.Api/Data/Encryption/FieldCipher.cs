using System.Security.Cryptography;
using System.Text;

namespace Ocwip.Api.Data.Encryption;

/// <summary>
/// Encrypts one sensitive value at a time (T-47a): AES-256-GCM, a random
/// nonce per value, and the key version in the text, so a rotated key still
/// reads what the old one wrote.
///
/// Stored form: <c>enc:&lt;version&gt;:&lt;base64(nonce | tag | ciphertext)&gt;</c>.
/// The purpose (the column, for example "entities.address") is bound in as
/// associated data: a ciphertext copied into another column does not decrypt.
///
/// The keys come from configuration (FieldEncryption:Keys:&lt;version&gt;,
/// base64 of 32 bytes), never from the repository, the database or
/// DataProtection: losing the DataProtection directory costs sessions, losing
/// this key costs the data (docs/wdrozenie.md, "Klucz szyfrowania").
/// </summary>
public sealed class FieldCipher
{
    public const string Prefix = "enc:";
    public const int KeyBytes = 32;

    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly IReadOnlyDictionary<int, byte[]> _keys;

    public FieldCipher(IReadOnlyDictionary<int, byte[]> keys)
    {
        if (keys.Count == 0)
        {
            throw new ArgumentException("At least one key is needed.", nameof(keys));
        }

        foreach (var (version, key) in keys)
        {
            if (version <= 0 || key.Length != KeyBytes)
            {
                throw new ArgumentException($"Key {version} must have a positive version and {KeyBytes} bytes.", nameof(keys));
            }
        }

        _keys = keys;
        CurrentVersion = keys.Keys.Max();
    }

    /// <summary>The version new values are written with: the highest one configured.</summary>
    public int CurrentVersion { get; }

    public static bool IsEncrypted(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Encrypt(string plaintext, string purpose)
    {
        var key = _keys[CurrentVersion];
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var sealedBytes = new byte[NonceBytes + TagBytes + plain.Length];
        var nonce = sealedBytes.AsSpan(0, NonceBytes);
        var tag = sealedBytes.AsSpan(NonceBytes, TagBytes);
        var cipher = sealedBytes.AsSpan(NonceBytes + TagBytes);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(purpose));

        return $"{Prefix}{CurrentVersion}:{Convert.ToBase64String(sealedBytes)}";
    }

    /// <summary>
    /// The plaintext of a stored value. A value without the prefix is a row
    /// written before T-47a and comes back as it is, until reencrypt-data
    /// rewrites it.
    /// </summary>
    public string Decrypt(string stored, string purpose)
    {
        if (!IsEncrypted(stored))
        {
            return stored;
        }

        var separator = stored.IndexOf(':', Prefix.Length);
        if (separator < 0
            || !int.TryParse(stored.AsSpan(Prefix.Length, separator - Prefix.Length), out var version))
        {
            throw new CryptographicException("A stored value is not in the encrypted format.");
        }

        if (!_keys.TryGetValue(version, out var key))
        {
            throw new CryptographicException($"A stored value needs key {version}, which is not configured.");
        }

        var sealedBytes = Convert.FromBase64String(stored[(separator + 1)..]);
        if (sealedBytes.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("A stored value is too short to be encrypted.");
        }

        var plain = new byte[sealedBytes.Length - NonceBytes - TagBytes];
        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(
            sealedBytes.AsSpan(0, NonceBytes),
            sealedBytes.AsSpan(NonceBytes + TagBytes),
            sealedBytes.AsSpan(NonceBytes, TagBytes),
            plain,
            Encoding.UTF8.GetBytes(purpose));

        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>
    /// A keyed digest of a value, in hexadecimal (S-20). For things that have
    /// to be short and stable but must not double as an oracle: a plain hash
    /// of a document holding a PESEL, cut to a few characters, is something
    /// whoever reads the database can brute force the PESEL out of, because
    /// the only unknown is eleven digits.
    ///
    /// The key is derived from the current field key rather than being it:
    /// one key, one primitive, and the purpose separates one digest from
    /// another. Rotating the field key therefore changes every digest, which
    /// is why docs/wdrozenie.md says what that means for a checksum already
    /// printed on somebody's confirmation.
    /// </summary>
    public string Sign(string purpose, string payload)
    {
        var key = DeriveKey(CurrentVersion, purpose);

        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>
    /// A key of its own for one use, derived from a configured key (S-38).
    /// One key, one primitive: the field key stays with AES-GCM over column
    /// values, and whatever else needs keying gets a separate one it cannot
    /// work backwards from.
    /// </summary>
    internal byte[] DeriveKey(int version, string purpose)
    {
        if (!_keys.TryGetValue(version, out var key))
        {
            throw new CryptographicException(
                $"A stored value needs key {version}, which is not configured.");
        }

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, key, KeyBytes, info: Encoding.UTF8.GetBytes(purpose));
    }

    /// <summary>Whether a stored value is already under the current key, so reencrypt-data can skip it.</summary>
    public bool IsCurrent(string stored) =>
        stored.StartsWith($"{Prefix}{CurrentVersion}:", StringComparison.Ordinal);
}
