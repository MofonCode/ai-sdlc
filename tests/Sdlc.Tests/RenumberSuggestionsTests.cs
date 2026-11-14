using FluentResults;
using Sdlc.Core.Findings;

namespace Sdlc.Tests;

/// <summary>
/// A suggestion id two branches each took, kept twice by a merge, is renumbered by the binary rather than by hand
/// in a file no agent may write (D-171).
/// </summary>
[TestClass]
public sealed class RenumberSuggestionsTests
{
    [TestMethod]
    public async Task The_first_keeps_its_id_the_later_takes_the_next_free_one_and_the_ledger_reads_again()
    {
        using var workspace = CycleWorkspace.Create();
        string ledger = workspace.Cycle.SuggestionsLedgerPath;
        CycleWorkspace.WriteRaw(ledger, Merged(workspace.Cycle.Id.ToString()));

        Result<SuggestionLedger> before = await FindingsReader.ReadSuggestions(workspace.Cycle, CancellationToken.None);
        StringAssert.Contains(before.Errors.Single().Message, $"'sdlc suggestions {workspace.Cycle.Id} --renumber'");

        RenumberSuggestions.Response moved = await Renumber(workspace);

        Assert.AreEqual(new RenumberedSuggestion("S_01", "S_03", "raised in plan round 2"), moved.Moved.Single());
        SuggestionLedger after = (await FindingsReader.ReadSuggestions(workspace.Cycle, CancellationToken.None)).Value;
        CollectionAssert.AreEqual(new[] { "S_01", "S_02", "S_03" }, after.Suggestions.Select(suggestion => suggestion.Id).ToArray());
        Assert.AreEqual("The die is unlabelled.", after.Find("S_01")!.Statement, "The first in the file keeps the id a person may already have read.");
        StringAssert.Contains(RenumberSuggestions.Render(moved), "S_01 -> S_03, raised in plan round 2");
    }

    [TestMethod]
    public async Task A_ledger_with_every_id_once_is_left_as_it_was()
    {
        using var workspace = CycleWorkspace.Create();
        string ledger = workspace.Cycle.SuggestionsLedgerPath;
        string once = Merged(workspace.Cycle.Id.ToString()).Replace("\"S_01\", \"cites\": null, \"statement\": \"The roll", "\"S_03\", \"cites\": null, \"statement\": \"The roll", StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(ledger, once);

        RenumberSuggestions.Response moved = await Renumber(workspace);

        Assert.AreEqual(0, moved.Moved.Count);
        Assert.AreEqual(once, await File.ReadAllTextAsync(ledger), "Nothing moved, so nothing is written.");
        StringAssert.Contains(RenumberSuggestions.Render(moved), "Nothing moved");
    }

    private static async Task<RenumberSuggestions.Response> Renumber(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);
        Result<RenumberSuggestions.Response> result = await gate.Send<RenumberSuggestions.Command, RenumberSuggestions.Response>(
            new RenumberSuggestions.Command(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    // Two branches each raised a new concern as S_01 past a ledger holding S_02's predecessor, and the merge kept both.
    private static string Merged(string cycle) => $$"""
        { "cycle": "{{cycle}}", "suggestions": [
          { "id": "S_01", "cites": null, "statement": "The die is unlabelled.", "history": [ { "artifact": "plan", "round": 1, "event": "raised" } ] },
          { "id": "S_02", "cites": null, "statement": "The page has no title.", "history": [ { "artifact": "plan", "round": 1, "event": "raised" } ] },
          { "id": "S_01", "cites": null, "statement": "The roll button has no focus ring.", "history": [ { "artifact": "plan", "round": 2, "event": "raised" } ] }
        ] }
        """;
}
