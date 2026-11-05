using System.Globalization;
using System.Text.RegularExpressions;
using Sdlc.Core.Init;

namespace Sdlc.Core.Solutions;

/// <summary>
/// The files that decide how .NET code is built, and which of them a declared stack takes.
/// </summary>
/// <remarks>
/// <para><strong>One list, because four things read it and a second copy is how they come to
/// disagree.</strong> The first draft of a declared stack lists these as rows, the options menu
/// describes each one, <c>sdlc scaffold</c> writes them, and <c>sdlc stack</c> compares what is
/// declared against what is on disk. Each used to be reachable only as a path and a template, so what
/// a file <em>does</em> was written out again wherever somebody needed to say it (F-22, one level
/// up).</para>
///
/// <para><strong>They are rows in the declared stack rather than a flag on adoption.</strong>
/// <c>sdlc init --adopt-standard</c> wrote them, which made adoption decide a .NET question before
/// anybody had said what the solution was built from, and wrote them whatever the stack went on to
/// declare. A row per file puts the decision in the one file that says what a solution is built from,
/// in a person's hand, and makes declining one file an edit to the line that names it (D-77).</para>
///
/// <para><strong>A row declares; it writes nothing.</strong> The files arrive inside a cycle, from
/// <c>sdlc scaffold</c>, for the ones an approved plan declares. <c>Directory.Build.props</c> makes
/// warnings errors, so the moment it lands is the moment a build can fail on files nobody touched --
/// which has to be the declared scope of one cycle rather than a side effect of whichever build ran
/// next.</para>
///
/// <para><strong>Read, never written</strong>, for the reason the rest of the file is.</para>
/// </remarks>
public static class BuildSettings
{
    /// <summary>The heading the rows sit under in a declared stack.</summary>
    public const string Heading = "## Build settings";

