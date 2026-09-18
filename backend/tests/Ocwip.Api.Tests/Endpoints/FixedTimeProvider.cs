namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// A clock a test can move.
///
/// The effective state of a competition is a function of the current moment,
/// and the cases worth asserting are a minute either side of the closing
/// minute (D7). A test that can only observe the real clock cannot reach them,
/// and one that waits for them is a test nobody runs.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}
