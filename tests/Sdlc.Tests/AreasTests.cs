using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Areas;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Drafts;
using Sdlc.Core.Findings;
using Sdlc.Core.Hooks;
using Sdlc.Core.Proposals;

namespace Sdlc.Tests;

/// <summary>
/// Areas (D-150, D-151): a name for a set of files, carried by rules and decisions, deciding which entries a
/// reader gets in full.
/// </summary>
/// <remarks>
/// The failure every test here guards against is the same one: a rule quietly reaching nobody. So most of
/// them are about what fails open -- no areas, an undefined name, globs that match nothing.
/// </remarks>
[TestClass]
public sealed class AreasTests
{
    private const string Areas = """
        # The parts of this solution's code

        ## market-data

        **Files:** src/MarketData/**, **/Massive*/**

        **Is:** fetching and replaying quotes.

        ## persistence

        **Files:** *.sql, src/Data/

        **Is:** what is stored and how.
        """;

    [TestMethod]
    [DataRow("src/MarketData/**", "src/MarketData/Quotes/Feed.cs", true)]
    [DataRow("src/MarketData/**", "src/Other/Feed.cs", false)]
    [DataRow("*.sql", "db/migrations/001.sql", true)]
    [DataRow("*.sql", "db/001.sqlx", false)]
    [DataRow("src/Data/", "src/Data/Context.cs", true)]
    [DataRow("**/Massive*/**", "tests/MassiveFake/Server.cs", true)]
    [DataRow("src/marketdata/**", "SRC/MarketData/Feed.cs", true)]
    [DataRow("src/*.cs", "src/a/b.cs", false)]
    public void A_glob_is_small_enough_to_predict(string glob, string path, bool matches)
    {
        Area area = new("an-area", [glob], "it");

        Assert.AreEqual(matches, area.Covers(path), $"{glob} against {path}");
    }

    [TestMethod]
    public void The_area_file_reads_each_area_and_names_every_malformed_one()
    {
        IReadOnlyList<Area> read = AreaParser.Parse(Areas, "areas.md").Value;

        CollectionAssert.AreEqual(new[] { "market-data", "persistence" }, read.Select(area => area.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "*.sql", "src/Data/" }, read[1].Files.ToArray());

        Result<IReadOnlyList<Area>> bad = AreaParser.Parse("## Market Data\n\n**Files:** a/**\n\n## empty\n\n**Is:** nothing.\n", "areas.md");

        Assert.IsTrue(bad.IsFailed);
        Assert.AreEqual(2, bad.Errors.Count, "Both malformed areas are reported in one pass.");
    }

    [TestMethod]
    public void A_rule_and_a_decision_carry_the_areas_their_line_names()
    {
        Decision decision = DecisionParser.Parse(
            "## DEC_01 — one feed\n\n**Areas:** market-data, `persistence`\n\n**Decided:** a.\n\n**Because:** b.\n\n**Instead of:** c.\n\n**From:** d.\n",
            "decisions").Value[0];
        Constraint rule = ConstraintParser.Parse(
            "## LOC_01 — a rule\n\n**Applies to:** a plan.\n\n**Forbids:** x.\n\n**Why:** y.\n\n**Areas:** persistence\n",
            "constraints").Value[0];

        CollectionAssert.AreEqual(new[] { "market-data", "persistence" }, decision.InAreas.ToArray());
        CollectionAssert.AreEqual(new[] { "persistence" }, rule.InAreas.ToArray());
        Assert.AreEqual("a.", decision.Decided, "The Areas line is a clause of its own, not part of the next.");
    }

    [TestMethod]
    public void With_no_areas_nothing_is_narrowed()
    {
        Assert.IsFalse(AreaScope.Everything.Narrowed);
        Assert.IsTrue(AreaScope.Everything.InFull(["market-data"]));
        Assert.IsFalse(AreaScope.For([], ["src/MarketData/Feed.cs"], []).Narrowed, "No area defined: today's behaviour.");
    }

