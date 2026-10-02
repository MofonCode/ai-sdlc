using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// A cycle's identity: the moment it was opened, to the second.
/// </summary>
/// <remarks>
/// Purely temporal, so ids sort chronologically and say when the cycle opened, with no opaque
/// component. Allocation needs no coordination and works offline, which a sequence number would not.
/// <para>
/// The id is load-bearing in three places — the branch name, the directory path, and every
/// cross-reference inside the cycle's own artifacts — so it is fixed at allocation and never
/// rewritten.
/// </para>
/// <para>
/// Two cycles opened in the same second would collide, and <see cref="Allocate"/> cannot catch that:
/// at allocation there is no directory to be already there. What catches it is the intent skill
/// creating the cycle's directory as its first act and failing if it exists — creating it <em>is</em>
/// the check, and the filesystem makes that atomic. A check that looked first and created second
/// would leave the race it was written to close.
/// </para>
/// </remarks>
public readonly record struct CycleId
{
    /// <summary>The format an id is written in: date, 24-hour time, seconds.</summary>
    public const string Format = "yyyyMMdd_HHmm_ss";

    private static readonly Regex Pattern = new(
        @"^(?<date>[0-9]{8})_(?<time>[0-9]{4})_(?<seconds>[0-9]{2})$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private CycleId(DateTimeOffset opened) => Opened = opened;

    /// <summary>The moment the cycle was opened, to the second.</summary>
    public DateTimeOffset Opened { get; }

    /// <summary>
    /// Allocates an id for a cycle opening now.
    /// </summary>
    /// <param name="time">
    /// The clock to read. Taken as a parameter rather than read from <see cref="DateTimeOffset.UtcNow"/>
    /// so the suite can allocate a known id instead of asserting against whatever second it ran in.
    /// </param>
    /// <returns>The allocated id.</returns>
    public static CycleId Allocate(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        // Truncated to the second, because the id's resolution is the second: an id carrying
        // milliseconds would not round-trip through its own directory name.
        DateTimeOffset now = time.GetUtcNow();
        return new CycleId(new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            now.Minute,
            now.Second,
            TimeSpan.Zero));
    }

    /// <summary>
    /// Parses an id such as <c>20260929_1456_32</c>.
    /// </summary>
    /// <param name="text">The id to parse.</param>
    /// <param name="id">The parsed id, when this returns true.</param>
    /// <returns>True when <paramref name="text"/> is a well-formed id naming a real moment.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out CycleId? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        if (!Pattern.IsMatch(trimmed))
        {
            return false;
        }

        // A well-formed shape can still name no moment - 20261332_2599_99 matches the pattern - so
        // the calendar has the final say.
        if (!DateTimeOffset.TryParseExact(
            trimmed,
            Format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset opened))
        {
            return false;
        }

        id = new CycleId(opened);
        return true;
    }

    /// <summary>The branch a cycle's work happens on.</summary>
    /// <returns>The branch name, as <c>cycle/&lt;id&gt;</c>.</returns>
    public string BranchName() => "cycle/" + ToString();

    /// <summary>Renders the id as it is written in paths, branches and citations.</summary>
    /// <returns>The id as text.</returns>
    public override string ToString() => Opened.ToString(Format, CultureInfo.InvariantCulture);
}
