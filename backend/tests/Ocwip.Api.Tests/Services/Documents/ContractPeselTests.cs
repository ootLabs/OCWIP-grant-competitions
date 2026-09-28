using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Documents;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Services.Documents;

/// <summary>
/// A PESEL typed into a contract (T-47a): masked on every screen, kept when
/// the screen sends the mask back, and in full only in the contract PDF.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContractPeselTests(PostgresDatabaseFixture database)
{
    private const string Pesel = "85010112345";

    [Fact]
    public void Only_the_last_four_characters_are_shown()
    {
        Assert.Equal("*******2345", TemplatePlaceholders.Mask(Pesel));
        Assert.Equal("***", TemplatePlaceholders.Mask("123"));
        Assert.True(TemplatePlaceholders.IsPesel("pesel_skarbnika"));
        Assert.False(TemplatePlaceholders.IsPesel("numer_rachunku"));
    }

    private async Task<Guid> ContractAsync()
    {
        await using var context = database.CreateContext();
        var chain = await TestApplicationChain.SeedAsync(database, "pesel");
        var application = TestApplication.Submitted(chain, $"PE/{Guid.NewGuid():N}"[..20]);
        context.Applications.Add(application);
        var template = new DocumentTemplate
        {
            CompetitionId = chain.CompetitionId, Kind = DocumentKind.Contract, VersionNumber = 1,
            Body = "Skarbnik: {{pesel_skarbnika}}, konto {{numer_rachunku}}.",
        };
        context.Add(template);
        await context.SaveChangesAsync();

        var contract = new Contract
        {
            ApplicationId = application.Id,
            CompetitionId = chain.CompetitionId,
            EntityId = chain.EntityId,
            TemplateId = template.Id,
            Values = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["pesel_skarbnika"] = Pesel }),
        };
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();
        return contract.Id;
    }

    private static string? Value(ContractResult result, string name) =>
        result.Contract!.Fields.Single(x => x.Name == name).Value;

    [RequiresDatabaseFact]
    public async Task The_screen_gets_the_mask_and_sending_it_back_keeps_the_number()
    {
        var id = await ContractAsync();

        await using (var context = database.CreateContext())
        {
            var service = new ContractService(context, TimeProvider.System);
            var shown = await service.GetAsync(id, CancellationToken.None);
            Assert.Equal("*******2345", Value(shown, "pesel_skarbnika"));

            // The form sends everything back, the untouched mask included.
            var saved = await service.SaveValuesAsync(id, new Dictionary<string, string?>
            {
                ["pesel_skarbnika"] = "*******2345",
                ["numer_rachunku"] = "12 3456",
            }, CancellationToken.None);
            Assert.Equal(ContractOutcome.Succeeded, saved.Outcome);
        }

        await using (var context = database.CreateContext())
        {
            var contract = await context.Contracts.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(Pesel, contract.Values.GetProperty("pesel_skarbnika").GetString());

            var pdf = await new ContractService(context, TimeProvider.System).PdfAsync(id, CancellationToken.None);
            Assert.Contains(Pesel, PdfTextReader.Text(pdf.Pdf!));
        }
    }

    [RequiresDatabaseFact]
    public async Task A_new_number_replaces_the_old_one()
    {
        var id = await ContractAsync();
        await using var context = database.CreateContext();
        var service = new ContractService(context, TimeProvider.System);

        await service.SaveValuesAsync(id, new Dictionary<string, string?> { ["pesel_skarbnika"] = "90010154321" }, CancellationToken.None);

        var stored = await context.Contracts.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("90010154321", stored.Values.GetProperty("pesel_skarbnika").GetString());
    }
}