    [TestMethod]
    public void An_entry_reaches_in_full_when_it_is_global_in_area_or_carries_only_names_that_cover_nothing()
    {
        IReadOnlyList<Area> defined = AreaParser.Parse(Areas, "areas.md").Value;
        var scope = AreaScope.For(defined, ["src/MarketData/Feed.cs"], stale: ["persistence"]);

        CollectionAssert.AreEqual(new[] { "market-data" }, scope.Touched.ToArray());
        Assert.IsTrue(scope.InFull([]), "No area: it applies everywhere.");
        Assert.IsTrue(scope.InFull(["market-data"]), "In the area the change touches.");
        Assert.IsTrue(scope.InFull(["persistence"]), "Its only area covers nothing, so it fails open.");
        Assert.IsTrue(scope.InFull(["nobody-defined-this"]), "An undefined name fails open.");

        var fresh = AreaScope.For(defined, ["src/MarketData/Feed.cs"], stale: []);
        Assert.IsFalse(fresh.InFull(["persistence"]), "Outside the change's areas: its headline only.");
    }

    [TestMethod]
    public async Task The_intent_and_spec_touch_no_area_and_the_plan_touches_what_its_files_fall_in()
    {
        using var workspace = CycleWorkspace.Create();
        WriteAreas(workspace);
        workspace.WriteArtifact(ArtifactKind.Intent);
        WritePlan(workspace, "- market-data");

        AreaScope intent = (await AreaScope.Of(workspace.Cycle, ArtifactKind.Intent, CancellationToken.None)).Value;
        AreaScope plan = (await AreaScope.Of(workspace.Cycle, ArtifactKind.Plan, CancellationToken.None)).Value;

        Assert.IsTrue(intent.Narrowed);
        Assert.AreEqual(0, intent.Touched.Count, "The intent is business-led and names no area.");
        CollectionAssert.AreEqual(new[] { "market-data" }, plan.Touched.ToArray());
    }

    [TestMethod]
    public void A_decision_outside_the_stage_s_areas_is_rendered_by_its_headline_and_says_how_to_read_it()
    {
        IReadOnlyList<Area> defined = AreaParser.Parse(Areas, "areas.md").Value;
        ReadDecisions.Response record = new(
        [
            new Decision(DecisionId.Of(1), "One feed", "only one feed.", "b", "c", "d", null, ["market-data"]),
            new Decision(DecisionId.Of(2), "Migrations are scripts", "plain sql scripts.", "b", "c", "d", null, ["persistence"]),
            new Decision(DecisionId.Of(3), "Every screen works on a phone", "narrow first.", "b", "c", "d"),
        ], 0, Recorded: true);

        string rendered = DecisionsRenderer.Render(record, DecisionDetail.Full, AreaScope.For(defined, ["src/MarketData/Feed.cs"], []));

        StringAssert.Contains(rendered, "2 apply here in full");
        StringAssert.Contains(rendered, "only one feed.");
        StringAssert.Contains(rendered, "narrow first.", "A decision with no area reaches every reader in full.");
        StringAssert.Contains(rendered, "DEC_02    Migrations are scripts [persistence]");
        Assert.IsFalse(rendered.Contains("plain sql scripts.", StringComparison.Ordinal), "Out of area: the headline, not the body.");
        StringAssert.Contains(rendered, "sdlc decisions show DEC_nn");
    }

    [TestMethod]
    public void A_reader_handed_a_headline_is_told_to_raise_it_rather_than_to_run_a_command()
    {
        // A reader runs with no tools: telling it to run 'sdlc decisions show' asks for what it cannot do.
        IReadOnlyList<Area> defined = AreaParser.Parse(Areas, "areas.md").Value;
        ReadDecisions.Response record = new([new Decision(DecisionId.Of(2), "Migrations are scripts", "x", "b", "c", "d", null, ["persistence"])], 0, Recorded: true);

        string judged = DecisionsRenderer.Render(record, DecisionDetail.Judged, AreaScope.For(defined, ["src/MarketData/Feed.cs"], []));

        StringAssert.Contains(judged, "raise that as a finding naming the entry");
        Assert.IsFalse(judged.Contains("sdlc decisions show", StringComparison.Ordinal));
    }

