using System.Globalization;
using System.Text;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Findings;

namespace Sdlc.Core.Cycles;

/// <summary>One artifact's state plus what its document says about itself.</summary>
/// <param name="State">Where the artifact has got to.</param>
/// <param name="Outline">Its headline and sections, read from the document.</param>
public sealed record StagePanel(ArtifactState State, ArtifactOutline Outline);

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
    /// Renders the panel for one stage.
    /// </summary>
    /// <param name="status">The cycle's state.</param>
    /// <param name="panel">The artifact the stage just finished, with its outline.</param>
    /// <returns>The rendered panel, ending in a newline.</returns>
    public static string RenderStage(CycleStatus status, StagePanel panel)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(panel);

        StringBuilder output = new();
        ArtifactState state = panel.State;

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

        Checks(output, state);
        output.AppendLine();

        Field(output, "Gate", GateLine(status));
        output.AppendLine();

        Next(output, status, state);

        return output.ToString();
    }

    private static void Checks(StringBuilder output, ArtifactState state)
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

        // The id and the constraint on one line, the statement under it: a statement is a sentence and
        // wrapping it beside two short columns would collapse their alignment.
        foreach (Finding finding in state.OpenBlockers)
        {
            Field(
                output,
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"{finding.Id,-7}{finding.Cites,-12}blocking"));

            Field(output, string.Empty, "    " + finding.Statement);
        }
    }

    private static string GateLine(CycleStatus status) => status.Gate.Refusal switch
    {
        GateRefusal.None => "approved, and all three documents match what was approved",
        GateRefusal.NotApproved => "not approved yet",
        GateRefusal.Refused => "sent back - " + (status.Gate.Ledger?.Current?.Reason ?? "no reason recorded"),
        GateRefusal.HashMismatch => "approved, but a document has changed since",
        GateRefusal.UnacceptedBlocker => "approved, with a blocking finding open and unaccepted",
        GateRefusal.Abandoned => "abandoned, which is terminal",
        GateRefusal.MissingDocument => "cannot be approved until all three documents exist",
        GateRefusal.NoCycle => "no cycle directory yet",
        _ => "unknown",
    };

    /// <summary>
    /// Writes the next command, literally, so it can be copied.
    /// </summary>
    /// <remarks>
    /// This is the detail that makes the flow feel smooth rather than merely informative: a panel that
    /// reports a state and leaves a person to work out the command has stopped one step short.
    /// </remarks>
    private static void Next(StringBuilder output, CycleStatus status, ArtifactState state)
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
            ArtifactState? waiting = status.Blocking;

            if (waiting is not null && waiting.Kind != state.Kind)
            {
                next.Add((
                    string.Create(CultureInfo.InvariantCulture, $"sdlc {Cycle.NameOf(waiting.Kind)} {id}"),
                    string.Create(CultureInfo.InvariantCulture, $"{Cycle.NameOf(waiting.Kind)} is next")));
            }
            else if (status.Gate.MayBuild)
            {
                next.Add((
                    string.Create(CultureInfo.InvariantCulture, $"sdlc build {id}"),
                    "the gate is satisfied"));
            }
            else
            {
                next.Add((
                    string.Create(CultureInfo.InvariantCulture, $"sdlc cycle-approval {id}"),
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

    /// <summary>
    /// Writes one labelled field, wrapping only when it has to.
    /// </summary>
    /// <remarks>
    /// Wrapping splits on spaces and rejoins with one, which collapses deliberate column padding - so a
    /// line that already fits is written through untouched. The first version wrapped everything and
    /// turned aligned columns into prose.
    /// </remarks>
    private static void Field(StringBuilder output, string label, string body)
    {
        string prefix = string.Create(CultureInfo.InvariantCulture, $"{Indent}{label,-10}");

        if (prefix.Length + body.Length <= Width)
        {
            output.AppendLine(prefix + body);
            return;
        }

        bool first = true;
        foreach (string piece in Wrap(body, Width - Label.Length))
        {
            output.AppendLine(first ? prefix + piece : Label + piece);
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
