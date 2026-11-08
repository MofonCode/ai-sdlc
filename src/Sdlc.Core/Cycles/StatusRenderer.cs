using System.Globalization;
using System.Text;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;

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
    ArtifactValidation? Shape = null,
    string? Size = null);

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
    /// <param name="host">The host this is printed inside, when it is known; it decides how a stage is named.</param>
    /// <returns>The rendered panel, ending in a newline.</returns>
    public static string RenderBrief(ReadCycleStatus.Response status, StagePanel panel, SupportedHost? host = null)
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
        int suggestions = ReadCycleStatus.OpenSuggestionCount(counted);

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}{status.Cycle.Id}   {Cycle.NameOf(state.Kind)}   {Round(state)}   blockers {blockers}   suggestions {suggestions}   gate {Gate(status)}"));

        output.AppendLine();

        Field(output, "Says", panel.Outline.Headline ?? "(no heading yet)");

        // Each blocker's gist and the command that explains them. This is the panel every stage skill ends
        // a lap on, and "blockers 3" in the counter line told somebody something was wrong without a single
        // word they could act on. The gist rather than the id and rule, because ids are what a reader then
        // has to go and resolve; a handful at most, because past that the full panel is the better read.
        if (state.OpenBlockers.Count > 0)
        {
            const int Shown = 5;

            foreach ((Finding finding, int at) in state.OpenBlockers.Take(Shown).Select((finding, at) => (finding, at)))
            {
                Hung(output, finding.Id.PadRight(Math.Max(7, finding.Id.Length + 2)), finding.Headline, at == 0 ? "Blocking" : string.Empty);
            }

            if (state.OpenBlockers.Count > Shown)
            {
                Field(output, string.Empty, string.Create(CultureInfo.InvariantCulture, $"and {state.OpenBlockers.Count - Shown} more"));
            }

            Field(output, string.Empty, "Why each fails: " + FindingsCommand(status, state));
        }

        if (suggestions > 0)
        {
            Field(output, "Open", string.Create(
                CultureInfo.InvariantCulture,
                $"{Count(suggestions, "suggestion"),-44}sdlc suggestions {status.Cycle.Id}"));
        }

        // On the brief panel too, because this is the one the stage skills print at the end of a lap.
        // A halt the short form omitted would be a halt the loop never saw.
        if (Oscillation.Describe(state.Loop) is { } loop)
        {
            Field(output, "Loop", loop);
        }

        Spent(output, status, state);
        Departing(output, status);

        Amended(output, status);
        Successors(output, status, host);
        Next(output, status, state, host);

        return output.ToString();
    }

    /// <summary>What this document's readers have cost so far, and nothing when nothing was recorded.</summary>
    /// <remarks>
    /// A number, not a warning: the rounds are uncapped (D-65), and the person deciding whether to run
    /// another is the one this is for (D-121).
    /// </remarks>
    private static void Spent(StringBuilder output, ReadCycleStatus.Response status, ReadCycleStatus.ArtifactState state)
    {
        if (status.Usage?.Summarise(Cycle.NameOf(state.Kind)) is { } spent)
        {
            Field(output, "Spent", spent.Describe());
        }
    }

    /// <summary>The round state, as the full panel's right-hand column says it.</summary>
    private static string Round(ReadCycleStatus.ArtifactState state) => state.Rounds == 0
        ? "not interrogated"
        : string.Create(CultureInfo.InvariantCulture, $"r{state.LatestRound} {Count(state.OpenBlockers.Count)}");

    /// <summary>The gate in one word, for the counter line.</summary>
    private static string Gate(ReadCycleStatus.Response status) => status.Gate.Refusal switch
    {
        ApprovalGate.GateRefusal.None when status.Gate.Declared => "unapproved, by declaration",
        ApprovalGate.GateRefusal.None => "approved",
        ApprovalGate.GateRefusal.NotApproved => "not approved",
        ApprovalGate.GateRefusal.UnreadableLedger => "unreadable",
        ApprovalGate.GateRefusal.OpenBlocker => "blocked",
        ApprovalGate.GateRefusal.HashMismatch => "stale hash",
        ApprovalGate.GateRefusal.StaleRound => "stale round",
        ApprovalGate.GateRefusal.RewrittenRound => "rewritten round",
        ApprovalGate.GateRefusal.Abandoned => "abandoned",
        _ => "refused",
    };

    /// <summary>
    /// Renders the panel for one stage.
    /// </summary>
    /// <param name="status">The cycle's state.</param>
    /// <param name="panel">The artifact the stage just finished, with its outline.</param>
    /// <param name="host">The host this is printed inside, when it is known; it decides how a stage is named.</param>
    /// <returns>The rendered panel, ending in a newline.</returns>
    public static string RenderStage(ReadCycleStatus.Response status, StagePanel panel, SupportedHost? host = null)
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

        // A signal, never a refusal (D-130): what a person reads to see a document outgrowing one pass.
        if (panel.Size is not null)
        {
            Field(output, "Size", panel.Size);
        }

        // A cycle runs on the release it opened on (D-131). Said when its rounds were written by another
        // release than the one printing this, and nothing is refused for it: merging the integration branch
        // in is how a fix reaches a cycle in flight, and the person decides whether to.
        if (ReleaseMoved(state.Releases) is { } moved)
        {
            Field(output, "Release", moved);
        }

        output.AppendLine();

        Shape(output, panel.Shape);

        Checks(output, status, state);
        Loop(output, state);
        Spent(output, status, state);
        Conflicts(output, state);
        output.AppendLine();

        Field(output, "Gate", GateLine(status));
        Hashes(output, status);

        if (Departing(output, status))
        {
            Field(output, string.Empty, "Approving this cycle approves each of these.");
        }

        output.AppendLine();

        if (Amended(output, status) | Successors(output, status, host))
        {
            output.AppendLine();
        }

        Next(output, status, state, host);

        return output.ToString();
    }

    /// <summary>
    /// Writes that the plan was amended after this build started, and nothing when it was not (D-123).
    /// </summary>
    /// <returns>Whether anything was written.</returns>
    /// <summary>
    /// The recorded decisions this cycle departs from, one line each, and nothing when there are none (D-139).
    /// </summary>
    /// <remarks>
    /// On both panels, beside the gate: a departure is the one thing in a cycle that sets aside something a
    /// person already decided, and the panel is what they read before they approve.
    /// </remarks>
    /// <returns>Whether anything was written.</returns>
    private static bool Departing(StringBuilder output, ReadCycleStatus.Response status)
    {
        if (status.Departures is not { Count: > 0 } departures)
        {
            return false;
        }

        for (int index = 0; index < departures.Count; index++)
        {
            Field(output, index == 0 ? "Departs" : string.Empty, departures[index]);
        }

        return true;
    }

    /// <summary>
    /// The successors a person can act on — ready to start, or stuck — and nothing when there are none (D-125).
    /// </summary>
    /// <remarks>
    /// On every panel, because whichever cycle a person is looking at, a successor that came ready is work they can
    /// start now; nothing starts it for them. One still waiting is not news, and <c>sdlc successors</c> lists it.
    /// </remarks>
    private static bool Successors(StringBuilder output, ReadCycleStatus.Response status, SupportedHost? host)
    {
        if (status.Successors is not { } read)
        {
            return false;
        }

        bool wrote = false;

        foreach (Sdlc.Core.Successors.ReadSuccessors.Standing standing in read.Successors
            .Where(s => s.State is Sdlc.Core.Successors.SuccessorState.Ready or Sdlc.Core.Successors.SuccessorState.Stuck))
        {
            bool ready = standing.State == Sdlc.Core.Successors.SuccessorState.Ready;
            string slug = "successor:" + standing.Successor.Slug;

            Field(output, wrote ? string.Empty : "Successor", ready
                ? string.Create(CultureInfo.InvariantCulture, $"{slug + " is ready",-44}{Stage(host, "intent --from " + slug)}")
                : string.Create(CultureInfo.InvariantCulture, $"{slug + " is stuck",-44}sdlc successors"));
            wrote = true;
        }

        return wrote;
    }

    private static bool Amended(StringBuilder output, ReadCycleStatus.Response status)
    {
        if (status.AmendedOn is not { } reference)
        {
            return false;
        }

        Field(output, "Plan", string.Create(
            CultureInfo.InvariantCulture,
            $"amended on {reference} after this build started, so this verify cannot pass the chain."));
        Field(output, string.Empty, string.Create(
            CultureInfo.InvariantCulture,
            $"Merge {reference} in to continue by hand, or delete this branch for the factory to rebuild."));
        return true;
    }

    /// <summary>
    /// Writes what the schema found, and nothing at all when it found nothing.
    /// </summary>
    /// <remarks>
    /// Silence on a sound artifact is deliberate. A panel that reported every check that passed would
    /// bury the one that did not, and the panel's whole job is that the state is the first thing read.
    /// </remarks>
    private static string? ReleaseMoved(IReadOnlyList<string>? releases)
    {
        if (releases is not { Count: > 0 }
            || (releases.Count == 1 && string.Equals(releases[0], Release.Version, StringComparison.Ordinal)))
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"rounds written by {string.Join(", ", releases)}; this is {Release.Version}. The rules may have moved under this cycle.");
    }

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
    /// Writes where the interrogation loop stands, and nothing at all while it is converging.
    /// </summary>
    /// <remarks>
    /// Silent until a rule has two points, which is the same silence the Shape block keeps: a loop that
    /// is making progress has nothing to report, however many rounds it has taken. There is no round
    /// count here on purpose -- a long honest interrogation is not a problem, and a panel that said
    /// "round 9" in a warning tone would make it look like one.
    /// </remarks>
    private static void Loop(StringBuilder output, ReadCycleStatus.ArtifactState state)
    {
        if (Oscillation.Describe(state.Loop) is not { } line)
        {
            return;
        }

        output.AppendLine();
        Field(output, "Loop", line);

        // Every rule in play, not only the worst, because the exit a refiner is looking for is a way to
        // satisfy both at once and it cannot look for that without the pair.
        foreach (StuckRule rule in state.Loop.Warnings)
        {
            if (rule.Rule.Line() is { } wording)
            {
                Field(output, string.Empty, "    " + wording);
            }

            // What came back, so a person can tell one objection returning from several different ones
            // raised under one rule. The second is not a loop at all, and the line above cannot say so.
            if (rule.OneObjection is { } one)
            {
                Field(
                    output,
                    string.Empty,
                    one
                        ? "    the same finding each time:"
                        : "    more than one finding came back, so not one objection returning:");

                foreach (Returned back in rule.Returned!)
                {
                    Field(output, string.Empty, string.Create(CultureInfo.InvariantCulture, $"      r{back.Round} {back.Id}  {back.Headline}"));
                }
            }
        }
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

    private static void Checks(StringBuilder output, ReadCycleStatus.Response status, ReadCycleStatus.ArtifactState state)
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

        if (Describe(state.Turnover) is { } turnover)
        {
            Field(output, "Turnover", turnover);
        }

        // Grouped by the rule they cite, with the rule's words once above its findings: three blockers
        // under one rule printed that sentence three times, and the repetition was what a reader skimmed
        // past to reach the one line that differed. The rule still comes before the objection, because it
        // is what a reader needs in order to weigh it -- an id with no words beside it says a rule was
        // broken without saying which.
        foreach (IGrouping<string, Finding> group in state.OpenBlockers.GroupBy(finding => finding.Cites?.Id ?? string.Empty, StringComparer.Ordinal))
        {
            Citation? cites = group.First().Cites;

            Field(
                output,
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"{cites?.Id ?? "(no rule)"}  {Count(group.Count())}"));

            if (cites?.Line() is { } rule)
            {
                Field(output, string.Empty, "    " + rule);
            }

            // What set it off, where, then what would clear it. Where is the line a reader follows into the
            // document, and without it the gist named a problem nobody could find. The statement in full is
            // 'sdlc findings' business: at five hundred to eighteen hundred characters it buried the rest,
            // and somebody at the gate came away knowing only which rule.
            foreach (Finding finding in group)
            {
                string id = "  " + finding.Id.PadRight(Math.Max(7, finding.Id.Length + 2));
                string under = new(' ', id.Length);

                Hung(output, id, finding.Headline);

                if (finding.Place is { } place)
                {
                    Hung(output, under + "Where: ", place);
                }

                if (finding.Remedy is { } remedy)
                {
                    Hung(output, under + "Fix:   ", remedy.ReplaceLineEndings(" "));
                }
            }
        }

        if (state.OpenBlockers.Count > 0)
        {
            Field(output, string.Empty, "Why each fails: " + FindingsCommand(status, state));
        }
    }

    /// <summary>The command that prints this artifact's open blockers with their reasoning in full.</summary>
    private static string FindingsCommand(ReadCycleStatus.Response status, ReadCycleStatus.ArtifactState state) =>
        string.Create(CultureInfo.InvariantCulture, $"sdlc findings {status.Cycle.Id} --stage {Cycle.NameOf(state.Kind)} --blocking");

    /// <summary>
    /// The turnover line, or nothing before a second round.
    /// </summary>
    /// <remarks>
    /// Counts, in the same flat voice as the history beside it: a loop that raises something new each
    /// round is not failing, and the line says which kind of loop this is without calling it a problem.
    /// The person reading it decides whether that means another refine or a smaller cycle.
    /// </remarks>
    private static string? Describe(Turnover? turnover)
    {
        if (turnover is null)
        {
            return null;
        }

        string open = turnover.Raised + turnover.Carried + turnover.Returned == 0
            ? "nothing open"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{turnover.Raised} new, {turnover.Carried} carried over, {turnover.Returned} came back");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"r{turnover.Round}: {open}; {turnover.Settled} raised earlier and settled");
    }

    /// <summary>
    /// What to do once the gate is satisfied, which depends on how far the build has got.
    /// </summary>
    /// <remarks>
    /// Telling somebody to build a cycle that has already built and verified is the panel being confidently
    /// wrong about the one thing it exists to say. The verification's outcome distinguishes most of the
    /// states; two more need what the outcome cannot say. Before a verification the outcome is null both
    /// before and after the build, so whether evidence was recorded decides between building and
    /// verifying. And a pass says what the verifier concluded, not whether the chain still holds, so a
    /// pass whose chain fails points at <c>verify-chain</c> rather than at a pull request pr-ready refuses.
    /// </remarks>
    private static (string Command, string Why) AfterTheGate(ReadCycleStatus.Response status, string id, SupportedHost? host) =>
        status.Outcome switch
        {
            null when status.Built => (
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"verify {id}")),
                "the build recorded its evidence - in a new session"),

            null => (
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"build {id}")),
                "the gate is satisfied - in a new session"),

            // The reason stays short enough for the column; verify-chain names the check that failed.
            string pass when pass.Equals("pass", StringComparison.OrdinalIgnoreCase)
                && status.Chain is { Holds: false } => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc verify-chain {id}"),
                "verified, but the chain does not hold"),

            string pass when pass.Equals("pass", StringComparison.OrdinalIgnoreCase) => (
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"pr {id}")),
                "verified clean - in a new session"),

            string blocked when blocked.Equals("blocked", StringComparison.OrdinalIgnoreCase) => (
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"remediate {id}")),
                "a criterion is not met - in a new session"),

            string escalated when escalated.Equals("escalated", StringComparison.OrdinalIgnoreCase) => (
                string.Create(CultureInfo.InvariantCulture, $"sdlc status {id}"),
                "escalated - this one needs a person"),

            _ => (
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"verify {id}")),
                string.Create(CultureInfo.InvariantCulture, $"the outcome reads '{status.Outcome}'")),
        };

    private static bool IsOutcome(ReadCycleStatus.Response status, string outcome) =>
        string.Equals(status.Outcome, outcome, StringComparison.OrdinalIgnoreCase);

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

    private static string GateLine(ReadCycleStatus.Response status) =>
        status.Gate.Declared ? DeclaredLine(status) : ApprovedLine(status);

    private static string ApprovedLine(ReadCycleStatus.Response status) => status.Gate.Refusal switch
    {
        ApprovalGate.GateRefusal.None => "approved, and all three documents match what was approved",
        ApprovalGate.GateRefusal.NotApproved => "not approved yet",
        ApprovalGate.GateRefusal.Refused => "sent back - " + (status.Gate.Ledger?.Current?.Reason ?? "no reason recorded"),
        ApprovalGate.GateRefusal.HashMismatch => "approved, but a document has changed since",
        ApprovalGate.GateRefusal.OpenBlocker => "approved, but a blocking finding is still open",
        ApprovalGate.GateRefusal.StaleRound => "approved, but an interrogation judged a document that has changed since",
        ApprovalGate.GateRefusal.RewrittenRound => "approved, but a findings round it anchored has been changed since",
        ApprovalGate.GateRefusal.Abandoned => "abandoned, which is terminal",
        ApprovalGate.GateRefusal.MissingDocument => "cannot be approved until all three documents exist",
        ApprovalGate.GateRefusal.NoCycle => "no cycle directory yet",
        _ => "unknown",
    };

    /// <summary>The gate line when the solution declares no approval is required and none was placed (D-137).</summary>
    /// <remarks>
    /// Never "approved": nobody did, and a panel that said so would tell a person the plan had been read.
    /// </remarks>
    private static string DeclaredLine(ReadCycleStatus.Response status) => status.Gate.Refusal switch
    {
        ApprovalGate.GateRefusal.None => "no approval required (approval: none), and all three documents are what their clean rounds judged",
        ApprovalGate.GateRefusal.OpenBlocker => "no approval required, but a blocking finding is still open",
        ApprovalGate.GateRefusal.StaleRound => "no approval required, but a document has changed since its round judged it",
        ApprovalGate.GateRefusal.Uninterrogated => "no approval required, but a document has never been interrogated",
        ApprovalGate.GateRefusal.UnreadableRound => "no approval required, but a findings round cannot be read",
        _ => "unknown",
    };

    /// <summary>
    /// Writes the next command, literally, so it can be copied.
    /// </summary>
    /// <remarks>
    /// This is the detail that makes the flow feel smooth rather than merely informative: a panel that
    /// reports a state and leaves a person to work out the command has stopped one step short.
    /// </remarks>
    private static void Next(StringBuilder output, ReadCycleStatus.Response status, ReadCycleStatus.ArtifactState state, SupportedHost? host)
    {
        string id = status.Cycle.Id.ToString();
        string name = Cycle.NameOf(state.Kind);
        List<(string Command, string Why)> next = [];

        if (!state.Exists)
        {
            next.Add((Stage(host, string.Create(CultureInfo.InvariantCulture, $"{name} {id}")), "write it"));
        }
        else if (state.Rounds == 0)
        {
            next.Add((
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"interrogate {id} {name}")),
                "nothing has argued with it yet"));
        }
        else if (state.Kind == ArtifactKind.Verify && state.OpenBlockers.Any(Findings.ReaderAnswers.IsAboutTheChange))
        {
            // The diff reader's blockers are about the code, not the verdict, so no adjudication answers them
            // (D-139). Remediate changes the code, or steer declares the departure in the plan.
            next.Add((
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"remediate {id}")),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the change does what it was not given leave to: {string.Join(", ", state.OpenBlockers.Where(Findings.ReaderAnswers.IsAboutTheChange).Select(f => f.Id))} - in a new session")));
        }
        else if (state.Kind == ArtifactKind.Verify && state.OpenBlockers.Count > 0)
        {
            // Never refine. A verdict's blockers are answered by a fresh adjudication, and a criterion not
            // met by remediating the code; editing verify.md is the self-grading verify exists to prevent.
            // This branch fell through to the one below, so a blocked cycle was told to refine its verdict.
            next.Add(IsOutcome(status, "pass")
                ? (Stage(host, string.Create(CultureInfo.InvariantCulture, $"verify {id}")),
                   string.Create(
                       CultureInfo.InvariantCulture,
                       $"answer {string.Join(", ", state.OpenBlockers.Select(f => f.Id))} in a fresh adjudication - in a new session"))
                : AfterTheGate(status, id, host));
        }
        else if (state.OpenBlockers.Count > 0)
        {
            next.Add((
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"refine {id} {name}")),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"apply {string.Join(", ", state.OpenBlockers.Select(f => f.Id))}")));
            next.Add((
                Stage(host, string.Create(CultureInfo.InvariantCulture, $"interrogate {id} {name}")),
                "re-check after"));
        }
        else
        {
            ReadCycleStatus.ArtifactState? waiting = status.Blocking;

            if (waiting is not null && waiting.Kind != state.Kind)
            {
                next.Add((
                    Stage(host, string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(waiting.Kind)} {id}")),
                    string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(waiting.Kind)} is next, in a new session")));
            }
            else if (status.Gate.MayBuild)
            {
                next.Add(AfterTheGate(status, id, host));
            }
            else if (status.Gate.Declared)
            {
                // No approval to draft (D-137): the refusal is about the documents, and the gate names it.
                next.Add((
                    string.Create(CultureInfo.InvariantCulture, $"sdlc gate {id}"),
                    "no approval is required, and the gate still refuses - it says why"));
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

    /// <summary>
    /// A stage, named the way the host this is printed in invokes one.
    /// </summary>
    /// <remarks>
    /// <strong>Only the stages, never a command.</strong> <c>sdlc verify-chain</c>, <c>sdlc status</c> and
    /// <c>sdlc draft approval</c> are this binary's own and are typed as printed on every host. A stage is
    /// a skill, so on Claude Code the copyable form is <c>/sdlc:pr</c>: printing <c>sdlc pr</c> there sent
    /// a person to a refusal that names the invocation, one step for nothing at the end of every lap. Off
    /// a known host it stays <c>sdlc &lt;stage&gt;</c>, which that same refusal answers. D-100.
    /// </remarks>
    /// <param name="host">The host, or null when the binary cannot tell.</param>
    /// <param name="stageAndArguments">The stage's name and what follows it.</param>
    /// <returns>What to print.</returns>
    private static string Stage(SupportedHost? host, string stageAndArguments) =>
        host?.StageInvocation is { } form
            ? string.Format(CultureInfo.InvariantCulture, form, stageAndArguments)
            : "sdlc " + stageAndArguments;

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

    /// <summary>
    /// Writes a lead in the value column and a body beside it, every wrapped line starting under the body.
    /// </summary>
    /// <remarks>
    /// For a blocker's id and its gist, and for its place and its fix: a continuation that went back to
    /// the id's column read as the next finding, and one under "Where:" read as a second location.
    /// </remarks>
    private static void Hung(StringBuilder output, string lead, string body, string label = "")
    {
        string prefix = string.Create(CultureInfo.InvariantCulture, $"{Indent}{label,-10}");
        string hanging = Label + new string(' ', lead.Length);
        bool first = true;

        foreach (string piece in Wrap(body, Math.Max(20, Width - hanging.Length)))
        {
            output.AppendLine((first ? prefix + lead : hanging) + piece);
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