    // Three cells, like a rule row, and told apart from one by where it sits rather than by an id: a
    // file name has no prefix to key on, so the section is what makes a row one of these.
    private static readonly Regex _rowPattern = new(
        @"^\s*\|\s*`?(?<file>[^|`]*?)`?\s*\|\s*(?<on>[^|]*?)\s*\|\s*(?<what>[^|]*?)\s*\|\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>One build-settings file the framework ships.</summary>
    /// <param name="Path">Where it goes, relative to the solution root, with forward slashes.</param>
    /// <param name="Summary">What it does, in the one line a row carries.</param>
    /// <param name="Detail">
    /// What is in it, what switching it on costs a codebase that predates it, and what it depends on.
    /// </param>
    /// <param name="Contents">The file as the framework writes it.</param>
    public sealed record Setting(string Path, string Summary, string Detail, string Contents)
    {
        /// <summary>The path as this machine writes it.</summary>
        public string OnDisk => Path.Replace('/', System.IO.Path.DirectorySeparatorChar);
    }

    /// <summary>One row of a declared stack's build settings.</summary>
    /// <param name="File">The file the row names, with forward slashes.</param>
    /// <param name="On">Whether the solution declares that it takes the file.</param>
    /// <param name="What">What the row says the file does. For a reader; nothing binds on it.</param>
    public sealed record Row(string File, bool On, string What);

    /// <summary>Every build-settings file the framework ships, in the order they are written.</summary>
    /// <remarks>
    /// The order is load order for a reader rather than for a build: the conventions, the policy that
    /// enforces them, the versions that policy needs, the SDK, and last the relaxations that only mean
    /// something once the policy is there.
    /// </remarks>
    public static IReadOnlyList<Setting> All { get; } =
    [
        new(
            ".editorconfig",
            "The code conventions: formatting and style a build can enforce, and naming an editor guides",
            "Indentation, file-scoped namespaces, explicit types, collection expressions and the naming "
            + "scheme. Formatting (`IDE0055`) and sealing (`CA1852`) are set to `error`, which decides a "
            + "build only where `Directory.Build.props` is also on; without it this file guides an editor "
            + "and nothing else. Naming is `IDE1006`, which no build enforces -- the skeleton's "
            + "architecture tests do. **On a codebase that predates it:** beside `Directory.Build.props`, "
            + "every formatting difference in an existing file becomes a build error. A repository that "
            + "already has an `.editorconfig` keeps its own; nothing is merged into it.",
            Templates.EditorConfig()),
        new(
            "Directory.Build.props",
            "Makes warnings errors, sets the analyser level and wires the banned-API list, for every project",
            "Targets `net10.0` with nullable and implicit usings on; `TreatWarningsAsErrors`, "
            + "`AnalysisLevel` at `latest-recommended`, security analysis at `All` and "
            + "`EnforceCodeStyleInBuild`; a NuGet audit that fails the build at `moderate`; "
            + "`EnableMSTestRunner`, which is half of a pair with the runner in `global.json`; and the "
            + "banned-API analyser, reading `BannedSymbols.txt` where one exists. **On a codebase that "
            + "predates it:** this is the one that breaks a build nobody touched -- every existing "
            + "warning becomes an error, and it sets the target framework for any project that does not "
            + "set its own. It references the analyser without a version, so it needs "
            + "`Directory.Packages.props` beside it to supply one.",
            Templates.BuildProps()),
        new(
            "Directory.Packages.props",
            "Central package versions, so a project cannot pick its own",
            "Switches on central package management with transitive pinning, and pins the versions this "
            + "release builds against: the kernel, validation, the banned-API analyser, the api "
            + "description and its viewer, OpenTelemetry, MSTest and dependency injection. **On a "
            + "codebase that predates it:** every `PackageReference` carrying its own `Version` is "
            + "refused until the version moves here, and every package the codebase uses that this file "
            + "does not list needs a line added.",
            Templates.PackageProps()),
        new(
            "global.json",
            "Pins the SDK and selects the test runner, without which the suite silently does not run",
            "SDK `10.0.100`, rolling forward to the latest feature band, and `Microsoft.Testing.Platform` "
            + "as the runner `dotnet test` uses. The runner is half of a pair with `EnableMSTestRunner` "
            + "in `Directory.Build.props`: with one and not the other, `dotnet test` exits zero having "
            + "run nothing, which reads as a clean run. **On a codebase that predates it:** every "
            + "machine and pipeline needs a .NET 10 SDK, and a test project still on the older runner "
            + "stops running under `dotnet test` until it moves.",
            Templates.GlobalJson()),
        new(
            "tests/Directory.Build.props",
            "The six analyser rules relaxed for test code, each with its reason",
            "Imports the root `Directory.Build.props` so a test project keeps the target framework, the "
            + "analyser level and warnings-as-errors; marks test projects as not packable; and relaxes "
            + "`CA1707`, `CA1515`, `CA1307`, `CA1305`, `CA2007` and `CA1861` for test code only, each "
            + "with the reason beside it. It imports the file above it, so it is only worth switching on "
            + "together with `Directory.Build.props`. **On a codebase that predates it:** it reaches "
            + "what is under `tests/` and nothing else, so a codebase that keeps its tests somewhere "
            + "else gets nothing from it.",
            Templates.TestBuildProps()),
    ];

    /// <summary>
    /// The shipped file a path names, if it names one.
    /// </summary>
    /// <param name="path">A path relative to the solution root, in either slash.</param>
    /// <returns>The setting, or null when the framework ships no file there.</returns>
    public static Setting? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string normalised = path.Replace('\\', '/').Trim().TrimStart('/');

        return All.FirstOrDefault(setting =>
            string.Equals(setting.Path, normalised, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every build-settings row in a declared stack.
    /// </summary>
    /// <remarks>
    /// Read from under <see cref="Heading"/> and nowhere else. A rule row is recognised anywhere by its
    /// id; a file name carries nothing like that, so reading three-cell rows from the whole document
    /// would count a rule as a file. A row naming a file the framework does not ship is returned like
    /// any other -- the caller decides what that means, and <c>sdlc doctor</c> says so.
    /// </remarks>
    /// <param name="markdown">The declared stack's text.</param>
    /// <returns>The rows in document order. Empty when the file has no such section.</returns>
    public static IReadOnlyList<Row> Rows(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<Row> rows = [];
        bool inside = false;

        foreach (string raw in markdown.Split('\n'))
        {
            string line = raw.TrimEnd('\r');

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line.Trim(), Heading, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inside)
            {
                continue;
            }

            Match match = _rowPattern.Match(line);

            if (!match.Success)
            {
                continue;
            }

            string file = match.Groups["file"].Value.Trim();

            // The header names the columns and the divider is dashes and colons. Keyed on content
            // rather than position, so prose above the table cannot shift which lines are skipped.
            if (file.Length == 0
                || string.Equals(file, "File", StringComparison.OrdinalIgnoreCase)
                || file.All(character => character is '-' or ':' or ' '))
            {
                continue;
            }

            rows.Add(new Row(
                file.Replace('\\', '/'),
                StackRules.IsOn(match.Groups["on"].Value),
                match.Groups["what"].Value));
        }

        return rows;
    }

    /// <summary>
    /// The build-settings table as a declared stack carries it.
    /// </summary>
    /// <param name="on">Whether the rows are written switched on.</param>
    /// <returns>The header, the divider and one row per shipped file.</returns>
    public static IEnumerable<string> Table(bool on) =>
    [
        "| File | On | What it does |",
        "|---|---|---|",
        .. All.Select(setting => string.Create(
            CultureInfo.InvariantCulture,
            $"| `{setting.Path}` | {(on ? "true" : "false")} | {setting.Summary} |")),
    ];
}
