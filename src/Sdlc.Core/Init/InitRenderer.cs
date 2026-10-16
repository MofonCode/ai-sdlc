using System.Globalization;
using System.Text;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Init;

/// <summary>
/// Renders what adoption did.
/// </summary>
/// <remarks>
/// Adoption runs once and is read closely, because it is the moment a team decides whether to trust the
/// thing. So it says what mode it ran in and why that matters, lists every file including the ones it
/// deliberately left alone, and ends with the one next step -- and it prints what it would not write,
/// rather than mentioning that something was skipped.
/// </remarks>
public static class InitRenderer
{
    private const string Indent = "  ";

    /// <summary>
    /// Renders an adoption report.
    /// </summary>
    /// <param name="report">What adoption did.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(Initialise.Response report)
    {
        ArgumentNullException.ThrowIfNull(report);

        StringBuilder output = new();

        output.AppendLine(Indent + Headline(report.Mode));
        output.AppendLine();

        foreach (string line in Explain(report))
        {
            output.AppendLine(Indent + line);
        }

        output.AppendLine();

        foreach (Initialise.InitAction action in report.Actions)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{Verb(action.Change),-8}{action.Path.Replace('\\', '/')}"));

            if (action.Why is { } why)
            {
                output.AppendLine(Indent + "        " + why);
            }
        }

        foreach (KeyValuePair<string, string> file in report.Printed)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{file.Key.Replace('\\', '/')} {Preamble(report, file.Key)}"));

            output.AppendLine();

