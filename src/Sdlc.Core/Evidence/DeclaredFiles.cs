using System.Text.RegularExpressions;
using Sdlc.Core.Artifacts;

namespace Sdlc.Core.Evidence;

/// <summary>How a declared file and a changed file compare.</summary>
/// <param name="Undelivered">Declared in the plan and not touched by the change.</param>
/// <param name="Undeclared">Touched by the change and not declared in the plan.</param>
public sealed record FileComparison(
    IReadOnlyList<string> Undelivered,
    IReadOnlyList<string> Undeclared)
{
    /// <summary>Whether the declared set and the changed set agree.</summary>
    public bool Agrees => Undelivered.Count == 0 && Undeclared.Count == 0;
}

/// <summary>
/// Reads the files a plan declares, and compares them against what changed.
/// </summary>
/// <remarks>
/// <strong>This is a signal, not a gate.</strong> <c>CON_07.2</c> exists precisely <em>because</em> the
/// comparison cannot see inside a file -- creep within a declared file passes it -- so the comparison was
/// never load-bearing for scope creep, and a noisy blocker gets routed around. What blocks instead is
/// outcome-based: the build fails, tests fail, or the proof is not met.
/// <para>
/// Its real uses are the two it is good at: estimating blast radius for the person at the gate, and
/// detecting collisions between cycles so a batch can schedule around them rather than resolve them.
/// </para>
/// </remarks>
public static class DeclaredFiles
{
    // Anything inside backticks, for reading a proof entry's test names. Wider than the path pattern
    // below, on purpose: a test name has no extension and no slash, so there is nothing else to key on.
    private static readonly Regex _backticked = new(
        @"`(?<text>[^`]+)`",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // A path in a Files table or list: inside backticks, which is how a plan writes one, so prose about a
    // file is not mistaken for a declaration of it.
    private static readonly Regex _quoted = new(
        @"`(?<path>[^`\s]+\.[A-Za-z0-9]+|[^`\s]*/[^`\s]*)`",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// The paths a plan's Files section declares.
    /// </summary>
    /// <remarks>
    /// Read from backticked spans rather than from table cells, because a plan may write the section as a
    /// table or as a list and both are legitimate -- the schema requires the section, not its layout. The
    /// cost is that a path mentioned in prose inside the section counts as declared, which errs toward a
    /// wider declared set; for a signal whose job is estimating blast radius, wide is the safer error.
    /// </remarks>
    /// <param name="markdown">The plan.</param>
    /// <returns>The declared paths, normalised to forward slashes, without duplicates.</returns>
    public static IReadOnlyList<string> Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        if (!ArtifactSections.Read(markdown).TryGetValue("Files", out IReadOnlyList<string>? lines))
        {
            return [];
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            foreach (Match match in _quoted.Matches(line))
            {
                paths.Add(Normalise(match.Groups["path"].Value));
            }
        }

        return [.. paths.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The test names one proof entry commits to.
    /// </summary>
    /// <remarks>
    /// Read from backticked spans, the same way a declared file is. A plan writes prose around the name --
    /// "a test named X asserts it" -- and pulling the name out of prose would mean guessing which word is
    /// the name. Backticks make it a declaration rather than a mention, and the plan skill asks for them.
    /// </remarks>
    /// <param name="entry">One numbered proof entry's text.</param>
    /// <returns>The names it commits to, which may be none.</returns>
    public static IReadOnlyList<string> TestsIn(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (Match match in _backticked.Matches(entry))
        {
            string name = match.Groups["text"].Value.Trim();

            // A path is a file, not a test. A proof entry may name both -- "`RollTests.cs` holds
            // `Rolls_within_one_and_six`" -- and counting the file as a test would make the check pass on a
            // name no runner ever reported.
            if (name.Length > 0 && !name.Contains('/', StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Compares what a plan declared against what a change touched.
    /// </summary>
    /// <param name="declared">The plan's paths.</param>
    /// <param name="changed">The paths the diff touched.</param>
    /// <returns>What each side holds that the other does not.</returns>
    public static FileComparison Compare(
        IReadOnlyList<string> declared,
        IReadOnlyList<string> changed)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(changed);

        HashSet<string> left = new(declared.Select(Normalise), StringComparer.OrdinalIgnoreCase);
        HashSet<string> right = new(changed.Select(Normalise), StringComparer.OrdinalIgnoreCase);

        // A cycle's own artifacts are not part of the change it describes. Counting them would make every
        // cycle report its own intent as undeclared, which is noise that teaches people to ignore this.
        right.RemoveWhere(IsFrameworkOwned);

        return new FileComparison(
            [.. left.Except(right, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)],
            [.. right.Except(left, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)]);
    }

    private static bool IsFrameworkOwned(string path) =>
        path.StartsWith(Cycles.Cycle.SdlcDirectoryName + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One spelling for a path, so the two sides of the comparison can be compared.
    /// </summary>
    /// <remarks>
    /// A leading <c>./</c> goes and a leading dot does not. The first version trimmed any leading dots and
    /// slashes, which turned <c>.sdlc/cycles/...</c> into <c>sdlc/cycles/...</c> — so the filter that keeps a
    /// cycle's own artifacts out of its own comparison never matched, and every cycle reported its own intent
    /// as an undeclared file. Exactly the noise the filter exists to prevent.
    /// </remarks>
    private static string Normalise(string path)
    {
        string slashed = path.Replace('\\', '/').Trim();

        while (slashed.StartsWith("./", StringComparison.Ordinal))
        {
            slashed = slashed[2..];
        }

        return slashed.TrimStart('/');
    }
}
