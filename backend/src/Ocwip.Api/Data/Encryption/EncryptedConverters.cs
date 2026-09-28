using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ocwip.Api.Data.Encryption;

/// <summary>
/// A text column stored encrypted (T-47a). The purpose names the column, see
/// <see cref="FieldCipher"/>. Null stays null, so an empty optional field is
/// not a ciphertext of nothing.
/// </summary>
public sealed class EncryptedStringConverter(string purpose) : ValueConverter<string, string>(
    plaintext => FieldEncryption.Cipher.Encrypt(plaintext, purpose),
    stored => FieldEncryption.Cipher.Decrypt(stored, purpose))
{
    /// <summary>Untyped, so it fits the nullable string properties it is used on.</summary>
    public static ValueConverter For(string purpose) => new EncryptedStringConverter(purpose);
}

/// <summary>
/// Sensitive values inside a jsonb document (T-47a). The document stays a
/// document: the check constraints on its shape and the jsonb type hold, and
/// only the chosen top level values turn into strings with the encrypted
/// JSON of the value, whatever its type (a text, a number, a table).
///
/// Reading decrypts every top level value that carries the prefix, whoever
/// chose it: so a document whose sensitive keys only a form knows
/// (applications.answers) is protected by the service that knows the form
/// (<see cref="Protect"/>) and still read back here.
/// </summary>
public static class EncryptedDocument
{
    public static JsonElement Protect(JsonElement document, Func<string, bool> sensitive, string purpose)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return document;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.EnumerateObject())
        {
            var value = property.Value;

            // A text that merely looks encrypted ("enc:1:..." typed into an
            // ordinary field) is encrypted too. Otherwise Reveal would try to
            // decrypt it and every read of the document would fail.
            var looksEncrypted = value.ValueKind == JsonValueKind.String && FieldCipher.IsEncrypted(value.GetString());
            var encrypt = looksEncrypted || (sensitive(property.Name) && value.ValueKind != JsonValueKind.Null);

            result[property.Name] = encrypt
                ? JsonSerializer.SerializeToElement(FieldEncryption.Cipher.Encrypt(value.GetRawText(), purpose))
                : value.Clone();
        }

        return JsonSerializer.SerializeToElement(result);
    }

    public static JsonElement Reveal(JsonElement document, string purpose)
    {
        if (document.ValueKind != JsonValueKind.Object || !HasProtectedValue(document))
        {
            return document;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.EnumerateObject())
        {
            var value = property.Value;
            if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && FieldCipher.IsEncrypted(text))
            {
                using var plain = JsonDocument.Parse(FieldEncryption.Cipher.Decrypt(text, purpose));
                result[property.Name] = plain.RootElement.Clone();
            }
            else
            {
                result[property.Name] = value.Clone();
            }
        }

        return JsonSerializer.SerializeToElement(result);
    }

    /// <summary>Whether any top level value is encrypted.</summary>
    public static bool HasProtectedValue(JsonElement document) =>
        document.ValueKind == JsonValueKind.Object
        && document.EnumerateObject().Any(x =>
            x.Value.ValueKind == JsonValueKind.String && FieldCipher.IsEncrypted(x.Value.GetString()));
}

/// <summary>Every top level value of the document chosen by <paramref name="sensitive"/> is encrypted on the way in.</summary>
public sealed class EncryptedDocumentConverter(string purpose, Func<string, bool> sensitive)
    : ValueConverter<JsonElement, JsonElement>(
        document => EncryptedDocument.Protect(document, sensitive, purpose),
        stored => EncryptedDocument.Reveal(stored, purpose));

/// <summary>
/// Only decrypts: the values are encrypted by the service that knows which
/// ones are sensitive (applications.answers, T-47a), and written as they are.
/// </summary>
public sealed class RevealedDocumentConverter(string purpose)
    : ValueConverter<JsonElement, JsonElement>(
        document => document,
        stored => EncryptedDocument.Reveal(stored, purpose));
