using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An entity that satisfies every constraint and every rule of the card
/// (T-93), so a test only has to state the one field it is about and a
/// submission is not refused for an incomplete card. Organisation by default,
/// because that is the type which fills in every optional column and therefore
/// exercises the widest row.
///
/// The numbers pass their checksums. KRS 0000000001 and an account of all
/// ones cannot belong to anybody; the NIP is drawn fresh for every entity,
/// because an active card's NIP is unique (T-93a) and the tests share one
/// database.
/// </summary>
internal static class TestEntity
{
    public const string BankAccount = "73111111111111111111111111";

    private static readonly int[] NipWeights = [6, 5, 7, 2, 3, 4, 5, 6, 7];

    /// <summary>Ten digits with a valid checksum, different on every call.</summary>
    public static string NewNip()
    {
        while (true)
        {
            var digits = new int[10];
            for (var i = 0; i < 9; i++)
            {
                digits[i] = Random.Shared.Next(i == 0 ? 1 : 0, 10);
            }

            var check = NipWeights.Select((weight, i) => weight * digits[i]).Sum() % 11;
            if (check == 10)
            {
                continue;
            }

            digits[9] = check;
            return string.Concat(digits);
        }
    }

    public static Entity New(string name = "Stowarzyszenie testowe") =>
        new()
        {
            Type = EntityType.Organisation,
            Name = name,
            LegalForm = LegalForm.Association,
            Register = EntityRegister.Krs,
            RegisterNumber = "0000000001",
            Nip = NewNip(),
            Address = "ul. Testowa 1, 45-000 Opole",
            Phone = "700 100 200",
            Email = "kontakt@example.org",
            BankAccount = BankAccount,
            Representatives = [new EntityRepresentative("Anna", "Testowa", "Prezeska")],
        };
}