    [TestMethod]
    public void A_solution_rule_outside_the_areas_is_a_headline_and_a_framework_rule_never_is()
    {
        IReadOnlyList<Area> defined = AreaParser.Parse(Areas, "areas.md").Value;
        Constraint local = new(ConstraintId.Of(ConstraintTier.Solution, 1), "Migrations are reviewed", "a plan", "an unreviewed migration", "data loss", Areas: ["persistence"]);
        Constraint global = new(ConstraintId.Of(ConstraintTier.Global, 1), "The intent states a problem", "an intent", "a solution in disguise", "scope creep", Areas: ["persistence"]);
        EffectiveConstraints.Response set = new([global, local], [], []);

        string rendered = ConstraintRenderer.Render(set, ConstraintDetail.Full, "plan", AreaScope.For(defined, ["src/MarketData/Feed.cs"], []));

        StringAssert.Contains(rendered, "a solution in disguise", "A framework rule gates every cycle and is always in full.");
        Assert.IsFalse(rendered.Contains("an unreviewed migration", StringComparison.Ordinal));
        StringAssert.Contains(rendered, "sdlc constraints show LOC_nn");
    }

    [TestMethod]
    public async Task A_round_refuses_while_an_area_covers_no_file()
    {
        using var workspace = CycleWorkspace.Create();
        WriteAreas(workspace);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, "src", "MarketData", "Feed.cs"), "class Feed;");
        workspace.WriteArtifact(ArtifactKind.Intent);

        Result<ReaderPrompts.Response> refused = await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, Path.Combine(workspace.Root, "round"));

        Assert.IsTrue(refused.IsFailed, "persistence covers nothing, and its entries would reach no reader.");
        StringAssert.Contains(refused.Errors[0].Message, "persistence");

        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, "db", "001.sql"), "select 1;");
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Intent, Path.Combine(workspace.Root, "round"))).IsSuccess);
    }

    [TestMethod]
    public async Task Sdlc_areas_says_what_each_area_covers_and_carries_and_what_names_nothing()
    {
        using var workspace = CycleWorkspace.Create();
        WriteAreas(workspace);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, "src", "MarketData", "Feed.cs"), "class Feed;");
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, ".sdlc", "decisions.md"),
            "# d\n\n## DEC_01 — one feed\n\n**Areas:** market-data, typo-area\n\n**Decided:** a.\n\n**Because:** b.\n\n**Instead of:** c.\n\n**From:** d.\n");

        using var gate = Gate.For(workspace.Root);
        ReadAreas.Response areas = (await gate.Send<ReadAreas.Query, ReadAreas.Response>(new ReadAreas.Query())).Value;
        string rendered = AreasRenderer.Render(areas);

        Assert.AreEqual(1, areas.Areas[0].Files);
        CollectionAssert.AreEqual(new[] { "DEC_01" }, areas.Areas[0].Entries.ToArray());
        StringAssert.Contains(rendered, "COVERS NOTHING");
        StringAssert.Contains(rendered, "DEC_01 names 'typo-area'");
    }

    [TestMethod]
    public void A_plan_s_areas_section_is_allowed_checked_for_shape_and_required_since_areas()
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? parsed));
        CycleId id = parsed.Value;
        string plan = Plan("- new trade-import: `src/Brokers/**`\n- none: `tools/once.ps1`");

        Assert.IsFalse(ArtifactSchema.Validate(ArtifactKind.Plan, plan, id, areas: true).Violations.Any(violation => violation.Where == "## Areas"));
        Assert.IsTrue(ArtifactSchema.Validate(ArtifactKind.Plan, Plan("- new Trade Import"), id).Violations.Any(violation => violation.Where == "## Areas"));
        Assert.IsTrue(ArtifactSchema.Validate(ArtifactKind.Plan, Plan(null), id, areas: true).Violations.Any(violation => violation.Where == "## Areas"));
        Assert.IsFalse(ArtifactSchema.Validate(ArtifactKind.Plan, Plan(null), id, areas: false).Violations.Any(violation => violation.Where == "## Areas"), "A cycle opened before areas keeps its shape.");
    }

    [TestMethod]
    public async Task A_plan_round_refuses_a_declared_file_with_no_area_and_records_a_proposed_name_as_an_area_suggestion()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        await RecordClean(workspace, ArtifactKind.Intent);
        await RecordClean(workspace, ArtifactKind.Spec);

        WritePlan(workspace, "- none: `docs/**`");
        Result<RecordRound.Response> refused = await RecordClean(workspace, ArtifactKind.Plan);

        Assert.IsTrue(refused.IsFailed, "src/Brokers/Import.cs has no area and no answer.");
        StringAssert.Contains(refused.Errors[0].Message, "src/Brokers/Import.cs");

        // D-161: refused before the readers are paid for, not only when the round is recorded.
        Result<ReaderPrompts.Response> unprompted = await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Plan, Path.Combine(workspace.Root, "round"));
        Assert.IsTrue(unprompted.IsFailed, "The same listing refuses the prompt.");
        StringAssert.Contains(unprompted.Errors[0].Message, "src/Brokers/Import.cs");

        WritePlan(workspace, "- new trade-import: `src/Brokers/**`\n- none: `src/MarketData/**`");
        Assert.IsTrue((await ReaderPromptsTests.Prompt(workspace, ArtifactKind.Plan, Path.Combine(workspace.Root, "round"))).IsSuccess);
        Assert.IsTrue((await RecordClean(workspace, ArtifactKind.Plan)).IsSuccess);

        LedgerSuggestion suggestion = (await FindingsReader.ReadSuggestions(workspace.Cycle, CancellationToken.None)).Value.Suggestions.Single();

        Assert.AreEqual("areas.S_01", suggestion.Id);
        StringAssert.Contains(suggestion.Statement, "trade-import");
        Assert.IsTrue(suggestion.IsOpen);
    }

    [TestMethod]
    public async Task An_area_file_written_by_an_agent_is_denied()
    {
        await Task.CompletedTask;

        Assert.IsTrue(ProtectedPaths.IsProtected(".sdlc/areas.md", out ProtectedFile? file));
        StringAssert.Contains(file!.Reason, "sdlc draft mining");
    }

    [TestMethod]
    public async Task Mining_drafts_an_area_and_tags_an_entry_and_refuses_a_tag_nobody_defined()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, ".sdlc", "decisions.md"),
            "# d\n\n## DEC_01 — one feed\n\n**Decided:** a.\n\n**Because:** b.\n\n**Instead of:** c.\n\n**From:** d.\n");
        string proposals = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(proposals, "## Areas\n\n### [x] add trade-import — importing trades from brokers\n**Files:** src/Brokers/**\n\n### [x] tag DEC_01 — trade-import\n");

        using var gate = Gate.For(workspace.Root);
        DraftResult drafted = (await gate.Send<DraftMining.Command, DraftResult>(new DraftMining.Command(proposals))).Value;

        Assert.IsTrue(drafted.MayDraft, drafted.Instructions);
        string areas = await File.ReadAllTextAsync(Path.Combine(workspace.Root, ".sdlc", "areas.draft.md"));
        string decisions = await File.ReadAllTextAsync(Path.Combine(workspace.Root, ".sdlc", "decisions.draft.md"));

        Assert.AreEqual("trade-import", AreaParser.Parse(areas, "areas").Value.Single().Name);
        CollectionAssert.AreEqual(new[] { "trade-import" }, DecisionParser.Parse(decisions, "d").Value[0].InAreas.ToArray());
        StringAssert.Contains(drafted.Instructions, ".sdlc/areas.draft.md .sdlc/areas.md");

        await File.WriteAllTextAsync(proposals, "## Areas\n\n### [x] tag DEC_01 — nobody-defined\n");
        DraftResult refused = (await gate.Send<DraftMining.Command, DraftResult>(new DraftMining.Command(proposals))).Value;

        Assert.IsFalse(refused.MayDraft);
        StringAssert.Contains(refused.Instructions, "DEC_01 names 'nobody-defined'");
    }

    [TestMethod]
    public void A_rename_reaches_every_entry_carrying_the_area_and_a_remove_leaves_them_global()
    {
        const string decisions = "## DEC_01 — one feed\n\n**Areas:** market-data, persistence\n\n**Decided:** a.\n\n**Because:** b.\n\n**Instead of:** c.\n\n**From:** d.\n";
        DraftMining.Proposal rename = new(DraftMining.Record.Areas, DraftMining.Operation.Rename, "market-data quotes", "a clearer word", string.Empty, Accepted: true);
        DraftMining.Proposal remove = new(DraftMining.Record.Areas, DraftMining.Operation.Remove, "persistence", "every plan touched it", string.Empty, Accepted: true);

        (string areas, _, string? after) = DraftMining.ApplyAreas(Areas, null, decisions, [rename, remove]).Value;

        CollectionAssert.AreEqual(new[] { "quotes" }, AreaParser.Parse(areas, "areas").Value.Select(area => area.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "quotes" }, DecisionParser.Parse(after!, "d").Value[0].InAreas.ToArray());
    }

    [TestMethod]
    public void An_area_proposal_named_in_three_plans_is_at_the_bar_and_the_replay_says_how_often_it_matches()
    {
        AreaHistory.Reading reading = PlanAreasProbe.Read();

        AreaHistory.Candidate candidate = reading.Candidates.Single();
        Assert.AreEqual(3, candidate.Cycles.Count);
        Assert.AreEqual(3, candidate.Plans, "Its globs cover the declared files of all three plans.");
    }

    private static void WriteAreas(CycleWorkspace workspace) =>
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", AreaParser.FileName), Areas);

    private static void WritePlan(CycleWorkspace workspace, string areaLines) =>
        CycleWorkspace.WriteRaw(workspace.Cycle.PathOf(ArtifactKind.Plan), Plan(areaLines, workspace.Cycle.Id.ToString()));

    private static string Plan(string? areaLines, string cycle = "20260929_1456_32") =>
        "---\ncycle: " + cycle + "\ntier: local\n---\n\n# plan\n\n"
        + "## Shape and departures\n\nOne importer.\n\n"
        + "## Files\n\n| File | Change |\n|---|---|\n| `src/Brokers/Import.cs` | new |\n| `src/MarketData/Feed.cs` | changed |\n\n"
        + "## Proof\n\n| Criterion | Proof |\n|---|---|\n\n"
        + "## Risk\n\nLow.\n\n"
        + "## Assumptions proven\n\nnone\n\n"
        + (areaLines is null ? string.Empty : "## Areas\n\n" + areaLines + "\n\n")
        + "## Open\n\nNothing.\n";

    private static async Task<Result<RecordRound.Response>> RecordClean(CycleWorkspace workspace, ArtifactKind kind)
    {
        string from = Path.Combine(workspace.Root, "readers." + Cycle.NameOf(kind) + ".json");
        await File.WriteAllTextAsync(from, """{ "findings": [] }""");

        using var gate = Gate.For(workspace.Root);
        return await gate.Send<RecordRound.Command, RecordRound.Response>(new RecordRound.Command(workspace.Cycle.Id, kind, from));
    }

    // Three cycles' plans, each proposing trade-import over the brokers folder.
    private static class PlanAreasProbe
    {
        public static AreaHistory.Reading Read()
        {
            using var workspace = CycleWorkspace.Create();

            foreach (string id in new[] { "20261001_0900_01", "20261002_0900_01", "20261003_0900_01" })
            {
                CycleWorkspace.WriteRaw(
                    Path.Combine(workspace.Root, ".sdlc", "cycles", id, "plan.md"),
                    Plan("- new trade-import: `src/Brokers/**`", id));
            }

            return AreaHistory.Read(workspace.Root, [], CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