            foreach (string line in file.Value.ReplaceLineEndings().Split(Environment.NewLine))
            {
                output.AppendLine(Indent + "    " + line);
            }
        }

        foreach (string line in Cautions(report))
        {
            output.AppendLine(line.Length == 0 ? string.Empty : Indent + line);
        }

        // The half of an upgrade this run could not do, because the file is a person's. Named, with
        // where the answers go, and nothing more: the skill walks them (D-93).
        if (report.Unmentioned is { IsEmpty: false } unmentioned)
        {
            output.AppendLine();
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}The declared stack does not mention {string.Join(", ", unmentioned.Names)}, which this release"));
            output.AppendLine(Indent + "ships. Nothing here writes it: 'sdlc stack' lists them, '/sdlc:init' walks each with you,");
            output.AppendLine(Indent + "and a row answering it -- 'none' or false included -- in a draft a person renames is the end.");
        }

        // One step, never the list of commands worth knowing: what is left is said in a sentence and
        // the first of it is named. The command sheet is in the README this just wrote (D-79).
        if (report.Setup is { } setup
            && Setup.Render(setup, "'sdlc doctor' names the step after this one.") is { Count: > 0 } next)
        {
            output.AppendLine();

            foreach (string line in next)
            {
                output.AppendLine(line);
            }
        }

        return output.ToString();
    }

    /// <summary>
    /// How to introduce a block that was printed rather than written.
    /// </summary>
    /// <remarks>
    /// The generic sentence is right for a pipeline, where what is printed is the whole file. It is wrong
    /// for the solution document, where what is printed is three frontmatter keys and the rest of the file
    /// is a team's own prose -- a reader told "here is what belongs in it" would paste the keys over the
    /// paragraph describing their solution.
    /// </remarks>
    /// <param name="report">What adoption did.</param>
    /// <param name="path">The path the block belongs at.</param>
    /// <returns>The sentence that follows the path.</returns>
    private static string Preamble(Initialise.Response report, string path) =>
        report.PlatformPending
        && path.Replace('\\', '/').EndsWith(Cycle.SdlcDirectoryName + "/" + Cycle.SolutionFileName, StringComparison.Ordinal)
            ? "already exists, and only a person writes it. Replace its frontmatter keys with these:"
            : "was not written. Here is what belongs in it, to merge by hand:";

    /// <summary>
    /// What the run bought that a file listing does not show.
    /// </summary>
    /// <remarks>
    /// One thing, and it is something a reader finds out later and badly if adoption does not say it:
    /// a pipeline on disk that the record does not know governs the merge.
    /// </remarks>
    /// <param name="report">What adoption did.</param>
    /// <returns>The lines, empty when there is nothing to say.</returns>
    private static string[] Cautions(Initialise.Response report)
    {
        List<string> lines = [];

        if (report.PlatformPending)
        {
            lines.AddRange([
                string.Empty,
                "The pipeline is written and the record is not. .sdlc/solution.md already exists and only",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a person writes it, so it still says platform: {SolutionDocument.NameOf(report.Platform)}. While it does, doctor"),
                "reports which guarantee is installed from the record rather than from disk, and the pr",
                "skill reads the same key before it pushes. Place the block above and they agree again.",
            ]);
        }

        return [.. lines];
    }

    private static string Headline(Initialise.InitMode mode) => mode switch
    {
        Initialise.InitMode.Greenfield => "adopted ai-sdlc into a new repository",
        Initialise.InitMode.Onboard => "adopted ai-sdlc into an existing repository",
        Initialise.InitMode.Update => "updated the ai-sdlc scaffolding",
        _ => "adopted ai-sdlc",
    };

    private static string Verb(Initialise.InitChange change) => change switch
    {
        Initialise.InitChange.Created => "created",
        Initialise.InitChange.Updated => "updated",
        Initialise.InitChange.Kept => "kept",
        Initialise.InitChange.Printed => "printed",
        _ => "touched",
    };

    /// <summary>
    /// What the mode means for the solution, in the words that matter to a reader.
    /// </summary>
    /// <remarks>
    /// The origin is the one thing here with lasting consequences, and nothing else will ever explain it:
    /// it decides whether a profile's defaults bind or the existing standard wins, and it never changes
    /// again. A reader who learns that later learns it from a surprise.
    /// </remarks>
    private static string[] Explain(Initialise.Response report) => report.Mode switch
    {
        Initialise.InitMode.Greenfield =>
        [
            "This repository held no code, so the solution is scaffolded: the profile's patterns are",
            "this solution's patterns, and a plan that departs from them has to say why.",
        ],

        Initialise.InitMode.Onboard =>
        [
            "This repository already held code, so the solution is adopted: where it has an existing",
            "standard, that standard wins. The profile proposes a shape for new work and does not rewrite",
            "the conventions of a codebase that predates it.",
            string.Empty,
            "Nothing here is in violation on its first day. Constraints govern a change, never code that",
            "came before adoption, so there is no baseline to record and nothing to forgive.",
            string.Empty,
            "To hold new work to the profile's conventions instead, 'sdlc draft solution --standard profile'",
            "writes that change for you to rename into place. The .NET build settings are separate, and",
            "are rows in the declared stack: 'sdlc draft techstack' lists them switched off here, because",
            "Directory.Build.props makes warnings errors and that can fail a build over files nobody has",
            "touched. Switch one on and a cycle whose plan declares the file brings it.",
        ],

        Initialise.InitMode.Update =>
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"The solution was already governed, stays {SolutionDocument.NameOf(report.Origin)}, and is on the {SolutionDocument.NameOf(report.Standard)} standard."),
            "Only what the framework owns was refreshed -- the hook configs, the pipeline, and the README.",
            "The six files only a person writes were not touched, by the same rule the hook uses rather",
            "than by a second list.",
        ],

        _ => [],
    };

    /// <summary>
    /// Renders what the solution skeleton wrote.
    /// </summary>
    /// <remarks>
    /// It ends by naming the build stage's next step rather than stopping at a file list. The skeleton
    /// is written inside an approved cycle, so the files are part of that cycle's diff and the build is
    /// what records them -- a reader who stops here has code on disk that no evidence covers.
    /// </remarks>
    /// <param name="report">What was written.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string RenderScaffold(ScaffoldSolution.Response report)
    {
        ArgumentNullException.ThrowIfNull(report);

        StringBuilder output = new();

        output.AppendLine();
        output.AppendLine(report.Skeleton
            ? string.Create(CultureInfo.InvariantCulture, $"{Indent}scaffolded {report.Name}")
            : Indent + "wrote the build settings this cycle's plan declares");
        output.AppendLine();

        foreach (string path in report.Written)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}  created {path.Replace(Path.DirectorySeparatorChar, '/')}"));
        }

        foreach (string path in report.Kept)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}  kept    {path.Replace(Path.DirectorySeparatorChar, '/')} - already there"));
        }

        output.AppendLine();

        foreach (string line in report.Written.Count == 0 && !report.Skeleton
            ? (string[])
            [
                "Nothing was written: every build settings file the plan declares was already there. A",
                "file this repository has is kept as it is, whoever wrote it -- overwriting is the one",
                "thing this must not do.",
            ]
            : report.Written.Count == 0
            ?
            [
                "Nothing was written: every file was already there. This has run before, or the",
                "solution has grown past the skeleton. Overwriting is the one thing a scaffold must",
                "not do, so it left them alone.",
            ]
            : !report.Skeleton
            ?
            [
                "No skeleton: this solution's origin is adopted, so it has a shape already. These are the",
                "build settings the plan declared and nothing else, and a file this repository already",
                "had was kept as it is.",
                string.Empty,
                "Directory.Build.props makes warnings errors, so where it was written the next build can",
                "fail on files this cycle has not touched. That is what the plan declared and a person",
                "approved. Each of these is created once and never rewritten, so narrowing a severity or",
                "scoping a rule to a directory is an ordinary edit to it.",
                string.Empty,
                "These files are part of this cycle's change. Build, test, and record the evidence, or",
                "there are settings on disk that no verification covers.",
            ]
            :
            [
                "The reference slice is one worked example of the shape every operation here should",
                "have. It is part of this cycle's change, and real work replaces it rather than being",
                "written beside it. The architecture tests beside it are what make the conventions in",
                ".editorconfig enforceable - naming is an IDE-only diagnostic, so the build decides",
                "none of it on its own.",
                string.Empty,
                "These files are part of this cycle's change. Build, test, and record the evidence, or",
                "there is code on disk that no verification covers.",
            ])
        {
            output.AppendLine(line.Length == 0 ? string.Empty : Indent + line);
        }

        return output.ToString();
    }
}
