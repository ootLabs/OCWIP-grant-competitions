using System.Runtime.CompilerServices;
using Ocwip.Api.Data.Encryption;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// One field encryption key for the whole test run (T-47a), set before any
/// test: for the contexts the fixtures create directly, and, through the
/// environment variable, for every host the tests boot, which would otherwise
/// read the development key. A key of its own, so nothing passes only
/// because it happens to use the development one.
/// </summary>
internal static class TestFieldEncryption
{
    public const string Key = "dGVzdC1maWVsZC1lbmNyeXB0aW9uLWtleS0zMmJ5dGU=";

    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("FieldEncryption__Keys__1", Key);
        FieldEncryption.Use(new FieldCipher(new Dictionary<int, byte[]> { [1] = Convert.FromBase64String(Key) }));
    }
}
