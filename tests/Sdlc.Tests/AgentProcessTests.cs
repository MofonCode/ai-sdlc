using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;
using Sdlc.Core.Harvest;

namespace Sdlc.Tests;

/// <summary>
/// The processes the binary starts on a skill's behalf: a round's readers, a harvest's readers, and the build's own
/// process (D-183, D-184, D-185).
/// </summary>
[TestClass]
public sealed class AgentProcessTests
{
    [TestMethod]
    public async Task The_rules_reader_starts_first_and_every_reader_is_marked_with_no_tools()
    {
        // D-163, D-118: the rules reader warms the cache alone; every reader has no tools, no MCP servers, no session.
        using TempDirectory round = new();
        round.Write("shared.md", "shared");
        round.Write("rules.prompt.md", "rules");
        round.Write("security.prompt.md", "security");
        round.Write("diff.prompt.md", "diff");
        FakeAgentProcess agents = new();

        RunRound.Response ran = await Run(round.Root, agents);

        Assert.IsTrue(ran.AllAnswered);
        Assert.AreEqual("rules", Name(agents.Launches[0]), "The rules reader starts first, alone.");
        Assert.IsTrue(agents.Launches.All(launch => launch.Reader), "Every reader is marked, so a red build lets its turn end.");
        Assert.IsTrue(agents.Launches.All(launch => launch.Arguments.Contains("--tools=") && launch.Arguments.Contains("--strict-mcp-config")));
        Assert.IsTrue(agents.Launches.All(launch => launch.WorkingDirectory == round.Root), "Started from the round's directory, outside the checkout.");
        CollectionAssert.Contains(agents.Launches.Single(launch => Name(launch) == "security").Arguments.ToList(), RunRound.SharedFileName);
        CollectionAssert.DoesNotContain(agents.Launches.Single(launch => Name(launch) == "diff").Arguments.ToList(), RunRound.SharedFileName, "The change's reader is handed no shared block.");
    }

    [TestMethod]
    public async Task A_reader_with_an_answer_is_not_started_again()
    {
        // Running again starts the reconciler once its prompt is written, and a reader that returned nothing.
        using TempDirectory round = new();
        round.Write("rules.prompt.md", "rules");
        round.Write("rules.json", "{}");
        round.Write("reconcile.prompt.md", "reconcile");
        FakeAgentProcess agents = new();

        RunRound.Response ran = await Run(round.Root, agents);

        Assert.AreEqual(1, agents.Launches.Count);
        Assert.AreEqual("reconcile", Name(agents.Launches[0]));
        Assert.IsTrue(ran.Readers.Single(reader => reader.Name == "rules").Kept);
    }

    [TestMethod]
    public async Task A_reader_that_returned_nothing_fails_the_run_and_says_so()
    {
        using TempDirectory round = new();
        round.Write("rules.prompt.md", "rules");
        FakeAgentProcess agents = new() { Silent = ["rules"] };

        RunRound.Response ran = await Run(round.Root, agents);

        Assert.IsFalse(ran.AllAnswered);
        StringAssert.Contains(RunRound.Render(ran), "A reader returned nothing");
    }

    [TestMethod]
    public async Task With_no_host_to_start_a_reader_the_run_is_refused()
    {
        using TempDirectory round = new();
        round.Write("rules.prompt.md", "rules");
        FakeAgentProcess agents = new() { NoHost = true };

        using var gate = Gate.For(round.Root, services => services.AddSingleton<IAgentProcess>(agents));
        Result<RunRound.Response> ran = await gate.Send<RunRound.Command, RunRound.Response>(
            new RunRound.Command(round.Root, TimeSpan.Zero, TimeSpan.FromMinutes(1)));

        Assert.IsTrue(ran.IsFailed);
        StringAssert.Contains(ran.Errors[0].Message, "No host to start a reader with");
    }

    [TestMethod]
    public async Task A_harvest_review_hands_each_reader_only_its_own_aspects_files_and_answers_in_prose()
    {
        using var workspace = CycleWorkspace.Create();
        string reports = Path.Combine(workspace.Root, ".sdlc", "reports");
        Directory.CreateDirectory(reports);
        await File.WriteAllTextAsync(Path.Combine(reports, "20261114_0900_00-harvest-decisions.md"), "the report");
        await File.WriteAllTextAsync(Path.Combine(reports, "20261114_0900_00-harvest-decisions.proposed.md"), "the proposals");
        await File.WriteAllTextAsync(Path.Combine(workspace.Root, ".sdlc", "decisions.draft.md"), "a draft");
        await File.WriteAllTextAsync(Path.Combine(workspace.Root, ".sdlc", "areas.draft.md"), "another aspect's draft");
        using TempDirectory review = new();
        FakeAgentProcess agents = new();

        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IAgentProcess>(agents));
        Result<RunRound.Response> ran = await gate.Send<HarvestReaders.Review, RunRound.Response>(
            new HarvestReaders.Review(review.Root, HarvestAspect.Decisions, null, TimeSpan.FromMinutes(1)));

