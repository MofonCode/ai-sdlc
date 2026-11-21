using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// The framework's share of a harvest (D-204): the notes a sitting writes under <c>## For the framework</c>, kept apart
/// from the solution's records, and what of them and of the closed cycles may leave the solution.
/// </summary>
/// <remarks>
/// The failures guarded against are a note lost with the gitignored report it was written in, a release-less note
/// nobody can tell from one already fixed, and an export carrying the solution's own evidence, files or rules.
/// </remarks>
[TestClass]
public sealed class FrameworkHarvestTests
{
    private const string Integration = "origin/development";

    private const string Note = """
        ## For the framework

        ### perspective:security — asks for vulnerable-package evidence every round though the build enforces the audit
        Kind: cost
        Measure: 6 cycles, 14 findings, longest 26 rounds
        > 20261021_0118_04 plan group 58, security.F_08, 26 rounds

        """;

    [TestMethod]
    public async Task A_sitting_keeps_its_notes_for_the_framework_with_the_release_its_cycles_ran_on()
    {
        using CycleWorkspace workspace = Completed();
        workspace.WriteRound(ArtifactKind.Intent, 1, findings: string.Empty, release: "0.39.2");

        Result<RecordHarvest.Response> recorded = await Record(workspace, HarvestAspect.Constraints, await Proposals(workspace, Note));

        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, recorded.Value.FrameworkNotes);
        StringAssert.Contains(HarvestRenderer.RenderRecorded(recorded.Value), "1 note for the framework kept in .sdlc/framework-harvest.json");

