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

    [TestMethod]
    public async Task A_rejected_series_brings_in_its_folder_and_every_cycle_it_ran_from_its_branch_abandoned_for_harvest()
    {
        // A series rejected at its pull request never merges, so its cycles live only on series/<id>. One abandonment
        // placed beside its record there covers them all, and a harvest reads them from that branch.
        const string Run = "20261121_0930_15";
        const string Successor = "20261121_0900_01";
        using var repo = Repo.Create();
        repo.Git("switch -q -c series/" + Run);
        string series = Path.Combine(repo.Root, ".sdlc", "series", Run);
        Directory.CreateDirectory(series);
        await File.WriteAllTextAsync(
            Path.Combine(series, "series.json"),
            "{ \"id\": \"" + Run + "\", \"started\": \"2026-09-30T08:00:00+00:00\", \"limit\": 2, \"approval\": \"none\", \"waves\": [ { \"number\": 1, \"started\": \"2026-09-30T08:00:00+00:00\", \"items\": [ { \"kind\": \"parked\", \"ref\": \"" + Id + "\", \"cycle\": \"" + Id + "\" }, { \"kind\": \"successor\", \"ref\": \"totals\", \"cycle\": null } ] } ] }\n");

        foreach ((string cycle, string lead) in new[] { (Id, string.Empty), (Successor, "lead: successor:totals\n") })
        {
            string folder = Path.Combine(repo.Root, ".sdlc", "cycles", cycle);
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "intent.md"), "---\ncycle: " + cycle + "\n" + lead + "---\n\n# Intent\n");
        }

        repo.Git("add .");
        repo.Git("commit -q -m series");
        await File.WriteAllTextAsync(Path.Combine(series, "abandoned.md"), "---\nseries: " + Run + "\nby: Suite\nat: 2026-11-21T12:00:00Z\n---\n\n# Why this series stopped\n\nThe wrong shape.\n");
        repo.Git("add .");
        repo.Git("commit -q -m rejected");
        repo.Git("switch -q development");

        Assert.IsFalse(Directory.Exists(series), "The integration branch never had it.");

        using var gate = Gate.For(repo.Root);
        Result<GatherAbandoned.Response> gathered = await gate.Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command());

        Assert.IsTrue(gathered.IsSuccess, string.Join("; ", gathered.Errors.Select(error => error.Message)));
        CollectionAssert.AreEquivalent(new[] { Id, Successor }, gathered.Value.Cycles.Select(cycle => cycle.Cycle.ToString()).ToArray());
        Assert.IsTrue(gathered.Value.Cycles.All(cycle => cycle.Branch == "series/" + Run));
        Assert.IsTrue(File.Exists(Path.Combine(series, "abandoned.md")));
        Assert.AreEqual(string.Empty, repo.Git("diff --cached --name-only").Trim(), "Nothing is staged.");

        foreach (Sdlc.Core.Cycles.CycleId id in gathered.Value.Cycles.Select(cycle => cycle.Cycle))
        {
            Sdlc.Core.Cycles.Cycle cycle = new(id, repo.Root);
            Assert.IsTrue(cycle.IsAbandoned, id + " reads as abandoned by its series.");
            Assert.IsTrue(await Completion.IsFinished(cycle, CancellationToken.None));
            Assert.AreEqual("series/" + Run, Completion.AbandonedOn(new Sdlc.Core.Git.GitRepository(), repo.Root, "development", id));
        }

        Assert.AreEqual(0, (await gate.Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command())).Value.Cycles.Count, "Brought in once.");
    }

    [TestMethod]
    public async Task A_rejected_series_leaves_a_cycle_on_the_integration_branch_as_the_integration_branch_holds_it()
    {
        // An approved cycle on the integration branch that the series took and built: the series' copy of it holds a
        // verdict nobody accepted. Restoring that over the integration branch's would overwrite the cycle with it, and
        // abandoning it would stop work the person approved (D-208). Opened after the series started, so only git can
        // tell it came from the integration branch.
        const string Run = "20261121_0930_15";
        const string Approved = "20261121_1000_05";
        const string Own = "20261121_1000_06";
        using var repo = Repo.Create();
        string approved = Path.Combine(repo.Root, ".sdlc", "cycles", Approved);
        Directory.CreateDirectory(approved);
        await File.WriteAllTextAsync(Path.Combine(approved, "intent.md"), "---\ncycle: " + Approved + "\n---\n\n# Intent\n");
        repo.Git("add .");
        repo.Git("commit -q -m approved");

        repo.Git("switch -q -c series/" + Run);
        string series = Path.Combine(repo.Root, ".sdlc", "series", Run);
        Directory.CreateDirectory(series);
        await File.WriteAllTextAsync(
            Path.Combine(series, "series.json"),
            "{ \"id\": \"" + Run + "\", \"started\": \"2026-09-30T08:00:00+00:00\", \"limit\": 1, \"approval\": \"required\", \"waves\": [ { \"number\": 1, \"started\": \"2026-09-30T08:00:00+00:00\", \"items\": [ { \"kind\": \"approved\", \"ref\": \"" + Approved + "\", \"cycle\": \"" + Approved + "\" }, { \"kind\": \"parked\", \"ref\": \"" + Own + "\", \"cycle\": \"" + Own + "\" } ] } ] }\n");
        await File.WriteAllTextAsync(Path.Combine(approved, "verify.md"), "---\ncycle: " + Approved + "\noutcome: pass\n---\n\n# v\n");
        string own = Path.Combine(repo.Root, ".sdlc", "cycles", Own);
        Directory.CreateDirectory(own);
        await File.WriteAllTextAsync(Path.Combine(own, "intent.md"), "---\ncycle: " + Own + "\n---\n\n# Intent\n");
        await File.WriteAllTextAsync(Path.Combine(series, "abandoned.md"), "---\nseries: " + Run + "\nby: Suite\nat: 2026-11-21T12:00:00Z\n---\n\n# Why this series stopped\n\nThe wrong shape.\n");
        repo.Git("add .");
        repo.Git("commit -q -m rejected");
        repo.Git("switch -q development");

        using var gate = Gate.For(repo.Root);
        Result<GatherAbandoned.Response> gathered = await gate.Send<GatherAbandoned.Command, GatherAbandoned.Response>(new GatherAbandoned.Command());

        Assert.IsTrue(gathered.IsSuccess, string.Join("; ", gathered.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { Own }, gathered.Value.Cycles.Select(cycle => cycle.Cycle.ToString()).ToArray());
        Assert.IsFalse(File.Exists(Path.Combine(approved, "verify.md")), "The integration branch's copy is not overwritten.");
        Assert.IsTrue(Sdlc.Core.Cycles.CycleId.TryParse(Approved, out Sdlc.Core.Cycles.CycleId? id));
        Assert.IsNull(Completion.AbandonedOn(new Sdlc.Core.Git.GitRepository(), repo.Root, "development", id.Value), "Nor abandoned by the series.");
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