        Assert.IsTrue(ran.IsSuccess, string.Join("; ", ran.Errors.Select(error => error.Message)));
        string alignment = await File.ReadAllTextAsync(Path.Combine(review.Root, "alignment.prompt.md"));
        string completeness = await File.ReadAllTextAsync(Path.Combine(review.Root, "completeness.prompt.md"));
        StringAssert.Contains(alignment, "the proposals");
        StringAssert.Contains(alignment, "a draft");
        Assert.IsFalse(alignment.Contains("another aspect", StringComparison.Ordinal), "Only the aspect's own draft is reviewed.");
        Assert.IsFalse(alignment.Contains("the report", StringComparison.Ordinal), "The first reviewer sees only what was proposed and approved.");
        StringAssert.Contains(completeness, "the report");
        Assert.IsTrue(agents.Launches.All(launch => launch.Arguments.Contains("text") && launch.Output!.EndsWith(".md", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void The_builds_process_names_its_tools_never_bypasses_and_is_not_a_reader()
    {
        // D-183: never with permission checks off; the tools named last, because the option takes every word after it.
        IReadOnlyList<string> arguments = BuildRun.ArgumentsFor(Cycle(), "/plugin");

        Assert.IsFalse(arguments.Any(argument => argument.Contains("bypass", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(arguments.Contains("--permission-mode"));
        CollectionAssert.Contains(arguments.ToList(), "/sdlc:build 20260929_1456_32 --unattended");
        CollectionAssert.AreEqual(BuildRun.AllowedTools.ToList(), arguments.Skip(arguments.ToList().IndexOf("--allowedTools") + 1).ToList());
    }

    [TestMethod]
    public async Task The_build_is_launched_in_its_worktree_unmarked_with_nothing_to_read()
    {
        using TempDirectory worktree = new();
        FakeAgentProcess agents = new();

        using var gate = Gate.For(worktree.Root, services => services.AddSingleton<IAgentProcess>(agents));
        Result<AgentExit> exit = await gate.Send<BuildRun.Launch, AgentExit>(new BuildRun.Launch(Cycle(), worktree.Root, "/plugin"));

        Assert.IsTrue(exit.IsSuccess);
        AgentLaunch launch = agents.Launches.Single();
        Assert.IsFalse(launch.Reader, "The mark would let a red build end its turn.");
        Assert.AreEqual(worktree.Root, launch.WorkingDirectory);
        Assert.IsNull(launch.Input);
    }

    [TestMethod]
    [DataRow("git switch --no-track -c cycle/x/build-unattended slices/sandbox-x/plan", "worktree add --no-track -b cycle/x/build-unattended \"/w\" slices/sandbox-x/plan")]
    [DataRow("git switch cycle/x/build-unattended", "worktree add \"/w\" cycle/x/build-unattended")]
    public void A_branch_start_becomes_a_worktree(string command, string expected)
    {
        Result<IReadOnlyList<string>> commands = BuildRun.WorktreeCommands(new StartBranch.Response("b", command, "why"), "/w");

        Assert.IsTrue(commands.IsSuccess);
        Assert.AreEqual(expected, commands.Value[0]);
    }

    [TestMethod]
    public void A_release_start_takes_its_documents_into_the_worktree()
    {
        Result<IReadOnlyList<string>> commands = BuildRun.WorktreeCommands(
            new StartBranch.Response("b", "git switch --no-track -c cycle/x/build release/1 && git checkout cycle/x/plan -- .sdlc/cycles/x", "why"),
            "/w");

        CollectionAssert.AreEqual(
            new[] { "worktree add --no-track -b cycle/x/build \"/w\" release/1", "-C \"/w\" checkout cycle/x/plan -- .sdlc/cycles/x" },
            commands.Value.ToArray());
    }

    [TestMethod]
    public async Task Remediation_reuses_the_builds_worktree()
    {
        using var workspace = CycleWorkspace.Create();
        FakeRepository git = new();
        git.WorktreeList.Add(("/elsewhere/builds/x", Cycle().BranchName(CycleBranchKind.BuildUnattended)));

        BuildRun.Place place = await Prepare(workspace, git, remediate: true);

        Assert.AreEqual("/elsewhere/builds/x", place.Path);
        Assert.IsFalse(place.Opened);
        Assert.AreEqual(0, git.Ran.Count);
    }

    [TestMethod]
    public async Task Remediation_opens_a_worktree_from_the_remotes_build_branch_when_there_is_no_local_one()
    {
        // D-185: a build on another machine pushed it; the person changes it here.
        using var workspace = CycleWorkspace.Create();
        FakeRepository git = new();
        string branch = Cycle().BranchName(CycleBranchKind.BuildUnattended);
        git.Refs.Add("origin/" + branch);

        BuildRun.Place place = await Prepare(workspace, git, remediate: true);

        Assert.IsTrue(place.Opened);
        StringAssert.StartsWith(git.Ran.Single().Arguments, "worktree add -b " + branch + " ");
        StringAssert.EndsWith(git.Ran.Single().Arguments, " origin/" + branch);
    }

    [TestMethod]
    public void A_superseded_rule_points_through_its_replacement_to_the_rule_in_force()
    {
        // CON_19 was replaced by CON_24, which CON_27 replaced: nobody is sent to a rule kept only for the record.
        string rendered = ConstraintRenderer.Render(Library(), ConstraintDetail.Full);

        StringAssert.Contains(rendered, "CON_19     replaced by CON_24 -> CON_27");
    }

    private static EffectiveConstraints.Response Library()
    {
        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(File.ReadAllText(RepoPaths.Library), RepoPaths.Library);
        Assert.IsTrue(parsed.IsSuccess);

        return new EffectiveConstraints.Response(
            [.. parsed.Value.Where(rule => rule.IsInForce)],
            [new ConstraintDocument(ConstraintTier.Global, "CONSTRAINTS.md", string.Empty)],
            [.. parsed.Value.Where(rule => !rule.IsInForce)]);
    }

    private static CycleId Cycle()
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));
        return id.Value;
    }

    private static string Name(AgentLaunch launch) =>
        Path.GetFileName(launch.Input!)[..^RunRound.PromptSuffix.Length];

    private static async Task<RunRound.Response> Run(string directory, FakeAgentProcess agents)
    {
        using var gate = Gate.For(directory, services => services.AddSingleton<IAgentProcess>(agents));
        Result<RunRound.Response> ran = await gate.Send<RunRound.Command, RunRound.Response>(
            new RunRound.Command(directory, TimeSpan.Zero, TimeSpan.FromMinutes(1)));

        Assert.IsTrue(ran.IsSuccess, string.Join("; ", ran.Errors.Select(error => error.Message)));
        return ran.Value;
    }

    private static async Task<BuildRun.Place> Prepare(CycleWorkspace workspace, FakeRepository git, bool remediate)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        Result<BuildRun.Place> place = await gate.Send<BuildRun.Prepare, BuildRun.Place>(new BuildRun.Prepare(Cycle(), remediate));

        Assert.IsTrue(place.IsSuccess, string.Join("; ", place.Errors.Select(error => error.Message)));
        return place.Value;
    }

    /// <summary>Starts nothing: records each launch and writes an answer where one is expected.</summary>
    private sealed class FakeAgentProcess : IAgentProcess
    {
        private readonly object _gate = new();

        public List<AgentLaunch> Launches { get; } = [];

        /// <summary>Readers that exit cleanly and write nothing.</summary>
        public HashSet<string> Silent { get; init; } = [];

        /// <summary>Whether there is no host to start.</summary>
        public bool NoHost { get; init; }

        public Task<AgentExit> Run(AgentLaunch launch, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                Launches.Add(launch);
            }

            if (NoHost)
            {
                return Task.FromResult(new AgentExit(-1, false, "claude is not on the PATH.", NeverStarted: true));
            }

            string? name = launch.Input is null ? null : Name(launch);

            if (launch.Output is not null && (name is null || !Silent.Contains(name)))
            {
                return Answer(launch.Output, cancellationToken);
            }

            return Task.FromResult(new AgentExit(0, false, null));
        }

        private static async Task<AgentExit> Answer(string output, CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(output, "{}", cancellationToken);
            return new AgentExit(0, false, null);
        }
    }

    /// <summary>A directory that is gone after the test.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("sdlc-agents-").FullName;

        public void Write(string name, string text) => File.WriteAllText(Path.Combine(Root, name), text);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
