using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// What 'sdlc findings' reports about one cycle.
/// </summary>
/// <remarks>
/// The thing it must not get wrong is the disposition: a finding keeps its id across rounds, so an
/// objection answered in round two is answered even when round one called it open. A reader told
/// otherwise would go and re-answer something already dealt with.
/// </remarks>
[TestClass]
public sealed class FindingsCommandTests
{
    [TestMethod]
    public async Task A_blocker_answered_by_a_later_round_reads_as_answered()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);
        workspace.WriteAnsweredRound(ArtifactKind.Spec, round: 2);

        string rendered = FindingsRenderer.Render(await Read(workspace));

        StringAssert.Contains(rendered, "blocking answered");
        StringAssert.Contains(rendered, "blockers 0");
    }

    [TestMethod]
    public async Task Asking_for_the_round_that_raised_it_still_reports_where_it_got_to()
    {
        // The trap this command exists not to fall into. Filtering the rounds and then folding would
        // report an answered blocker as open, and send somebody to re-answer it.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);
        workspace.WriteAnsweredRound(ArtifactKind.Spec, round: 2);

        string rendered = FindingsRenderer.Render(
            await Read(workspace, ArtifactKind.Spec, round: 1));

        StringAssert.Contains(rendered, "F_01");
        StringAssert.Contains(
            rendered,
            "answered",
            "Round one is where the objection was raised. Where it got to is a fact about the history.");
    }

    [TestMethod]
    public async Task Open_blockers_come_before_open_suggestions_and_settled_findings_come_last()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteRound(
            ArtifactKind.Plan,
            round: 1,
            findings: """
              {
                "id": "F_01",
                "severity": "suggestion",
                "disposition": "answered",
                "statement": "settled already."
              },
              {
                "id": "F_02",
                "severity": "suggestion",
                "disposition": "open",
                "statement": "noticed and not acted on."
              },
              {
                "id": "F_03",
                "severity": "blocking",
                "disposition": "open",
                "cites": { "id": "CON_06", "heading": "a plan's proof is adequate to the criterion it claims to prove" },
                "statement": "stops the cycle.",
                "locus": "proof 5",
                "remedy": "say what makes the second value a face."
              }
              """);

        string rendered = FindingsRenderer.Render(await Read(workspace, ArtifactKind.Plan));

        int blocker = rendered.IndexOf("F_03", StringComparison.Ordinal);
        int suggestion = rendered.IndexOf("F_02", StringComparison.Ordinal);
        int settled = rendered.IndexOf("F_01", StringComparison.Ordinal);

        Assert.IsTrue(
            blocker < suggestion && suggestion < settled,
            $"What is owed has to come first:\n{rendered}");

        StringAssert.Contains(rendered, "remedy", "An open finding shows what would fix it.");
    }

    [TestMethod]
    public async Task A_cycle_nothing_has_argued_with_says_so_rather_than_reading_as_clean()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        string rendered = FindingsRenderer.Render(await Read(workspace));

        StringAssert.Contains(
            rendered,
            "unexamined",
            "An empty findings list and a clean one are different states, and a reader who cannot tell "
            + "them apart reads the first as the second.");
    }

    [TestMethod]
    public async Task The_brief_form_drops_the_statement_and_keeps_the_identity()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1);

        ReadFindings.Response read = await Read(workspace);

        string full = FindingsRenderer.Render(read);
        string brief = FindingsRenderer.Render(read, brief: true);

        StringAssert.Contains(full, "something that must be answered.");
        StringAssert.Contains(brief, "F_01");
        Assert.IsFalse(
            brief.Contains("something that must be answered.", StringComparison.Ordinal),
            "The brief form is for deciding what to open.");
    }

    [TestMethod]
    public async Task The_history_shows_the_latest_rounds_conflict()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteRound(ArtifactKind.Spec, round: 1, findings: string.Empty, conflicts: DeclaredConflict);

        string rendered = FindingsRenderer.Render(await Read(workspace, ArtifactKind.Spec));

        StringAssert.Contains(rendered, "conflict  CON_05 and CON_16");
        StringAssert.Contains(rendered, "a spec introduces no scope its intent did not ask for");
    }

    [TestMethod]
    public async Task A_conflict_a_later_round_did_not_re_declare_is_gone_from_the_history()
    {
        // Resolved by the next reader's silence, the same rule that moves a finding to answered. A
        // conflict has no id and no disposition, so accumulating them would report a resolved one for
        // the life of the cycle.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteRound(ArtifactKind.Spec, round: 1, findings: string.Empty, conflicts: DeclaredConflict);
        workspace.WriteCleanRound(ArtifactKind.Spec, round: 2);

        string rendered = FindingsRenderer.Render(await Read(workspace, ArtifactKind.Spec));

        Assert.IsFalse(
            rendered.Contains("conflict", StringComparison.Ordinal),
            "Round two said nothing about it, which is how a conflict is resolved.");
    }

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

    private static async Task<ReadFindings.Response> Read(
        CycleWorkspace workspace,
        ArtifactKind? artifact = null,
        int? round = null)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ReadFindings.Response> result = await gate
            .Send<ReadFindings.Query, ReadFindings.Response>(
                new ReadFindings.Query(workspace.Cycle.Id, artifact, round));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
