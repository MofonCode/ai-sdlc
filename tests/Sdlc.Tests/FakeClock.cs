namespace Sdlc.Tests;

/// <summary>
/// A clock that answers with the instant a test handed it.
/// </summary>
/// <remarks>
/// This is the whole return on taking <see cref="TimeProvider"/> rather than reading the clock from a
/// static: a stamped line can be asserted for what it says instead of asserted around. Every solution
/// this framework scaffolds has the static variants banned by <c>BannedSymbols.txt</c>, and since the
/// framework now bans them in its own build too, this is how its drafters are tested.
/// </remarks>
/// <param name="now">What the clock reads, for as long as the test runs.</param>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
