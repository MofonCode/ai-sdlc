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
              "cites": "CON_04",
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

    private static async Task<string> Render(CycleWorkspace workspace, ArtifactKind kind)
    {
        Result<CycleStatus> read = await new CycleStatusHandler(workspace.Root)
            .Handle(new CycleStatusQuery(workspace.Cycle.Id), CancellationToken.None);

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));

        ArtifactState state = read.Value.Artifacts.First(artifact => artifact.Kind == kind);
        ArtifactOutline outline = await ArtifactOutlineReader.ReadFile(state.Path, CancellationToken.None);

        return StatusRenderer.RenderStage(read.Value, new StagePanel(state, outline));
    }
}
