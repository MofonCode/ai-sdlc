using FluentResults;
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

        ClaimGraph graph = await Compute(solution);

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

        ClaimGraph graph = await Compute(solution);

        Collision only = graph.Collisions.Single();

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

        ClaimGraph graph = await Compute(solution);

        Collision only = graph.Collisions.Single();

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

        ClaimGraph graph = await Compute(solution);

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

        ClaimGraph graph = await Compute(solution);

        Assert.IsTrue(graph.Claims.Single().NeedsServices, shape);
    }

    [TestMethod]
    public async Task A_plan_that_stands_nothing_up_is_not_flagged()
    {
        using var solution = Solution.Create();

        solution.PlanShaped("20260929_1000_00", "A scoped service decides the roll.", "src/A.cs");

        ClaimGraph graph = await Compute(solution);

        Assert.IsFalse(graph.Claims.Single().NeedsServices);
    }

    [TestMethod]
    public async Task A_cycle_with_no_plan_is_left_out_rather_than_reported()
    {
        // This answers "what may run together", and a cycle that cannot run is not part of that answer.
        using var solution = Solution.Create();

        solution.Plan("20260929_1000_00", "src/A.cs");
        solution.EmptyCycle("20260929_1100_00");

        ClaimGraph graph = await Compute(solution);

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

    private static async Task<ClaimGraph> Compute(Solution solution)
    {
        using var gate = Gate.For(solution.Root);

        Result<ClaimGraph> result = await gate
            .Send<ComputeClaimGraph.Request, ClaimGraph>(new ComputeClaimGraph.Request());

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
