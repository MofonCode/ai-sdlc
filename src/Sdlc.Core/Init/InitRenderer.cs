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
    public static string Render(InitReport report)
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

        foreach (InitAction action in report.Actions)
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

    private static string Headline(InitMode mode) => mode switch
    {
        InitMode.Scaffold => "scaffolded a new governed solution",
        InitMode.Onboard => "adopted ai-sdlc into an existing repository",
        InitMode.Update => "updated the ai-sdlc scaffolding",
        _ => "adopted ai-sdlc",
    };

    private static string Verb(InitChange change) => change switch
    {
        InitChange.Created => "created",
        InitChange.Updated => "updated",
        InitChange.Kept => "kept",
        InitChange.Printed => "printed",
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
    private static string[] Explain(InitReport report) => report.Mode switch
    {
        InitMode.Scaffold =>
        [
            "This repository held no code, so the solution is scaffolded: the profile's patterns are",
            "this solution's patterns, and a plan that departs from them has to say why.",
        ],

        InitMode.Onboard =>
        [
            "This repository already held code, so the solution is adopted: where it has an existing",
            "standard, that standard wins. The profile proposes a shape for new work and does not rewrite",
            "the conventions of a codebase that predates it.",
            string.Empty,
            "Nothing here is in violation on its first day. Constraints govern a change, never code that",
            "came before adoption, so there is no baseline to record and nothing to forgive.",
        ],

        InitMode.Update =>
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"The solution was already governed, and stays {SolutionDocument.NameOf(report.Origin)}. Only what the framework owns"),
            "was refreshed -- the hook configs, the pipeline, and the README. The four files only a person",
            "writes were not touched, by the same rule the hook uses rather than by a second list.",
        ],

        _ => [],
    };
}
