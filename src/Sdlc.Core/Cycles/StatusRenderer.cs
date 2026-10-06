using System.Globalization;
using System.Text;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Findings;

namespace Sdlc.Core.Cycles;

/// <summary>One artifact's state plus what its document says about itself.</summary>
/// <param name="State">Where the artifact has got to.</param>
/// <param name="Outline">Its headline and sections, read from the document.</param>
/// <param name="Shape">
/// What the schema made of it. Shown above the findings, because a shape problem is usually why a
/// round found what it found -- an interrogation reading an artifact with no Proof section raises
/// something about the proof, and the author chases the finding instead of the missing heading.
/// </param>
public sealed record StagePanel(
    ReadCycleStatus.ArtifactState State,
    ArtifactOutline Outline,
    ArtifactValidation? Shape = null);

/// <summary>
/// The panel a person sees at the end of a stage.
/// </summary>
/// <remarks>
/// This is the largest adoption lever in the plugin, because a tool people cannot read does not get
/// used — so it is a contract rather than presentation, and the suite asserts it. Four rules it keeps:
/// the command-line tool prints it and no skill writes it as prose, so the format cannot drift between
/// hosts; an artifact's checks are called checks rather than verification, because <c>verify</c> is a
/// phase name here with a specific meaning; it ends with the literal next command, copy-pasteable; and
/// the glyphs are ASCII so it renders in a pipeline log and in a console that is not UTF-8.
/// </remarks>
public static class StatusRenderer
{
    private const string Indent = "  ";
    private const string Label = "            ";
    private const int Width = 92;

    /// <summary>
    /// Renders the four lines a person wants between stages.
    /// </summary>
    /// <remarks>
    /// <para>A renderer argument rather than a second query, which is the same reason
    /// <c>constraints --brief</c> is one: there is one answer about where a cycle has got to, and two
    /// queries for it would eventually disagree.</para>
    /// <para><strong>It ends with the same next command as the full panel, computed by the same
    /// method.</strong> A short form that worked out the next step for itself would be a second opinion
    /// on the only line people copy.</para>
    /// </remarks>
    /// <param name="status">The cycle's state.</param>
    /// <param name="panel">The artifact the stage just finished, with its outline.</param>
    /// <returns>The rendered panel, ending in a newline.</returns>
    public static string RenderBrief(ReadCycleStatus.Response status, StagePanel panel)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(panel);

        StringBuilder output = new();
        ReadCycleStatus.ArtifactState state = panel.State;

        // Verify counts too. Omitting it made this say "suggestions 4" above a line pointing at
        // 'sdlc suggestions', which lists five -- the panel disagreeing with the command it sends
        // people to. A verification's own interrogation raises real findings and they are still open.
        IEnumerable<ReadCycleStatus.ArtifactState> counted = status.Verify is { } verify
            ? [.. status.Artifacts, verify]
            : status.Artifacts;

        int blockers = counted.Sum(artifact => artifact.OpenBlockers.Count);
        int suggestions = counted.Sum(artifact => artifact.OpenSuggestions.Count);

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{status.Cycle.Id}   {Cycle.NameOf(state.Kind)}   {Round(state)}   blockers {blockers}   suggestions {suggestions}   gate {Gate(status)}"));

        output.AppendLine();

        Field(output, "Says", panel.Outline.Headline ?? "(no heading yet)");

        if (suggestions > 0)
        {
            Field(output, "Open", string.Create(
                CultureInfo.InvariantCulture,
                $"{Count(suggestions, "suggestion"),-44}sdlc suggestions {status.Cycle.Id}"));
        }

        Next(output, status, state);

