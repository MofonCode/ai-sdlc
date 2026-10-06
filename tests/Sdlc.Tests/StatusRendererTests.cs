using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Tests;

/// <summary>
/// The stage panel, which is the plugin's most-read output.
/// </summary>
/// <remarks>
/// Asserted because a skill shows it verbatim, so its shape is a contract: if a host's rendering of it
/// drifts, the thing people actually read drifts with it.
/// </remarks>
[TestClass]
public sealed class StatusRendererTests
{
    [TestMethod]
    public async Task The_panel_names_the_stage_the_cycle_and_the_round()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "spec - 20260929_1456_32");
        StringAssert.Contains(panel, "round 1 - clean");
    }

    [TestMethod]
    public async Task The_panel_says_what_the_document_is_about_from_its_own_heading()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        await File.WriteAllTextAsync(
            workspace.Cycle.PathOf(ArtifactKind.Spec),
            "---\nid: x\n---\n\n# Asking for the page returns one roll\n\n## Acceptance criteria\n\n## Out of scope\n");
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "Asking for the page returns one roll");
        StringAssert.Contains(panel, "2 sections: Acceptance criteria, Out of scope");
    }

    [TestMethod]
    public async Task The_panel_shows_what_each_round_found_rather_than_only_where_it_ended()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 2, id: "F_02");
        workspace.WriteAnsweredRound(ArtifactKind.Spec, round: 3);

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(
            panel,
            "r1:",
            "The history is the record of what was challenged; a reader who sees only the last round "
            + "learns nothing about what their artifact got wrong.");
        StringAssert.Contains(panel, "r2:");
        StringAssert.Contains(panel, "r3:");
    }

    [TestMethod]
    public async Task The_panel_attaches_no_recommendation_about_whether_to_keep_interrogating()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 1);
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 2);
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 3);

        string panel = await Render(workspace, ArtifactKind.Spec);

        foreach (string word in (string[])["diminishing", "converged", "enough", "stop now"])
        {
            Assert.IsFalse(
                panel.Contains(word, StringComparison.OrdinalIgnoreCase),
                $"The panel must not suggest stopping ('{word}'). That judgement has been wrong before, "
                + "and the owner decides.");
        }
    }

    [TestMethod]
    public async Task An_open_blocker_is_shown_with_its_constraint_and_its_statement()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, id: "F_04", cites: "CON_04.1");

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "F_04");
        StringAssert.Contains(panel, "CON_04.1");
        StringAssert.Contains(panel, "something that must be answered.");
    }

    [TestMethod]
    public async Task An_artifact_checks_are_called_checks_and_never_verification()
    {
        // `verify` is a phase name here with a specific meaning. Using the word for an artifact would
        // make two different things sound like one.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "Checks");
        Assert.IsFalse(panel.Contains("verif", StringComparison.OrdinalIgnoreCase), panel);
    }

    [TestMethod]
    public async Task An_uninterrogated_artifact_is_told_to_interrogate_rather_than_to_approve()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        string panel = await Render(workspace, ArtifactKind.Intent);

        StringAssert.Contains(panel, "not interrogated");
        StringAssert.Contains(panel, "sdlc interrogate 20260929_1456_32 intent");
    }

    [TestMethod]
    public async Task An_artifact_with_an_open_blocker_is_told_to_refine_then_re_check()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Plan, id: "F_09");

        string panel = await Render(workspace, ArtifactKind.Plan);

        StringAssert.Contains(panel, "sdlc refine 20260929_1456_32 plan");
        StringAssert.Contains(panel, "apply F_09");
        StringAssert.Contains(panel, "sdlc interrogate 20260929_1456_32 plan");
    }

    [TestMethod]
    public async Task A_cycle_whose_three_documents_are_clear_is_told_to_approve()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        string panel = await Render(workspace, ArtifactKind.Plan);

        StringAssert.Contains(panel, "sdlc draft approval 20260929_1456_32");
        StringAssert.Contains(panel, "not approved yet");
    }

    [TestMethod]
    public async Task An_approved_cycle_is_told_to_build()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();

        string panel = await Render(workspace, ArtifactKind.Plan);

        StringAssert.Contains(panel, "sdlc build 20260929_1456_32");
        StringAssert.Contains(panel, "all three documents match");
    }

    [TestMethod]
    public async Task A_clear_artifact_points_at_whichever_one_the_cycle_is_still_waiting_on()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);

        string panel = await Render(workspace, ArtifactKind.Intent);

        StringAssert.Contains(panel, "sdlc spec 20260929_1456_32");
        StringAssert.Contains(panel, "spec is next");
    }

    [TestMethod]
    public async Task No_line_exceeds_the_width_a_narrow_console_would_wrap_at()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteRound(
            ArtifactKind.Spec,
            1,
            """
            {
              "id": "F_01",
              "severity": "blocking",
              "disposition": "open",
              "cites": { "id": "CON_04", "heading": "a spec covers every outcome its intent asks for, and all of each one, which is itself a line long enough to need wrapping" },
              "statement": "a statement long enough to need wrapping, repeated so that it certainly does: a statement long enough to need wrapping, repeated so that it certainly does."
            }
            """);

        string panel = await Render(workspace, ArtifactKind.Spec);

        foreach (string candidate in panel.Split(Environment.NewLine))
        {
            Assert.IsTrue(candidate.Length <= 92, $"A {candidate.Length}-character line: '{candidate}'");
        }
    }

    [TestMethod]
    public async Task The_panel_is_ascii_so_it_renders_in_a_pipeline_log_and_a_plain_console()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string panel = await Render(workspace, ArtifactKind.Spec);

        // The artifact's own heading is passed through verbatim, so only the frame is asserted here -
        // and the fixture writes an ASCII heading.
        Assert.IsTrue(
            panel.All(char.IsAscii),
            "The frame the panel draws must be ASCII: it renders where UTF-8 is not a given.");
    }

    [TestMethod]
    [DataRow(null, "sdlc build", "the gate is satisfied")]
    [DataRow("pass", "sdlc land", "verified clean")]
    [DataRow("blocked", "sdlc remediate", "a criterion is not met")]
    [DataRow("escalated", "sdlc status", "needs a person")]
    public async Task An_approved_cycle_is_pointed_at_the_step_its_verification_calls_for(
        string? outcome,
        string command,
        string why)
    {
        // Telling somebody to build a cycle that has already built and verified is the panel being
        // confidently wrong about the one thing it exists to say.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();

        if (outcome is not null)
        {
            workspace.WriteVerify(outcome);
        }

        string panel = await Render(workspace, ArtifactKind.Plan);

        StringAssert.Contains(panel, command, panel);
        StringAssert.Contains(panel, why, panel);
    }

    [TestMethod]
    public async Task A_verify_document_with_an_outcome_nobody_recognises_points_back_at_verify()
    {
        // Rather than guessing. An outcome the panel cannot read is a verification to look at, and saying
        // "land it" on one would be the worst possible guess.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            workspace.WriteCleanRound(kind);
        }

        workspace.WriteApproval();
        workspace.WriteVerify("probably fine");

        string panel = await Render(workspace, ArtifactKind.Plan);

        StringAssert.Contains(panel, "sdlc verify");
        StringAssert.Contains(panel, "probably fine");
    }

    [TestMethod]
    public async Task A_shape_problem_is_shown_above_the_findings()
    {
        // A shape problem is usually why a round found what it found: an interrogation reading an
        // artifact with no Proof section raises something about the proof, and the author chases the
        // finding rather than the missing heading.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);

        // The fixture is schema-valid, so the problem has to be introduced on purpose.
        CycleWorkspace.WriteRaw(
            workspace.Cycle.PathOf(ArtifactKind.Intent),
            await File.ReadAllTextAsync(workspace.Cycle.PathOf(ArtifactKind.Intent))
                + Environment.NewLine
                + "## Notes"
                + Environment.NewLine);

        string panel = await Render(workspace, ArtifactKind.Intent, shape: true);

        StringAssert.Contains(panel, "Shape");
        Assert.IsTrue(
            panel.IndexOf("Shape", StringComparison.Ordinal) < panel.IndexOf("Checks", StringComparison.Ordinal),
            "The shape has to come before the checks, or a reader acts on the finding first.");
    }

    [TestMethod]
    public async Task A_sound_artifact_says_nothing_about_its_shape()
    {
        // Reporting every check that passed would bury the one that did not, and the panel's whole job
        // is that the state is the first thing read.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);

        ArtifactValidation sound = new(ArtifactKind.Intent, []);
        string panel = await Render(workspace, ArtifactKind.Intent, shapeResult: sound);

        Assert.IsFalse(panel.Contains("Shape", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task A_wrapped_line_keeps_the_indent_its_first_line_had()
    {
        // Without it, the continuation of an indented list item starts back at the label column and
        // reads as a new item, which is worse than the overflow it was meant to fix.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);

        ArtifactValidation long_problem = new(
            ArtifactKind.Intent,
            [new SchemaViolation(
                "ticket",
                "is not a key an intent carries, and a field nothing declared is a field nothing reads, "
                + "which is the whole reason an undeclared key is a violation rather than an extra.")]);

        string panel = await Render(workspace, ArtifactKind.Intent, shapeResult: long_problem);

        string[] wrapped = [.. panel
            .ReplaceLineEndings()
            .Split(Environment.NewLine)
            .SkipWhile(line => !line.Contains("ticket", StringComparison.Ordinal))
            .Take(2)
            .Select(line => line.TrimEnd())];

        Assert.AreEqual(2, wrapped.Length, "The problem is long enough to wrap: " + panel);

        int first = wrapped[0].Length - wrapped[0].TrimStart().Length;
        int second = wrapped[1].Length - wrapped[1].TrimStart().Length;

        Assert.AreEqual(
            first,
            second,
            "The continuation lost the item's indent. " + panel);
    }

    [TestMethod]
    public async Task A_cycle_that_does_not_exist_gets_no_panel_at_all()
    {
        // A mistyped id used to render 'intent - not interrogated - gate refused', which is exactly what
        // a cycle that does exist and has had nothing written yet renders. Two different situations
        // reading identically, and the one the person is actually in is the one not named.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        Assert.IsTrue(CycleId.TryParse("20260101_0000_01", out CycleId? parsed));
        CycleId absent = parsed!.Value;

        using var gate = Gate.For(workspace.Root);

        Result<ReadCycleStatus.Response> read = await gate
            .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(absent));

        Assert.IsTrue(read.IsFailed, "There is no such cycle, so there is no state to report.");
        StringAssert.Contains(read.Errors[0].Message, "has no directory");
        StringAssert.Contains(
            read.Errors[0].Message,
            "sdlc cycles",
            "The remedy for a wrong id is the command that lists the right ones.");
    }

    [TestMethod]
    public async Task A_cycle_with_a_directory_and_nothing_in_it_still_gets_a_panel()
    {
        // The other half of the rule above. 'sdlc new' creates the directory before any artifact exists,
        // so refusing on an empty cycle would refuse every cycle at the moment it is opened.
        using var workspace = CycleWorkspace.Create();

        using var gate = Gate.For(workspace.Root);

        Result<ReadCycleStatus.Response> read = await gate
            .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(
                new ReadCycleStatus.Query(workspace.Cycle.Id));

        Assert.IsTrue(
            read.IsSuccess,
            "A cycle that has just been opened has a directory and no artifacts, and that is the state "
            + "the panel exists to report. " + string.Join("; ", read.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task The_brief_panel_counts_the_blockers_and_the_suggestions_on_one_line()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Intent);
        workspace.WriteRound(
            ArtifactKind.Spec,
            round: 1,
            findings: """
              {
                "id": "F_01",
                "severity": "blocking",
                "disposition": "open",
                "cites": { "id": "CON_06", "heading": "a plan's proof is adequate to the criterion it claims to prove" },
                "statement": "a check that can pass while the criterion is unmet.",
                "locus": "criterion 5",
                "remedy": "say what makes the second value a face."
              },
              {
                "id": "F_02",
                "severity": "suggestion",
                "disposition": "open",
                "statement": "worth doing and nothing forbids it."
              },
              {
                "id": "F_03",
                "severity": "suggestion",
                "disposition": "open",
                "statement": "also worth doing."
              }
              """);

        string panel = await RenderBrief(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "20260929_1456_32");
        StringAssert.Contains(panel, "spec");
        StringAssert.Contains(panel, "blockers 1");
        StringAssert.Contains(
            panel,
            "suggestions 2",
            "The count a person asked for after every stage. A suggestion is not a blocker and is still "
            + "work somebody noticed.");
    }

    [TestMethod]
    public async Task The_brief_panel_ends_with_the_same_next_command_as_the_full_one()
    {
        // A short form that worked the next step out for itself would be a second opinion on the one
        // line people copy.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string full = await Render(workspace, ArtifactKind.Spec);
        string brief = await RenderBrief(workspace, ArtifactKind.Spec);

        string command = full
            .Split('\n')
            .First(candidate => candidate.Contains("Next", StringComparison.Ordinal))
            .Replace("Next", string.Empty, StringComparison.Ordinal)
            .Trim();

        StringAssert.Contains(brief, command);
    }

    [TestMethod]
    public async Task The_brief_panel_stays_within_a_handful_of_lines()
    {
        // The whole point of it. A short form that grew would be the full panel with less in it.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string brief = await RenderBrief(workspace, ArtifactKind.Spec);

        int lines = brief.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Length;

        Assert.IsTrue(lines <= 8, $"The brief panel grew to {lines} lines:\n{brief}");
    }

    [TestMethod]
    public async Task The_panel_shows_a_conflict_apart_from_the_blockers()
    {
        // A blocker is answered by editing this artifact; a conflict cannot be. An agent that read one
        // as the other would edit the document in a circle, which is the loop this exists to end -- so
        // they get separate headings rather than one list a reader has to sort.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteRound(ArtifactKind.Spec, round: 1, findings: string.Empty, conflicts: DeclaredConflict);

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "Conflicts", "Its own heading.");
        StringAssert.Contains(panel, "CON_05 and CON_16", "Naming both rules.");
        StringAssert.Contains(
            panel,
            "a spec introduces no scope its intent did not ask for",
            "With each rule's own line, because two numbers cannot be weighed against each other.");
        StringAssert.Contains(panel, "narrowing leaves the outcome half covered", "And the reasoning.");

        foreach (string line in panel.Split(Environment.NewLine))
        {
            Assert.IsTrue(line.Length <= 92, $"A {line.Length}-character line: \'{line}\'");
        }
    }

    [TestMethod]
    public async Task The_panel_says_nothing_about_conflicts_when_there_are_none()
    {
        // The same silence the Shape block keeps. A panel that reported every check that passed would
        // bury the one that did not.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteCleanRound(ArtifactKind.Spec);

        string panel = await Render(workspace, ArtifactKind.Spec);

        Assert.IsFalse(panel.Contains("Conflicts", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task The_panel_warns_when_a_rule_has_come_back_twice()
    {
        // Two relapses on one rule. The warning is what changes what the refiner is told, so it has to
        // reach the panel the refiner reads -- and it names the rule it is caught against, because an
        // exit that satisfies both is what it should be looking for.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        Oscillating(workspace);

        string panel = await Render(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "Loop");
        StringAssert.Contains(panel, "has come back 2 times");
        StringAssert.Contains(panel, "against CON_16", "What the rule is trading with.");
        StringAssert.Contains(panel, "Look for an exit");

        foreach (string line in panel.Split(Environment.NewLine))
        {
            Assert.IsTrue(line.Length <= 92, $"A {line.Length}-character line: \'{line}\'");
        }
    }

    [TestMethod]
    public async Task The_brief_panel_carries_the_loop_line_too()
    {
        // The stage skills print the brief panel at the end of a lap, so a halt the short form omitted
        // would be a halt the loop never saw.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        Oscillating(workspace);

        string panel = await RenderBrief(workspace, ArtifactKind.Spec);

        StringAssert.Contains(panel, "Loop");
        StringAssert.Contains(panel, "CON_05");
    }

    [TestMethod]
    public async Task The_panel_says_nothing_about_the_loop_while_it_is_converging()
    {
        // No round count in a warning tone. A long honest interrogation is not a problem, and a panel
        // that mentioned the loop on every round would make slowness look like failure.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 2, id: "F_02");
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 3, id: "F_03");

        string panel = await Render(workspace, ArtifactKind.Spec);

        Assert.IsFalse(
            panel.Contains("Loop", StringComparison.Ordinal),
            "Three rounds with nothing coming back is a loop that is working.");
    }

    /// <summary>
    /// Writes a spec history in which CON_05 is answered and comes back twice, trading with CON_16.
    /// </summary>
    /// <remarks>
    /// The shape of a refiner finding a boundary: narrow, which breaks the other rule, broaden, which
    /// breaks the first one again. Two relapses, so it warns and does not halt.
    /// </remarks>
    /// <param name="workspace">The workspace to write into.</param>
    private static void Oscillating(CycleWorkspace workspace)
    {
        workspace.WriteRound(ArtifactKind.Spec, 1, Cited("F_01", "CON_05", "open"));
        workspace.WriteRound(
            ArtifactKind.Spec,
            2,
            Cited("F_01", "CON_05", "answered") + "," + Cited("F_02", "CON_16", "open"));
        workspace.WriteRound(ArtifactKind.Spec, 3, Cited("F_03", "CON_05", "open"));
        workspace.WriteRound(
            ArtifactKind.Spec,
            4,
            Cited("F_03", "CON_05", "answered") + "," + Cited("F_04", "CON_16", "open"));
        workspace.WriteRound(ArtifactKind.Spec, 5, Cited("F_05", "CON_05", "open"));
    }

    private static string Cited(string id, string cites, string disposition) => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $$"""
          {
            "id": "{{id}}",
            "severity": "blocking",
            "disposition": "{{disposition}}",
            "cites": { "id": "{{cites}}", "heading": "the rule {{cites}} states, in one line" },
            "statement": "something that must be answered."
          }
          """);

    /// <summary>
    /// One declared conflict, as a reader writes it: two rules, each with its own line, and why each
    /// remedy excludes the other.
    /// </summary>
    private const string DeclaredConflict = """
        {
          "between": [
            { "id": "CON_05", "heading": "a spec introduces no scope its intent did not ask for" },
            { "id": "CON_16", "heading": "a spec covers every outcome its intent asks for, and all of each one" }
          ],
          "statement": "covering the visual half needs criteria the intent does not support; narrowing leaves the outcome half covered.",
          "locus": "## Acceptance criteria"
        }
        """;

    private static Task<string> RenderBrief(CycleWorkspace workspace, ArtifactKind kind) =>
        Render(workspace, kind, brief: true);

    private static async Task<string> Render(
        CycleWorkspace workspace,
        ArtifactKind kind,
        bool shape = false,
        ArtifactValidation? shapeResult = null,
        bool brief = false)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ReadCycleStatus.Response> read = await gate
            .Send<ReadCycleStatus.Query, ReadCycleStatus.Response>(new ReadCycleStatus.Query(workspace.Cycle.Id));

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));

        ReadCycleStatus.ArtifactState state = read.Value.Artifacts.First(artifact => artifact.Kind == kind);
        ArtifactOutline outline = await ArtifactOutlineReader.ReadFile(state.Path, CancellationToken.None);

        ArtifactValidation? checked_shape = shapeResult ?? (shape
            ? ArtifactSchema.Validate(
                kind,
                await File.ReadAllTextAsync(state.Path, CancellationToken.None),
                workspace.Cycle.Id)
            : null);

        StagePanel panel = new(state, outline, checked_shape);

        return brief
            ? StatusRenderer.RenderBrief(read.Value, panel)
            : StatusRenderer.RenderStage(read.Value, panel);
    }
}
