using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Data.Encryption;
using Xunit;

namespace Ocwip.Api.Tests.Data.Encryption;

/// <summary>The field cipher of T-47a on its own, without a database.</summary>
public sealed class FieldCipherTests
{
    private static byte[] Key(byte fill) => Enumerable.Repeat(fill, FieldCipher.KeyBytes).ToArray();

    private static FieldCipher Cipher(params (int Version, byte Fill)[] keys) =>
        new(keys.ToDictionary(x => x.Version, x => Key(x.Fill)));

    [Fact]
    public void A_value_comes_back_and_the_stored_text_does_not_show_it()
    {
        var cipher = Cipher((1, 7));

        var stored = cipher.Encrypt("85010112345", "users.pesel");

        Assert.StartsWith("enc:1:", stored);
        Assert.DoesNotContain("85010112345", stored);
        Assert.Equal("85010112345", cipher.Decrypt(stored, "users.pesel"));
    }

    [Fact]
    public void The_same_value_twice_is_stored_differently()
    {
        var cipher = Cipher((1, 7));

        // A random nonce each time: two equal PESELs must not show as equal.
        Assert.NotEqual(cipher.Encrypt("ul. Polna 1", "entities.address"), cipher.Encrypt("ul. Polna 1", "entities.address"));
    }

    [Fact]
    public void A_ciphertext_moved_to_another_column_does_not_decrypt()
    {
        var cipher = Cipher((1, 7));
        var stored = cipher.Encrypt("700 100 200", "entities.phone");

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(stored, "entities.address"));
    }

    [Fact]
    public void Without_the_right_key_nothing_is_read()
    {
        var stored = Cipher((1, 7)).Encrypt("Anna Testowa", "entities.representatives");

        Assert.ThrowsAny<CryptographicException>(() => Cipher((1, 8)).Decrypt(stored, "entities.representatives"));
        Assert.ThrowsAny<CryptographicException>(() => Cipher((2, 7)).Decrypt(stored, "entities.representatives"));
    }

    [Fact]
    public void After_a_rotation_new_values_use_the_new_key_and_old_ones_still_read()
    {
        var old = Cipher((1, 7)).Encrypt("stara wartość", "contracts.values");
        var rotated = Cipher((1, 7), (2, 9));

        Assert.Equal(2, rotated.CurrentVersion);
        Assert.StartsWith("enc:2:", rotated.Encrypt("nowa", "contracts.values"));
        Assert.Equal("stara wartość", rotated.Decrypt(old, "contracts.values"));
        Assert.False(rotated.IsCurrent(old));
    }

    [Fact]
    public void A_value_written_before_encryption_reads_as_it_is()
    {
        Assert.Equal("ul. Testowa 1", Cipher((1, 7)).Decrypt("ul. Testowa 1", "entities.address"));
    }

    [Theory]
    [InlineData("enc:x:AAAA")]
    [InlineData("enc:1")]
    [InlineData("enc:1:AAAA")]
    public void A_broken_ciphertext_is_refused_not_returned(string stored)
    {
        Assert.ThrowsAny<Exception>(() => Cipher((1, 7)).Decrypt(stored, "entities.address"));
    }

    [Fact]
    public void Keys_are_read_from_configuration_and_a_bad_one_stops_the_start()
    {
        IConfiguration Settings(string? value) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FieldEncryption:Keys:1"] = value })
            .Build();

        Assert.NotNull(FieldEncryption.Read(Settings(Convert.ToBase64String(Key(3)))));
        Assert.Null(FieldEncryption.Read(Settings("")));
        Assert.Throws<InvalidOperationException>(() => FieldEncryption.Read(Settings(Convert.ToBase64String(new byte[16]))));
    }

    [Fact]
    public void Inside_a_document_only_the_chosen_values_are_encrypted_and_the_rest_stays_searchable()
    {
        using var document = JsonDocument.Parse("""{"tytul":"Nasz projekt","czlonkowie":[{"imie":"Anna"}],"liczba":3}""");

        var stored = EncryptedDocument.Protect(document.RootElement, key => key == "czlonkowie", "applications.answers");

        Assert.Equal("Nasz projekt", stored.GetProperty("tytul").GetString());
        Assert.Equal(3, stored.GetProperty("liczba").GetInt32());
        Assert.DoesNotContain("Anna", stored.GetRawText());
        var revealed = EncryptedDocument.Reveal(stored, "applications.answers");
        Assert.Equal("Anna", revealed.GetProperty("czlonkowie")[0].GetProperty("imie").GetString());
    }

    [Fact]
    public void Text_that_only_looks_encrypted_is_kept_as_typed()
    {
        // Typed into an ordinary field. Stored as it is, the next read would
        // try to decrypt it and fail for the whole document.
        using var document = JsonDocument.Parse("""{"tytul":"enc:1:AAAA"}""");

        var stored = EncryptedDocument.Protect(document.RootElement, _ => false, "applications.answers");

        Assert.Equal("enc:1:AAAA", EncryptedDocument.Reveal(stored, "applications.answers").GetProperty("tytul").GetString());
    }
}
