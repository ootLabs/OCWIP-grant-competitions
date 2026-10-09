using Ocwip.Api.Models;
using Ocwip.Api.Services.Documents;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Services.Documents;

/// <summary>
/// The contract template of Kierunek NOWE FIO 2026 (T-45b), read from
/// backend/seed/templates/contract-2026.txt, and the list of the group's
/// members it prints (P17).
/// </summary>
public sealed class Contract2026TemplateTests
{
    internal static string Template()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "seed", "templates", "contract-2026.txt");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new FileNotFoundException($"seed/templates/contract-2026.txt not found above {AppContext.BaseDirectory}");
    }

    [Fact]
    public void The_template_passes_the_check_and_keeps_all_twenty_paragraphs()
    {
        var template = Template();

        Assert.Empty(TemplatePlaceholders.Problems(template));
        var filled = TemplatePlaceholders.Fill(template, new Dictionary<string, string?>());
        Assert.All(Enumerable.Range(1, 20), paragraph => Assert.Contains($"§{paragraph}.", filled));
        Assert.Contains("Klauzula informacyjna dotycząca przetwarzania danych osobowych", filled);
    }

    // An informal group is party to the same contract, and the clause about
    // the register, the number in it, the NIP and the representative's
    // function is not about it. It used to be asked of the group too, which
    // blocked the contract until the operator typed "nie dotyczy" into it
    // (znalezisko 11).
    [Fact]
    public void An_informal_group_is_asked_for_its_leader_instead_of_a_register()
    {
        var template = Template();

        var group = TemplatePlaceholders.In(template, EntityType.InformalGroup).Select(x => x.Name).ToList();
        var organisation = TemplatePlaceholders.In(template, EntityType.Organisation).Select(x => x.Name).ToList();
        var patron = TemplatePlaceholders.In(template, EntityType.PatronInformalGroup).Select(x => x.Name).ToList();

        Assert.DoesNotContain("rejestr", group);
        Assert.DoesNotContain("numer_w_rejestrze", group);
        Assert.DoesNotContain("funkcja_reprezentanta", group);
        Assert.DoesNotContain("nip", group);
        Assert.Contains("adres_lidera", group);
        Assert.Contains("reprezentant", group);

        Assert.Contains("rejestr", organisation);
        Assert.Contains("nip", organisation);
        Assert.Contains("funkcja_reprezentanta", organisation);
        Assert.DoesNotContain("adres_lidera", organisation);
        // The members line belongs to groups only (O-14): an organisation's
        // contract printed "Członkowie grupy nieformalnej ...: nie dotyczy".
        Assert.DoesNotContain("czlonkowie_grupy", organisation);
        Assert.Contains("czlonkowie_grupy", group);
        Assert.Equal(organisation, patron.Where(name => name != "czlonkowie_grupy"));

        // The printed clause reads as a sentence for both kinds.
        var values = new Dictionary<string, string?>
        {
            ["nazwa_realizatora"] = "Grupa Sąsiedzka Zaodrze",
            ["adres_lidera"] = "ul. Polna 1, 45-001 Opole",
            ["reprezentant"] = "Jan Kowalski",
        };
        Assert.Contains(
            "Grupa Sąsiedzka Zaodrze z siedzibą ul. Polna 1, 45-001 Opole reprezentowaną/ym przez: Jan Kowalski, zwaną/ym dalej",
            TemplatePlaceholders.Fill(template, values, EntityType.InformalGroup));
    }

    [Fact]
    public void The_system_fills_what_it_knows_and_the_operator_types_the_rest()
    {
        var placeholders = TemplatePlaceholders.In(Template());

        Assert.Equal(
            ["numer_umowy", "data_zawarcia", "nazwa_realizatora", "adres", "nip", "czlonkowie_grupy",
             "tytul_projektu", "data_zlozenia_wniosku", "kwota_dotacji", "kwota_dotacji_slownie"],
            placeholders.Where(x => x.System).Select(x => x.Name));
        Assert.Contains(placeholders, x => !x.System && x.Name == "numer_rachunku");
        Assert.Contains(placeholders, x => !x.System && x.Name == "numer_umowy_niw");
    }

    [Theory]
    [InlineData(EntityType.PatronInformalGroup, "Anna Testowa, Jan Testowy, Ewa Testowa")]
    [InlineData(EntityType.InformalGroup, "Anna Testowa, Jan Testowy, Ewa Testowa")]
    [InlineData(EntityType.Organisation, GroupMembersValue.NotApplicable)]
    public void The_members_come_from_the_application_table(EntityType kind, string members)
    {
        var answers = AnswerSamples.Element(Application2026FormTests.Answers(kind));

        Assert.Equal(members, GroupMembersValue.Read(Application2026FormTests.Form(), answers, kind));
    }
}
