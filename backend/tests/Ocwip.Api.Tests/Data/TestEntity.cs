using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An entity that satisfies every constraint and every rule of the card
/// (T-93), so a test only has to state the one field it is about and a
/// submission is not refused for an incomplete card. Organisation by default,
/// because that is the type which fills in every optional column and therefore
/// exercises the widest row.
///
/// The numbers pass their checksums and still cannot belong to anybody:
/// NIP 1111111111, KRS 0000000001, an account of all ones.
/// </summary>
internal static class TestEntity
{
    public const string Nip = "1111111111";
    public const string BankAccount = "73111111111111111111111111";

    public static Entity New(string name = "Stowarzyszenie testowe") =>
        new()
        {
            Type = EntityType.Organisation,
            Name = name,
            LegalForm = LegalForm.Association,
            Register = EntityRegister.Krs,
            RegisterNumber = "0000000001",
            Nip = Nip,
            Address = "ul. Testowa 1, 45-000 Opole",
            Phone = "700 100 200",
            Email = "kontakt@example.org",
            BankAccount = BankAccount,
            Representatives = [new EntityRepresentative("Anna", "Testowa", "Prezeska")],
        };
}
