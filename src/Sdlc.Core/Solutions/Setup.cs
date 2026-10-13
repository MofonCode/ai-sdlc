using System.Globalization;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Solutions;

/// <summary>One thing a person places before the first cycle.</summary>
/// <param name="Thing">What it is, as it reads inside a sentence listing what is left.</param>
/// <param name="Command">The command that writes its draft.</param>
/// <param name="What">What that command does and what the person does with it, as wrapped lines.</param>
/// <param name="Done">Whether it is in place.</param>
public sealed record SetupStep(string Thing, string Command, IReadOnlyList<string> What, bool Done);

/// <summary>What stands between an adopted repository and its first cycle.</summary>
/// <param name="Steps">Every step, in the order a person takes them, done or not.</param>
/// <param name="HasCycles">Whether this solution has opened a cycle yet.</param>
public sealed record SetupState(IReadOnlyList<SetupStep> Steps, bool HasCycles)
{
    /// <summary>The steps not yet done, in order.</summary>
    public IReadOnlyList<SetupStep> Left => [.. Steps.Where(step => !step.Done)];
}

/// <summary>
/// The one list of what a person places after adoption, read from disk, and the block that says which
/// of them is next.
/// </summary>
/// <remarks>
/// <para><strong>One step is named, never the list of commands.</strong> Adoption used to end with four
/// commands under "Next". One was the step, two were views that show nothing in a repository adopted a
/// minute ago, and one reads a history that does not exist yet -- so a person was handed five things
/// and had to work out which was theirs to do. This says what is left in a sentence and names one
/// command (D-79).</para>
///
/// <para><strong>It is read from disk every time, because a rename is silent.</strong> Each step ends
/// with a person renaming a draft into place, and nothing runs when they do. So the answer to "what
/// now" cannot be something adoption printed once: <c>sdlc doctor</c> asks the same list and prints
/// the same block, and that is where the second step is named.</para>
///
/// <para><strong>The order is the order the answers depend on.</strong> What a solution is comes
/// before what it is built from, because the second follows from the first -- and it is the first
/// question the <c>init</c> skill asks, so the binary and the skill name the same step. The declared
/// stack is the one that blocks a cycle, and the block says so when it is the step left.</para>
/// </remarks>
public static class Setup
{
    private const string Indent = "  ";

    private const int CommandWidth = 24;

    /// <summary>
    /// Reads what is in place.
    /// </summary>
    /// <param name="solutionPath">The solution root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>Every step with whether it is done, and whether a cycle has been opened.</returns>
    public static async Task<SetupState> Read(string solutionPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        string sdlc = Path.Combine(solutionPath, Cycle.SdlcDirectoryName);
        string document = Path.Combine(sdlc, Cycle.SolutionFileName);

        // Described means the shipped placeholder is gone. A file that cannot be read is not this
        // list's to report -- doctor has a check for it -- so it counts as not yet described.
        bool described = File.Exists(document)
            && !(await File.ReadAllTextAsync(document, cancellationToken).ConfigureAwait(false))
                .Contains(WriteSolutionDraft.Placeholder, StringComparison.Ordinal);

        // Present is the whole test, as it is for 'sdlc new': an empty file is a person declaring
        // that nothing is pinned (D-76).
        bool declared = File.Exists(Path.Combine(sdlc, Cycle.TechStackFileName));

        string cycles = Path.Combine(sdlc, Cycle.CyclesDirectoryName);

        return new SetupState(
            [
                new SetupStep(
                    "a paragraph saying what this solution is",
                    "sdlc draft solution",
                    [
                        "writes the solution document beside itself. Put your",
                        "paragraph in the draft, then run the rename it prints",
                    ],
                    described),
                new SetupStep(
                    "a declared stack saying what the solution is built from",
                    "sdlc draft techstack",
                    [
                        "writes a draft of the framework's default stack. Cut it",
                        "down to what is true here, then run the rename it prints.",
                        "No cycle opens until it is placed",
                    ],
                    declared),
            ],
            Directory.Exists(cycles) && Directory.EnumerateDirectories(cycles).Any());
    }

    /// <summary>
    /// Renders what is left and the one step that is next.
    /// </summary>
    /// <remarks>
    /// Empty once a cycle exists and nothing is left to place: from there the status panel names the
    /// next command for each cycle, and a block here would be a second answer to that question.
    /// </remarks>
    /// <param name="state">What is in place.</param>
    /// <param name="after">
    /// The closing line, said only while more than one step is left: where the step after this one is
    /// named. It differs by caller because one of the callers is that place.
    /// </param>
    /// <returns>The lines to print, unindented lines empty, or none when there is nothing to say.</returns>
    public static IReadOnlyList<string> Render(SetupState state, string after)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(after);

        IReadOnlyList<SetupStep> left = state.Left;

        if (left.Count == 0)
        {
            return state.HasCycles
                ? []
                :
                [
                    Indent + "Nothing is left to place, so the first cycle can open.",
                    string.Empty,
                    Indent + "Next",
                    .. Step("sdlc intent", ["starts it. A skill, so ask your agent for it:", "in Claude Code, /sdlc:intent"]),
                ];
        }

        string count = left.Count switch
        {
            1 => "One thing is",
            2 => "Two things are",
            _ => string.Create(CultureInfo.InvariantCulture, $"{left.Count} things are"),
        };

        string things = left.Count == 1
            ? left[0].Thing
            : string.Join(", ", left.Take(left.Count - 1).Select(step => step.Thing)) + ", and " + left[^1].Thing;

        List<string> lines =
        [
            .. Wrap(string.Create(
                CultureInfo.InvariantCulture,
                $"{count} yours to place before the first cycle: {things}.")),
            string.Empty,
            Indent + "Next",
            .. Step(left[0].Command, left[0].What),
        ];

        if (left.Count > 1)
        {
            lines.Add(string.Empty);
            lines.Add(Indent + after);
        }

        return lines;
    }

    private static IEnumerable<string> Step(string command, IReadOnlyList<string> what)
    {
        for (int line = 0; line < what.Count; line++)
        {
            yield return Indent + "    " + (line == 0 ? command : string.Empty).PadRight(CommandWidth) + what[line];
        }
    }

    private static IEnumerable<string> Wrap(string sentence)
    {
        const int Width = 88;

        string current = Indent;

        foreach (string word in sentence.Split(' '))
        {
            if (current.Length + word.Length + 1 > Width && current.Length > Indent.Length)
            {
                yield return current.TrimEnd();
                current = Indent;
            }

            current += word + " ";
        }

        yield return current.TrimEnd();
    }
}
