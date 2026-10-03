using System.Globalization;
using System.Text;
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
                $"{Indent}{file.Key} already exists. Here is the leaned version, to merge by hand:"));

            output.AppendLine();

            foreach (string line in file.Value.ReplaceLineEndings().Split(Environment.NewLine))
            {
                output.AppendLine(Indent + "    " + line);
            }
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

    private static string Headline(Initialise.InitMode mode) => mode switch
    {
        Initialise.InitMode.Scaffold => "scaffolded a new governed solution",
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
        Initialise.InitMode.Scaffold =>
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
        ],

        Initialise.InitMode.Update =>
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"The solution was already governed, and stays {SolutionDocument.NameOf(report.Origin)}. Only what the framework owns"),
            "was refreshed -- the hook configs, the pipeline, and the README. The four files only a person",
            "writes were not touched, by the same rule the hook uses rather than by a second list.",
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
                "The reference slice is written in the shape every operation here should have, and it",
                "is meant to be deleted once there is real work. The architecture tests beside it are",
                "what make the conventions in .editorconfig enforceable - naming is an IDE-only",
                "diagnostic, so the build decides none of it on its own.",
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
