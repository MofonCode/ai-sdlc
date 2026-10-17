using System.Globalization;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;

namespace Sdlc.Core.Hooks;

/// <summary>A file no agent may write, and what to say when one tries.</summary>
/// <param name="Description">What the file is, for the refusal message.</param>
/// <param name="Reason">Why it is a person's to write.</param>
public sealed record ProtectedFile(string Description, string Reason);

/// <summary>
/// The files only a person writes.
/// </summary>
/// <remarks>
/// These rules are absolute rather than conditional, and that is the point. There is no approve
/// command for an exception to be carved out for, so the hook can deny every agent write to these
/// paths without a mode, a flag, or a check on which skill is running — and a rule with no exception
/// has nowhere for a bypass to live.
/// </remarks>
public static class ProtectedPaths
{
    /// <summary>
    /// The note at the top of every protected document adoption drafts, saying who may change it.
    /// </summary>
    /// <remarks>
    /// In the file rather than only in the refusal, because the refusal reaches the agent and the file is
    /// what a person opens. Somebody who asks their agent to fix a line here and watches it be denied has
    /// been told by the document itself why, before the denial reads like a broken install. One text for
    /// every document, so it is said the same way wherever it appears.
    /// </remarks>
    public static IReadOnlyList<string> Note { get; } =
    [
        "> **Only a person changes this file.** An AI agent cannot: the framework's hooks deny every",
        "> agent write to it, by file path and by shell command alike. An agent may write a draft",
        "> beside it, and renaming the draft into place is yours to do. `.sdlc/README.md` says how",
        "> the guard works and which files it covers.",
    ];

