namespace Ocwip.Api.Data.Encryption;

/// <summary>
/// The process wide <see cref="FieldCipher"/> the EF value converters use
/// (T-47a).
///
/// Static on purpose: EF builds the model once per context type and caches
/// it, converters included, so a cipher passed through the context would be
/// whichever the first context carried. The converters ask for the cipher
/// when they convert, and it is set once at start: by Program, by the
/// administrative commands, and by the test project's module initializer.
/// Converting without it throws, so a missing key stops the write instead of
/// storing plaintext.
/// </summary>
public static class FieldEncryption
{
    public const string Section = "FieldEncryption:Keys";

    private static FieldCipher? _cipher;

    public static FieldCipher Cipher =>
        _cipher ?? throw new InvalidOperationException(
            "No field encryption key is configured. Set FieldEncryption__Keys__1 " +
            "to the base64 of 32 random bytes (docs/wdrozenie.md).");

    public static bool IsConfigured => _cipher is not null;

    /// <summary>Reads FieldEncryption:Keys and sets the cipher. Does nothing when none is configured.</summary>
    public static void Configure(IConfiguration configuration)
    {
        if (Read(configuration) is { } cipher)
        {
            _cipher = cipher;
        }
    }

    public static void Use(FieldCipher cipher) => _cipher = cipher;

    /// <summary>The cipher the configuration describes, or null without keys. Throws on a malformed key.</summary>
    public static FieldCipher? Read(IConfiguration configuration)
    {
        var keys = new Dictionary<int, byte[]>();

        foreach (var entry in configuration.GetSection(Section).GetChildren())
        {
            // Empty counts as missing, like an unset variable in compose.
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            if (!int.TryParse(entry.Key, out var version))
            {
                throw new InvalidOperationException($"{Section}:{entry.Key}: a key version must be a whole number.");
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(entry.Value);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException($"{Section}:{version} is not base64.");
            }

            if (key.Length != FieldCipher.KeyBytes || version <= 0)
            {
                throw new InvalidOperationException(
                    $"{Section}:{version} must be a positive version with a key of {FieldCipher.KeyBytes} bytes.");
            }

            keys[version] = key;
        }

        return keys.Count == 0 ? null : new FieldCipher(keys);
    }
}
