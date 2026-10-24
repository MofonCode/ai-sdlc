using System.Globalization;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Solutions;

/// <summary>Where one of the files a person places has got to.</summary>
public enum SetupProgress
{
    /// <summary>Neither the file nor a draft of it is there, so a drafter has to write one.</summary>
    Missing,

    /// <summary>A draft is waiting beside where the file goes, to be filled in and renamed.</summary>
    Drafted,

    /// <summary>The file is in place and still carries the template's text where a person's goes.</summary>
    Unfilled,

    /// <summary>The file is in place and says something.</summary>
    Placed,
}

/// <summary>One file a person places before the first cycle.</summary>
/// <param name="Thing">What it is, as it reads inside a sentence listing what is left.</param>
/// <param name="FileName">The file's name inside <c>.sdlc/</c>.</param>
/// <param name="DraftFileName">Its draft's name, beside it.</param>
/// <param name="Command">
/// The command that writes its draft when there is none. For the solution document that is adoption,
/// which drafts it again: the drafter carries a placed file's prose through, and has none to carry.
/// </param>
/// <param name="Fill">What a person does to the draft, in a few words.</param>
/// <param name="Progress">Where it has got to.</param>
/// <param name="Optional">Whether a solution may go without it, which only its own constraints may.</param>
public sealed record SetupStep(
    string Thing,
    string FileName,
    string DraftFileName,
    string Command,
    string Fill,
    SetupProgress Progress,
    bool Optional = false)
{
    /// <summary>Whether it is in place and filled in.</summary>
    public bool Done => Progress == SetupProgress.Placed;

    /// <summary>Where it goes, as a person types it from the solution root.</summary>
    public string Path => Cycle.SdlcDirectoryName + "/" + FileName;

    /// <summary>Where its draft goes, as a person types it from the solution root.</summary>
    public string DraftPath => Cycle.SdlcDirectoryName + "/" + DraftFileName;
}

/// <summary>What stands between an adopted repository and its first cycle.</summary>
/// <param name="Steps">Every file, in the order a person fills them in, done or not.</param>
/// <param name="HasCycles">Whether this solution has opened a cycle yet.</param>
public sealed record SetupState(IReadOnlyList<SetupStep> Steps, bool HasCycles)
{
    /// <summary>The required files not yet done, in order.</summary>
    public IReadOnlyList<SetupStep> Left => [.. Steps.Where(step => !step.Done && !step.Optional)];
}

/// <summary>
/// The one list of what a person places after adoption, read from disk, and the block that says how.
/// </summary>
/// <remarks>
/// <para><strong>Every file left is named, with its rename.</strong> Adoption drafts all three, so what
/// is left is the same three acts for each -- fill it in, rename it, commit it -- and naming one at a time
/// had a person run <c>doctor</c> between renames to learn the next. D-79 named one step because the list
/// it replaced mixed steps with views; this is a list of steps only (D-94).</para>
///
/// <para><strong>It is read from disk every time, because a rename is silent.</strong> Nothing runs when a
/// person renames a draft into place, so the answer to "what now" cannot be something adoption printed
/// once: <c>sdlc doctor</c> asks the same list and prints the same block.</para>
///
/// <para><strong>The order is the order the answers depend on.</strong> What a solution is comes before
/// what it is built from, because the second follows from the first -- and it is the first question the
/// <c>init</c> skill asks. The solution's own constraints come last and are optional: a solution with no
/// rules of its own is sound.</para>
/// </remarks>
public static class Setup
{
    private const string Indent = "  ";

    private const int PathWidth = 28;

