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
/// deliberately left alone, and ends with the next command -- and it prints what it would not write,
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

        output.AppendLine();
        output.AppendLine(Indent + "Next");
        output.AppendLine(Indent + "    sdlc constraints            the rules now in force");
        output.AppendLine(Indent + "    sdlc draft constraint       add one of this solution's own");
        output.AppendLine();

        // Said rather than done. A hook written into .git/hooks would exist on one machine and silently not
        // on anybody else's, and a check that is absent where nobody looks is worse than no check.
        output.AppendLine(Indent + "The pre-push hook is written but not installed -- .git is not tracked, so a hook");
        output.AppendLine(Indent + "placed there would exist on your machine and silently not on anybody else's. To");
        output.AppendLine(Indent + "install it, per clone:");
        output.AppendLine();
        output.AppendLine(Indent + "    git config core.hooksPath .sdlc/hooks");

        return output.ToString();
    }

    /// <summary>
    /// How to introduce a block that was printed rather than written.
    /// </summary>
    /// <remarks>
    /// The generic sentence is right for a pipeline, where what is printed is the whole file. It is wrong
    /// for the solution document, where what is printed is four frontmatter keys and the rest of the file
    /// is a team's own prose -- a reader told "here is what belongs in it" would paste the keys over the
    /// paragraph describing their solution.
    /// </remarks>
    /// <param name="report">What adoption did.</param>
    /// <param name="path">The path the block belongs at.</param>
    /// <returns>The sentence that follows the path.</returns>
    private static string Preamble(Initialise.Response report, string path) =>
        (report.StandardPending || report.PlatformPending)
        && path.Replace('\\', '/').EndsWith(Cycle.SdlcDirectoryName + "/" + Cycle.SolutionFileName, StringComparison.Ordinal)
            ? "already exists, and only a person writes it. Replace its four frontmatter keys with these:"
            : "was not written. Here is what belongs in it, to merge by hand:";

    /// <summary>
    /// What the run bought that a file listing does not show.
    /// </summary>
    /// <remarks>
    /// Both of these are consequences of <c>--adopt-standard</c>, and both are things a reader finds out
    /// later and badly if adoption does not say them: a build that now fails on code nobody touched, and
    /// a recorded standard that still disagrees with the files on disk.
    /// </remarks>
    /// <param name="report">What adoption did.</param>
    /// <returns>The lines, empty when there is nothing to say.</returns>
    private static string[] Cautions(Initialise.Response report)
    {
        List<string> lines = [];

        bool applied = report.Origin == SolutionOrigin.Adopted
            && Initialise.StackFiles().Any(file => report.Actions.Any(action =>
                string.Equals(action.Path, file.Path, StringComparison.Ordinal)
                && action.Change == Initialise.InitChange.Created));

        if (applied)
        {
            lines.AddRange([
                string.Empty,
                "The profile's stack files were written into a codebase that predates them, which is what",
                "--adopt-standard asks for and nothing here softens. Directory.Build.props makes warnings",
                "errors, so the next build can fail on files nobody has touched.",
                string.Empty,
                "Each of them is yours to amend -- adoption creates one once and never rewrites it, update",
                "included -- so narrowing a severity or scoping a rule to a directory is the move, rather",
                "than giving the opt-in back. A file this repository already had was kept, not replaced.",
            ]);
        }

        if (report.StandardPending)
        {
            lines.AddRange([
                string.Empty,
                "The files are written and the record is not. .sdlc/solution.md already exists and only a",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"person writes it, so it still says standard: {SolutionDocument.NameOf(report.Standard)} and every plan is still held to"),
                "the surrounding code. Place the block above and the two agree again; until then this is",
                "the one state where the files on disk and the rule a plan reads disagree.",
            ]);
        }

        if (report.PlatformPending)
        {
            lines.AddRange([
                string.Empty,
                "The pipeline is written and the record is not. .sdlc/solution.md already exists and only",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a person writes it, so it still says platform: {SolutionDocument.NameOf(report.Platform)}. While it does, doctor"),
                "reports which guarantee is installed from the record rather than from disk, and the land",
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

        Initialise.InitMode.Onboard when report.Standard == SolutionStandard.Profile =>
        [
            "This repository already held code, so the solution is adopted -- the origin says so and never",
            "changes. But the standard recorded is the profile's, which --adopt-standard asked for: its",
            "patterns are this solution's patterns for new work, and a plan that departs from them has to",
            "say why.",
            string.Empty,
            "Nothing here is in violation on its first day. Constraints govern a change, never code that",
            "came before adoption, so there is no baseline to record and nothing to forgive. The code",
            "already here is not being migrated by this command -- a cycle does that, like any other work.",
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
            "To hold new work to the profile's conventions instead, re-run adoption with --adopt-standard.",
            "That is a decision rather than a default, because it applies a convention to a codebase that",
            "never agreed to one.",
        ],

        Initialise.InitMode.Update =>
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"The solution was already governed, stays {SolutionDocument.NameOf(report.Origin)}, and is on the {SolutionDocument.NameOf(report.Standard)} standard."),
            "Only what the framework owns was refreshed -- the hook configs, the pipeline, and the README.",
            "The five files only a person writes were not touched, by the same rule the hook uses rather",
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
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}scaffolded {report.Name}"));
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

        foreach (string line in report.Written.Count == 0
            ? (string[])
            [
                "Nothing was written: every file was already there. This has run before, or the",
                "solution has grown past the skeleton. Overwriting is the one thing a scaffold must",
                "not do, so it left them alone.",
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
