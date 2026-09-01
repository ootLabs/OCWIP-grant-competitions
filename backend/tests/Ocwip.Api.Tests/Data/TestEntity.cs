using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// An entity that satisfies every constraint, so a test only has to state the
/// one field it is about. Organisation by default, because that is the type
/// which fills in every optional column and therefore exercises the widest row.
/// </summary>
internal static class TestEntity
{
    public static Entity New(string name = "Stowarzyszenie testowe") =>
        new()
        {
            Type = EntityType.Organisation,
            Name = name,
            ContactInformation = "kontakt@example.org",
            Nip = "1234567890",
            Address = "ul. Testowa 1, 45-000 Opole",
        };
}
