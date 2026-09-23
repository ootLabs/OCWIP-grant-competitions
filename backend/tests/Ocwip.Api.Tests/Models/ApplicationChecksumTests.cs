using System.Text.Json;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Models;

/// <summary>
/// The checksum on an application's "informacje techniczne" block (D15,
/// R-13). No database here: the value is a pure function of the id, the last
/// saved instant and the answers, and that purity is the point under test.
/// </summary>
public sealed class ApplicationChecksumTests
{
    private static readonly Guid Id =
        Guid.Parse("00000000-0000-4000-a000-000000000001");

    private static readonly DateTimeOffset SavedAt =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Matches_the_three_group_hexadecimal_shape_from_the_decision()
    {
        var checksum = ApplicationChecksum.Compute(Id, SavedAt, Parse("{}"));

        Assert.Matches("^[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}$", checksum);
    }

    [Fact]
    public void Is_stable_for_the_exact_same_input()
    {
        var answers = Parse("""{"opis":"tresc"}""");

        var first = ApplicationChecksum.Compute(Id, SavedAt, answers);
        var second = ApplicationChecksum.Compute(Id, SavedAt, answers);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Does_not_depend_on_object_key_order_or_whitespace()
    {
        // PostgreSQL's jsonb reorders keys and drops whitespace on the way in
        // (docs/model-danych.md), so a value read back after a save must
        // checksum the same as the value that was posted, or the number on
        // screen would change every time nothing did.
        var asPosted = Parse("""{ "b": 2, "a": 1 }""");
        var asStoredByPostgres = Parse("""{"a":1,"b":2}""");

        Assert.Equal(
            ApplicationChecksum.Compute(Id, SavedAt, asPosted),
            ApplicationChecksum.Compute(Id, SavedAt, asStoredByPostgres));
    }

    [Fact]
    public void Changes_when_the_answers_change()
    {
        var before = ApplicationChecksum.Compute(Id, SavedAt, Parse("""{"a":1}"""));
        var after = ApplicationChecksum.Compute(Id, SavedAt, Parse("""{"a":2}"""));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Changes_with_every_saved_version_even_when_the_answers_do_not()
    {
        // D15: the checksum changes with every saved version. A save that
        // re-writes identical answers still moves the last saved instant, and
        // that alone has to be enough to tell the two versions apart.
        var answers = Parse("""{"a":1}""");

        var first = ApplicationChecksum.Compute(Id, SavedAt, answers);
        var second = ApplicationChecksum.Compute(
            Id, SavedAt.AddMinutes(1), answers);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Two_applications_with_identical_answers_do_not_share_a_checksum()
    {
        var otherId = Guid.Parse("00000000-0000-4000-a000-000000000002");
        var answers = Parse("""{"a":1}""");

        Assert.NotEqual(
            ApplicationChecksum.Compute(Id, SavedAt, answers),
            ApplicationChecksum.Compute(otherId, SavedAt, answers));
    }

    [Fact]
    public void Preserves_array_order_as_meaningful()
    {
        var first = Parse("""["a","b"]""");
        var reordered = Parse("""["b","a"]""");

        Assert.NotEqual(
            ApplicationChecksum.Compute(Id, SavedAt, first),
            ApplicationChecksum.Compute(Id, SavedAt, reordered));
    }
}
