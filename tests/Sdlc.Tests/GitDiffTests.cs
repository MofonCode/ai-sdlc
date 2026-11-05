using System.Diagnostics;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// <see cref="GitRepository.Diff"/> against a real repository.
/// </summary>
/// <remarks>
/// <strong>The one place in this suite that builds a real repository, and it earns it.</strong>
/// Everything else uses <see cref="FakeRepository"/>, because what is worth testing is what the
/// framework does with git's answers rather than git itself. This is the exception: the defect was
/// which question the framework asked. <c>Diff</c> ran <c>git diff &lt;base&gt;</c>, which cannot see an
/// untracked file, while <c>ChangedFiles</c> asked <c>ls-files --others</c> as well -- so for every
/// cycle that created a file, and that is every first cycle, <c>diff.patch</c> was empty while the
/// comparison reported agreement. A fake answers both questions from one field and so can never
/// disagree with itself, which is precisely the bug.
/// </remarks>
[TestClass]
public sealed class GitDiffTests
{
    [TestMethod]
    public void A_patch_names_a_file_git_is_not_yet_tracking()
    {
        using var repo = Repo.Create();
        string head = repo.Git("rev-parse HEAD").Trim();

        File.WriteAllText(Path.Combine(repo.Root, "Roll.cs"), "public static class Roll { }\n");

        string? patch = new GitRepository().Diff(repo.Root, head, []);

        Assert.IsNotNull(patch);
        StringAssert.Contains(patch, "Roll.cs", "An untracked new file is part of the change.");
        StringAssert.Contains(patch, "public static class Roll", "And so are its lines.");
    }

    [TestMethod]
    public void Diff_and_ChangedFiles_answer_the_same_question()
    {
        // The property that was broken, asserted as a property. One generous notion of "changed" and
        // one strict one, with the reassuring line coming from the generous one and the artifact a
        // reviewer depends on coming from the strict one.
        using var repo = Repo.Create();
        string head = repo.Git("rev-parse HEAD").Trim();

        File.WriteAllText(Path.Combine(repo.Root, "New.cs"), "// created, never staged\n");
        File.WriteAllText(Path.Combine(repo.Root, "Tracked.cs"), "// edited\n");
        repo.Git("add Tracked.cs");
        repo.Git("commit -m tracked");
        head = repo.Git("rev-parse HEAD~1").Trim();
        File.WriteAllText(Path.Combine(repo.Root, "Tracked.cs"), "// edited again\n");

        var git = new GitRepository();
        string? patch = git.Diff(repo.Root, head, []);
        IReadOnlyList<string> changed = git.ChangedFiles(repo.Root, head);

        Assert.IsNotNull(patch);

        foreach (string path in changed)
        {
            StringAssert.Contains(
                patch,
                path,
                $"{path} is in the changed list and not in the patch, which is how an empty diff.patch "
                + "came to sit beside a panel saying everything was in order.");
        }
    }

    [TestMethod]
    public void Recording_a_diff_leaves_no_content_staged()
    {
        // --intent-to-add stages the path and none of the content, which is what makes it safe to run
        // against a person's working tree. If it staged content, `sdlc evidence` would silently decide
        // what their next commit contains.
        using var repo = Repo.Create();
        string head = repo.Git("rev-parse HEAD").Trim();

        File.WriteAllText(Path.Combine(repo.Root, "Roll.cs"), "public static class Roll { }\n");

        _ = new GitRepository().Diff(repo.Root, head, []);

        Assert.AreEqual(
            string.Empty,
            repo.Git("diff --cached --name-only").Trim(),
            "Nothing of the file's content may be staged.");
    }

    [TestMethod]
    public void An_excluded_path_is_absent_and_everything_else_is_still_there()
    {
        // A real repository because the question is what git was asked. A pathspec list of nothing but
        // exclusions matches nothing, so `-- :(exclude).sdlc` alone yields an empty patch rather than
        // everything else -- which would be the same silent empty diff.patch this file already exists
        // for, arriving by a different route.
        using var repo = Repo.Create();
        string head = repo.Git("rev-parse HEAD").Trim();

        Directory.CreateDirectory(Path.Combine(repo.Root, ".sdlc", "cycles", "20260929_1456_32"));
        File.WriteAllText(
            Path.Combine(repo.Root, ".sdlc", "cycles", "20260929_1456_32", "plan.md"),
            "# the plan a verify must not mistake for the change\n");
        File.WriteAllText(Path.Combine(repo.Root, "Roll.cs"), "public static class Roll { }\n");

        string? patch = new GitRepository().Diff(repo.Root, head, [".sdlc"]);

        Assert.IsNotNull(patch);
        StringAssert.Contains(patch, "Roll.cs", "The change itself still has to be in the patch.");
        StringAssert.Contains(patch, "public static class Roll", "And so do its lines.");
        Assert.IsFalse(
            patch.Contains("plan.md", StringComparison.Ordinal),
            "A verify reads this patch and nothing else, so the plan it is checked against may not be in "
            + "it. The patch was: " + patch);
    }

    [TestMethod]
    public void Uncommitted_changes_name_an_edit_and_a_new_file_and_never_an_ignored_one()
    {
        // The parse is the risk: porcelain leads an unstaged edit with a space, and a reader that trims
        // each line takes it and the path's first character with it. A fake answers whatever it is told,
        // so only real git can say whether this asked the question right.
        using var repo = Repo.Create();

        File.WriteAllText(Path.Combine(repo.Root, ".gitignore"), "*.draft.md\n");
        repo.Git("add .gitignore");
        repo.Git("commit -m ignore");

        File.WriteAllText(Path.Combine(repo.Root, "README.md"), "# edited\n");
        Directory.CreateDirectory(Path.Combine(repo.Root, "src"));
        File.WriteAllText(Path.Combine(repo.Root, "src", "New.cs"), "// new\n");
        File.WriteAllText(Path.Combine(repo.Root, "solution.draft.md"), "ignored\n");

        IReadOnlyList<string> changed = new GitRepository().UncommittedChanges(repo.Root);

        CollectionAssert.AreEquivalent(new[] { "README.md", "src/New.cs" }, changed.ToArray());
    }

    [TestMethod]
    public void Switching_to_a_new_branch_creates_it_at_what_is_checked_out()
    {
        using var repo = Repo.Create();
        string head = repo.Git("rev-parse HEAD").Trim();

        var git = new GitRepository();

        Assert.IsTrue(git.SwitchToNewBranch(repo.Root, "development").IsSuccess);
        Assert.AreEqual("development", git.CurrentBranch(repo.Root));
        Assert.AreEqual(head, repo.Git("rev-parse development").Trim());
        Assert.IsTrue(git.SwitchToNewBranch(repo.Root, "development").IsFailed, "A branch that exists is not created twice.");
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
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, $"git {arguments}: {error}");
            return output;
        }

        public void Dispose()
        {
            // Git writes its object files read-only on Windows, so a plain recursive delete throws
            // UnauthorizedAccessException. Clear the attribute first, and let a leftover temp
            // directory be a leftover temp directory rather than a failed test.
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
}
