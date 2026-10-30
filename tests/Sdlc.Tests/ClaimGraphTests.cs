using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;

namespace Sdlc.Tests;

/// <summary>
/// Which cycles may be built at once, computed before anything is built.
/// </summary>
/// <remarks>
/// This is in the binary rather than in the batch skill because it is a set intersection over a file table,
/// and the binary does it the same way every time. A graph a skill derived by reading plans would come out
/// slightly differently each run, and what it decides is what runs concurrently.
/// </remarks>
[TestClass]
public sealed class ClaimGraphTests
{
    [TestMethod]
    public async Task Cycles_that_claim_nothing_in_common_all_run_together()
    {
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs");
        solution.Plan("20260929_1100_00", "src/B.cs");
        solution.Plan("20260929_1200_00", "src/C.cs");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.AreEqual(0, graph.Collisions.Count);
        Assert.AreEqual(1, graph.Groups.Count);
        Assert.AreEqual(3, graph.Groups[0].Count);
    }

    [TestMethod]
    public async Task Two_cycles_claiming_one_file_collide_and_are_separated()
    {
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs", "src/Shared.cs");
        solution.Plan("20260929_1100_00", "src/Shared.cs");

        ComputeClaimGraph.Response graph = await Compute(solution);

        ComputeClaimGraph.Collision only = graph.Collisions.Single();

        CollectionAssert.AreEqual(new[] { "src/Shared.cs" }, only.Files.ToArray());
        Assert.AreEqual(2, graph.Groups.Count);
    }

    [TestMethod]
    public async Task The_earlier_cycle_goes_first_because_the_order_is_the_id()
    {
        // Taken rather than declared. Nothing in a plan says which cycle depends on which, because the
        // skill that would have declared it was removed with split.
        using var solution = Solution.Create();

        solution.Plan("20261001_0900_00", "src/Shared.cs");
        solution.Plan("20260929_1000_00", "src/Shared.cs");

        ComputeClaimGraph.Response graph = await Compute(solution);

        ComputeClaimGraph.Collision only = graph.Collisions.Single();

        Assert.AreEqual("20260929_1000_00", only.First.ToString());
        Assert.AreEqual("20261001_0900_00", only.Second.ToString());
    }

    [TestMethod]
    public async Task A_cycle_that_collides_with_one_group_joins_the_next()
    {
        // Greedy rather than optimal. An optimal packing would run marginally wider and would also change a
        // batch's ordering when an unrelated cycle was added, which makes a failed run harder to reason about.
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs");
        solution.Plan("20260929_1100_00", "src/A.cs");
        solution.Plan("20260929_1200_00", "src/B.cs");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.AreEqual(2, graph.Groups.Count);
        Assert.AreEqual(2, graph.Groups[0].Count, "The third claims nothing the first does.");
        Assert.AreEqual(1, graph.Groups[1].Count);
    }

    [TestMethod]
    [DataRow("Adds an Aspire AppHost.")]
    [DataRow("Stands up a Redis cache.")]
    [DataRow("Adds an EF Core migration.")]
    [DataRow("Uses WithDataVolume for the database.")]
    public async Task A_plan_that_stands_something_up_is_flagged_so_a_batch_can_cap_it(string shape)
    {
        // Errs toward caution: a false positive costs some concurrency, while a miss means two runs sharing a
        // named volume -- the same database files, two writers -- which fails strangely rather than loudly.
        using var solution = Solution.Create();

        solution.PlanShaped("20260929_1000_00", shape, "src/A.cs");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.IsTrue(graph.Claims.Single().NeedsServices, shape);
    }

    [TestMethod]
    public async Task A_plan_that_stands_nothing_up_is_not_flagged()
    {
        using var solution = Solution.Create();

        solution.PlanShaped("20260929_1000_00", "A scoped service decides the roll.", "src/A.cs");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.IsFalse(graph.Claims.Single().NeedsServices);
    }

    [TestMethod]
    public async Task A_cycle_with_no_plan_is_left_out_rather_than_reported()
    {
        // This answers "what may run together", and a cycle that cannot run is not part of that answer.
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs");
        solution.EmptyCycle("20260929_1100_00");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.AreEqual(1, graph.Claims.Count);
    }

