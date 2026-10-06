using Microsoft.Extensions.Configuration;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The local disk half of attachment storage (T-32): a round trip returns
/// exactly what was written, and two files never collide on the same path.
/// </summary>
public sealed class AttachmentStorageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly AttachmentStorageService _storage;

    public AttachmentStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"ocwip-attachments-{Guid.NewGuid():N}");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Attachments:StoragePath"] = _root,
            })
            .Build();

        _storage = new AttachmentStorageService(configuration);
    }

    [Fact]
    public async Task Saved_bytes_are_read_back_unchanged()
    {
        // Arrange
        var content = "tresc pliku testowego"u8.ToArray();

        // Act
        var path = await _storage.SaveAsync(new MemoryStream(content), CancellationToken.None);

        await using var stream = await _storage.OpenReadAsync(path, CancellationToken.None);
        using var reader = new MemoryStream();
        await stream.CopyToAsync(reader);

        // Assert
        Assert.Equal(content, reader.ToArray());
    }

    [Fact]
    public async Task Two_uploads_never_share_a_path()
    {
        // Act
        var first = await _storage.SaveAsync(
            new MemoryStream("a"u8.ToArray()), CancellationToken.None);
        var second = await _storage.SaveAsync(
            new MemoryStream("b"u8.ToArray()), CancellationToken.None);

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task The_stored_path_carries_no_trace_of_a_file_name()
    {
        // A path built from the applicant's file name would not be opaque:
        // see Models/Attachment.cs on why that column exists at all.
        var path = await _storage.SaveAsync(
            new MemoryStream("dowolna tresc"u8.ToArray()), CancellationToken.None);

        Assert.DoesNotContain(".", path);
    }

    [Fact]
    public async Task Replacing_never_deletes_the_previous_file()
    {
        // T-32's own "poprzedni nie znika twardo": nothing in this type ever
        // removes a path once SaveAsync returns it, so the file a caller
        // marked inactive is still exactly where OpenReadAsync can find it.
        var original = await _storage.SaveAsync(
            new MemoryStream("oryginal"u8.ToArray()), CancellationToken.None);

        _ = await _storage.SaveAsync(
            new MemoryStream("zastepca"u8.ToArray()), CancellationToken.None);

        await using var stream = await _storage.OpenReadAsync(original, CancellationToken.None);
        using var reader = new MemoryStream();
        await stream.CopyToAsync(reader);

        Assert.Equal("oryginal"u8.ToArray(), reader.ToArray());
    }

    /// <summary>
    /// S-38 through reencrypt-data: a file written before the content was
    /// encrypted is still plaintext on the volume, so the command has to move
    /// it, and what it reads afterwards has to be the same document.
    /// </summary>
    [Fact]
    public async Task A_file_from_before_the_change_is_rewritten_and_still_reads()
    {
        var path = Guid.NewGuid().ToString("N");
        var content = "statut z PESEL 85010112345"u8.ToArray();
        await File.WriteAllBytesAsync(Path.Combine(_root, path), content);

        Assert.True(await _storage.RewriteAsync(path, CancellationToken.None));
        Assert.DoesNotContain(
            "85010112345",
            System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(_root, path))));

        await using var stream = await _storage.OpenReadAsync(path, CancellationToken.None);
        using var reader = new MemoryStream();
        await stream.CopyToAsync(reader);

        Assert.Equal(content, reader.ToArray());
    }

    /// <summary>
    /// A file already under the current key is left exactly as it is. The
    /// command tells the operator to run it again after any failure, and on a
    /// volume of 25 MB documents a second full decrypt and re-encrypt is
    /// hours of work and a fresh chance to break a file that was fine.
    /// </summary>
    [Fact]
    public async Task A_file_already_under_the_current_key_is_left_alone()
    {
        var path = await _storage.SaveAsync(
            new MemoryStream("tresc pod biezacym kluczem"u8.ToArray()), CancellationToken.None);

        var before = await File.ReadAllBytesAsync(Path.Combine(_root, path));

        Assert.False(await _storage.RewriteAsync(path, CancellationToken.None));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(_root, path)));
    }

    /// <summary>A path no row points at any more is not an error, just nothing to do.</summary>
    [Fact]
    public async Task A_missing_file_is_nothing_to_rewrite() =>
        Assert.False(await _storage.RewriteAsync(Guid.NewGuid().ToString("N"), CancellationToken.None));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
