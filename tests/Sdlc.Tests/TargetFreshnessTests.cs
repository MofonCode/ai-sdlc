using System.Diagnostics;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// <see cref="TargetFreshness"/> against real repositories: a remote, the clone a pull request would come from, and
/// a second clone that moves the target while the first is not looking (D-227).
/// </summary>
/// <remarks>
/// <strong>Real git, because the defect this guards is a stale copy of the target.</strong> A fake answers from
/// whatever it was told the target is, so it could never show a branch judged current against a local
/// <c>origin/development</c> that the remote had already moved past -- which is the whole case.
/// </remarks>
[TestClass]
public sealed class TargetFreshnessTests
{
    private const string Target = "development";

    [TestMethod]
    public void A_branch_holding_the_targets_head_is_up_to_date()
    {
        using var remote = Remote.Create();
        remote.Work.Git("switch -c cycle/1/build");
        remote.Work.Commit("src/Roll.cs", "public static class Roll { }\n");

        FreshnessCheck check = TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target);

        Assert.AreEqual(Freshness.UpToDate, check.Standing);
        Assert.AreEqual("cycle/1/build", check.Branch);
        StringAssert.Contains(TargetFreshness.Render(check), "has everything development has on origin");
    }

    [TestMethod]
    public void A_target_moved_on_the_remote_is_fetched_before_it_is_compared()
    {
        // The local origin/development still points where the branch started; only a fetch shows it moved.
        using var remote = Remote.Create();
        remote.Work.Git("switch -c cycle/1/build");
        remote.Work.Commit("src/Roll.cs", "public static class Roll { }\n");
        remote.MoveTarget("src/Other.cs", "public static class Other { }\n");

        FreshnessCheck check = TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target);

        Assert.AreEqual(Freshness.Behind, check.Standing, "Judged against the stale local copy, this would read as up to date.");
        Assert.AreEqual(1, check.Behind);
        Assert.IsFalse(check.RecordsOnly, "The branch carries code.");
        Assert.IsEmpty(check.Conflicts, "The two changes touch different files.");
    }

    [TestMethod]
    public void A_branch_of_records_alone_is_told_to_merge_and_ask_again()
    {
        using var remote = Remote.Create();
        remote.Work.Git("switch -c cycle/1/plan");
        remote.Work.Commit(".sdlc/cycles/1/plan.md", "# plan\n");
        remote.MoveTarget("src/Other.cs", "public static class Other { }\n");

        FreshnessCheck check = TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target);
        string said = TargetFreshness.Render(check);

        Assert.AreEqual(Freshness.Behind, check.Standing);
        Assert.IsTrue(check.RecordsOnly);
        StringAssert.Contains(said, "git merge origin/development");
        StringAssert.Contains(said, "never rebase");
        Assert.IsFalse(said.Contains("remediate", StringComparison.Ordinal), "Nothing was built, so there is nothing to verify again.");
    }

    [TestMethod]
    public void A_branch_of_built_code_is_sent_to_remediate_and_told_where_the_merge_stops()
    {
        using var remote = Remote.Create();
        remote.Work.Git("switch -c cycle/1/build");
        remote.Work.Commit("README.md", "# ours\n");
        remote.MoveTarget("README.md", "# theirs\n");

        FreshnessCheck check = TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target);
        string said = TargetFreshness.Render(check);

        Assert.AreEqual(Freshness.Behind, check.Standing);
        CollectionAssert.AreEqual(new[] { "README.md" }, check.Conflicts.ToArray());
        StringAssert.Contains(said, "/sdlc:remediate");
        StringAssert.Contains(said, "The merge stops on: README.md");
    }

    [TestMethod]
    public void Merging_the_target_in_makes_the_branch_up_to_date()
    {
        using var remote = Remote.Create();
        remote.Work.Git("switch -c cycle/1/build");
        remote.Work.Commit("src/Roll.cs", "public static class Roll { }\n");
        remote.MoveTarget("src/Other.cs", "public static class Other { }\n");
        Assert.AreEqual(Freshness.Behind, TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target).Standing);

        remote.Work.Git("merge --no-edit origin/development");

        Assert.AreEqual(Freshness.UpToDate, TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target).Standing);
    }

    [TestMethod]
    public void A_remote_that_cannot_be_reached_refuses_rather_than_passing()
    {
        using var remote = Remote.Create();
        remote.Work.Git("switch -c cycle/1/build");
        remote.Work.Git("remote set-url origin \"" + Path.Combine(remote.Root, "gone.git") + "\"");

        FreshnessCheck check = TargetFreshness.Check(new GitRepository(), remote.Work.Root, Target);

        Assert.AreEqual(Freshness.Unchecked, check.Standing, "Nothing was compared, so nothing may be assumed.");
        StringAssert.Contains(TargetFreshness.Render(check), "git fetch origin development");
    }

    [TestMethod]
    public void A_target_the_remote_does_not_have_refuses()
    {
        using var remote = Remote.Create();

        FreshnessCheck check = TargetFreshness.Check(new GitRepository(), remote.Work.Root, "refs/heads/nowhere");

        Assert.AreEqual(Freshness.Unchecked, check.Standing);
        Assert.AreEqual("nowhere", check.Target, "A fully qualified name is read as the bare branch.");
    }

    /// <summary>A bare remote holding <c>development</c>, the clone a pull request comes from, and a second clone.</summary>
    private sealed class Remote : IDisposable
    {
        private Remote(string root)
        {
            Root = root;
            Work = new Clone(Path.Combine(root, "work"));
            Other = new Clone(Path.Combine(root, "other"));
        }

        public string Root { get; }

        public Clone Work { get; }

        private Clone Other { get; }

        public static Remote Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-git", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var remote = new Remote(root);
            string origin = Path.Combine(root, "origin.git");
            Clone.Run(root, $"init --bare -b {Target} \"{origin}\"");

            Clone.Run(root, $"clone \"{origin}\" work");
            remote.Work.Configure();
            remote.Work.Git($"switch -c {Target}");
            remote.Work.Commit("README.md", "# scratch\n");
            remote.Work.Git($"push -u origin {Target}");

            Clone.Run(root, $"clone -b {Target} \"{origin}\" other");
            remote.Other.Configure();
            return remote;
        }

        /// <summary>Another person's merge: a commit on the target, pushed from the second clone.</summary>
        public void MoveTarget(string path, string content)
        {
            Other.Git($"pull --ff-only origin {Target}");
            Other.Commit(path, content);
            Other.Git($"push origin {Target}");
        }

        public void Dispose()
        {
            // Git writes its object files read-only on Windows, so clear the attribute before deleting, and let a
            // leftover temp directory be a leftover rather than a failed test.
            try
            {
                foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class Clone(string root)
    {
        public string Root { get; } = root;

        public void Configure()
        {
            Git("config user.email suite@example.invalid");
            Git("config user.name Suite");
        }

        public void Commit(string path, string content)
        {
            string full = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            Git($"add \"{path}\"");
            Git($"commit -m \"{path}\"");
        }

        public string Git(string arguments) => Run(Root, arguments);

        public static string Run(string workingDirectory, string arguments)
        {
            using Process process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;

            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, $"git {arguments}: {error.Result}");
            return output.Result;
        }
    }
}
