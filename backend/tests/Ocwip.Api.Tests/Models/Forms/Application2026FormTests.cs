using System.Text.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The application form of Kierunek NOWE FIO 2026 (T-94), read from
/// backend/seed/forms/application-2026.json: one conditional form for the
/// three templates 1a, 1b and 1c. A file that stops passing the contract, or
/// a sample application of any kind that stops passing the submission check,
/// fails here and not in somebody's seed run or in a live competition.
/// </summary>
public sealed class Application2026FormTests
{
    /// <summary>The ceilings of the 2026 competition, from its rules.</summary>
    internal static readonly Dictionary<string, decimal?> Bases = new()
    {
        ["competition.maxGrantAmount"] = 7000m,
        ["competition.maxIndirectCostPercent"] = 10m,
        ["competition.maxAverageAnnualRevenue"] = 50000m,
    };

    internal static JsonElement Definition()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "seed", "forms", "application-2026.json");

            if (File.Exists(path))
            {
                return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
            }
        }

        throw new FileNotFoundException($"seed/forms/application-2026.json not found above {AppContext.BaseDirectory}");
    }

    internal static FormDocument Form()
    {
        var result = FormSchemaValidator.Validate(Definition(), FormPurpose.Application);
        Assert.Empty(result.Errors);
        return result.Document!;
    }

    /// <summary>A complete application of one kind, the way a careful applicant fills it in.</summary>
    internal static JsonObject Answers(EntityType kind, decimal directCost = 5000m, decimal indirectCost = 500m)
    {
        var answers = new JsonObject
        {
            ["rodzaj_wnioskodawcy"] = kind.ToString(),
            ["gmina_realizacji"] = "Lewin Brzeski",
            ["charakterystyka_wnioskodawcy"] = "Działamy od dwóch lat na rzecz sąsiadów.",
            ["tytul_projektu"] = "Ogród sąsiedzki przy szkole",
            ["data_zakonczenia"] = "2026-11-30",
            ["charakterystyka_projektu"] = "Zakładamy ogród i uczymy uprawy warzyw.",
            ["cel_glowny"] = "Zbliżyć sąsiadów przez wspólną pracę.",
            ["opis_pomyslu"] = new string('o', 1000),
            ["opis_dzialan"] = "Przygotowanie terenu, sadzenie, warsztaty, festyn.",
            ["promocja"] = "Plakaty w szkole i profil w mediach społecznościowych.",
            ["rezultaty_opis"] = "Ogród zostaje pod opieką rady rodziców.",
            ["liczba_uczestnikow"] = 40,
            ["liczba_uczestnikow_monitorowanie"] = "Listy obecności",
            ["rezultaty"] = new JsonArray(new JsonObject
            {
                ["rezultat"] = "Promocja",
                ["wartosc_docelowa"] = "10 plakatów",
                ["monitorowanie"] = "Zdjęcia",
            }),
            ["dostepnosc_architektoniczna"] = "Teren bez progów.",
            ["dostepnosc_cyfrowa"] = "Ogłoszenia z tekstem alternatywnym.",
            ["dostepnosc_informacyjna"] = "Informacja w tekście łatwym do czytania.",
            ["koszty_bezposrednie"] = new JsonArray(Cost("Sadzonki", 1, directCost)),
            ["koszty_promocji"] = new JsonArray(Cost("Plakaty", 10, 20m)),
            ["koszty_posrednie"] = new JsonArray(Cost("Księgowość", 1, indirectCost)),
            ["o_zwiazanie"] = true,
            ["o_zgodnosc"] = true,
            ["o_niekaralnosc"] = true,
            ["o_pozytek"] = true,
            ["o_regulamin"] = true,
            ["o_rodo"] = true,
        };

        if (kind is EntityType.Organisation)
        {
            answers["rodzaj_organizacji"] = "mloda";
            answers["data_wpisu"] = "2024-03-01";
            answers["przychod"] = 18000;
            answers["o_siedziba"] = true;
        }
        else
        {
            answers["nazwa_grupy"] = "Sąsiedzi z Zaodrza";
            answers["czlonkowie_grupy"] = new JsonArray(
                Member("Anna Testowa"), Member("Jan Testowy"), Member("Ewa Testowa"));
            answers["o_mieszkancy"] = true;
        }

        if (kind is EntityType.InformalGroup)
        {
            answers["rachunek_lidera"] = "73 1111 1111 1111 1111 1111 1111";
        }

        if (kind is not EntityType.InformalGroup)
        {
            answers["o_podatki"] = true;
            answers["o_skladki"] = true;
        }

        if (kind is EntityType.PatronInformalGroup)
        {
            answers["o_brak_powiazan"] = true;
            answers["o_dane_patrona"] = true;
        }

        return answers;
    }

    private static JsonObject Cost(string name, int count, decimal price) => new()
    {
        ["nazwa"] = name,
        ["jednostka"] = "szt.",
        ["liczba"] = count,
        ["cena"] = price,
    };

    private static JsonObject Member(string name) => new()
    {
        ["imie_i_nazwisko"] = name,
        ["adres"] = "ul. Testowa 1, Opole",
        ["telefon"] = "700 100 200",
        ["email"] = null,
    };

    private static AnswerValidationResult Submit(JsonObject answers, EntityType kind) =>
        AnswerValidator.Validate(Form(), AnswerSamples.Element(answers), Bases, AnswerStrictness.Submission, kind);

    [Theory]
    [InlineData(EntityType.Organisation)]
    [InlineData(EntityType.PatronInformalGroup)]
    [InlineData(EntityType.InformalGroup)]
    public void A_complete_application_of_every_kind_passes_the_submission_check(EntityType kind)
    {
        Assert.Empty(Submit(Answers(kind), kind).Errors);
    }

    [Fact]
    public void Each_kind_is_asked_its_own_part_one_and_its_own_statements()
    {
        var calculator = (EntityType kind) => new AnswerCalculator(Form(), AnswerSamples.Element(Answers(kind)));
        var visible = (EntityType kind) => Form().Sections.SelectMany(section => section.Fields)
            .Where(field => calculator(kind).IsVisible(field.VisibleWhen))
            .ToList();

        var statements = (EntityType kind) => visible(kind).Count(field => field.Type == FormFieldType.Statement);

        // The paper: eight for an organisation, six for a group, seven plus
        // the patron's three; one more everywhere for the data clause.
        Assert.Equal(8 + 1, statements(EntityType.Organisation));
        Assert.Equal(6 + 1, statements(EntityType.InformalGroup));
        Assert.Equal(7 + 3 + 1, statements(EntityType.PatronInformalGroup));

        Assert.Contains(visible(EntityType.Organisation), field => field.Key == "przychod");
        Assert.DoesNotContain(visible(EntityType.PatronInformalGroup), field => field.Key == "przychod");
        Assert.Contains(visible(EntityType.InformalGroup), field => field.Key == "rachunek_lidera");
        Assert.DoesNotContain(visible(EntityType.PatronInformalGroup), field => field.Key == "rachunek_lidera");
    }

    [Fact]
    public void The_operator_s_list_reads_the_title_the_cost_and_the_grant_from_the_roles()
    {
        var values = ApplicationRoleValues.Read(Form(), AnswerSamples.Element(Answers(EntityType.Organisation)));

        Assert.Equal("Ogród sąsiedzki przy szkole", values.ProjectTitle);
        Assert.Equal(5700m, values.TotalCost);
        Assert.Equal(5700m, values.RequestedGrant);
    }

    [Fact]
    public void The_grant_ceiling_and_the_indirect_cost_share_come_from_the_competition()
    {
        // 6 800 + 200 + 800 = 7 800 is over the 7 000 ceiling, and 800 of
        // 7 800 is over the 10 % share of the grant.
        var errors = Submit(Answers(EntityType.Organisation, directCost: 6800m, indirectCost: 800m), EntityType.Organisation)
            .Errors.Select(error => error.Key).ToList();

        Assert.Contains("dotacja", errors);
        // The table total, and the row from which it goes over (T-31).
        Assert.Contains("suma_posrednie", errors);
        Assert.Contains("koszty_posrednie[0].wartosc", errors);
    }

    [Fact]
    public void The_revenue_ceiling_is_the_competition_s_and_asked_of_organisations_only()
    {
        var answers = Answers(EntityType.Organisation);
        answers["przychod"] = 60000;

        Assert.Contains(Submit(answers, EntityType.Organisation).Errors, error => error.Key == "przychod");
    }

    [Theory]
    [InlineData(EntityType.Organisation, EntityType.Organisation, EntityType.Organisation)]
    [InlineData(EntityType.Organisation, EntityType.PatronInformalGroup, EntityType.PatronInformalGroup)]
    [InlineData(EntityType.PatronInformalGroup, EntityType.Organisation, EntityType.Organisation)]
    [InlineData(EntityType.InformalGroup, EntityType.InformalGroup, EntityType.InformalGroup)]
    public void The_kind_comes_from_the_application_within_what_the_card_allows(
        EntityType card, EntityType answered, EntityType stored)
    {
        var resolved = ApplicantKinds.Resolve(Form(), AnswerSamples.Element(Answers(answered)), card);

        Assert.Equal(stored, resolved.Kind);
    }

    [Theory]
    [InlineData(EntityType.InformalGroup, EntityType.Organisation)]
    [InlineData(EntityType.InformalGroup, EntityType.PatronInformalGroup)]
    [InlineData(EntityType.Organisation, EntityType.InformalGroup)]
    public void A_kind_the_card_cannot_be_is_refused_on_the_field(EntityType card, EntityType answered)
    {
        var resolved = ApplicantKinds.Resolve(Form(), AnswerSamples.Element(Answers(answered)), card);

        Assert.Null(resolved.Kind);
        Assert.Equal("rodzaj_wnioskodawcy", resolved.FieldKey);
    }

    [Fact]
    public void The_formal_card_asks_a_foundation_applying_as_a_patron_the_patron_s_criteria()
    {
        // Pitfall 1 of T-94: by the card alone a foundation would get the
        // organisation's criteria even when it applies as a group's patron.
        var kind = ApplicantKinds.Resolve(
            Form(), AnswerSamples.Element(Answers(EntityType.PatronInformalGroup)), EntityType.Organisation).Kind!.Value;
        var formal = FormSchemaValidator.Validate(EvaluationCard("formal"), FormPurpose.FormalEvaluation).Document!;
        var calculator = new AnswerCalculator(formal, AnswerSamples.Element(new JsonObject()), kind);

        var asked = formal.Sections.SelectMany(section => section.Fields)
            .Where(field => field.Role == FormFieldRole.FormalCriterion && calculator.IsApplicable(field))
            .Select(field => field.Key)
            .ToList();

        Assert.DoesNotContain("przychod_do_50000", asked);
        Assert.DoesNotContain("rejestracja_do_60_miesiecy", asked);
    }

    [Fact]
    public void Technical_fields_are_not_printed_and_every_question_is()
    {
        var fields = Form().Sections.SelectMany(section => section.Fields).ToList();

        Assert.All(
            fields.Where(field => field.Key.StartsWith("suma_", StringComparison.Ordinal)),
            field => Assert.False(field.Printed));
        Assert.All(
            fields.Where(field => field.Type is not FormFieldType.Calculated),
            field => Assert.True(field.Printed, field.Key));
    }

    [Theory]
    [InlineData(EntityType.Organisation)]
    [InlineData(EntityType.InformalGroup)]
    public void The_printed_application_has_the_answers_and_neither_technical_nor_hidden_fields(EntityType kind)
    {
        var facts = new Ocwip.Api.Services.Pdf.ApplicationPdfFacts(
            "001", "Kierunek NOWE FIO 2026", "Wnioskodawca testowy", kind, 1,
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero), "0000-0000-0000");

        var text = Ocwip.Api.Tests.Services.PdfTextReader.Text(
            Ocwip.Api.Services.Pdf.ApplicationPdfBuilder.Build(facts, Form(), AnswerSamples.Element(Answers(kind))));

        Assert.Contains("Ogród sąsiedzki przy szkole", text);
        Assert.Contains("Wnioskowana kwota dotacji", text);
        Assert.DoesNotContain("Razem A", text);
        if (kind is EntityType.Organisation)
        {
            Assert.Contains("Roczny przychód", text);
            Assert.DoesNotContain("Członkowie grupy", text);
        }
        else
        {
            Assert.Contains("Członkowie grupy", text);
            Assert.DoesNotContain("Roczny przychód", text);
        }
    }

    private static JsonElement EvaluationCard(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "seed", "evaluation-cards", $"{name}-2026.json");

            if (File.Exists(path))
            {
                return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
            }
        }

        throw new FileNotFoundException($"seed/evaluation-cards/{name}-2026.json");
    }
}
