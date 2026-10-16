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

    private static readonly Regex _pattern = new(
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
        if (!_pattern.IsMatch(trimmed))
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

    /// <summary>The prefix every cycle branch carries.</summary>
    public const string BranchPrefix = "cycle/";

    /// <summary>The bare branch an older cycle's work happened on.</summary>
    /// <remarks>
    /// Still read, for cycles already on one, and still what the turn-end gate and evidence fall back to
    /// when they cannot tell which branch is the cycle's. Nothing new creates it: git stores branches as
    /// paths, so <c>cycle/&lt;id&gt;</c> and <c>cycle/&lt;id&gt;/plan</c> cannot both exist. D-85.
    /// </remarks>
    /// <returns>The branch name, as <c>cycle/&lt;id&gt;</c>.</returns>
    public string BranchName() => BranchPrefix + ToString();

    /// <summary>The branch a given kind of work on this cycle happens on.</summary>
    /// <param name="kind">Which kind of work.</param>
    /// <returns>The branch name, as <c>cycle/&lt;id&gt;/&lt;kind&gt;</c>.</returns>
    public string BranchName(CycleBranchKind kind) => BranchName() + "/" + CycleBranch.NameOf(kind);

    /// <summary>
    /// Reads the cycle out of a branch name.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="TryParse(string?, out CycleId?)"/> rather than folded into it,
    /// because the two answer different questions. This one asks "is this a cycle branch", and a
    /// branch that is not one is the ordinary case rather than a malformed id -- which is exactly
    /// what the turn-end gate needs to know before it has an opinion about anything.
    /// </remarks>
    /// <param name="branch">
    /// A branch name, as a host or a platform spells it: the bare branch, or a fully qualified
    /// <c>refs/heads/</c> reference. Azure DevOps reports the second and GitHub the first, and both
    /// hand it straight to the pull-request check.
    /// </param>
    /// <param name="id">The cycle the branch belongs to, when this returns true.</param>
    /// <returns>True when the branch is a cycle branch naming a well-formed id.</returns>
    public static bool TryParseBranch(string? branch, [NotNullWhen(true)] out CycleId? id)
    {
        bool parsed = CycleBranch.TryParse(branch, out CycleBranch? read);
        id = read?.Id;
        return parsed;
    }

    /// <summary>Renders the id as it is written in paths, branches and citations.</summary>
    /// <returns>The id as text.</returns>
    public override string ToString() => Opened.ToString(Format, CultureInfo.InvariantCulture);
}