    [TestMethod]
    public async Task A_solution_with_no_cycles_says_there_is_nothing_to_build()
    {
        using var solution = Solution.Create();

        string rendered = ClaimGraphRenderer.Render(await Compute(solution));

        StringAssert.Contains(rendered, "Nothing to build.");
    }

    [TestMethod]
    public async Task The_report_names_the_file_a_collision_is_about_and_is_ascii()
    {
        // A group count on its own tells nobody whether the serialisation is reasonable.
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/Shared.cs");
        solution.Plan("20260929_1100_00", "src/Shared.cs");

        string rendered = ClaimGraphRenderer.Render(await Compute(solution));

        StringAssert.Contains(rendered, "src/Shared.cs");
        StringAssert.Contains(rendered, "rebases on the earlier one");
        Assert.IsTrue(rendered.All(char.IsAscii), rendered);
    }

    [TestMethod]
    public async Task An_unapproved_cycle_is_not_scheduled_and_is_reported()
    {
        // §10.1 says the graph is built from the approved cycles in this working copy. The handler
        // applied no approval filter, no abandonment filter and no already-verified filter, so an
        // abandoned cycle's plan claimed its files forever and every merged cycle narrowed the graph
        // for every future batch. batch-build reads this to plan a run. F-13.
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs");
        solution.Plan("20260929_1100_00", "src/B.cs");
        solution.Unapprove("20260929_1100_00");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.AreEqual("20260929_1000_00", graph.Claims.Single().Cycle.ToString());
        StringAssert.Contains(graph.Skipped.Single().Why, "approved");
    }

    [TestMethod]
    public async Task An_abandoned_cycle_stops_claiming_its_files()
    {
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/Shared.cs");
        solution.Plan("20260929_1100_00", "src/Shared.cs");
        solution.Abandon("20260929_1100_00");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.AreEqual(0, graph.Collisions.Count, "An abandoned cycle collides with nothing.");
        Assert.AreEqual("abandoned", graph.Skipped.Single().Why);
    }

    [TestMethod]
    public async Task A_cycle_that_already_has_a_verify_is_not_scheduled_again()
    {
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/Shared.cs");
        solution.Plan("20260929_1100_00", "src/Shared.cs");
        solution.Verified("20260929_1000_00");

        ComputeClaimGraph.Response graph = await Compute(solution);

        Assert.AreEqual(1, graph.Claims.Count);
        Assert.AreEqual(0, graph.Collisions.Count, "A merged cycle must not serialise a future batch.");
        StringAssert.Contains(graph.Skipped.Single().Why, "verified");
    }

    [TestMethod]
    public async Task The_report_names_what_it_left_out()
    {
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs");
        solution.Plan("20260929_1100_00", "src/B.cs");
        solution.Abandon("20260929_1100_00");

        string rendered = ClaimGraphRenderer.Render(await Compute(solution));

        StringAssert.Contains(rendered, "20260929_1100_00");
        StringAssert.Contains(rendered, "not scheduled: abandoned");
    }

