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
    /// S-44: a file without the header is refused, not served as it is. Only
    /// a file from before S-38 or one put on the volume by hand can lack it,
    /// and serving it would hand out any content with no integrity check.
    /// The stored stream is closed on the way out all the same.
    /// </summary>
    [Fact]
    public async Task A_file_without_the_header_is_refused()
    {
        var stored = new MemoryStream(Document);

        await Assert.ThrowsAnyAsync<CryptographicException>(
            () => Cipher().ReadAsync(stored, "plik", CancellationToken.None));
        Assert.False(stored.CanRead);
    }

    /// <summary>
    /// reencrypt-data is the one reader that still takes a file from before
    /// S-38 as it is, because moving those files under a key is its job.
    /// </summary>
    [Fact]
    public async Task A_file_from_before_the_header_is_read_for_rewriting()
    {
        await using var read = await Cipher().ReadForRewriteAsync(
            new MemoryStream(Document), "plik", CancellationToken.None);
        using var plain = new MemoryStream();
        await read.CopyToAsync(plain);

        Assert.Equal(Document, plain.ToArray());
    }

    /// <summary>
    /// What comes back is a stream of its own, so the stored one is closed
    /// here. Left open it would cost the API a file descriptor per download
    /// and reencrypt-data one per attachment on the volume, until a finalizer
    /// got round to it.
    /// </summary>
    [Fact]
    public async Task The_stored_stream_is_closed_once_its_plaintext_is_out()
    {
        var stored = new MemoryStream(await StoredAsync(Document));

        await using (await Cipher().ReadAsync(stored, "plik", CancellationToken.None))
        {
            // Nothing: the question is what happened to the source.
        }

        Assert.False(stored.CanRead);
    }

    /// <summary>
    /// Whether reencrypt-data has anything to do: a file under the key in
    /// force is left alone, one from before S-38 and one under an older key
    /// are not. Rerunning the command is the documented answer to any
    /// failure, and a volume of 25 MB documents must not be rewritten twice
    /// over for nothing.
    /// </summary>
    [Fact]
    public async Task Only_a_file_under_an_older_key_or_none_needs_rewriting()
    {
        var first = new byte[FieldCipher.KeyBytes];
        var second = new byte[FieldCipher.KeyBytes];
        RandomNumberGenerator.Fill(first);
        RandomNumberGenerator.Fill(second);

        var one = new FileCipher(new FieldCipher(new Dictionary<int, byte[]> { [1] = first }));
        var two = new FileCipher(new FieldCipher(new Dictionary<int, byte[]> { [1] = first, [2] = second }));

        using var plain = new MemoryStream("tresc"u8.ToArray());
        using var underKeyOne = new MemoryStream();
        await one.WriteAsync(plain, underKeyOne, "plik", CancellationToken.None);

        Assert.True(await one.IsCurrentAsync(new MemoryStream(underKeyOne.ToArray()), CancellationToken.None));
        Assert.False(await two.IsCurrentAsync(new MemoryStream(underKeyOne.ToArray()), CancellationToken.None));
        Assert.False(await two.IsCurrentAsync(new MemoryStream(Document), CancellationToken.None));
    }

    /// <summary>
    /// Nothing is staged on the way out. The first version of this decrypted
    /// into a temporary file, which put the whole document back in the clear
    /// on a disk: the one thing S-38 is about, and `DeleteOnClose` does not
    /// survive a kill. Reading is chunk by chunk, so a reader that stops
    /// early has never touched the rest of the file.
    /// </summary>
    [Fact]
    public async Task Reading_decrypts_only_as_far_as_the_reader_goes()
    {
        var stored = new MemoryStream(await StoredAsync(Document));
        await using var read = await Cipher().ReadAsync(stored, "plik", CancellationToken.None);

        var head = new byte[16];
        var taken = await read.ReadAsync(head, CancellationToken.None);

        Assert.Equal(16, taken);
        Assert.Equal(Document[..16], head);

        // The source is not at its end: the rest has not been touched.
        Assert.True(stored.Position < stored.Length);
    }

    [Fact]
    public async Task The_stored_stream_closes_with_the_one_handed_back()
    {
        var stored = new MemoryStream(await StoredAsync(Document));

        await using (await Cipher().ReadAsync(stored, "plik", CancellationToken.None))
        {
        }

        Assert.Throws<ObjectDisposedException>(() => stored.Position);
    }
}