    /// <summary>
    /// Reads what is in place.
    /// </summary>
    /// <param name="solutionPath">The solution root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>Every step with where it has got to, and whether a cycle has been opened.</returns>
    public static async Task<SetupState> Read(string solutionPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        string sdlc = System.IO.Path.Combine(solutionPath, Cycle.SdlcDirectoryName);
        string document = System.IO.Path.Combine(sdlc, Cycle.SolutionFileName);

        // A file that cannot be read is not this list's to report -- doctor has a check for it -- so it
        // counts as not yet filled in.
        SetupProgress solution = File.Exists(document)
            ? WriteSolutionDraft.IsPlaceholder(await File.ReadAllTextAsync(document, cancellationToken).ConfigureAwait(false))
                ? SetupProgress.Unfilled
                : SetupProgress.Placed
            : Waiting(sdlc, WriteSolutionDraft.DraftFileName);

        // Present is the whole test, as it is for 'sdlc new': an empty file is a person declaring that
        // nothing is pinned (D-76).
        SetupProgress stack = File.Exists(System.IO.Path.Combine(sdlc, Cycle.TechStackFileName))
            ? SetupProgress.Placed
            : Waiting(sdlc, WriteTechStackDraft.DraftFileName);

        SetupProgress constraints = File.Exists(System.IO.Path.Combine(sdlc, FileConstraintDocuments.SolutionConstraintsFileName))
            ? SetupProgress.Placed
            : Waiting(sdlc, ConstraintsDraftFileName);

        string cycles = System.IO.Path.Combine(sdlc, Cycle.CyclesDirectoryName);

        return new SetupState(
            [
                new SetupStep(
                    "a paragraph saying what this solution is",
                    Cycle.SolutionFileName,
                    WriteSolutionDraft.DraftFileName,
                    "sdlc init",
                    "the paragraph: what it does, and who for",
                    solution),
                new SetupStep(
                    "a declared stack saying what the solution is built from",
                    Cycle.TechStackFileName,
                    WriteTechStackDraft.DraftFileName,
                    "sdlc draft techstack",
                    "'none' in each row not true here",
                    stack),
                new SetupStep(
                    "this solution's own constraints",
                    FileConstraintDocuments.SolutionConstraintsFileName,
                    ConstraintsDraftFileName,
                    "sdlc propose constraints",
                    "a rule of this solution's own, if it has one",
                    constraints,
                    Optional: true),
            ],
            Directory.Exists(cycles) && Directory.EnumerateDirectories(cycles).Any());
    }

    /// <summary>What adoption names the draft of this solution's own constraints.</summary>
    public const string ConstraintsDraftFileName = "constraints.draft.md";

    /// <summary>
    /// Renders what is left and how to place it.
    /// </summary>
    /// <remarks>
    /// Empty once a cycle exists and nothing is left to place: from there the status panel names the
    /// next command for each cycle, and a block here would be a second answer to that question.
    /// </remarks>
    /// <param name="state">What is in place.</param>
    /// <returns>The lines to print, unindented lines empty, or none when there is nothing to say.</returns>
    public static IReadOnlyList<string> Render(SetupState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        IReadOnlyList<SetupStep> left = state.Left;

        if (left.Count == 0)
        {
            return state.HasCycles
                ? []
                :
                [
                    Indent + "Nothing is left to place, so the first cycle can open once what you placed",
                    Indent + "is committed.",
                    string.Empty,
                    Indent + "Next",
                    Indent + "    /sdlc:intent            starts it: the intent skill opens the cycle, switches to its",
                    Indent + "                            branch and interviews you. In another host, ask your agent",
                    Indent + "                            for the intent skill",
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

        // The optional one rides along while its draft is waiting, so the rename a person is already
        // doing for the other two covers it -- and is left out once nothing is waiting, because a
        // solution with no rules of its own is sound and has nothing to be told.
        SetupStep[] shown =
        [
            .. left,
            .. state.Steps.Where(step => step.Optional && step.Progress == SetupProgress.Drafted),
        ];

        SetupStep[] renamed = [.. shown.Where(step => step.Progress == SetupProgress.Drafted)];

        List<string> lines =
        [
            .. Wrap(string.Create(
                CultureInfo.InvariantCulture,
                $"{count} yours to place before the first cycle: {things}. Only you can: an agent's write to any of these files is denied.")),
            string.Empty,
            Indent + "Next",
            Indent + "    1. Fill in",
            .. shown.Select(Fill),
        ];

        if (renamed.Length > 0)
        {
            lines.Add(Indent + "    2. Rename into place, from the repository root");

            foreach (SetupStep step in renamed)
            {
                lines.Add(Indent + "           mv -f " + step.DraftPath + " " + step.Path);
            }

            lines.Add(Indent + "       In PowerShell, Move-Item -Force in place of mv -f.");
        }

        lines.Add(Indent + (renamed.Length > 0 ? "    3. " : "    2. ") + "Commit them. A cycle does not open over uncommitted work");

        return lines;
    }

    private static SetupProgress Waiting(string sdlc, string draftFileName) =>
        File.Exists(System.IO.Path.Combine(sdlc, draftFileName)) ? SetupProgress.Drafted : SetupProgress.Missing;

    private static string Fill(SetupStep step)
    {
        (string where, string what) = step.Progress switch
        {
            SetupProgress.Drafted => (step.DraftPath, step.Fill + (step.Optional ? " -- optional" : string.Empty)),
            SetupProgress.Unfilled => (step.Path, "still the template's text: write " + step.Fill),
            _ => (step.Command, "writes " + step.DraftPath + " to fill in"),
        };

        return Indent + "           " + where.PadRight(PathWidth) + what;
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