        FrameworkNote note = (await FrameworkHarvest.Read(workspace.Root, CancellationToken.None)).Value.Notes.Single();
        Assert.AreEqual("perspective:security", note.Surface);
        Assert.AreEqual(FrameworkNoteKind.Cost, note.Kind);
        StringAssert.StartsWith(note.Statement, "asks for vulnerable-package evidence");
        Assert.AreEqual("6 cycles, 14 findings, longest 26 rounds", note.Measure);
        CollectionAssert.AreEqual(new[] { workspace.Cycle.Id.ToString() }, note.Cycles.ToArray(), "A note naming no cycles was seen in the sitting's.");
        CollectionAssert.AreEqual(new[] { "0.39.2" }, note.Releases.ToArray(), "The release its cycle's rounds recorded.");
        CollectionAssert.AreEqual(new[] { "20261021_0118_04 plan group 58, security.F_08, 26 rounds" }, note.Evidence.ToArray());
    }

    [TestMethod]
    [DataRow("### the security perspective — costs too much\nKind: cost\n", "names no surface of the framework")]
    [DataRow("### stage:verify — says Not met for unproven\n", "needs a 'Kind:' line")]
    [DataRow("### stage:verify — says Not met for unproven\nKind: annoying\n", "needs a 'Kind:' line")]
    [DataRow("### stage:verify\nKind: defect\n", "says nothing after its surface")]
    public async Task A_note_that_does_not_read_refuses_the_whole_sitting(string entry, string refusal)
    {
        using CycleWorkspace workspace = Completed();

        Result<RecordHarvest.Response> refused = await Record(workspace, HarvestAspect.Decisions, await Proposals(workspace, "## For the framework\n\n" + entry));

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, refusal);
        Assert.IsFalse(File.Exists(HarvestLedger.PathIn(workspace.Root)), "Nothing is recorded when a note is refused.");
        Assert.IsFalse(File.Exists(FrameworkHarvest.PathIn(workspace.Root)));
    }

    [TestMethod]
    [DataRow("CON_06", true)]
    [DataRow("PRO_02.1", true)]
    [DataRow("evidence", true)]
    [DataRow("command:evidence", true)]
    [DataRow("skill:harvest-constraints", true)]
    [DataRow("LOC_01", false)]
    [DataRow("the verify stage", false)]
    [DataRow("perspective:", false)]
    public void A_surface_is_a_framework_rule_or_a_kind_and_a_name(string surface, bool valid) =>
        Assert.AreEqual(valid, FrameworkHarvest.IsSurface(surface));

    [TestMethod]
    public async Task A_sitting_with_nothing_for_the_framework_writes_no_file()
    {
        using CycleWorkspace workspace = Completed();

        Assert.IsTrue((await Record(workspace, HarvestAspect.Stack, await Proposals(workspace, string.Empty))).IsSuccess);
        Assert.IsFalse(File.Exists(FrameworkHarvest.PathIn(workspace.Root)));
    }

    [TestMethod]
    public async Task Close_keeps_each_cycles_release_what_each_reader_raised_and_what_it_cost()
    {
        using CycleWorkspace workspace = Completed();
        workspace.WriteRound(
            ArtifactKind.Plan,
            1,
            findings: """
                { "id": "security.F_01", "severity": "blocking", "disposition": "open", "cites": { "id": "CON_06", "heading": "h" }, "statement": "a.", "perspective": "security" },
                { "id": "security.F_02", "severity": "suggestion", "disposition": "withdrawn", "statement": "b.", "perspective": "security" },
                { "id": "F_03", "severity": "suggestion", "disposition": "open", "cites": { "id": "CON_06", "heading": "h" }, "statement": "c." }
                """,
            release: "0.51.0");
        FakeRepository git = Merged(workspace);

        foreach (HarvestAspect aspect in HarvestAspects.All)
        {
            Result<RecordHarvest.Response> recorded = await Record(workspace, aspect, await Proposals(workspace, string.Empty), git);
            Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(error => error.Message)));
        }

        HarvestedCycle husk = (await Close(workspace, git)).Value.Closed.Single();

        CollectionAssert.AreEqual(new[] { "0.51.0" }, husk.Releases!.ToArray());
        ReaderYield security = husk.Readers!.Single(reader => reader.Reader == "security");
        Assert.AreEqual(new ReaderYield("security", "plan", 2, 1, 1, 1), security);
        Assert.AreEqual(new ReaderYield("rules", "plan", 1, 0, 1, 0), husk.Readers!.Single(reader => reader.Reader == "rules"));
        CollectionAssert.Contains(git.Commits.Single().ToArray(), ".sdlc/" + HarvestLedger.FileName);

        HarvestedCycle read = HarvestLedger.Parse((await HarvestLedger.Read(workspace.Root, CancellationToken.None)).Value.Write(), "x").Value.Cycles.Single();
        Assert.AreEqual(2, read.Readers!.Count, "The new fields read back.");
    }

    [TestMethod]
    public void A_ledger_written_before_the_new_fields_still_reads()
    {
        const string before = """
            { "cycles": [ { "cycle": "20261001_0900_01", "merge": "abc", "closed": "2026-10-01T00:00:00Z", "abandoned": false,
              "declared": [], "changed": [], "cited": [], "areas": [], "rounds": { "plan": 2 }, "reopens": 0, "verdicts": ["pass"] } ],
              "watch": [], "sittings": [] }
            """;

        HarvestedCycle cycle = HarvestLedger.Parse(before, "x").Value.Cycles.Single();

        Assert.IsNull(cycle.Releases);
        Assert.IsNull(cycle.Readers);
        Assert.IsNull(cycle.Spent);
    }

    [TestMethod]
    public void The_export_leaves_the_evidence_the_cycle_ids_the_files_and_the_solutions_own_rules_behind()
    {
        FrameworkHarvest notes = FrameworkHarvest.Empty.Adding([
            new FrameworkNote(DateTimeOffset.UnixEpoch, HarvestAspect.Constraints, "stage:verify", FrameworkNoteKind.Missing, "has no word for unproven",
                "1 cycle", ["20261021_0118_04"], ["0.39.2"], ["20261021_0118_04 rules F_05: the credential criteria"]),
        ]);
        HarvestLedger ledger = HarvestLedger.Empty.Closing([
            new HarvestedCycle("20261021_0118_04", "abc123", DateTimeOffset.UnixEpoch, false, ["src/Secret/Thing.cs"], ["src/Secret/Thing.cs"],
                ["CON_06", "LOC_02", "PRO_02"], [new HarvestedArea("charts", ["src/Charts/**"])], new Dictionary<string, int> { ["plan"] = 26 }, 1, ["blocked", "pass"],
                ["0.39.2"], [new ReaderYield("security", "plan", 40, 3, 2, 1)], new Dictionary<string, double> { ["plan"] = 1_800_000 }),
        ]);

        FrameworkExport export = ExportFramework.Of(notes, ledger, "0.53.0");
        string json = export.Write();

        StringAssert.Contains(json, "has no word for unproven");
        StringAssert.Contains(json, "\"release\": \"0.53.0\"");
        CollectionAssert.AreEqual(new[] { "CON_06", "PRO_02" }, export.Cycles.Single().Cited.ToArray(), "A rule of the solution's own is its business.");

        foreach (string kept in (string[])["20261021_0118_04", "credential", "abc123", "src/Secret", "charts", "LOC_02"])
        {
            Assert.IsFalse(json.Contains(kept, StringComparison.Ordinal), "The export carries " + kept + ".");
        }

        FrameworkExport read = FrameworkExport.Parse(json, "x").Value;
        Assert.AreEqual(1, read.Notes.Single().Cycles);
        Assert.AreEqual(40, read.Cycles.Single().Readers.Single().Raised);
        StringAssert.Contains(ExportFramework.Render(read, null), "stage:verify [missing] has no word for unproven");
    }

    [TestMethod]
    public void An_agent_writing_the_framework_notes_is_denied_by_path_and_by_command()
    {
        Assert.IsTrue(ProtectedPaths.IsProtected(".sdlc/framework-harvest.json", out ProtectedFile? byPath));
        Assert.AreSame(ProtectedPaths.FrameworkNotes, byPath);
        StringAssert.Contains(byPath!.Reason, "sdlc harvest record");

        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("echo {} > .sdlc/framework-harvest.json", out ProtectedFile? byRedirect));
        Assert.AreSame(ProtectedPaths.FrameworkNotes, byRedirect);
        Assert.IsTrue(ProtectedPaths.CommandTouchesProtected("cp other.json .sdlc/framework-harvest.json", out _));
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("cat .sdlc/framework-harvest.json", out _), "Reading it is allowed.");
    }

    private static CycleWorkspace Completed()
    {
        var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.WriteVerify("pass");
        return workspace;
    }

    private static FakeRepository Merged(CycleWorkspace workspace)
    {
        FakeRepository git = new() { Branch = "development" };
        git.Refs.Add(Integration);
        git.History[Integration + ":.sdlc/cycles/" + workspace.Cycle.Id] = [("merged", DateTimeOffset.UnixEpoch)];
        return git;
    }

    private static async Task<string> Proposals(CycleWorkspace workspace, string body)
    {
        string path = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(path, "# Harvest proposals\n\n" + body);
        return path;
    }

    private static async Task<Result<RecordHarvest.Response>> Record(CycleWorkspace workspace, HarvestAspect aspect, string proposals, FakeRepository? git = null)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git ?? Merged(workspace)));
        return await gate.Send<RecordHarvest.Command, RecordHarvest.Response>(new RecordHarvest.Command(aspect, proposals));
    }

    private static async Task<Result<CloseHarvest.Response>> Close(CycleWorkspace workspace, FakeRepository git)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<CloseHarvest.Command, CloseHarvest.Response>(new CloseHarvest.Command());
    }
}
