using System.Security.Cryptography;
using System.Text;
using Ocwip.Api.Data.Encryption;
using Xunit;

namespace Ocwip.Api.Tests.Data.Encryption;

/// <summary>
/// The attachments were the one place where personal data still lay in the
/// clear (S-38): the volume, and every backup copying it, were the cheapest
/// way to every statute and power of attorney an applicant ever sent. These
/// tests say what the format promises, in the order it matters: the bytes
/// come back, the file is unreadable without the key, and cutting, reordering
/// or swapping what is on disk is detected rather than served.
/// </summary>
public sealed class FileCipherTests
{
    private static readonly byte[] Document =
        Encoding.UTF8.GetBytes(new string('x', 200_000) + "PESEL 85010112345");

    private static FileCipher Cipher() => new(FieldEncryption.Cipher);

    private static async Task<byte[]> StoredAsync(byte[] plain, string name = "plik")
    {
        using var source = new MemoryStream(plain);
        using var stored = new MemoryStream();
        await Cipher().WriteAsync(source, stored, name, CancellationToken.None);

        return stored.ToArray();
    }

    private static async Task<byte[]> ReadBackAsync(byte[] stored, string name = "plik")
    {
        await using var read = await Cipher().ReadAsync(new MemoryStream(stored), name, CancellationToken.None);
        using var plain = new MemoryStream();
        await read.CopyToAsync(plain);

        return plain.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    // Exactly one chunk, and one chunk plus a byte: the boundaries.
    [InlineData(64 * 1024)]
    [InlineData(64 * 1024 + 1)]
    public async Task A_file_of_any_length_comes_back_as_it_went_in(int length)
    {
        var plain = RandomNumberGenerator.GetBytes(length);

        Assert.Equal(plain, await ReadBackAsync(await StoredAsync(plain)));
    }

    [Fact]
    public async Task What_lands_on_the_volume_does_not_carry_the_document()
    {
        var stored = await StoredAsync(Document);

        Assert.DoesNotContain("85010112345", Encoding.UTF8.GetString(stored));
        Assert.Equal(Document, await ReadBackAsync(stored));
    }

    [Fact]
    public async Task A_file_cut_short_is_refused_instead_of_read_as_a_shorter_one()
    {
        var stored = await StoredAsync(Document);

        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => ReadBackAsync(stored[..(stored.Length / 2)]));
    }

    [Fact]
    public async Task One_file_cannot_be_served_in_place_of_another()
    {
        var stored = await StoredAsync(Document, name: "pierwszy");

        await Assert.ThrowsAnyAsync<CryptographicException>(() => ReadBackAsync(stored, name: "drugi"));
    }

    [Fact]
    public async Task A_byte_changed_on_the_volume_is_refused()
    {
        var stored = await StoredAsync(Document);
        stored[^20] ^= 0xFF;

        await Assert.ThrowsAnyAsync<CryptographicException>(() => ReadBackAsync(stored));
    }

    /// <summary>
    /// A file written before S-38 has no header. It reads back as it is, so
    /// the change ships without rewriting the volume first, and
    /// reencrypt-data is what moves those files under a key.
    /// </summary>
    [Fact]
    public async Task A_file_from_before_the_header_is_read_as_it_is() =>
        Assert.Equal(Document, await ReadBackAsync(Document));
}
