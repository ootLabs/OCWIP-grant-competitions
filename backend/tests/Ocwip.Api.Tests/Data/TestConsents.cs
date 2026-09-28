using Microsoft.Extensions.Configuration;
using Ocwip.Api.Services.Consents;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The versions of the documents in force (T-107), read from the same
/// seed/consents the host reads, for a test that registers an account the
/// way the form does: accepting everything.
/// </summary>
internal static class TestConsents
{
    private static readonly Lazy<IReadOnlyList<string>> Versions =
        new(() => [.. new ConsentCatalog(new ConfigurationBuilder().Build()).Current.Select(x => x.Version)]);

    public static IReadOnlyList<string> All => Versions.Value;
}
