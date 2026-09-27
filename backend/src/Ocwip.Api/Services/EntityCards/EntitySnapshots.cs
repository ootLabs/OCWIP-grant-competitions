using System.Text.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.EntityCards;

/// <summary>
/// The card as data (T-93): read off the row, and frozen into the copy a
/// submitted application keeps (Application.EntitySnapshot). One shape for
/// both, <see cref="EntityCardData"/>, so the copy reads back exactly like the
/// card it was taken from.
/// </summary>
internal static class EntitySnapshots
{
    // The same options the API writes with, so enums stay names in the copy
    // for the reason docs/architektura.md gives for the wire.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static EntityCardData ToData(Entity entity) =>
        new(
            entity.Type,
            entity.Name,
            entity.LegalForm,
            entity.LegalFormOther,
            entity.Register,
            entity.RegisterNumber,
            entity.Nip,
            entity.Regon,
            entity.Address,
            entity.CorrespondenceAddress,
            entity.Phone,
            entity.Email,
            entity.BankAccount,
            entity.Representatives.ToList());

    public static JsonElement Capture(Entity entity) =>
        JsonSerializer.SerializeToElement(ToData(entity), Json);

    public static EntityCardData? Read(JsonElement? snapshot) =>
        snapshot is { } value ? value.Deserialize<EntityCardData>(Json) : null;

    /// <summary>Writes a checked card onto the row. The type too, where the caller allows it.</summary>
    public static void Apply(Entity entity, EntityCardData card)
    {
        entity.Type = card.Type;
        entity.Name = card.Name;
        entity.LegalForm = card.LegalForm;
        entity.LegalFormOther = card.LegalFormOther;
        entity.Register = card.Register;
        entity.RegisterNumber = card.RegisterNumber;
        entity.Nip = card.Nip;
        entity.Regon = card.Regon;
        entity.Address = card.Address;
        entity.CorrespondenceAddress = card.CorrespondenceAddress;
        entity.Phone = card.Phone;
        entity.Email = card.Email;
        entity.BankAccount = card.BankAccount;
        entity.Representatives = card.Representatives?.ToList() ?? [];
    }
}
