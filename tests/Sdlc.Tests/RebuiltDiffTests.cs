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
