using Ocwip.Api.Services.EntityCards;
using Xunit;

namespace Ocwip.Api.Tests.Services.EntityCards;

/// <summary>The checksums the schema leaves to the API edge (T-93).</summary>
public sealed class RegistryNumbersTests
{
    [Theory]
    [InlineData("1111111111", "1111111111")]
    [InlineData("111-111-11-11", "1111111111")]
    [InlineData("526 025 02 74", "5260250274")]
    public void A_valid_nip_is_stored_as_digits(string typed, string stored) =>
        Assert.Equal(stored, RegistryNumbers.Nip(typed));

    [Theory]
    [InlineData("1234567890")] // the seed's old number: checksum 1, last digit 0
    [InlineData("111111111")]
    [InlineData("11111111111")]
    [InlineData("11111111a1")]
    [InlineData("")]
    [InlineData(null)]
    public void An_invalid_nip_is_refused(string? typed) => Assert.Null(RegistryNumbers.Nip(typed));

    [Fact]
    public void A_nip_whose_checksum_is_ten_is_refused()
    {
        // 000000003: 3 * 7 = 21, and 21 mod 11 is 10, which no single last
        // digit can match. Such prefixes are never issued.
        for (var last = 0; last <= 9; last++)
        {
            Assert.Null(RegistryNumbers.Nip($"000000003{last}"));
        }
    }

    [Theory]
    [InlineData("531234563")]
    [InlineData("111111110")]
    [InlineData("12345678512347")]
    public void A_valid_regon_passes(string typed) => Assert.Equal(typed, RegistryNumbers.Regon(typed));

    [Theory]
    [InlineData("531234564")]
    [InlineData("12345678512348")]
    [InlineData("1234567890")]
    public void An_invalid_regon_is_refused(string typed) => Assert.Null(RegistryNumbers.Regon(typed));

    [Theory]
    [InlineData("0000000001", "0000000001")]
    [InlineData("000 000 00 01", "0000000001")]
    public void A_krs_number_is_ten_digits(string typed, string stored) =>
        Assert.Equal(stored, RegistryNumbers.Krs(typed));

    [Theory]
    [InlineData("123")]
    [InlineData("00000000011")]
    public void Any_other_length_is_not_a_krs_number(string typed) => Assert.Null(RegistryNumbers.Krs(typed));

    [Theory]
    [InlineData("73111111111111111111111111")]
    [InlineData("73 1111 1111 1111 1111 1111 1111")]
    [InlineData("PL73 1111 1111 1111 1111 1111 1111")]
    [InlineData("pl61109010140000071219812874")]
    public void A_valid_account_is_stored_as_26_digits(string typed) =>
        Assert.Equal(26, RegistryNumbers.BankAccount(typed)!.Length);

    [Theory]
    [InlineData("74111111111111111111111111")] // one check digit off
    [InlineData("73111111111111111111111112")] // one account digit off
    [InlineData("7311111111111111111111111")]
    [InlineData("DE73111111111111111111111111")]
    public void An_invalid_account_is_refused(string typed) => Assert.Null(RegistryNumbers.BankAccount(typed));
}
