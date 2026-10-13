using System.Text.RegularExpressions;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Evidence;

/// <summary>How a declared file and a changed file compare.</summary>
/// <param name="Undelivered">Declared in the plan and not touched by the change.</param>
/// <param name="Undeclared">Touched by the change and not declared in the plan.</param>
/// <param name="Governing">Touched by the change and holding a rule the change is judged by.</param>
public sealed record FileComparison(
    IReadOnlyList<string> Undelivered,
    IReadOnlyList<string> Undeclared,
    IReadOnlyList<string> Governing)
{
    /// <summary>
    /// Whether the declared set and the changed set agree.
    /// </summary>
    /// <remarks>
    /// <c>Governing</c> is deliberately not part of this. A plan may declare a change to an architecture
    /// test and be entirely in order, so a governing file is not a disagreement -- it is the same two sets
    /// agreeing about a file that happens to judge the cycle. Folding it in here would report a correct
    /// plan as inconsistent, which is the noise D-15 says gets the whole line ignored.
    /// </remarks>
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
    //
    // Three shapes: a name with an extension, anything with a slash, and a dotfile at the root. The
    // third was missing, so a plan declaring `.editorconfig` -- a file the comparison itself reports
    // as governing -- had it come back as undeclared, and 'sdlc scaffold' could not see that an
    // adopted solution's plan had asked for it. Lower case only, which is what a dotfile is and what
    // keeps a backticked `.NET` in the same section from being read as a path.
    private static readonly Regex _quoted = new(
        @"`(?<path>[^`\s]+\.[A-Za-z0-9]+|[^`\s]*/[^`\s]*|\.[a-z][a-z0-9_-]*)`",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // Matched on the file name alone, because each is written in more than one directory and means the
    // same thing in all of them: Directory.Build.props at the root makes warnings errors and again under
    // tests/ relaxes that, and either decides whether a build passes.
    private static readonly HashSet<string> _governingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Directory.Build.props",
        "BannedSymbols.txt",
        ".editorconfig",
    };

    // Matched whole. The solution's own constraints and its declared stack are the two rule sources a
    // plan is read against; the hook configuration is what denies a write before it happens.
    private static readonly HashSet<string> _governingPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        Cycle.SdlcDirectoryName + "/" + FileConstraintDocuments.SolutionConstraintsFileName,
        Cycle.SdlcDirectoryName + "/" + Cycle.TechStackFileName,
        ".claude/settings.json",
        ".agents/hooks.json",
    };

    // Matched as a prefix, because each is a directory whose every file is a rule: the pre-push hook, the
    // host's hook manifests, and the pipeline that installs the gate and calls it (D-37).
    private static readonly string[] _governingPrefixes =
    [
        Cycle.SdlcDirectoryName + "/hooks/",
        ".github/hooks/",
        ".github/workflows/",
    ];

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

        // Taken before the cycle's own paperwork is dropped, because two governing files live under
        // .sdlc/ -- the solution's constraints and its declared stack -- and dropping them first would
        // hide the two changes most worth a second look.
        string[] governing = [.. right.Where(Governs).Order(StringComparer.Ordinal)];

        // A cycle's own artifacts are not part of the change it describes. Counting them would make every
        // cycle report its own intent as undeclared, which is noise that teaches people to ignore this.
        right.RemoveWhere(IsFrameworkOwned);

        return new FileComparison(
            [.. left.Except(right, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)],
            [.. right.Except(left, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)],
            governing);
    }

    /// <summary>
    /// Whether a path holds a rule the change touching it is judged by.
    /// </summary>
    /// <remarks>
    /// <strong>A cycle is allowed to change these, and that is exactly why they are reported.</strong> The
    /// architecture suite is the only mechanical enforcement of shape the framework has, and a cycle may
    /// amend it as ordinary declared work -- the dice-roller cycle added an <c>AppHost</c> role to both of
    /// its rule tables, correctly, and it reached a reviewer as one row among thirty-nine in a file table.
    /// A change to the rules is not a change to the work, and a reviewer who cannot tell them apart reviews
    /// the second while the first goes past.
    /// <para>
    /// The set is what the framework itself writes or scaffolds, for D-40's reason: it can name those with
    /// confidence, and a guess at what else a team treats as a rule would be a list that is wrong somewhere
    /// and trusted everywhere. A solution wanting more adds an architecture test, which is the tier that
    /// binds.
    /// </para>
    /// <para>
    /// A signal like the rest of this comparison, under D-15. It refuses nothing.
    /// </para>
    /// </remarks>
    /// <param name="path">A path relative to the solution root, in either slash.</param>
    /// <returns>True when the path holds a rule rather than work.</returns>
    public static bool Governs(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string normalised = Normalise(path);

        // Any directory: Directory.Build.props is written at the root and again under tests/, and the
        // second is where the relaxations for test code live.
        string name = normalised[(normalised.LastIndexOf('/') + 1)..];

        if (_governingNames.Contains(name))
        {
            return true;
        }

        // The architecture suite, wherever a solution puts it. Keyed on the project's own name rather than
        // a path, because 'sdlc scaffold' writes tests/<Solution>.Architecture.Tests and a solution that
        // moved it still means the same thing by it.
        foreach (string segment in normalised.Split('/'))
        {
            if (segment.EndsWith(".Architecture.Tests", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return _governingPaths.Contains(normalised)
            || _governingPrefixes.Any(prefix =>
                normalised.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a path is one of the framework's own files rather than part of the change.
    /// </summary>
    /// <remarks>
    /// Public because two readers need the same answer and they used to disagree. This comparison has
    /// always excluded a cycle's own artifacts, for the reason stated where it is called; the recorded
    /// patch did not, so <c>diff.patch</c> opened with the intent, the spec, the plan and every findings
    /// round before reaching a line of code -- and on a second recording it contained the previous
    /// <c>diff.patch</c>, which doubles its size every time. A verify reads that patch and nothing else,
    /// so the top of it has to be the change.
    /// </remarks>
    /// <param name="path">A path relative to the solution root, in either slash.</param>
    /// <returns>True when the framework owns it.</returns>
    public static bool IsFrameworkOwned(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Normalise(path)
            .StartsWith(Cycles.Cycle.SdlcDirectoryName + "/", StringComparison.OrdinalIgnoreCase);
    }

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