        return output.ToString();
    }

    /// <summary>The round state, as the full panel's right-hand column says it.</summary>
    private static string Round(ReadCycleStatus.ArtifactState state) => state.Rounds == 0
        ? "not interrogated"
        : string.Create(CultureInfo.InvariantCulture, $"r{state.LatestRound} {Count(state.OpenBlockers.Count)}");

    /// <summary>The gate in one word, for the counter line.</summary>
    private static string Gate(ReadCycleStatus.Response status) => status.Gate.Refusal switch
    {
        ApprovalGate.GateRefusal.None => "approved",
        ApprovalGate.GateRefusal.NotApproved => "not approved",
        ApprovalGate.GateRefusal.UnreadableLedger => "unreadable",
        ApprovalGate.GateRefusal.OpenBlocker => "blocked",
        ApprovalGate.GateRefusal.HashMismatch => "stale hash",
        ApprovalGate.GateRefusal.StaleRound => "stale round",
        ApprovalGate.GateRefusal.Abandoned => "abandoned",
        _ => "refused",
    };

    /// <summary>
    /// Renders the panel for one stage.
    /// </summary>
    /// <param name="status">The cycle's state.</param>
    /// <param name="panel">The artifact the stage just finished, with its outline.</param>
    /// <returns>The rendered panel, ending in a newline.</returns>
    public static string RenderStage(ReadCycleStatus.Response status, StagePanel panel)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(panel);

        StringBuilder output = new();
        ReadCycleStatus.ArtifactState state = panel.State;

        string heading = string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{Cycle.NameOf(state.Kind)} - {status.Cycle.Id}");

        string rounds = state.Rounds == 0
            ? "not interrogated"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"round {state.LatestRound} - {Count(state.OpenBlockers.Count)}");

        // Right-aligned on the same line as the heading, so the state is the first thing read.
        int gap = Math.Max(1, Width - heading.Length - rounds.Length);
        output.AppendLine(heading + new string(' ', gap) + rounds);
        output.AppendLine();

        Field(output, "Says", panel.Outline.Headline ?? "(no heading yet)");
        Field(output, string.Empty, ArtifactOutlineReader.Describe(panel.Outline));
        output.AppendLine();

        Shape(output, panel.Shape);

        Checks(output, state);
        Conflicts(output, state);
        output.AppendLine();

        Field(output, "Gate", GateLine(status));
        Hashes(output, status);
        output.AppendLine();

        Next(output, status, state);

        return output.ToString();
    }

    /// <summary>
    /// Writes what the schema found, and nothing at all when it found nothing.
    /// </summary>
    /// <remarks>
    /// Silence on a sound artifact is deliberate. A panel that reported every check that passed would
    /// bury the one that did not, and the panel's whole job is that the state is the first thing read.
    /// </remarks>
    private static void Shape(StringBuilder output, ArtifactValidation? shape)
    {
        if (shape is null || shape.IsValid)
        {
            return;
        }

        Field(output, "Shape", Count(shape.Violations.Count, "problem"));

        foreach (SchemaViolation violation in shape.Violations)
        {
            Field(output, string.Empty, "    " + violation.Where + " " + violation.Problem);
        }

        output.AppendLine();
    }

    /// <summary>
    /// Writes the rules the latest round found cannot both be satisfied, and nothing when there are none.
    /// </summary>
    /// <remarks>
    /// Under its own heading rather than among the blockers, because the two call for different things.
    /// A blocker is answered by editing this artifact; a conflict cannot be, and an agent that read one
    /// as a blocker would edit the document in a circle. That is the loop this exists to end.
    /// <para>
    /// It says what it knows and no more. Whether the rules genuinely cannot both hold, or whether this
    /// reader was mistaken, is not something a panel can decide -- so the line names the rules and the
    /// reader's reasoning, and leaves the judgement to whoever reads it.
    /// </para>
    /// </remarks>
    private static void Conflicts(StringBuilder output, ReadCycleStatus.ArtifactState state)
    {
        if (state.Conflicts.Count == 0)
        {
            return;
        }

        output.AppendLine();
        Field(output, "Conflicts", Count(state.Conflicts.Count, "conflict"));

        foreach (Conflict conflict in state.Conflicts)
        {
            Field(output, string.Empty, "    " + conflict.Ids());

            foreach (Citation citation in conflict.Between ?? [])
            {
                if (citation.Line() is { } rule)
                {
                    Field(output, string.Empty, "        " + rule);
                }
            }

            Field(output, string.Empty, "    " + conflict.Statement);
        }
    }

    private static void Checks(StringBuilder output, ReadCycleStatus.ArtifactState state)
    {
        if (state.Rounds == 0)
        {
            Field(
                output,
                "Checks",
                "none yet. A stage will not run until the artifact before it has a round with no open blocking findings.");
            return;
        }

        // The per-round history, so a reader sees what each round found rather than only where it
        // ended. No recommendation about whether to continue is attached: that judgement has been
        // wrong before, and the owner decides.
        IEnumerable<string> history = state.History.Select((blockers, index) => string.Create(
            CultureInfo.InvariantCulture,
            $"r{index + 1}: {Count(blockers)}"));

        Field(output, "Checks", string.Join(" - ", history));

        // The id and the constraint on one line, then the rule it cites, then the statement: both of
        // those are sentences, and wrapping either beside two short columns would collapse their
        // alignment. The rule comes first because it is the one a reader needs in order to weigh the
        // objection -- this panel is where somebody at the gate meets a blocker, and an id with no
        // words beside it tells them a rule was broken without telling them which.
        foreach (Finding finding in state.OpenBlockers)
        {
            Field(
                output,
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"{finding.Id,-7}{finding.Cites?.Id,-12}blocking"));

            if (finding.Cites?.Line() is { } rule)
            {
                Field(output, string.Empty, "    " + rule);
            }

            Field(output, string.Empty, "    " + finding.Statement);
        }
    }

    /// <summary>
    /// What to do once the gate is satisfied, which depends on how far the build has got.
    /// </summary>
    /// <remarks>
    /// Telling somebody to build a cycle that has already built and verified is the panel being confidently
    /// wrong about the one thing it exists to say. The verification's outcome is the only thing that
    /// distinguishes the four states, so it is what this reads.
    /// </remarks>
    private static (string Command, string Why) AfterTheGate(ReadCycleStatus.Response status, string id) =>
        status.Outcome switch
        {
            null => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc build {id}"),
                "the gate is satisfied"),

            string pass when pass.Equals("pass", StringComparison.OrdinalIgnoreCase) => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc land {id}"),
                "verified clean"),

            string blocked when blocked.Equals("blocked", StringComparison.OrdinalIgnoreCase) => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc remediate {id}"),
                "a criterion is not met"),

            string escalated when escalated.Equals("escalated", StringComparison.OrdinalIgnoreCase) => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc status {id}"),
                "escalated - this one needs a person"),

            _ => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc verify {id}"),
                string.Create(CultureInfo.InvariantCulture, $"the outcome reads '{status.Outcome}'")),
        };

    /// <summary>
    /// Each document's approved hash beside its current one, when the gate says one has changed.
    /// </summary>
    /// <remarks>
    /// <strong>The second half of §5.3's mitigation, which shipped without it.</strong> That section
    /// names two things that make the paste flow bearable: a refusal that names which document changed,
    /// and <c>status</c> showing the first twelve characters of each hash beside the current twelve.
    /// Only the first existed, so the panel said "approved, but a document has changed since" and a
    /// person had to run <c>sdlc gate</c> to find out which -- and the panel is the most-read output in
    /// the plugin (§14). F-19.
    /// <para>
    /// Only on a mismatch. Three hash pairs on every panel is noise, and the pairs say nothing a reader
    /// needs while they agree.
    /// </para>
    /// </remarks>
    private static void Hashes(StringBuilder output, ReadCycleStatus.Response status)
    {
        if (status.Gate.Refusal != ApprovalGate.GateRefusal.HashMismatch)
        {
            return;
        }

        foreach (ApprovalGate.DocumentHash hash in status.Gate.Hashes)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{new string(' ', 10)}{hash.Document,-12}{Digits(hash.Approved)} {(hash.Matches ? "==" : "->")} {Digits(hash.Current)}"));
        }
    }

    /// <summary>The first twelve characters of a hash, or a word saying there was not one.</summary>
    /// <param name="hash">The hash, which is null when the document or the approval did not record it.</param>
    /// <returns>Twelve characters, or "none".</returns>
    private static string Digits(string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? "none".PadRight(12) : ContentHash.Abbreviate(hash);

    private static string GateLine(ReadCycleStatus.Response status) => status.Gate.Refusal switch
    {
        ApprovalGate.GateRefusal.None => "approved, and all three documents match what was approved",
        ApprovalGate.GateRefusal.NotApproved => "not approved yet",
        ApprovalGate.GateRefusal.Refused => "sent back - " + (status.Gate.Ledger?.Current?.Reason ?? "no reason recorded"),
        ApprovalGate.GateRefusal.HashMismatch => "approved, but a document has changed since",
        ApprovalGate.GateRefusal.OpenBlocker => "approved, but a blocking finding is still open",
        ApprovalGate.GateRefusal.StaleRound => "approved, but an interrogation judged a document that has changed since",
        ApprovalGate.GateRefusal.Abandoned => "abandoned, which is terminal",
        ApprovalGate.GateRefusal.MissingDocument => "cannot be approved until all three documents exist",
        ApprovalGate.GateRefusal.NoCycle => "no cycle directory yet",
        _ => "unknown",
    };

    /// <summary>
    /// Writes the next command, literally, so it can be copied.
    /// </summary>
    /// <remarks>
    /// This is the detail that makes the flow feel smooth rather than merely informative: a panel that
    /// reports a state and leaves a person to work out the command has stopped one step short.
    /// </remarks>
    private static void Next(StringBuilder output, ReadCycleStatus.Response status, ReadCycleStatus.ArtifactState state)
    {
        string id = status.Cycle.Id.ToString();
        string name = Cycle.NameOf(state.Kind);
        List<(string Command, string Why)> next = [];

        if (!state.Exists)
        {
            next.Add((string.Create(CultureInfo.InvariantCulture, $"sdlc {name} {id}"), "write it"));
        }
        else if (state.Rounds == 0)
        {
            next.Add((
                string.Create(CultureInfo.InvariantCulture, $"sdlc interrogate {id} {name}"),
                "nothing has argued with it yet"));
        }
        else if (state.OpenBlockers.Count > 0)
        {
            next.Add((
                string.Create(CultureInfo.InvariantCulture, $"sdlc refine {id} {name}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"apply {string.Join(", ", state.OpenBlockers.Select(f => f.Id))}")));
            next.Add((
                string.Create(CultureInfo.InvariantCulture, $"sdlc interrogate {id} {name}"),
                "re-check after"));
        }
        else
        {
            ReadCycleStatus.ArtifactState? waiting = status.Blocking;

            if (waiting is not null && waiting.Kind != state.Kind)
            {
                next.Add((
                    string.Create(CultureInfo.InvariantCulture, $"sdlc {Cycle.NameOf(waiting.Kind)} {id}"),
                    string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(waiting.Kind)} is next")));
            }
            else if (status.Gate.MayBuild)
            {
                next.Add(AfterTheGate(status, id));
            }
            else
            {
                next.Add((
                    string.Create(CultureInfo.InvariantCulture, $"sdlc draft approval {id}"),
                    "all three are clear - approve them"));
            }
        }

        bool first = true;
        foreach ((string command, string why) in next)
        {
            Field(output, first ? "Next" : string.Empty, string.Create(
                CultureInfo.InvariantCulture,
                $"{command,-44}{why}"));
            first = false;
        }
    }

    private static string Count(int blockers) => blockers switch
    {
        0 => "clean",
        1 => "1 blocking",
        _ => string.Create(CultureInfo.InvariantCulture, $"{blockers} blocking"),
    };

    private static string Count(int number, string noun) => number == 1
        ? "1 " + noun
        : string.Create(CultureInfo.InvariantCulture, $"{number} {noun}s");

    /// <summary>
    /// Writes one labelled field, wrapping only when it has to.
    /// </summary>
    /// <remarks>
    /// Wrapping splits on spaces and rejoins with one, which collapses deliberate column padding - so a
    /// line that already fits is written through untouched. The first version wrapped everything and
    /// turned aligned columns into prose.
    /// <para>
    /// A wrapped line keeps whatever indent its first line had. Without that, the continuation of an
    /// indented list item starts back at the label column and reads as a new item, which is worse than
    /// the overflow it was meant to fix.
    /// </para>
    /// </remarks>
    private static void Field(StringBuilder output, string label, string body)
    {
        string prefix = string.Create(CultureInfo.InvariantCulture, $"{Indent}{label,-10}");

        if (prefix.Length + body.Length <= Width)
        {
            output.AppendLine(prefix + body);
            return;
        }

        string hanging = Label + new string(' ', body.Length - body.TrimStart().Length);

        bool first = true;
        foreach (string piece in Wrap(body.TrimStart(), Width - hanging.Length))
        {
            output.AppendLine(first ? prefix + body[..(body.Length - body.TrimStart().Length)] + piece : hanging + piece);
            first = false;
        }
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        StringBuilder line = new();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}
