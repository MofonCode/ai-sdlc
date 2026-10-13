using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;

namespace Sdlc.Tests;

/// <summary>
/// Opening a cycle: the id, the claim that makes it exclusive, and what git is told about it.
/// </summary>
/// <remarks>
/// <para><strong>Nothing exercised this operation before, which is how its mechanism and the document
/// drifted apart unnoticed.</strong> D-10 said creating the directory was the collision check and that
/// the filesystem made it atomic. Creating a directory cannot be either — <c>CreateDirectory</c>
/// succeeds on one that is already there — so the code moved to a marker file opened with
/// <see cref="FileMode.CreateNew"/> while the entry, two sections and one skill kept the old claim. A
/// test here would have caught the disagreement from the day the code changed.</para>
///
/// <para>The collision is driven through a fixed clock rather than by racing two threads. An id is the
/// moment it opened to the second (§16), so two opens on one instant is exactly the case, and asserting
/// it deterministically is worth more than a race that passes when the machine is slow.</para>
/// </remarks>
[TestClass]
public sealed class NewCycleTests
{
    private static readonly DateTimeOffset Instant = new(2026, 10, 9, 14, 7, 9, TimeSpan.Zero);

    [TestMethod]
    public async Task Opening_a_cycle_creates_its_directory_and_claims_the_id()
    {
        using var workspace = CycleWorkspace.Create();

        Cycle cycle = await Open(workspace);

        Assert.IsTrue(Directory.Exists(cycle.Directory), cycle.Directory);

        Assert.IsTrue(
            File.Exists(Path.Combine(cycle.Directory, ".opened")),
            "The marker is the claim. Without it the id is allocated and nothing holds it.");
    }

    [TestMethod]
    public async Task The_id_is_the_moment_the_cycle_opened()
    {
        using var workspace = CycleWorkspace.Create();

        Cycle cycle = await Open(workspace);

        Assert.AreEqual("20261009_1407_09", cycle.Id.ToString());
    }

    [TestMethod]
    public async Task Two_cycles_opened_in_the_same_second_collide_rather_than_sharing_a_directory()
    {
        // The whole of D-10. Both opens allocate the same id because an id is the second it opened in,
        // so the second one has to lose -- and it has to lose on the claim, because the directory is
        // already there by then and creating it again succeeds.
        using var workspace = CycleWorkspace.Create();

        Cycle first = await Open(workspace);

        using var gate = Gate.For(
            workspace.Root,
            services => services.AddSingleton<TimeProvider>(new FakeClock(Instant)));

        Result<Cycle> second = await gate.Send<NewCycle.Command, Cycle>(new NewCycle.Command());

        Assert.IsTrue(
            second.IsFailed,
            "The second open was allowed, so two cycles share one directory and one id. Creating the "
            + "directory cannot be the check: it succeeds on a directory that is already there.");

        Assert.AreEqual(
            1,
            second.Errors.Count(error => error.Message.Contains(first.Id.ToString(), StringComparison.Ordinal)),
            "The refusal has to name the id that collided: " + string.Join("; ", second.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task The_cycles_directory_ignores_the_claim_and_keeps_its_own_ignore_tracked()
    {
        // The claim is local state the filesystem decides, not history. Committed, it would reach the
        // first cycle's diff as a file no plan declared and be reported UNDECLARED on every cycle
        // after. The negation is what makes a clone arrive already ignoring it.
        using var workspace = CycleWorkspace.Create();

        Cycle cycle = await Open(workspace);

        string ignore = Path.Combine(Path.GetDirectoryName(cycle.Directory)!, ".gitignore");

        Assert.IsTrue(File.Exists(ignore), "The cycles directory does not ignore anything: " + ignore);

        string[] lines = await File.ReadAllLinesAsync(ignore);

        CollectionAssert.Contains(lines, ".opened", "The claim is not ignored.");
        CollectionAssert.Contains(lines, "!.gitignore", "The ignore file itself must stay tracked.");
    }

    [TestMethod]
    public async Task An_ignore_a_person_amended_is_left_alone()
    {
        // Written only when absent, for the reason ReportFile gives: the framework owns the file it
        // named, and rewriting it on every open would discard whatever a team added beside it.
        using var workspace = CycleWorkspace.Create();

        string cycles = Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName);
        Directory.CreateDirectory(cycles);

        string ignore = Path.Combine(cycles, ".gitignore");
        await File.WriteAllTextAsync(ignore, "# ours\n.opened\n!.gitignore\nscratch/\n");

        await Open(workspace);

        StringAssert.Contains(await File.ReadAllTextAsync(ignore), "scratch/");
    }

    [TestMethod]
    public async Task No_cycle_opens_until_a_person_has_placed_a_declared_stack()
    {
        // Adoption writes none, so without this a first cycle could reach its plan with nothing to be
        // read against and no stack rule in force -- every gate green, and the argument the stack file
        // exists to force never made. Refused here, before an id is spent on it.
        using var workspace = CycleWorkspace.Create();

        using var gate = Gate.For(
            workspace.Root,
            services => services.AddSingleton<TimeProvider>(new FakeClock(Instant)));

        Result<Cycle> result = await gate.Send<NewCycle.Command, Cycle>(new NewCycle.Command());

        Assert.IsTrue(result.IsFailed, "A cycle opened in a solution that declares no stack.");

        string message = string.Join("; ", result.Errors.Select(error => error.Message));

        StringAssert.Contains(message, "sdlc draft techstack", "It names the command that writes the draft.");
        StringAssert.Contains(message, "--empty", "And the way to say nothing is pinned, which is an answer.");

        Assert.IsFalse(
            Directory.Exists(Path.Combine(
                workspace.Root,
                Cycle.SdlcDirectoryName,
                Cycle.CyclesDirectoryName,
                "20261009_1407_09")),
            "Nothing was claimed. A refusal that had already created the directory would leave an id spent.");
    }

    /// <summary>Opens a cycle in a solution whose stack a person has declared.</summary>
    /// <remarks>
    /// Declared as empty, which is the smallest thing that passes and the case worth holding: a file
    /// with nothing in it is a person saying nothing is pinned, and that is a decision rather than the
    /// absence of one.
    /// </remarks>
    private static async Task<Cycle> Open(CycleWorkspace workspace)
    {
        workspace.WriteTechStack(string.Empty);

        using var gate = Gate.For(
            workspace.Root,
            services => services.AddSingleton<TimeProvider>(new FakeClock(Instant)));

        Result<Cycle> result = await gate.Send<NewCycle.Command, Cycle>(new NewCycle.Command());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));

        return result.Value;
    }
}
