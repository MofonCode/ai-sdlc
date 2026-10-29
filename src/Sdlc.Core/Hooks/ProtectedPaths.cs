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
                "An approval is a person's act. A tool drafts the entry and the person renames it into "
                + "place, which is what makes a recorded approval mean anything. Run 'sdlc draft approval "
                + "<id>' and rename the draft it writes.");
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

        file = segments.Length >= 2
            && string.Equals(segments[^2], "findings", StringComparison.OrdinalIgnoreCase)
            && (IsRoundFileName(segments[^1]) || IsLedgerFileName(segments[^1]))
                ? Round
                : Describe(segments[^1]);

        return file is not null;
    }

    /// <summary>
    /// A findings round: not a person's file but the binary's, denied to an agent for the same reason.
    /// </summary>
    /// <remarks>
    /// D-101. A round the session wrote itself carried whatever round number, time and hashes the
    /// session typed, and in a real cycle that let three stages be interrogated at once against
    /// documents still moving. <c>sdlc round</c> stamps all of that and refuses a round whose upstream is
    /// not settled; the denial is what makes it the only way a round lands.
    /// </remarks>
    public static ProtectedFile Round { get; } = new(
        "a findings ledger",
        "A round is written by the binary, which numbers it, stamps when it ran and what it judged, chains "
        + "it to the rounds before it, and refuses it while anything above it in the chain is unsettled. "
        + "Put the readers' findings and your dispositions in a file outside the repository and run "
        + "'sdlc round <id> <artifact> --from <file>'.");

    /// <summary>
    /// Whether a file name is a findings ledger's: <c>&lt;artifact&gt;.json</c> (D-107).
    /// </summary>
    /// <remarks>
    /// Only ever asked of a name whose directory is <c>findings</c>. Unlike a round file's, the name alone
    /// is not distinctive -- a solution may well have a <c>plan.json</c> of its own -- so a command is
    /// judged by the path it names, through <see cref="IsFindingsPath"/>, and never by the name.
    /// </remarks>
    /// <param name="name">The file's name, without a directory.</param>
    /// <returns>True for a ledger file name.</returns>
    public static bool IsLedgerFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.Equals(name, Findings.SuggestionLedger.FileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] parts = name.Split('.');
        return parts.Length == 2
            && Cycle.TryParseKind(parts[0], out _)
            && string.Equals(parts[1], "json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a path a command names is a round file anywhere, or a ledger in a <c>findings</c> directory.
    /// </summary>
    /// <param name="path">The path as the command wrote it.</param>
    /// <returns>True when it is one of the binary's findings files.</returns>
    public static bool IsFindingsPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string[] segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length > 0
            && (IsRoundFileName(segments[^1])
                || (segments.Length >= 2
                    && string.Equals(segments[^2], "findings", StringComparison.OrdinalIgnoreCase)
                    && IsLedgerFileName(segments[^1])));
    }

    /// <summary>
    /// Whether a file name is a round's: <c>&lt;artifact&gt;.r&lt;n&gt;.json</c>.
    /// </summary>
    /// <param name="name">The file's name, without a directory.</param>
    /// <returns>True for a round file name.</returns>
    public static bool IsRoundFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string[] parts = name.Split('.');
        return parts.Length == 3
            && Cycle.TryParseKind(parts[0], out _)
            && parts[1].Length > 1
            && (parts[1][0] is 'r' or 'R')
            && parts[1][1..].All(char.IsAsciiDigit)
            && string.Equals(parts[2], "json", StringComparison.OrdinalIgnoreCase);
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

        // Judged one segment at a time -- the pieces between `;`, `&&`, `||`, `|` and newlines, outside
        // quotes -- so a read in one piece and an unrelated write in another are not added together.
        // A redirect is a write of the file it points at, and only that file: `grep x decisions.md
        // 2>/dev/null` reads it, and `cat approval.md > /tmp/x` writes somewhere else. A write verb is
        // judged by whether the same segment names a protected file, because which argument a verb
        // writes is not something this rule can know without parsing every command there is.
        foreach (string segment in Segments(command))
        {
            foreach (string target in RedirectTargets(segment))
            {
                string name = target.Replace('\\', '/').Split('/')[^1];
                if ((IsFindingsPath(target) ? Round : Describe(name)) is { } redirected)
                {
                    file = redirected;
                    return true;
                }
            }

            if (!HasWriteVerb(segment))
            {
                continue;
            }

            foreach (string candidate in Names)
            {
                if (NamesWhole(segment, candidate))
                {
                    file = Describe(candidate);
                    return file is not null;
                }
            }

            if (NamesARound(segment))
            {
                file = Round;
                return true;
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

    /// <summary>
    /// Whether a protected file's name appears as a name of its own rather than inside a longer one.
    /// </summary>
    /// <remarks>
    /// <c>review-decisions.md</c> and <c>decisions.draft.md</c> are not the decision record, and
    /// refusing a command for naming them was a false refusal that taught agents to route round the
    /// gate. Case is still ignored: on Windows <c>DECISIONS.md</c> is the same file.
    /// </remarks>
    private static bool NamesWhole(string text, string name)
    {
        int from = 0;
        while (from < text.Length)
        {
            int at = text.IndexOf(name, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return false;
            }

            int after = at + name.Length;
            bool startsAlone = at == 0 || !IsNameCharacter(text[at - 1]);
            bool endsAlone = after == text.Length || !IsNameCharacter(text[after]);
            if (startsAlone && endsAlone)
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }

    private static bool NamesARound(string segment) => segment
        .Split([' ', '\t', '"', '\'', '=', '(', ')', ','], StringSplitOptions.RemoveEmptyEntries)
        .Any(IsFindingsPath);

    private static bool IsNameCharacter(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.';

    /// <summary>
    /// The pieces of a command a shell runs separately, split outside quotes.
    /// </summary>
    /// <remarks>
    /// Splitting inside quotes would be a bypass rather than a false refusal: <c>sed -i "s/a/b/;s/c/d/"
    /// decisions.md</c> split on the quoted semicolon leaves the verb in one piece and the file in the
    /// next. An ampersand beside a redirect is a descriptor, as in <c>2&gt;&amp;1</c>, not a separator.
    /// </remarks>
    private static IEnumerable<string> Segments(string command)
    {
        int start = 0;
        char quote = '\0';
        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            bool descriptor = c == '&'
                && ((i > 0 && command[i - 1] == '>') || (i + 1 < command.Length && command[i + 1] == '>'));

            if (c is ';' or '|' or '\n' or '\r' || (c == '&' && !descriptor))
            {
                yield return command[start..i];
                start = i + 1;
            }
        }

        yield return command[start..];
    }

    /// <summary>
    /// What each redirect in a segment writes to, leaving out the null device and descriptor copies.
    /// </summary>
    private static IEnumerable<string> RedirectTargets(string segment)
    {
        char quote = '\0';
        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c != '>')
            {
                continue;
            }

            int j = i + 1;
            if (j < segment.Length && segment[j] == '>')
            {
                j++;
            }

            if (j < segment.Length && segment[j] == '&')
            {
                i = j;
                continue;
            }

            while (j < segment.Length && char.IsWhiteSpace(segment[j]))
            {
                j++;
            }

            int begin = j;
            while (j < segment.Length && !char.IsWhiteSpace(segment[j]))
            {
                j++;
            }

            string target = segment[begin..j].Trim('"', '\'');
            i = j - 1;

            if (target.Length == 0
                || string.Equals(target, "/dev/null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, "$null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, "nul", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return target;
        }
    }

    /// <summary>
    /// Whether a segment runs something that creates, moves, copies, deletes or edits a file.
    /// </summary>
    /// <remarks>
    /// Matched as whole words, because a substring match found <c>rm </c> in "perform ", <c>del </c>
    /// in "model " and <c>tee</c> in "guarantee", so any command with a sentence in it was a write.
    /// PowerShell's verbs are listed in full: on Windows those are what somebody reaches for, and a
    /// rename is the move that matters most since a decision is placed by renaming a draft. A shell has
    /// more ways than these, which is why the shell rule is a fast local layer and the pipeline check
    /// is the guarantee.
    /// </remarks>
    private static bool HasWriteVerb(string segment)
    {
        string[] words =
        [
            "tee", "cp", "copy", "mv", "move", "rm", "del", "ren", "rename", "touch",
            "Remove-Item", "Move-Item", "Rename-Item", "Copy-Item", "Set-Item",
            "Set-Content", "Add-Content", "Out-File", "New-Item",
        ];

        string[] phrases = ["sed -i", "sed --in-place", "git mv", "git checkout", "git restore"];

        return Array.Exists(words, word => IsWord(segment, word))
            || Array.Exists(phrases, phrase => segment.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWord(string text, string word)
    {
        int from = 0;
        while (from < text.Length)
        {
            int at = text.IndexOf(word, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return false;
            }

            int after = at + word.Length;
            bool startsAlone = at == 0 || !IsNameCharacter(text[at - 1]);
            bool endsAlone = after == text.Length || char.IsWhiteSpace(text[after]);
            if (startsAlone && endsAlone)
            {
                return true;
            }

            from = at + 1;
        }

        return false;
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
