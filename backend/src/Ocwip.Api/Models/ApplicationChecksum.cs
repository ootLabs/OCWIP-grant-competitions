using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ocwip.Api.Models;

/// <summary>
/// The checksum shown on the "informacje techniczne" block of an application
/// (D15, R-13), computed rather than stored.
///
/// Nothing here is persisted. The value is a pure function of the id, the last
/// saved instant and the answers, so it can never disagree with what is
/// actually on the row. A stored checksum would have to be rewritten on every
/// save right alongside the answers it describes, which is the kind of
/// duplicated fact that drifts apart the day somebody touches one column and
/// not the other.
///
/// Hashed from a CANONICAL form of the answers, not from their raw JSON text:
/// PostgreSQL's jsonb does not preserve object key order or whitespace (see
/// docs/model-danych.md), so the bytes a caller posted and the bytes a later
/// read gets back from the column can differ even though nothing changed.
/// Sorting object keys before hashing makes the checksum agree with itself
/// before the first save and after every one that follows.
/// </summary>
public static class ApplicationChecksum
{
    public static string Compute(Guid id, DateTimeOffset lastSavedAt, JsonElement answers)
    {
        var payload = $"{id:N}|{lastSavedAt.UtcTicks}|{Canonical(answers)}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        // Twelve hex characters, D15's own example: "0a55-22c2-b414". Twelve
        // rather than the full digest, because this is read out over the
        // phone to a support line, not verified cryptographically.
        var hex = Convert.ToHexString(hash)[..12].ToLowerInvariant();

        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}";
    }

    private static string Canonical(JsonElement element)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Object keys sorted ordinally, everything else written as it stands.
    /// Arrays keep their order: order is meaning there, and reordering a list
    /// of budget rows would be reordering the budget.
    /// </summary>
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