    /// <summary>
    /// What a protected file is, by its name alone.
    /// </summary>
    /// <remarks>
    /// One lookup, used by both rules. The first version had the shell rule call the path rule with a
    /// bare file name, and the path rule requires an <c>.sdlc</c> segment - so every shell command was
    /// allowed while the file rule worked, and the gate had a front door with the back door open. Two
    /// ways to ask the same question is how that happens.
    /// </remarks>
    /// <param name="fileName">The file's name, without a directory.</param>
    /// <returns>What the file is, or null when it is not protected.</returns>
    private static ProtectedFile? Describe(string fileName)
    {
        if (string.Equals(fileName, "approval.md", StringComparison.OrdinalIgnoreCase))
        {
            return new ProtectedFile(
                "a cycle's approval ledger",
                "An approval is a person's act. A skill prints the entry and the person pastes it, which "
                + "is what makes a pasted approval mean anything. Run 'sdlc draft approval <id>' and "
                + "paste what it prints.");
        }

        if (string.Equals(fileName, "abandoned.md", StringComparison.OrdinalIgnoreCase))
        {
            // This is the one protected act the agent itself usually wants, so naming the drafter looks
            // like paving the exit. It is not, and the silence was the worse of the two: the hook denies
            // the write and cannot deny the decision, `draft abandon` is named in five other places an
            // agent can read, and the guard that actually works is the drafter printing a reason that
            // fails to parse until a person writes one. Withholding the command made abandoning ON THE
            // RECORD harder without making abandoning harder, and the record is the whole point of the
            // file. The friction also landed on the wrong party: every host renders this text as the
            // error a person reads.
            return new ProtectedFile(
                "a cycle's abandonment record",
                "Abandoning a cycle is a decision, not an exit. An agent stuck on a blocking finding has "
                + "an obvious escape - abandon and start clean - and a hand-written reason is what keeps "
                + "the objections that killed it on the record. Run 'sdlc draft abandon <id>' and write "
                + "the reason by hand.");
        }

        if (string.Equals(fileName, FileConstraintDocuments.SolutionConstraintsFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new ProtectedFile(
                "this solution's own constraints",
                "A cycle judged by the constraints it is changing is the same deadlock as the framework "
                + "governing itself. Constraints are edited by a person, by hand. Run "
                + "'sdlc propose constraints' and hand back what it prints: it reads this solution's "
                + "whole history, says which rules its findings keep citing, and prints an empty entry "
                + "at the next free id when the record holds no pattern.");
        }

        if (string.Equals(fileName, Cycle.TechStackFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new ProtectedFile(
                "what this solution is built from",
                "The declared stack is the second rule source a plan is read against, and CON_15 judges "
                + "every plan against it - so an agent that could write it could satisfy that rule by "
                + "adding the row it wanted, or drop a stack rule by switching its row off. A person "
                + "places it and nothing else writes it. Run 'sdlc draft techstack', edit the draft, "
                + "and rename it.");
        }

        if (string.Equals(fileName, DecisionParser.FileName, StringComparison.OrdinalIgnoreCase))
        {
            return new ProtectedFile(
                "the choices this solution has already settled",
                "A stage reads the decision record for alignment, so an agent that could write it could "
                + "invent doctrine and then align to it - which is the one failure this record exists to "
                + "avoid. Provenance is the point: an entry traced to an approved cycle carries a "
                + "person's signature. Run 'sdlc propose decisions' and hand back what it prints.");
        }

        if (string.Equals(fileName, Cycle.SolutionFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new ProtectedFile(
                "what this solution is and where its code came from",
                "The origin is a historical fact that must never change, and the standard and the "
                + "platform are choices revisited rarely. Protecting the file costs one rename per "
                + "change and keeps every hook rule absolute - per-key protection would be the first "
                + "conditional rule, which is where a bypass would live. Run 'sdlc draft solution'.");
        }

        return null;
    }

    /// <summary>The names of every file only a person writes.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "approval.md",
        "abandoned.md",
        FileConstraintDocuments.SolutionConstraintsFileName,
        DecisionParser.FileName,
        Cycle.SolutionFileName,
        Cycle.TechStackFileName,
    ];

    /// <summary>
    /// Decides whether a path is one only a person writes.
    /// </summary>
    /// <param name="path">The path the agent named, absolute or relative.</param>
    /// <param name="file">What the path is, when it is protected.</param>
    /// <returns>True when no agent may write it.</returns>
    public static bool IsProtected(string? path, out ProtectedFile? file)
    {
        file = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Compared on segments rather than on a full path, because the hook sees whatever the host
        // sent: an absolute path, one relative to a working directory it may not have named, or one
        // with the other platform's separators.
        string[] segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return false;
        }

        bool insideSdlc = Array.Exists(
            segments,
            segment => string.Equals(segment, Cycle.SdlcDirectoryName, StringComparison.OrdinalIgnoreCase));

        if (!insideSdlc)
        {
            return false;
        }

        file = Describe(segments[^1]);
        return file is not null;
    }

    /// <summary>
    /// Finds a protected file named anywhere in a shell command.
    /// </summary>
    /// <remarks>
    /// A shell can write a file without any tool naming its path, so the file-write rules alone would
    /// leave the obvious way round them open. This looks for the file names rather than parsing a
    /// command line, and does not require the <c>.sdlc</c> directory to appear - a command can change
    /// directory first. Deliberately broad: a false refusal on a command that merely mentions the file
    /// costs a person one rephrasing, and a miss costs the guarantee.
    /// </remarks>
    /// <param name="command">The command the agent is about to run.</param>
    /// <param name="file">What it would touch, when it names one.</param>
    /// <returns>True when the command would write a protected file.</returns>
    public static bool CommandTouchesProtected(string? command, out ProtectedFile? file)
    {
        file = null;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        // Adoption is denied before the write test, because it writes through a command rather than a
        // redirection and so would pass every indicator below.
        if (IsAdoption(command))
        {
            file = new ProtectedFile(
                "adoption of the framework itself",
                "Adopting a process is a decision about how a team works, so 'sdlc init' is run by a "
                + "person. It writes the hook configuration that constrains you, the pipeline that gates "
                + "the merge, and the file saying where this code came from -- which is a historical fact "
                + "that must never change. Ask for it to be run, and give the person the command rather "
                + "than a description of it. Its options are in 'sdlc help', under 'Options for init' -- "
                + "this rule matches the verb rather than parsing the line, so even 'sdlc init --help' "
                + "is refused, and 'sdlc help' is the way to read them.");

            return true;
        }

        if (!LooksLikeAWrite(command))
        {
            // A command that only reads one of these is fine. Refusing `cat approval.md` would make the
            // gate obstructive without making it safer.
            return false;
        }

        foreach (string candidate in Names)
        {
            if (command.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                file = Describe(candidate);
                return file is not null;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a command would adopt or re-adopt the framework.
    /// </summary>
    /// <remarks>
    /// Matched on the verb rather than on a full parse, and deliberately broad: a refusal on a command
    /// that merely mentions adoption costs a person one rephrasing, and a miss costs the one rule that
    /// keeps the hook configuration out of the hands of the thing it constrains.
    /// </remarks>
    private static bool IsAdoption(string command)
    {
        string flattened = command.Replace("\"", string.Empty, StringComparison.Ordinal);

        return flattened.Contains("sdlc init", StringComparison.OrdinalIgnoreCase)
            || flattened.Contains("sdlc.exe init", StringComparison.OrdinalIgnoreCase)
            || flattened.Contains("Sdlc.Cli -- init", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAWrite(string command)
    {
        // The redirections and the commands that create, move or edit a file. A shell has more ways
        // than this, which is why the shell rule is a fast local layer and the pipeline check is the
        // guarantee.
        // PowerShell's verbs are listed in full rather than left to the bare words beside them: "move "
        // and "copy " carry a trailing space, so neither matches Move-Item or Copy-Item, and on Windows
        // those are the commands somebody actually reaches for. A rename is the move that matters most
        // now that a decision is placed by renaming a draft onto the ledger.
        string[] writeIndicators =
        [
            ">", ">>", "tee", "cp ", "copy ", "mv ", "move ", "rm ", "del ", "ren ", "rename ",
            "Remove-Item", "Move-Item", "Rename-Item", "Copy-Item", "Set-Item",
            "Set-Content", "Add-Content", "Out-File", "New-Item", "sed -i", "touch ",
            "git mv", "git checkout", "git restore",
        ];

        return Array.Exists(
            writeIndicators,
            indicator => command.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// How every refusal opens.
    /// </summary>
    /// <remarks>
    /// <strong>A refusal here is the design working, and it did not read that way.</strong> Every host
    /// renders this text as an error -- red, prefixed `Error:` -- so a rule doing exactly its job
    /// arrived looking like a crash. A person met it, went looking for the bug, and was sent back twice
    /// by the thing that was working. So the text says what it is before it says anything else.
    /// <para>
    /// It is not an apology and not a softening. The refusal still cannot be worked around; what
    /// changes is that the reader knows within four words whether they are debugging or being told
    /// something.
    /// </para>
    /// </remarks>
    public const string Preface = "Refused by the ai-sdlc gate -- this is a rule, not a fault.";

    /// <summary>
    /// Phrases a refusal about a named path.
    /// </summary>
    /// <param name="path">What the agent tried to write.</param>
    /// <param name="file">What that file is.</param>
    /// <returns>The reason to hand back to the host.</returns>
    public static string Refusal(string path, ProtectedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Preface} {path} is {file.Description}, and no agent may write it. {file.Reason}");
    }

    /// <summary>
    /// Phrases a refusal about a shell command.
    /// </summary>
    /// <remarks>
    /// Separate from the path phrasing because a command is an act rather than a file: the shell rule
    /// catches commands that would write a protected file and commands that would adopt the framework,
    /// and "that file is adoption of the framework, and no agent may write it" is not a sentence.
    /// </remarks>
    /// <param name="file">What the command would reach.</param>
    /// <returns>The reason to hand back to the host.</returns>
    public static string CommandRefusal(ProtectedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Preface} That command would reach {file.Description}, which is not an agent's to change. {file.Reason}");
    }
}
