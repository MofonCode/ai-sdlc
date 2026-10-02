using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// The findings nobody had to act on, and the only cross-cycle reading that exists today.
/// </summary>
/// <remarks>
/// A non-blocking finding is non-blocking rather than trivial, so the failure to guard against is a list
/// nobody reads: one that over-counts by listing a finding once per round, or that includes findings
/// already dealt with.
/// </remarks>
[TestClass]
public sealed class SuggestionsTests
{
    [TestMethod]
    public async Task An_open_suggestion_is_listed_with_its_cycle_artifact_and_round()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_02", cites: "CON_12");

        IReadOnlyList<Suggestion> found = await Collect(workspace);

        Suggestion only = found.Single();
        Assert.AreEqual(workspace.Cycle.Id, only.Cycle);
        Assert.AreEqual(ArtifactKind.Spec, only.Artifact);
        Assert.AreEqual(1, only.Round);
        Assert.AreEqual("F_02", only.Finding.Id);
    }

    [TestMethod]
    public async Task A_blocking_finding_is_not_a_suggestion()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteBlockingRound(ArtifactKind.Spec);

        Assert.AreEqual(0, (await Collect(workspace)).Count);
    }

    [TestMethod]
    public async Task A_suggestion_carried_across_rounds_is_listed_once()
    {
        // A list that over-counts is a list people stop reading.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_02");
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 2, id: "F_02");
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 3, id: "F_02");

        Suggestion only = (await Collect(workspace)).Single();

        Assert.AreEqual(
            3,
            only.Round,
            "The latest round that mentions a finding says where it got to, so that is the round to show.");
    }

    [TestMethod]
    public async Task A_suggestion_answered_in_a_later_round_drops_off_the_list()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_02");
        workspace.WriteSuggestionRound(
            ArtifactKind.Spec,
            round: 2,
            id: "F_02",
            disposition: "answered");

        Assert.AreEqual(0, (await Collect(workspace)).Count);
    }

    [TestMethod]
    public async Task A_suggestion_that_cites_nothing_is_still_listed()
    {
        // The case the removed third severity existed for. A citation is required on a blocker and
        // optional on a suggestion, so an uncited one is ordinary rather than malformed.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_07", cites: null);

        Suggestion only = (await Collect(workspace)).Single();

        Assert.IsNull(only.Finding.Cites);
    }

    [TestMethod]
    public async Task Suggestions_are_collected_from_every_cycle_when_none_is_named()
    {
        using var first = CycleWorkspace.Create("20260929_1456_32");
        first.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_02");

        // A second cycle in the same solution, which is what the no-argument form is for.
        Directory.CreateDirectory(Path.Combine(
            first.Root,
            Cycle.SdlcDirectoryName,
            Cycle.CyclesDirectoryName,
            "20261001_0905_11"));

        Assert.IsTrue(CycleId.TryParse("20261001_0905_11", out CycleId? second));
        Cycle other = new(second.Value, first.Root);
        Directory.CreateDirectory(other.FindingsDirectory);
        CycleWorkspace.WriteRaw(
            other.FindingsPath(ArtifactKind.Plan, 1),
            """
            {
              "cycle": "20261001_0905_11",
              "artifact": "plan.md",
              "round": 1,
              "findings": [
                { "id": "F_01", "severity": "suggestion", "disposition": "open",
                  "statement": "the departure is reasonable but unexplained." }
              ]
            }
            """);

        IReadOnlyList<Suggestion> found = await Collect(first, all: true);

        Assert.AreEqual(2, found.Count, string.Join("; ", found.Select(s => s.Cycle + " " + s.Finding.Id)));
        Assert.AreEqual(2, found.Select(s => s.Cycle).Distinct().Count());
    }

    [TestMethod]
    public async Task A_solution_with_no_cycles_at_all_lists_nothing_rather_than_failing()
    {
        using var workspace = CycleWorkspace.Create();

        string rendered = SuggestionsRenderer.Render(new Suggestions.Response(await Collect(workspace, all: true)));

        StringAssert.Contains(rendered, "No open suggestions.");
    }

    [TestMethod]
    public async Task The_listing_offers_no_ranking_and_no_age()
    {
        // A suggestion's age says nothing about its worth, and a tool that sorted by age would quietly
        // argue that the oldest matters most.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_02");

        string rendered = SuggestionsRenderer.Render(new Suggestions.Response(await Collect(workspace)));

        foreach (string word in (string[])["oldest", "priority", "stale", "days", "overdue"])
        {
            Assert.IsFalse(
                rendered.Contains(word, StringComparison.OrdinalIgnoreCase),
                $"The listing must not rank or age suggestions ('{word}').");
        }
    }

    private static async Task<IReadOnlyList<Suggestion>> Collect(
        CycleWorkspace workspace,
        bool all = false)
    {
        using var gate = Gate.For(workspace.Root);

        Result<Suggestions.Response> result = await gate
            .Send<Suggestions.Query, Suggestions.Response>(
                new Suggestions.Query(all ? null : workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value.Suggestions;
    }
}
