using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.EntityCards;

/// <summary>The card as it will be stored, or the problems keyed by field.</summary>
internal sealed record EntityCardCheck(
    EntityCardData? Card,
    IDictionary<string, string[]> Problems)
{
    public bool IsValid => Problems.Count == 0;
}

/// <summary>
/// The rules of the Podmiot's card (T-93, docs/runbook/pola.md step 2.2),
/// checked at the API edge because the schema leaves every field nullable
/// (Entity.Nip says why).
///
/// Used twice: on every write of the card, and at submission, where an
/// organisation card written before these rules existed (the seed, an
/// operator's hand) must not become the copy the organiser receives.
///
/// An informal group without a patron has no card (pola.md, type 3), so for
/// <see cref="EntityType.InformalGroup"/> only the name is checked and every
/// other field is dropped rather than stored.
/// </summary>
internal static class EntityCardValidator
{
    private const int NameLength = 300;
    private const int AddressLength = 500;
    private const int ShortTextLength = 100;
    private const int MaxRepresentatives = 20;

    public static EntityCardCheck Validate(EntityCardData card)
    {
        var problems = new Dictionary<string, string[]>();

        var name = Text(card.Name, "name", "Pełna nazwa", NameLength, required: true, problems);

        if (card.Type is EntityType.InformalGroup)
        {
            return Result(new EntityCardData(card.Type, name!, Representatives: []), problems);
        }

        if (card.LegalForm is null)
        {
            problems["legalForm"] = ["Wybierz formę prawną."];
        }

        var legalFormOther = card.LegalForm is LegalForm.Other
            ? Text(card.LegalFormOther, "legalFormOther", "Nazwa formy prawnej", 200, required: true, problems)
            : null;

        string? registerNumber = null;
        switch (card.Register)
        {
            case null:
                problems["register"] = ["Wybierz rejestr."];
                break;
            case EntityRegister.Krs:
                registerNumber = RegistryNumbers.Krs(card.RegisterNumber);
                if (registerNumber is null)
                {
                    problems["registerNumber"] = ["Numer KRS ma 10 cyfr."];
                }

                break;
            default:
                registerNumber = Text(
                    card.RegisterNumber, "registerNumber", "Numer w rejestrze", ShortTextLength, required: true, problems);
                break;
        }

        var nip = RegistryNumbers.Nip(card.Nip);
        if (nip is null)
        {
            problems["nip"] = [string.IsNullOrWhiteSpace(card.Nip)
                ? "Podaj NIP."
                : "To nie jest poprawny NIP. Sprawdź, czy nie ma literówki."];
        }

        string? regon = null;
        if (!string.IsNullOrWhiteSpace(card.Regon))
        {
            regon = RegistryNumbers.Regon(card.Regon);
            if (regon is null)
            {
                problems["regon"] = ["REGON ma 9 albo 14 cyfr i poprawną cyfrę kontrolną."];
            }
        }

        var address = Text(card.Address, "address", "Adres siedziby", AddressLength, required: true, problems);
        var correspondence = Text(
            card.CorrespondenceAddress, "correspondenceAddress", "Adres korespondencyjny", AddressLength,
            required: false, problems);

        var phone = Phone(card.Phone, problems);

        var email = card.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            problems["email"] = ["Podaj adres e-mail."];
        }
        else if (email.Length > 320 || !RegisterRequestValidator.IsAddress(email))
        {
            problems["email"] = ["To nie jest poprawny adres e-mail."];
        }

        var bankAccount = RegistryNumbers.BankAccount(card.BankAccount);
        if (bankAccount is null)
        {
            problems["bankAccount"] = [string.IsNullOrWhiteSpace(card.BankAccount)
                ? "Podaj numer rachunku bankowego."
                : "To nie jest poprawny numer rachunku: 26 cyfr z poprawną sumą kontrolną."];
        }

        var representatives = Representatives(card.Representatives, problems);

        return Result(
            new EntityCardData(
                card.Type,
                name!,
                card.LegalForm,
                legalFormOther,
                card.Register,
                registerNumber,
                nip,
                regon,
                address,
                correspondence,
                phone,
                email,
                bankAccount,
                representatives),
            problems);
    }

    private static EntityCardCheck Result(EntityCardData card, Dictionary<string, string[]> problems) =>
        new(problems.Count == 0 ? card : null, problems);

    private static string? Text(
        string? value, string key, string label, int length, bool required, Dictionary<string, string[]> problems)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (required)
            {
                problems[key] = [$"{label}: to pole jest wymagane."];
            }

            return null;
        }

        if (trimmed.Length > length)
        {
            problems[key] = [$"{label}: najwyżej {length} znaków."];
        }
        else if (RegisterRequestValidator.HasInvisibleCharacter(trimmed))
        {
            problems[key] = [$"{label} zawiera niedozwolone znaki."];
        }

        return trimmed;
    }

    /// <summary>Digits, spaces and "+-()", with 9 to 15 digits: a Polish number with or without a country code.</summary>
    private static string? Phone(string? value, Dictionary<string, string[]> problems)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            problems["phone"] = ["Podaj telefon."];
            return null;
        }

        var digits = trimmed.Count(char.IsAsciiDigit);
        var allowed = trimmed.All(character => char.IsAsciiDigit(character) || character is ' ' or '+' or '-' or '(' or ')');
        if (!allowed || digits is < 9 or > 15 || trimmed.Length > 50)
        {
            problems["phone"] = ["To nie jest poprawny numer telefonu."];
        }

        return trimmed;
    }

    private static List<EntityRepresentative> Representatives(
        IReadOnlyList<EntityRepresentative>? rows, Dictionary<string, string[]> problems)
    {
        var result = new List<EntityRepresentative>();
        if (rows is null || rows.Count == 0)
        {
            problems["representatives"] = ["Podaj co najmniej jedną osobę uprawnioną do reprezentowania."];
            return result;
        }

        if (rows.Count > MaxRepresentatives)
        {
            problems["representatives"] = [$"Najwyżej {MaxRepresentatives} osób."];
            return result;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var prefix = $"representatives[{i}]";
            var row = rows[i];
            result.Add(new EntityRepresentative(
                Text(row?.FirstName, $"{prefix}.firstName", "Imię", ShortTextLength, required: true, problems) ?? string.Empty,
                Text(row?.LastName, $"{prefix}.lastName", "Nazwisko", ShortTextLength, required: true, problems) ?? string.Empty,
                Text(row?.Function, $"{prefix}.function", "Funkcja", ShortTextLength, required: true, problems) ?? string.Empty));
        }

        return result;
    }
}
