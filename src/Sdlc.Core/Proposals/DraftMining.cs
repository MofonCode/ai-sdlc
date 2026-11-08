using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Areas;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Drafts;
using Sdlc.Core.Hooks;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Proposals;

/// <summary>
/// Turns the lines a person accepted in a mining proposal into drafts of the three record files
/// (D-104).
/// </summary>
/// <remarks>
/// <para><strong>Each draft is a byte copy of the file as it stands, with only the accepted lines
/// applied.</strong> D-69 refused a drafter for the rule file because a regenerated file drops superseded
/// entries and closes id gaps and nothing notices. Nothing is regenerated here: an entry is inserted, a
/// <c>Superseded-by</c> line is added, a revision replaces one entry's own block, or one stack row's choice
/// changes. And it is checked rather than trusted -- the draft is parsed back, and a draft missing any id
/// or stack row the file held is refused rather than written.</para>
/// <para><strong>There is no remove.</strong> A rule out of force keeps its place marked
/// <c>Superseded-by</c>; a reversed decision takes a new id and the old one is marked the same way; a stack
/// concern no longer used is set to <c>none</c>. The person still places every draft by renaming it.</para>
/// </remarks>
public static class DraftMining
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddDraftMiningRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, DraftResult>, Handler>();
    }

    /// <summary>The kind of change one accepted proposal makes.</summary>
    public enum Operation
    {
        /// <summary>A new entry, at the next free id.</summary>
        Add,

        /// <summary>A new entry that replaces an existing one, which gains a <c>Superseded-by</c> line.</summary>
        Supersede,

        /// <summary>A rule's wording replaced in place, under the next revision of its id.</summary>
        Reword,

        /// <summary>A stack concern's choice, set or changed.</summary>
        Set,

        /// <summary>An area renamed, in <c>areas.md</c> and on every entry carrying it (D-150).</summary>
        Rename,

        /// <summary>One area folded into another: its globs joined to the other's, its entries carried over.</summary>
        Merge,

        /// <summary>An area taken out, its entries left global. The one record that has a remove.</summary>
        Remove,

        /// <summary>An area's globs replaced, when its files moved.</summary>
        Files,

        /// <summary>The areas one rule or decision carries, set or cleared.</summary>
        Tag,
    }

    /// <summary>Which record a proposal changes.</summary>
    public enum Record
    {
        /// <summary>The solution's own constraints.</summary>
        Rules,

        /// <summary>The decision record.</summary>
        Decisions,

        /// <summary>The declared stack's defaults.</summary>
        Stack,

        /// <summary>The solution's areas, and which area each entry carries (D-150).</summary>
        Areas,
    }

    /// <summary>One proposal, as the file holds it.</summary>
    /// <param name="Record">Which record it changes.</param>
    /// <param name="Operation">What it does there.</param>
    /// <param name="Target">The id superseded or reworded, or the stack concern set; null for an add.</param>
    /// <param name="Statement">The entry's one line, or the stack choice.</param>
    /// <param name="Body">The entry's clauses, evidence lines left out.</param>
    /// <param name="Accepted">Whether the person marked it.</param>
    public sealed record Proposal(Record Record, Operation Operation, string? Target, string Statement, string Body, bool Accepted);

    /// <summary>Drafts the accepted lines of one proposal file.</summary>
    /// <param name="From">The proposal file, as <c>/sdlc:mine</c> wrote it and the person marked it.</param>
    public sealed record Command(string From) : IRequest<DraftResult>;

    /// <summary>
    /// Reads a proposal file: <c>## Rules</c>, <c>## Decisions</c> and <c>## Stack</c>, each holding
    /// <c>### [ ] add — statement</c>, <c>### [ ] supersede LOC_02 — statement</c>,
    /// <c>### [ ] reword LOC_01 — statement</c> or <c>### [ ] set concern — choice</c> headings with the
    /// entry's clauses below. <c>[x]</c> marks one accepted. A line starting <c>&gt;</c> is evidence and
    /// is not drafted.
    /// </summary>
    /// <param name="markdown">The file.</param>
    /// <returns>Every proposal, accepted or not, or a failure naming the heading it could not read.</returns>
    public static Result<IReadOnlyList<Proposal>> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<Proposal> proposals = [];
        Record? record = null;
        (Record Record, Operation Operation, string? Target, string Statement, bool Accepted)? open = null;
        List<string> body = [];

        void Close()
        {
            if (open is { } current)
            {
                proposals.Add(new Proposal(current.Record, current.Operation, current.Target, current.Statement, string.Join('\n', body).Trim(), current.Accepted));
            }

            open = null;
            body.Clear();
        }

        foreach (string raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.TrimEnd();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                record = line[3..].Trim().ToUpperInvariant() switch
                {
                    "RULES" => Record.Rules,
                    "DECISIONS" => Record.Decisions,
                    "STACK" => Record.Stack,
                    "AREAS" => Record.Areas,
                    _ => null,
                };
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                Close();

                if (record is not { } section)
                {
                    continue;
                }

                Result<(Operation, string?, string, bool)> heading = Heading(line[4..].Trim(), section);

                if (heading.IsFailed)
                {
                    return Result.Fail<IReadOnlyList<Proposal>>(heading.Errors);
                }

                (Operation operation, string? target, string statement, bool accepted) = heading.Value;
                open = (section, operation, target, statement, accepted);
                continue;
            }

            if (open is not null && !line.StartsWith('>'))
            {
                body.Add(line);
            }
        }

        Close();
        return Result.Ok<IReadOnlyList<Proposal>>(proposals);
    }

    /// <summary>Applies accepted rule proposals to the rule file's text.</summary>
    /// <param name="text">The file as it stands.</param>
    /// <param name="proposals">The accepted proposals for this record.</param>
    /// <param name="assigned">Each id assigned, in order, for the report.</param>
    /// <returns>The draft text, or a failure.</returns>
    public static Result<string> ApplyRules(string text, IReadOnlyList<Proposal> proposals, ICollection<string> assigned)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(assigned);

        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(text, "constraints.md");

        if (parsed.IsFailed)
        {
            return Result.Fail<string>(parsed.Errors);
        }

        int next = ConstraintParser.NextId(parsed.Value, ConstraintTier.Solution).Number;
        string draft = text;

        foreach (Proposal proposal in proposals)
        {
            switch (proposal.Operation)
            {
                case Operation.Add:
                case Operation.Supersede:
                    string id = ConstraintId.Of(ConstraintTier.Solution, next++).ToString();
                    assigned.Add(id);
                    draft = Insert(draft, Entry(id, proposal), "## Considered and rejected");

                    if (proposal.Operation == Operation.Supersede)
                    {
                        Result<string> marked = MarkSuperseded(draft, proposal.Target!, id);
                        if (marked.IsFailed)
                        {
                            return marked;
                        }

                        draft = marked.Value;
                    }

                    break;

                case Operation.Reword:
                    if (!ConstraintId.TryParse(proposal.Target, out ConstraintId? old) || old.Value.Tier != ConstraintTier.Solution)
                    {
                        return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"'{proposal.Target}' is not one of this solution's rules to reword."));
                    }

                    string revised = string.Create(CultureInfo.InvariantCulture, $"LOC_{old.Value.Number:00}.{old.Value.Revision + 1}");
                    assigned.Add(revised);
                    Result<string> replaced = ReplaceEntry(draft, proposal.Target!, Entry(revised, proposal));
                    if (replaced.IsFailed)
                    {
                        return replaced;
                    }

                    draft = replaced.Value;
                    break;

                default:
                    return Result.Fail<string>("A rule is added, superseded or reworded, never set.");
            }
        }

        return Result.Ok(draft);
    }

    /// <summary>Applies accepted decision proposals to the decision record's text.</summary>
    /// <param name="text">The file as it stands.</param>
    /// <param name="proposals">The accepted proposals for this record.</param>
    /// <param name="assigned">Each id assigned, in order, for the report.</param>
    /// <returns>The draft text, or a failure.</returns>
    public static Result<string> ApplyDecisions(string text, IReadOnlyList<Proposal> proposals, ICollection<string> assigned)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(assigned);

        Result<IReadOnlyList<Decision>> parsed = DecisionParser.Parse(text, DecisionParser.FileName);

        if (parsed.IsFailed)
        {
            return Result.Fail<string>(parsed.Errors);
        }

        int next = DecisionParser.NextId(parsed.Value).Number;
        string draft = text;

        foreach (Proposal proposal in proposals)
        {
            if (proposal.Operation is not (Operation.Add or Operation.Supersede))
            {
                return Result.Fail<string>("A decision is added or superseded. A reversed decision takes a new id, so there is no rewording one in place (D-70).");
            }

            string id = DecisionId.Of(next++).ToString();
            assigned.Add(id);
            draft = Insert(draft, Entry(id, proposal), null);

            if (proposal.Operation == Operation.Supersede)
            {
                Result<string> marked = MarkSuperseded(draft, proposal.Target!, id);
                if (marked.IsFailed)
                {
                    return marked;
                }

                draft = marked.Value;
            }
        }

        return Result.Ok(draft);
    }

    /// <summary>Applies accepted stack proposals to the declared stack's defaults table.</summary>
    /// <param name="text">The file as it stands.</param>
    /// <param name="proposals">The accepted proposals for this record.</param>
    /// <returns>The draft text, or a failure.</returns>
    public static Result<string> ApplyStack(string text, IReadOnlyList<Proposal> proposals)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(proposals);

        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];

        foreach (Proposal proposal in proposals)
        {
            if (proposal.Operation != Operation.Set || string.IsNullOrWhiteSpace(proposal.Target))
            {
                return Result.Fail<string>("A stack proposal sets one concern's choice: '### [x] set <concern> — <choice>'.");
            }

            string row = "| " + proposal.Target + " | " + proposal.Statement + " |";
            int existing = lines.FindIndex(line => StackTable.Rows(line) is [var found, ..]
                && string.Equals(found.Concern, proposal.Target, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                lines[existing] = row;
                continue;
            }

            int defaults = lines.FindIndex(line => line.Trim() == "## Defaults");
            int last = -1;

            for (int index = defaults + 1; defaults >= 0 && index < lines.Count && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
            {
                if (lines[index].TrimStart().StartsWith('|'))
                {
                    last = index;
                }
            }

            if (last < 0)
            {
                return Result.Fail<string>("The declared stack has no '## Defaults' table to add a row to.");
            }

            lines.Insert(last + 1, row);
        }

        return Result.Ok(string.Join(newline, lines));
    }

    private static Result<(Operation, string?, string, bool)> Heading(string heading, Record record)
    {
        bool accepted;

        if (heading.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
        {
            accepted = true;
        }
        else if (heading.StartsWith("[ ]", StringComparison.Ordinal))
        {
            accepted = false;
        }
        else
        {
            return Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}' has no [ ] or [x] to say whether it was accepted."));
        }

        string rest = heading[3..].Trim();
        int dash = rest.IndexOf(" — ", StringComparison.Ordinal);
        if (dash < 0)
        {
            dash = rest.IndexOf(" -- ", StringComparison.Ordinal);
        }

        if (dash < 0)
        {
            return Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}' has no ' — ' between the operation and the statement."));
        }

        string[] words = rest[..dash].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string statement = rest[(dash + 3)..].Trim().TrimStart('-').Trim();
        string? target = words.Length > 1 ? words[1] : null;

        if (record == Record.Areas)
        {
            return AreaHeading(heading, words, statement, accepted);
        }

        Operation? operation = words.Length == 0 ? null : words[0].ToUpperInvariant() switch
        {
            "ADD" => Operation.Add,
            "SUPERSEDE" => Operation.Supersede,
            "REWORD" => Operation.Reword,
            "SET" => Operation.Set,
            "REMOVE" or "DELETE" => null,
            _ => null,
        };

        if (operation is null)
        {
            return Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}': the operation is add, supersede, reword or set. There is no remove: a rule or decision out of force is superseded, and a stack concern no longer used is set to none."));
        }

        bool needsTarget = operation is Operation.Supersede or Operation.Reword or Operation.Set;
        if (needsTarget != (target is not null))
        {
            return Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}': {(needsTarget ? "say what it supersedes, rewords or sets" : "an add names no existing entry")}."));
        }

        if (record == Record.Stack && operation != Operation.Set)
        {
            return Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}': a stack proposal sets a concern."));
        }

        return Result.Ok((operation.Value, target, statement, accepted));
    }

    // An area may be removed, unlike a rule or a decision: nothing cites an area's name the way a finding
    // cites a rule, and its entries simply become global (D-150).
    private static Result<(Operation, string?, string, bool)> AreaHeading(string heading, string[] words, string statement, bool accepted)
    {
        Operation? operation = words.Length == 0 ? null : words[0].ToUpperInvariant() switch
        {
            "ADD" => Operation.Add,
            "RENAME" => Operation.Rename,
            "MERGE" => Operation.Merge,
            "REMOVE" => Operation.Remove,
            "FILES" => Operation.Files,
            "TAG" => Operation.Tag,
            _ => null,
        };

        string? target = words.Length > 1 ? words[1] : null;

        if (operation is null || target is null)
        {
            return Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}': an area proposal is 'add <name>', 'rename <old> <new>', 'merge <from> into <to>', 'remove <name>', 'files <name>' or 'tag <entry id>', then ' — ' and what it says."));
        }

        string[] parts = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool shaped = operation switch
        {
            Operation.Rename => parts.Length == 2 && parts.All(AreaParser.IsName),
            Operation.Merge => parts.Length == 3 && parts[1] == "into" && AreaParser.IsName(parts[0]) && AreaParser.IsName(parts[2]),
            Operation.Tag => parts.Length == 1 && parts[0].Contains('_', StringComparison.Ordinal),
            _ => parts.Length == 1 && AreaParser.IsName(parts[0]),
        };

        return shaped
            ? Result.Ok((operation.Value, (string?)target, statement, accepted))
            : Result.Fail(string.Create(CultureInfo.InvariantCulture, $"'### {heading}': '{target}' is not what '{words[0]}' takes. An area's name is lower case words joined by hyphens."));
    }

    /// <summary>
    /// Applies accepted area proposals to the area file and to the rules and decisions that carry areas.
    /// </summary>
    /// <param name="areas">The area file as it stands, or the empty form for a solution with none.</param>
    /// <param name="rules">The rule file's text, after any rule proposals, or null when there is none.</param>
    /// <param name="decisions">The decision record's text, after any decision proposals, or null.</param>
    /// <param name="proposals">The accepted area proposals.</param>
    /// <returns>The three texts, each changed only where a proposal reached it, or a failure.</returns>
    public static Result<(string Areas, string? Rules, string? Decisions)> ApplyAreas(string areas, string? rules, string? decisions, IReadOnlyList<Proposal> proposals)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(proposals);

        string newline = areas.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. areas.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];

        foreach (Proposal proposal in proposals)
        {
            string[] parts = proposal.Target!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (proposal.Operation)
            {
                case Operation.Add:
                    if (AreaBlock(lines, parts[0]).Start >= 0)
                    {
                        return Result.Fail<(string, string?, string?)>(string.Create(CultureInfo.InvariantCulture, $"There is already an area called {parts[0]}."));
                    }

                    string files = proposal.Body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
                        .FirstOrDefault(line => line.StartsWith("**Files:**", StringComparison.Ordinal)) ?? string.Empty;

                    if (files.Length == 0)
                    {
                        return Result.Fail<(string, string?, string?)>(string.Create(CultureInfo.InvariantCulture, $"'add {parts[0]}' has no '**Files:**' line beneath it. An area is the files it covers."));
                    }

                    while (lines.Count > 0 && lines[^1].Trim().Length == 0)
                    {
                        lines.RemoveAt(lines.Count - 1);
                    }

                    lines.AddRange([string.Empty, "## " + parts[0], string.Empty, files, string.Empty, "**Is:** " + proposal.Statement, string.Empty]);
                    break;

                case Operation.Rename:
                    {
                        (int start, _) = AreaBlock(lines, parts[0]);
                        if (start < 0)
                        {
                            return NoArea(parts[0]);
                        }

                        lines[start] = "## " + parts[1];
                        rules = Retag(rules, names => [.. names.Select(name => name == parts[0] ? parts[1] : name)]);
                        decisions = Retag(decisions, names => [.. names.Select(name => name == parts[0] ? parts[1] : name)]);
                        break;
                    }

                case Operation.Merge:
                    {
                        (int start, int end) = AreaBlock(lines, parts[0]);
                        (int into, int intoEnd) = AreaBlock(lines, parts[2]);
                        if (start < 0)
                        {
                            return NoArea(parts[0]);
                        }

                        if (into < 0)
                        {
                            return NoArea(parts[2]);
                        }

                        string moved = lines.Skip(start).Take(end - start).FirstOrDefault(line => line.StartsWith("**Files:**", StringComparison.Ordinal))?["**Files:**".Length..].Trim() ?? string.Empty;
                        int filesAt = lines.FindIndex(into, intoEnd - into, line => line.StartsWith("**Files:**", StringComparison.Ordinal));
                        lines[filesAt] = lines[filesAt].TrimEnd() + ", " + moved;
                        (start, end) = AreaBlock(lines, parts[0]);
                        lines.RemoveRange(start, end - start);
                        rules = Retag(rules, names => [.. names.Select(name => name == parts[0] ? parts[2] : name).Distinct(StringComparer.Ordinal)]);
                        decisions = Retag(decisions, names => [.. names.Select(name => name == parts[0] ? parts[2] : name).Distinct(StringComparer.Ordinal)]);
                        break;
                    }

                case Operation.Remove:
                    {
                        (int start, int end) = AreaBlock(lines, parts[0]);
                        if (start < 0)
                        {
                            return NoArea(parts[0]);
                        }

                        lines.RemoveRange(start, end - start);
                        rules = Retag(rules, names => [.. names.Where(name => name != parts[0])]);
                        decisions = Retag(decisions, names => [.. names.Where(name => name != parts[0])]);
                        break;
                    }

                case Operation.Files:
                    {
                        (int start, int end) = AreaBlock(lines, parts[0]);
                        if (start < 0)
                        {
                            return NoArea(parts[0]);
                        }

                        int filesAt = lines.FindIndex(start, end - start, line => line.StartsWith("**Files:**", StringComparison.Ordinal));
                        lines[filesAt] = "**Files:** " + proposal.Statement;
                        break;
                    }

                case Operation.Tag:
                    {
                        IReadOnlyList<string> names = string.Equals(proposal.Statement.Trim(), "none", StringComparison.OrdinalIgnoreCase)
                            ? []
                            : AreaParser.Names(proposal.Statement);
                        bool isDecision = parts[0].StartsWith("DEC_", StringComparison.Ordinal);
                        Result<string> tagged = Tag(isDecision ? decisions : rules, parts[0], names);

                        if (tagged.IsFailed)
                        {
                            return tagged.ToResult<(string, string?, string?)>();
                        }

                        if (isDecision)
                        {
                            decisions = tagged.Value;
                        }
                        else
                        {
                            rules = tagged.Value;
                        }

                        break;
                    }

                default:
                    return Result.Fail<(string, string?, string?)>("An area is added, renamed, merged, removed, given new files, or tagged onto an entry.");
            }
        }

        return Result.Ok((string.Join(newline, lines), rules, decisions));
    }

    /// <summary>The empty area file, for a solution placing its first area.</summary>
    public static string EmptyAreas { get; } = string.Join(
        "\n",
        [
            "# The parts of this solution's code",
            string.Empty,
            .. ProtectedPaths.Note,
            string.Empty,
            "Each area is a name for a set of files. A rule or decision carrying an area's name reaches a reader",
            "in full only when the change touches those files; one carrying none reaches every reader (D-150).",
            string.Empty,
        ]);

    private static Result<(string, string?, string?)> NoArea(string name) =>
        Result.Fail<(string, string?, string?)>(string.Create(CultureInfo.InvariantCulture, $"There is no area called {name}."));

    private static (int Start, int End) AreaBlock(List<string> lines, string name)
    {
        int start = lines.FindIndex(line => line.TrimEnd() == "## " + name);

        if (start < 0)
        {
            return (-1, -1);
        }

        int end = lines.FindIndex(start + 1, line => line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("# ", StringComparison.Ordinal));
        return (start, end < 0 ? lines.Count : end);
    }

    // Rewrites every entry's Areas line through a function, dropping a line left empty.
    private static string? Retag(string? text, Func<IReadOnlyList<string>, IReadOnlyList<string>> change)
    {
        if (text is null)
        {
            return null;
        }

        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];

        for (int index = lines.Count - 1; index >= 0; index--)
        {
            if (!lines[index].StartsWith("**Areas:**", StringComparison.Ordinal))
            {
                continue;
            }

            IReadOnlyList<string> names = change(AreaParser.Names(lines[index]["**Areas:**".Length..]));

            if (names.Count == 0)
            {
                lines.RemoveAt(index);

                if (index < lines.Count && lines[index].Trim().Length == 0 && index > 0 && lines[index - 1].Trim().Length == 0)
                {
                    lines.RemoveAt(index);
                }
            }
            else
            {
                lines[index] = "**Areas:** " + string.Join(", ", names);
            }
        }

        return string.Join(newline, lines);
    }

    // Sets one entry's Areas line: replaced where it has one, added beneath its heading where it has none.
    private static Result<string> Tag(string? text, string id, IReadOnlyList<string> names)
    {
        if (text is null)
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"There is no file holding {id} to tag."));
        }

        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];
        (int start, int end) = Block(lines, id);

        if (start < 0)
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"There is no {id} to tag."));
        }

        int existing = lines.FindIndex(start, end - start, line => line.StartsWith("**Areas:**", StringComparison.Ordinal));

        if (existing >= 0)
        {
            if (names.Count == 0)
            {
                lines.RemoveRange(existing, existing + 1 < lines.Count && lines[existing + 1].Trim().Length == 0 ? 2 : 1);
            }
            else
            {
                lines[existing] = "**Areas:** " + string.Join(", ", names);
            }
        }
        else if (names.Count > 0)
        {
            lines.InsertRange(start + 1, [string.Empty, "**Areas:** " + string.Join(", ", names)]);
        }

        return Result.Ok(string.Join(newline, lines));
    }

    private static string Verb(Operation operation) => operation switch
    {
        Operation.Add => "add",
        Operation.Supersede => "supersede",
        Operation.Reword => "reword",
        Operation.Rename => "rename",
        Operation.Merge => "merge",
        Operation.Remove => "remove",
        Operation.Files => "files",
        Operation.Tag => "tag",
        _ => "set",
    };

    private static string Entry(string id, Proposal proposal) =>
        "## " + id + " — " + proposal.Statement + "\n\n" + proposal.Body.Trim() + "\n";

    private static string Insert(string text, string entry, string? before)
    {
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        int at = before is null ? -1 : normalised.IndexOf("\n" + before, StringComparison.Ordinal);

        string result = at < 0
            ? normalised.TrimEnd('\n') + "\n\n" + entry
            : normalised[..(at + 1)] + entry + "\n" + normalised[(at + 1)..];

        return result.Replace("\n", newline, StringComparison.Ordinal);
    }

    private static (int Start, int End) Block(List<string> lines, string id)
    {
        int start = lines.FindIndex(line => line.StartsWith("## " + id + " ", StringComparison.Ordinal)
            || line.StartsWith("## " + id + ".", StringComparison.Ordinal));

        if (start < 0)
        {
            return (-1, -1);
        }

        int end = lines.FindIndex(start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        return (start, end < 0 ? lines.Count : end);
    }

    private static Result<string> MarkSuperseded(string text, string target, string by)
    {
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];
        (int start, int end) = Block(lines, target);

        if (start < 0)
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"There is no {target} to supersede."));
        }

        if (lines.Skip(start).Take(end - start).Any(line => line.StartsWith("**Superseded-by:**", StringComparison.Ordinal)))
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"{target} is already superseded. Supersede the entry that replaced it."));
        }

        int last = end - 1;
        while (last > start && lines[last].Trim().Length == 0)
        {
            last--;
        }

        lines.Insert(last + 1, string.Empty);
        lines.Insert(last + 2, "**Superseded-by:** " + by);

        return Result.Ok(string.Join(newline, lines));
    }

    private static Result<string> ReplaceEntry(string text, string target, string entry)
    {
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];
        (int start, int end) = Block(lines, target);

        if (start < 0)
        {
            return Result.Fail<string>(string.Create(CultureInfo.InvariantCulture, $"There is no {target} to reword."));
        }

        lines.RemoveRange(start, end - start);
        lines.InsertRange(start, [.. entry.TrimEnd('\n').Split('\n'), string.Empty]);

        return Result.Ok(string.Join(newline, lines));
    }

    // One record's file on its way to a draft: as it stands, as drafted, what it assigned, what reached it.
    private sealed record Pending(string File, string? Original, string? Draft, List<string> Assigned, List<Proposal> Mine);

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Command, DraftResult>
    {
        public async Task<Result<DraftResult>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!File.Exists(request.From))
            {
                return Result.Ok(DraftResult.Refused(string.Create(CultureInfo.InvariantCulture, $"There is no proposal file at {request.From}. '/sdlc:mine' writes one.")));
            }

            Result<IReadOnlyList<Proposal>> parsed = Parse(await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false));

            if (parsed.IsFailed)
            {
                return Result.Ok(DraftResult.Refused(string.Join(Environment.NewLine, parsed.Errors.Select(error => error.Message))));
            }

            List<Proposal> accepted = [.. parsed.Value.Where(proposal => proposal.Accepted)];

            if (accepted.Count == 0)
            {
                return Result.Ok(DraftResult.Refused(string.Create(CultureInfo.InvariantCulture, $"Nothing in {request.From} is marked [x], so there is nothing to draft. Mark each proposal you accept, edit its wording as you want it, and run this again.")));
            }

            string directory = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);
            StringBuilder report = new();
            List<string> renames = [];
            Dictionary<Record, Pending> drafts = [];

            foreach ((Record record, string file) in new[]
            {
                (Record.Rules, FileConstraintDocuments.SolutionConstraintsFileName),
                (Record.Decisions, DecisionParser.FileName),
                (Record.Stack, Cycle.TechStackFileName),
                (Record.Areas, AreaParser.FileName),
            })
            {
                string path = Path.Combine(directory, file);
                string? original = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;
                List<Proposal> mine = [.. accepted.Where(proposal => proposal.Record == record)];
                drafts[record] = new Pending(file, original, original, [], mine);

                // A solution places its first area from mining, so the area file is the one that may be absent.
                if (mine.Count > 0 && original is null && record != Record.Areas)
                {
                    return Result.Ok(DraftResult.Refused(string.Create(CultureInfo.InvariantCulture, $"{mine.Count} accepted proposal(s) change {file}, and there is no {file} to copy. Place it first.")));
                }
            }

            foreach (Record record in new[] { Record.Rules, Record.Decisions, Record.Stack })
            {
                Pending entry = drafts[record];

                if (entry.Mine.Count == 0)
                {
                    continue;
                }

                Result<string> draft = record switch
                {
                    Record.Rules => ApplyRules(entry.Original!, entry.Mine, entry.Assigned),
                    Record.Decisions => ApplyDecisions(entry.Original!, entry.Mine, entry.Assigned),
                    _ => ApplyStack(entry.Original!, entry.Mine),
                };

                if (draft.IsFailed)
                {
                    return Result.Ok(DraftResult.Refused(string.Join(Environment.NewLine, draft.Errors.Select(error => error.Message))));
                }

                drafts[record] = entry with { Draft = draft.Value };
            }

            // Areas last: a rename or a tag reaches the rule and decision drafts as they now stand (D-150).
            if (drafts[Record.Areas].Mine.Count > 0)
            {
                Pending areas = drafts[Record.Areas];
                Result<(string Areas, string? Rules, string? Decisions)> applied = ApplyAreas(
                    areas.Original ?? EmptyAreas,
                    drafts[Record.Rules].Draft,
                    drafts[Record.Decisions].Draft,
                    areas.Mine);

                if (applied.IsFailed)
                {
                    return Result.Ok(DraftResult.Refused(string.Join(Environment.NewLine, applied.Errors.Select(error => error.Message))));
                }

                drafts[Record.Areas] = areas with { Draft = applied.Value.Areas };
                drafts[Record.Rules] = drafts[Record.Rules] with { Draft = applied.Value.Rules };
                drafts[Record.Decisions] = drafts[Record.Decisions] with { Draft = applied.Value.Decisions };
            }

            if (Unplaced(drafts[Record.Areas].Draft, drafts[Record.Rules].Draft, drafts[Record.Decisions].Draft) is { } unplaced)
            {
                return Result.Ok(DraftResult.Refused(unplaced));
            }

            foreach ((Record record, Pending entry) in drafts)
            {
                if (entry.Draft is null || string.Equals(entry.Draft, entry.Original, StringComparison.Ordinal))
                {
                    continue;
                }

                string? lost = entry.Original is null || record == Record.Areas ? null : Lost(record, entry.Original, entry.Draft);

                if (lost is not null)
                {
                    return Result.Ok(DraftResult.Refused(string.Create(CultureInfo.InvariantCulture, $"The draft of {entry.File} would lose {lost}, so nothing was written. A draft is the file as it stands with the accepted lines applied, and nothing may go missing on the way.")));
                }

                string? over = OverBudget(record, entry.Draft, entry.Assigned);

                if (over is not null)
                {
                    return Result.Ok(DraftResult.Refused(string.Create(CultureInfo.InvariantCulture, $"The draft of {entry.File} would place {over}, so nothing was written. A new entry's title takes at most {EntryBudget.Headline} words and each clause {EntryBudget.Clause}, so every reader can be handed its headline and the ones in scope its body (D-149). Shorten it in {request.From} and run this again.")));
                }
            }

            foreach ((Record record, Pending entry) in drafts)
            {
                if (entry.Draft is null || string.Equals(entry.Draft, entry.Original, StringComparison.Ordinal))
                {
                    continue;
                }

                string draftName = Path.GetFileNameWithoutExtension(entry.File) + ".draft.md";
                await File.WriteAllTextAsync(Path.Combine(directory, draftName), entry.Draft, cancellationToken).ConfigureAwait(false);

                report.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.File}: {entry.Mine.Count} accepted{(entry.Assigned.Count > 0 ? " -- " + string.Join(", ", entry.Assigned) : string.Empty)}"));

                foreach (Proposal proposal in entry.Mine)
                {
                    report.AppendLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {Verb(proposal.Operation)}{(proposal.Target is null ? string.Empty : " " + proposal.Target)} — {proposal.Statement}"));
                }

                if (entry.Mine.Count == 0)
                {
                    report.AppendLine("  carried along: an area renamed, merged, removed or tagged reaches the entries here");
                }

                string from = Cycle.SdlcDirectoryName + "/" + draftName;
                string to = Cycle.SdlcDirectoryName + "/" + entry.File;
                renames.Add(string.Create(CultureInfo.InvariantCulture, $"    Move-Item -Force {from} {to}"));
            }

            int declined = parsed.Value.Count - accepted.Count;
            report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{declined} proposal(s) not accepted."));

            string instructions = string.Join(
                Environment.NewLine,
                [
                    "Each draft is the file as it stands with only the accepted lines applied, checked to lose",
                    "no entry and no row. Read it, then rename it from the repository root -- the rename is the",
                    "decision, and it is yours:",
                    string.Empty,
                    .. renames,
                ]);

            return Result.Ok(DraftResult.Drafted(report.ToString(), instructions));
        }

        // An entry naming an area nobody defines would be shown in full everywhere -- harmless, and a typo nobody
        // notices -- so a draft that would place one says so instead (D-150).
        private static string? Unplaced(string? areas, string? rules, string? decisions)
        {
            HashSet<string> defined = new(
                areas is null ? [] : (AreaParser.Parse(areas, AreaParser.FileName).ValueOrDefault ?? []).Select(area => area.Name),
                StringComparer.Ordinal);

            List<string> unknown =
            [
                .. (rules is null ? [] : ConstraintParser.Parse(rules, "constraints.draft.md").ValueOrDefault ?? [])
                    .Where(rule => rule.IsInForce)
                    .SelectMany(rule => rule.InAreas.Where(name => !defined.Contains(name)).Select(name => rule.Id + " names '" + name + "'")),
                .. (decisions is null ? [] : DecisionParser.Parse(decisions, "decisions.draft.md").ValueOrDefault ?? [])
                    .Where(decision => decision.IsStanding)
                    .SelectMany(decision => decision.InAreas.Where(name => !defined.Contains(name)).Select(name => decision.Id + " names '" + name + "'")),
            ];

            return unknown.Count == 0
                ? null
                : "The drafts would carry areas that are not defined: " + string.Join("; ", unknown) + ". Add the area under '## Areas', or tag the entry with one that exists, and run this again.";
        }

        // Only the entries this draft writes are held to the budget. One already on the page is the person's,
        // and mining proposes rewording it rather than refusing it (D-149).
        private static string? OverBudget(Record record, string draft, IReadOnlyCollection<string> assigned)
        {
            List<string> over = record switch
            {
                Record.Rules => [.. (ConstraintParser.Parse(draft, "constraints.draft.md").ValueOrDefault ?? [])
                    .Where(rule => assigned.Contains(rule.Id.ToString()))
                    .Select(rule => (Id: rule.Id.ToString(), Overruns: EntryBudget.Of(rule)))
                    .Where(entry => entry.Overruns.Count > 0)
                    .Select(entry => entry.Id + " (" + EntryBudget.Describe(entry.Overruns) + ")")],
                Record.Decisions => [.. (DecisionParser.Parse(draft, "decisions.draft.md").ValueOrDefault ?? [])
                    .Where(decision => assigned.Contains(decision.Id.ToString()))
                    .Select(decision => (Id: decision.Id.ToString(), Overruns: EntryBudget.Of(decision)))
                    .Where(entry => entry.Overruns.Count > 0)
                    .Select(entry => entry.Id + " (" + EntryBudget.Describe(entry.Overruns) + ")")],
                _ => [],
            };

            return over.Count == 0 ? null : string.Join("; ", over);
        }

        private static string? Lost(Record record, string original, string draft)
        {
            switch (record)
            {
                case Record.Rules:
                    {
                        HashSet<int> before = [.. ConstraintParser.Parse(original, "constraints.md").ValueOrDefault?.Select(rule => rule.Id.Number) ?? []];
                        Result<IReadOnlyList<Constraint>> after = ConstraintParser.Parse(draft, "constraints.draft.md");
                        if (after.IsFailed)
                        {
                            return "its readability: " + string.Join("; ", after.Errors.Select(error => error.Message));
                        }

                        List<int> missing = [.. before.Except(after.Value.Select(rule => rule.Id.Number))];
                        return missing.Count == 0 ? null : string.Join(", ", missing.Select(number => string.Create(CultureInfo.InvariantCulture, $"LOC_{number:00}")));
                    }

                case Record.Decisions:
                    {
                        HashSet<int> before = [.. DecisionParser.Parse(original, DecisionParser.FileName).ValueOrDefault?.Select(decision => decision.Id.Number) ?? []];
                        Result<IReadOnlyList<Decision>> after = DecisionParser.Parse(draft, "decisions.draft.md");
                        if (after.IsFailed)
                        {
                            return "its readability: " + string.Join("; ", after.Errors.Select(error => error.Message));
                        }

                        List<int> missing = [.. before.Except(after.Value.Select(decision => decision.Id.Number))];
                        return missing.Count == 0 ? null : string.Join(", ", missing.Select(number => DecisionId.Of(number).ToString()));
                    }

                default:
                    {
                        HashSet<string> after = new(StackTable.Rows(draft).Select(row => row.Concern), StringComparer.OrdinalIgnoreCase);
                        List<string> missing = [.. StackTable.Rows(original).Select(row => row.Concern).Where(concern => !after.Contains(concern))];
                        return missing.Count == 0 ? null : "the rows " + string.Join(", ", missing);
                    }
            }
        }
    }
}
