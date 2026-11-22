using System.Diagnostics;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// The patch a verify judged is rebuilt from git and never committed (D-124).
/// </summary>
[TestClass]
public sealed class RebuiltDiffTests
{
    [TestMethod]
    public async Task Code_nobody_committed_is_refused_before_anything_is_written()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);

        Result<RecordEvidence.Response> result = await Record(workspace, new FakeRepository { Uncommitted = ["src/Thing.cs"] });

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "src/Thing.cs");
        StringAssert.Contains(result.Errors[0].Message, "commit the change first");
        Assert.IsFalse(Directory.Exists(workspace.Cycle.EvidenceDirectory), "A refused recording writes nothing.");
    }

    [TestMethod]
    public async Task The_cycles_own_paperwork_and_the_runs_own_output_are_not_the_change()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);
        string log = Path.Combine(workspace.Root, "build.log");
        CycleWorkspace.WriteRaw(log, "Build succeeded.\n");

        Result<RecordEvidence.Response> result = await Record(
            workspace,
            new FakeRepository { Uncommitted = [".sdlc/cycles/" + workspace.Cycle.Id + "/plan.md", "build.log"] },
            buildLog: log);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [TestMethod]
    public async Task The_patch_is_measured_between_the_base_and_the_commit_the_build_is_at()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);
        FakeRepository git = new();

        Result<RecordEvidence.Response> result = await Record(workspace, git);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual((git.Base!, git.Head!), git.Between);
        Assert.AreEqual(git.Head + Environment.NewLine, await File.ReadAllTextAsync(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.HeadFileName)));
        CollectionAssert.Contains(result.Value.Written.ToArray(), RecordEvidence.HeadFileName);
    }

    [TestMethod]
    public async Task The_evidence_hash_reads_the_rebuilt_patch_whatever_is_on_disk()
    {
        // A fresh clone has no patch, and a working copy may hold a stale one; both hash the commits.
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);
        FakeRepository git = new();
        Assert.IsTrue((await Record(workspace, git)).IsSuccess);

        string diff = Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.DiffFileName);
        string? recorded = await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root);

        File.Delete(diff);
        Assert.AreEqual(recorded, await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root));

        await File.WriteAllTextAsync(diff, "a stale copy");
        Assert.AreEqual(recorded, await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root));

        git.CommittedPatch = "diff --git a/src/Other.cs b/src/Other.cs\n";
        Assert.AreNotEqual(recorded, await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root), "Different commits are different evidence.");
    }

    [TestMethod]
    public async Task Evidence_recorded_before_head_was_named_is_hashed_from_its_committed_patch()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);
        FakeRepository git = new();
        Assert.IsTrue((await Record(workspace, git)).IsSuccess);
        File.Delete(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.HeadFileName));

        string? withGit = await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root);
        git.CommittedPatch = "anything";

        Assert.AreEqual(await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None), withGit);
        Assert.AreEqual(withGit, await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root), "Git is not asked.");
    }

    [TestMethod]
    public async Task A_checkout_without_the_patch_gets_it_back_from_the_two_commits()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs: 1);
        FakeRepository git = new();
        Assert.IsTrue((await Record(workspace, git)).IsSuccess);
        string diff = Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.DiffFileName);
        File.Delete(diff);

        await EvidenceDiff.Restore(workspace.Cycle, git, workspace.Root, CancellationToken.None);

        Assert.AreEqual(git.Patch, await File.ReadAllTextAsync(diff));
    }

    [TestMethod]
    public void The_rebuilt_patch_is_the_one_the_build_recorded_and_leaves_the_cycle_out()
    {
        // Real git, because the claim is about git: once the change is committed, the diff between the two
        // commits is the diff to the working tree, byte for byte, so a hash taken either way agrees.
        using var repo = Repo.Create();
        string from = repo.Git("rev-parse HEAD").Trim();

        Directory.CreateDirectory(Path.Combine(repo.Root, ".sdlc", "cycles", "20260929_1456_32"));
        File.WriteAllText(Path.Combine(repo.Root, ".sdlc", "cycles", "20260929_1456_32", "plan.md"), "# the plan\n");
        File.WriteAllText(Path.Combine(repo.Root, "Roll.cs"), "public static class Roll { }\n");
        File.WriteAllText(Path.Combine(repo.Root, "README.md"), "# edited\n");
        repo.Git("add -A");
        repo.Git("commit -m change");

        var git = new GitRepository();
        string? until = git.CommitOf(repo.Root, "HEAD");
        string? working = git.Diff(repo.Root, from, [".sdlc"]);
        string? between = git.DiffBetween(repo.Root, from, until!, [".sdlc"]);

        Assert.AreEqual(40, until!.Length);
        Assert.IsNotNull(between);
        Assert.AreEqual(working, between);
        StringAssert.Contains(between, "public static class Roll");
        Assert.IsFalse(between.Contains("plan.md", StringComparison.Ordinal));
        Assert.IsNull(git.DiffBetween(repo.Root, from, "0000000000000000000000000000000000000000", [".sdlc"]), "A commit this clone lacks is no patch, not an empty one.");
    }

    [TestMethod]
    public void A_merge_tree_is_a_tree_and_the_paths_it_could_not_settle()
    {
        // Real git, because the claim is about how its output is read: the tree first, each conflicted path after it.
        using var repo = Repo.Create();
        repo.Git("branch -M development");
        repo.Git("switch -c run");
        File.WriteAllText(Path.Combine(repo.Root, "Run.cs"), "// run\n");
        File.WriteAllText(Path.Combine(repo.Root, "README.md"), "# run\n");
        repo.Git("add -A");
        repo.Git("commit -m run");
        repo.Git("switch development");
        File.WriteAllText(Path.Combine(repo.Root, "Other.cs"), "// other\n");
        repo.Git("add -A");
        repo.Git("commit -m other");
        string other = repo.Git("rev-parse HEAD").Trim();
        File.WriteAllText(Path.Combine(repo.Root, "README.md"), "# development\n");
        repo.Git("add -A");
        repo.Git("commit -m clash");

        var git = new GitRepository();
        (string Tree, IReadOnlyList<string> Conflicts)? clean = git.MergeTree(repo.Root, "run", other);
        (string Tree, IReadOnlyList<string> Conflicts)? clash = git.MergeTree(repo.Root, "run", "development");

        Assert.IsNotNull(clean);
        Assert.AreEqual(0, clean.Value.Conflicts.Count);
        StringAssert.Contains(git.CommittedChanges(repo.Root, "run", clean.Value.Tree)!.Single(), "Other.cs");
        Assert.IsNotNull(clash);
        CollectionAssert.AreEqual(new[] { "README.md" }, clash.Value.Conflicts.ToArray());
        Assert.IsNull(git.MergeTree(repo.Root, "run", "0000000000000000000000000000000000000000"));
    }

    [TestMethod]
    public async Task A_cycle_built_after_the_run_merged_the_integration_branch_records_only_its_own_change()
    {
        // Real git end to end: one cycle on the run's branch, a change merged in from the integration branch, then the
        // next cycle. Its patch holds its own file, never the merged one nor the cycle before it's, and a checkout
        // without the patch rebuilds the same one from what the evidence records.
        using var repo = Repo.Create();
        repo.Git("branch -M development");
        repo.Git("switch -c series/20261121_0930_15");
        const string First = "20261121_1000_00";
        const string Second = "20261121_1000_01";

        await Cycle(repo, First, "First.cs");
        Assert.IsTrue((await RecordReal(repo, First)).IsSuccess);
        repo.Git("add -A");
        repo.Git("commit -m first-evidence");

        repo.Git("switch development");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, "Merged.cs"), "// from the integration branch\n");
        repo.Git("add -A");
        repo.Git("commit -m merged");
        repo.Git("switch series/20261121_0930_15");
        repo.Git("merge --no-edit development");

        await Cycle(repo, Second, "Second.cs");
        Result<RecordEvidence.Response> recorded = await RecordReal(repo, Second);

        Assert.IsTrue(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(e => e.Message)));
        string evidence = Path.Combine(repo.Root, ".sdlc", "cycles", Second, "evidence");
        string patch = await File.ReadAllTextAsync(Path.Combine(evidence, RecordEvidence.DiffFileName));
        StringAssert.Contains(patch, "Second.cs");
        Assert.IsFalse(patch.Contains("Merged.cs", StringComparison.Ordinal), patch);
        Assert.IsFalse(patch.Contains("First.cs", StringComparison.Ordinal), patch);
        CollectionAssert.DoesNotContain(recorded.Value.Changed.ToArray(), "Merged.cs");
        Assert.IsTrue(File.Exists(Path.Combine(evidence, RecordEvidence.MergedFileName)));

        Assert.IsTrue(Sdlc.Core.Cycles.CycleId.TryParse(Second, out Sdlc.Core.Cycles.CycleId? id));
        Sdlc.Core.Cycles.Cycle cycle = new(id.Value, repo.Root);
        Assert.AreEqual(patch, await EvidenceDiff.Rebuild(cycle, new GitRepository(), repo.Root, CancellationToken.None));
        CollectionAssert.AreEqual(
            new[] { "Second.cs" },
            (await EvidenceDiff.ChangedFiles(cycle, new GitRepository(), repo.Root, CancellationToken.None))!.Where(path => !path.StartsWith(".sdlc/", StringComparison.Ordinal)).ToArray(),
            "Outside .sdlc/, the change is the cycle's own file alone.");
    }

    private static async Task Cycle(Repo repo, string id, string file)
    {
        string folder = Path.Combine(repo.Root, ".sdlc", "cycles", id);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "plan.md"), "---\ncycle: " + id + "\n---\n\n# Plan\n");
        await File.WriteAllTextAsync(Path.Combine(repo.Root, file), "// " + id + "\n");
        repo.Git("add -A");
        repo.Git("commit -m " + id);
    }

    private static async Task<Result<RecordEvidence.Response>> RecordReal(Repo repo, string id)
    {
        Assert.IsTrue(Sdlc.Core.Cycles.CycleId.TryParse(id, out Sdlc.Core.Cycles.CycleId? parsed));
        using var gate = Gate.For(repo.Root);
        return await gate.Send<RecordEvidence.Command, RecordEvidence.Response>(new RecordEvidence.Command(parsed.Value));
    }

    private static async Task<Result<RecordEvidence.Response>> Record(CycleWorkspace workspace, FakeRepository git, string? buildLog = null)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));
        return await gate.Send<RecordEvidence.Command, RecordEvidence.Response>(new RecordEvidence.Command(workspace.Cycle.Id, BuildLog: buildLog));
    }

    /// <summary>A throwaway git repository with one commit in it.</summary>
    private sealed class Repo : IDisposable
    {
        private Repo(string root) => Root = root;

        public string Root { get; }

        public static Repo Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-git", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var repo = new Repo(root);
            repo.Git("init");
            repo.Git("config user.email suite@example.invalid");
            repo.Git("config user.name Suite");
            repo.Git("config core.autocrlf false");
            File.WriteAllText(Path.Combine(root, "README.md"), "# scratch\n");
            repo.Git("add README.md");
            repo.Git("commit -m first");
            return repo;
        }

        public string Git(string arguments)
        {
            using Process process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}
