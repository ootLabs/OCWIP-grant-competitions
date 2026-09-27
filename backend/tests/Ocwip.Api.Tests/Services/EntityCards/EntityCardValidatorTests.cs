using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.EntityCards;
using Ocwip.Api.Tests.Endpoints;
using Xunit;

namespace Ocwip.Api.Tests.Services.EntityCards;

/// <summary>The rules of the card per type (T-93, pola.md step 2.2 and type 3).</summary>
public sealed class EntityCardValidatorTests
{
    [Fact]
    public void A_complete_organisation_card_is_stored_normalized()
    {
        var check = EntityCardValidator.Validate(EntityCardEndpointsTests.OrganisationCard());

        Assert.True(check.IsValid);
        Assert.Equal("1111111111", check.Card!.Nip);
        Assert.Equal("73111111111111111111111111", check.Card.BankAccount);
    }

    [Fact]
    public void An_empty_organisation_card_names_every_required_field()
    {
        var check = EntityCardValidator.Validate(new EntityCardData(EntityType.Organisation, ""));

        Assert.False(check.IsValid);
        Assert.Equal(
            ["address", "bankAccount", "email", "legalForm", "name", "nip", "phone", "register", "representatives"],
            check.Problems.Keys.Order());
    }

    [Fact]
    public void A_patron_s_card_is_an_organisation_card()
    {
        var check = EntityCardValidator.Validate(new EntityCardData(EntityType.PatronInformalGroup, "Fundacja"));

        Assert.Contains("nip", check.Problems.Keys);
    }

    [Fact]
    public void An_informal_group_keeps_only_its_name()
    {
        // pola.md, type 3: no card, and never a question about a NIP.
        var check = EntityCardValidator.Validate(
            EntityCardEndpointsTests.OrganisationCard(EntityType.InformalGroup, "  Sąsiedzi  ") with { Nip = "zły" });

        Assert.True(check.IsValid);
        Assert.Equal("Sąsiedzi", check.Card!.Name);
        Assert.Null(check.Card.Nip);
        Assert.Null(check.Card.BankAccount);
        Assert.Empty(check.Card.Representatives!);
    }

    [Fact]
    public void Other_legal_form_needs_its_name_and_another_register_takes_free_text()
    {
        var card = EntityCardEndpointsTests.OrganisationCard() with
        {
            LegalForm = LegalForm.Other,
            Register = EntityRegister.Other,
            RegisterNumber = "Ewidencja starosty nr 12/2019",
        };

        var check = EntityCardValidator.Validate(card);

        Assert.Equal(["legalFormOther"], check.Problems.Keys);
        Assert.True(EntityCardValidator.Validate(card with { LegalFormOther = "Uczniowski klub sportowy" }).IsValid);
    }

    [Theory]
    [InlineData("700 100 200", true)]
    [InlineData("+48 (77) 441-00-00", true)]
    [InlineData("12345", false)]
    [InlineData("zadzwoń po 16", false)]
    public void A_phone_is_digits_and_separators(string phone, bool valid) =>
        Assert.Equal(valid, EntityCardValidator.Validate(EntityCardEndpointsTests.OrganisationCard() with { Phone = phone }).IsValid);

    [Fact]
    public void Each_representative_needs_all_three_fields()
    {
        var check = EntityCardValidator.Validate(EntityCardEndpointsTests.OrganisationCard() with
        {
            Representatives = [new EntityRepresentative("Anna", "", "Prezeska")],
        });

        Assert.Equal(["representatives[0].lastName"], check.Problems.Keys);
    }

    [Fact]
    public void A_number_outside_an_enum_is_refused_rather_than_stored()
    {
        // JsonStringEnumConverter takes {"type": 7} as readily as a name.
        var card = EntityCardEndpointsTests.OrganisationCard();

        Assert.Equal(["type"], EntityCardValidator.Validate(card with { Type = (EntityType)7 }).Problems.Keys);
        Assert.Equal(["legalForm"], EntityCardValidator.Validate(card with { LegalForm = (LegalForm)42 }).Problems.Keys);
        Assert.Equal(["register"], EntityCardValidator.Validate(card with { Register = (EntityRegister)9 }).Problems.Keys);
    }

    [Fact]
    public void A_line_break_in_a_name_is_refused()
    {
        var check = EntityCardValidator.Validate(
            EntityCardEndpointsTests.OrganisationCard(name: "Fundacja\r\nBcc: zly@example.org"));

        Assert.Equal(["name"], check.Problems.Keys);
    }
}