    private static async Task<ComputeClaimGraph.Response> Compute(Solution solution)
    {
        using var gate = Gate.For(solution.Root);

        Result<ComputeClaimGraph.Response> result = await gate
            .Send<ComputeClaimGraph.Query, ComputeClaimGraph.Response>(new ComputeClaimGraph.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    /// <summary>A scratch solution holding several cycles, which the cycle workspace does not do.</summary>
    private sealed class Solution : IDisposable
    {
        private Solution(string root) => Root = root;

        public string Root { get; }

        public static Solution Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-claims", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new Solution(root);
        }

        public void EmptyCycle(string id) =>
            Directory.CreateDirectory(Path.Combine(
                Root,
                Cycle.SdlcDirectoryName,
                Cycle.CyclesDirectoryName,
                id));

        public void Plan(string id, params string[] files) => PlanShaped(id, "No departure.", files);

        /// <summary>
        /// Everything beside the plan that the gate needs before it will permit a build.
        /// </summary>
        /// <remarks>
        /// The graph is built from the approved cycles in this working copy (§10.1), and the handler
        /// used to apply no filter at all -- so these tests passed with nothing but a plan.md on disk,
        /// asserting a graph that included cycles nothing would ever build. F-13.
        /// <para>
        /// The two upstream documents hold placeholder text rather than valid artifacts, deliberately:
        /// the gate checks existence, hashes and rounds, and does not validate a schema. Writing
        /// schema-valid ones would make this fixture track the schema for no gain.
        /// </para>
        /// </remarks>
        public void Approve(string id)
        {
            Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed), id);
            var cycle = new Cycle(parsed!.Value, Root);

            CycleWorkspace.WriteRaw(cycle.PathOf(ArtifactKind.Intent), "an intent\n");
            CycleWorkspace.WriteRaw(cycle.PathOf(ArtifactKind.Spec), "a spec\n");

            foreach (ArtifactKind kind in Cycle.PlanningDocuments)
            {
                string upstream = Cycle.UpstreamOf(kind) is { } above
                    ? ContentHash.Of(File.ReadAllText(cycle.PathOf(above)))
                    : string.Empty;

                CycleWorkspace.WriteRaw(
                    cycle.FindingsPath(kind, 1),
                    $$"""
                    { "round": 1, "artifact": "{{Cycle.NameOf(kind)}}", "upstream_sha256": "{{upstream}}", "findings": [] }
                    """);
            }

            List<string> ledger =
            [
                "---",
                "cycle: " + id,
                "framework_version: 1.0.0",
                "decisions:",
                "  - decision: approved",
                "    by: Peter Almanzar",
                "    at: 2026-10-01T12:00:00Z",
                "    hashes:",
                .. Cycle.PlanningDocuments.Select(kind =>
                    "      " + Cycle.FileNameOf(kind) + ": "
                    + ContentHash.Of(File.ReadAllText(cycle.PathOf(kind)))),
                "---",
            ];

            CycleWorkspace.WriteRaw(cycle.ApprovalPath, string.Join('\n', ledger) + "\n");
        }

        /// <summary>Removes a cycle's approval, leaving it with a plan the gate will not build.</summary>
        public void Unapprove(string id)
        {
            Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed), id);
            File.Delete(new Cycle(parsed!.Value, Root).ApprovalPath);
        }

        /// <summary>Marks a cycle abandoned, which takes it out of the graph.</summary>
        public void Abandon(string id)
        {
            Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed), id);
            CycleWorkspace.WriteRaw(new Cycle(parsed!.Value, Root).AbandonedPath, "stopped.\n");
        }

        /// <summary>Gives a cycle a verify document, which means it has already been built.</summary>
        public void Verified(string id)
        {
            Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed), id);
            CycleWorkspace.WriteRaw(new Cycle(parsed!.Value, Root).PathOf(ArtifactKind.Verify), "a verify\n");
        }

        /// <summary>A plan whose shape section says something, so the service heuristic can be tested.</summary>
        public void PlanShaped(string id, string shape, params string[] files)
        {
            EmptyCycle(id);

            string table = string.Join(
                Environment.NewLine,
                files.Select(file => "| `" + file + "` | new |"));

            CycleWorkspace.WriteRaw(
                Path.Combine(Root, Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, id, "plan.md"),
                string.Join(
                    Environment.NewLine,
                    [
                        "---",
                        "framework_version: 1.0.0",
                        "cycle: " + id,
                        "tier: local",
                        "---",
                        string.Empty,
                        "# a plan",
                        string.Empty,
                        "## Shape and departures",
                        string.Empty,
                        shape,
                        string.Empty,
                        "## Files",
                        string.Empty,
                        "| File | Change |",
                        "|---|---|",
                        table,
                        string.Empty,
                        "## Proof",
                        string.Empty,
                        "1. `A_test` asserts it.",
                        string.Empty,
                        "## Risk",
                        string.Empty,
                        "Some.",
                        string.Empty,
                        "## Open",
                    ]));

            // Approved by default, because the graph is built from approved cycles and almost every
            // test here is about collisions rather than about the gate. The tests that care about the
            // filter undo it.
            Approve(id);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // A scratch directory left under the temp path is not worth failing a test over.
            }
        }
    }
}
