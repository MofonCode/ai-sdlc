using System.Diagnostics;
using FluentResults;
using Sdlc.Core.Harvest;

namespace Sdlc.Tests;

/// <summary>
/// Bringing in a cycle abandoned before any of it merged (D-190), against a real repository.
/// </summary>
/// <remarks>
/// A fake answers <c>restore</c> with nothing, so it cannot say whether the folder arrives, or whether it arrives
/// staged and the close would commit a cycle's documents into a harvest. That is git's answer, so git gives it.
/// </remarks>
[TestClass]
public sealed class GatherAbandonedTests
{
    private const string Id = "20261001_0900_00";

    [TestMethod]
    public async Task The_folder_arrives_from_the_cycles_branch_untracked_and_nothing_is_staged()
    {
        using var repo = Repo.Create();
        repo.Git("switch -q -c cycle/" + Id + "/plan");
        string folder = Path.Combine(repo.Root, ".sdlc", "cycles", Id);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "intent.md"), "# Intent\n");
        await File.WriteAllTextAsync(Path.Combine(folder, "abandoned.md"), "# Why this cycle stopped\n\nSuperseded.\n");
        repo.Git("add .");
        repo.Git("commit -q -m cycle");
        repo.Git("switch -q development");

        Assert.IsFalse(Directory.Exists(folder), "The integration branch never had it.");

        using var gate = Gate.For(repo.Root);
        Result<GatherAbandoned.Response> gathered = await gate.Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command());

        Assert.IsTrue(gathered.IsSuccess, string.Join("; ", gathered.Errors.Select(error => error.Message)));
        Assert.AreEqual("cycle/" + Id + "/plan", gathered.Value.Cycles.Single().Branch);
        Assert.IsTrue(File.Exists(Path.Combine(folder, "abandoned.md")));
        Assert.AreEqual(string.Empty, repo.Git("diff --cached --name-only").Trim(), "Nothing is staged.");
        StringAssert.Contains(repo.Git("status --porcelain"), "?? .sdlc/", "The folder is untracked.");
        Assert.AreEqual(0, (await gate.Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command())).Value.Cycles.Count, "Brought in once.");
    }

    private sealed class Repo : IDisposable
    {
        private Repo(string root) => Root = root;

        public string Root { get; }

        public static Repo Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-git", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var repo = new Repo(root);
            repo.Git("init -q -b development");
            repo.Git("config user.email suite@example.invalid");
            repo.Git("config user.name Suite");
            File.WriteAllText(Path.Combine(root, "README.md"), "# scratch\n");
            repo.Git("add README.md");
            repo.Git("commit -q -m first");
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
            // Git writes its object files read-only on Windows: clear the attribute, and let a leftover temp
            // directory be a leftover rather than a failed test.
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
