using System.Diagnostics;
using FluentResults;
using Sdlc.Core;
using Sdlc.Core.Factory;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// <c>sdlc upgrade apply</c> (D-213): the release being moved to moves the solution, in a real repository, from files an
/// older release wrote with a team's own beside them.
/// </summary>
[TestClass]
public sealed class ApplyUpgradeTests
{
    private const string Integration = "development";
    private const string Older = "0.51.0";

    [TestMethod]
    public async Task It_moves_a_solution_an_older_release_wrote_and_the_move_is_one_the_upgrade_check_accepts()
    {
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt();
        using var gate = Gate.For(repo.Root);
        string seed = repo.Git($"rev-parse {Integration}").Trim();

        ApplyUpgrade.Response move = await Apply(gate);

        Assert.AreEqual(seed, repo.Git($"rev-parse {Integration}").Trim(), "The integration branch is left as it was.");
        Assert.AreEqual(Older, move.From);
        Assert.AreEqual(Release.Version, move.To);
        Assert.AreEqual(ApplyUpgrade.BranchFor(Release.Version), move.Branch);
        Assert.IsTrue(move.Holds, ApplyUpgrade.Render(move));
        Assert.AreEqual(move.Branch, repo.Git("branch --show-current").Trim(), "Committed on a branch of its own.");
        Assert.AreEqual(ApplyUpgrade.Title(Release.Version), repo.Git("log -1 --format=%s").Trim());

        // Each move committed whole, the old path as gone, so the check judges the move rather than a new file.
        CollectionAssert.IsSubsetOf(new[] { "azure-pipelines.yml", "azure-pipelines.factory.yml", "azure-pipelines.upgrade.yml" }, move.Committed.ToList());
        Assert.IsTrue(move.Verdict!.Checks.Any(check => check.Name == "azure-pipelines.factory.yml moved to .azuredevops/sdlc-factory.yml" && check.Passed), ApplyUpgrade.Render(move));

        // The factory moved from where 0.51 kept it, merged, and an older release's step left out.
        Assert.IsFalse(File.Exists(Path.Combine(repo.Root, "azure-pipelines.factory.yml")));
        string factory = await File.ReadAllTextAsync(Path.Combine(repo.Root, ".azuredevops", "sdlc-factory.yml"));
        Assert.IsFalse(factory.Contains("build the queue", StringComparison.Ordinal));
        StringAssert.Contains(factory, "name: Team-Pool");

        // The chain check merged rather than printed, the team's step kept.
        string verify = await File.ReadAllTextAsync(Path.Combine(repo.Root, ".azuredevops", "sdlc-verify.yml"));
        Assert.IsFalse(verify.Contains("--old", StringComparison.Ordinal));
        StringAssert.Contains(verify, "echo the team's own");

        Assert.AreEqual(Release.Version, PinnedRelease.Find(repo.Root));
        CollectionAssert.Contains(move.Changes.Select(change => change.What).ToList(), "build the queue", "A move from 0.51 takes what 0.55 changed.");
        CollectionAssert.DoesNotContain(move.Changes.Select(change => change.What).ToList(), "install the gate", "Nor what was changed before 0.51.");
        Assert.AreEqual(string.Empty, repo.Git("status --porcelain").Trim(), "Nothing it wrote is left out of the commit: " + string.Join(", ", move.LeftOut));
    }

    [TestMethod]
    public async Task Run_again_once_the_move_has_merged_it_has_nothing_to_do()
    {
        // Each change is read from the files, so a solution already moved takes nothing.
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt();
        using var gate = Gate.For(repo.Root);
        ApplyUpgrade.Response first = await Apply(gate);
        repo.Git($"switch -q {Integration}");
        repo.Git($"merge -q --ff-only {first.Branch}");

        ApplyUpgrade.Response again = await Apply(gate);

        Assert.IsNull(again.Branch, ApplyUpgrade.Render(again));
        Assert.AreEqual(0, again.Committed.Count);
        StringAssert.Contains(ApplyUpgrade.Render(again), "writes nothing new here");
    }

    [TestMethod]
    public async Task It_is_taken_from_the_integration_branch()
    {
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt();
        repo.Git("switch -q -c somewhere-else");
        using var gate = Gate.For(repo.Root);

        Result<ApplyUpgrade.Response> refused = await gate.Send<ApplyUpgrade.Command, ApplyUpgrade.Response>(new ApplyUpgrade.Command());

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "Switch to " + Integration);
    }

    [TestMethod]
    public async Task An_older_release_does_not_move_a_solution_back()
    {
        // Up only: the way back is reverting the upgrade's pull request.
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt(pinned: "99.0.0");
        using var gate = Gate.For(repo.Root);

        Result<ApplyUpgrade.Response> refused = await gate.Send<ApplyUpgrade.Command, ApplyUpgrade.Response>(new ApplyUpgrade.Command());

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, "revert the pull request");
    }

    [TestMethod]
    public async Task A_change_not_yet_committed_to_a_file_an_upgrade_carries_is_refused_rather_than_mixed_in()
    {
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt();
        await File.AppendAllTextAsync(Path.Combine(repo.Root, ".azuredevops", "sdlc-verify.yml"), "# mine\n");
        using var gate = Gate.For(repo.Root);

        Result<ApplyUpgrade.Response> refused = await gate.Send<ApplyUpgrade.Command, ApplyUpgrade.Response>(new ApplyUpgrade.Command());

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, ".azuredevops/sdlc-verify.yml");
    }

    [TestMethod]
    public async Task The_release_installed_into_the_manifest_first_as_the_upgrade_pipeline_does_is_the_moves_own()
    {
        // The pipeline runs 'dotnet tool update --local' and then this, so the pin has moved before the move starts.
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt();
        await repo.Install(Release.Version);
        using var gate = Gate.For(repo.Root);

        ApplyUpgrade.Response move = await Apply(gate);

        Assert.IsTrue(move.Holds, ApplyUpgrade.Render(move));
        CollectionAssert.Contains(move.Committed.ToList(), PinnedRelease.ManifestPath);
        Assert.AreEqual(Release.Version, PinnedRelease.PinnedIn(repo.Git($"show HEAD:{PinnedRelease.ManifestPath}")));
        Assert.AreEqual(string.Empty, repo.Git("status --porcelain").Trim());
    }

    [TestMethod]
    public async Task A_manifest_changed_beyond_the_release_it_pins_is_still_refused_rather_than_mixed_in()
    {
        using Repo repo = await Repo.AsAnOlderReleaseLeftIt();
        await repo.Install(Release.Version, alsoAddingTool: true);
        using var gate = Gate.For(repo.Root);

        Result<ApplyUpgrade.Response> refused = await gate.Send<ApplyUpgrade.Command, ApplyUpgrade.Response>(new ApplyUpgrade.Command());

        Assert.IsTrue(refused.IsFailed);
        StringAssert.Contains(refused.Errors[0].Message, PinnedRelease.ManifestPath);
    }

    [TestMethod]
    public void The_pull_request_is_opened_by_each_platforms_own_command_line_and_one_already_open_is_found_first()
    {
        ApplyUpgrade.Response move = new(
            Older, "0.56.0", "upgrade/sdlc-0.56.0", Integration, Platform.AzureDevOps, [], [".config/dotnet-tools.json"], [], ["Merged a file."], null);

        IReadOnlyList<(string Tool, string Arguments)> azure = ApplyUpgrade.PullRequestCommands(move, name => name switch
        {
            "BUILD_REPOSITORY_NAME" => "Shop",
            "SYSTEM_COLLECTIONURI" => "https://dev.azure.com/example/",
            "SYSTEM_TEAMPROJECT" => "Shop",
            _ => null,
        });
        IReadOnlyList<(string Tool, string Arguments)> gitHub = ApplyUpgrade.PullRequestCommands(move with { Platform = Platform.GitHub }, _ => null);

        Assert.IsTrue(azure.All(command => command.Tool == "az"));
        StringAssert.StartsWith(azure[1].Arguments, "repos pr list");
        StringAssert.Contains(azure[2].Arguments, "--repository \"Shop\"");
        StringAssert.Contains(azure[2].Arguments, "--target-branch \"development\"");
        StringAssert.Contains(azure[2].Arguments, "Merged a file.", "What the merge carried is in the pull request for its reviewer.");
        StringAssert.StartsWith(gitHub[0].Arguments, "pr list");
        StringAssert.Contains(gitHub[1].Arguments, "--base \"development\" --head \"upgrade/sdlc-0.56.0\"");
        StringAssert.Contains(ApplyUpgrade.Body(move), "revert this pull request", "The way back is said where the move is reviewed.");
    }

    [TestMethod]
    public void Every_path_a_release_change_touches_is_one_an_upgrade_may_carry()
    {
        // A change that reached further would have every solution's next upgrade refused as a change with no chain.
        foreach (ReleaseChanges.Change change in ReleaseChanges.All.Where(change => change.Kind == ReleaseChanges.Kind.PipelineMoved))
        {
            Assert.IsTrue(Sdlc.Core.Chain.VerifyUpgrade.Carries([change.What, change.To!]), change.What);
        }

        CollectionAssert.AreEquivalent(
            new[] { Initialise.PipelinePath(Platform.AzureDevOps), FactoryPipeline.PathOf(Platform.AzureDevOps), UpgradePipeline.PathOf(Platform.AzureDevOps) }.Select(path => path.Replace('\\', '/')).ToList(),
            ReleaseChanges.MovedPipelines.Select(pair => pair.Current).ToList(),
            "Each moved pipeline lands where its owner says it lives.");
        CollectionAssert.AreEqual(
            ReleaseChanges.All.Select(change => change.Release).ToList(),
            ReleaseChanges.All.Select(change => change.Release).OrderBy(release => new Version(release)).ToList(),
            "Oldest first.");
    }

    [TestMethod]
    public void A_move_takes_the_changes_made_after_the_release_it_leaves()
    {
        Assert.AreEqual(ReleaseChanges.All.Count, ReleaseChanges.Between(null, "0.56.0").Count);
        Assert.IsTrue(ReleaseChanges.Between("0.53.4", "0.56.0").All(change => change.Release == "0.55"));
        Assert.AreEqual(0, ReleaseChanges.Between("0.55.1", "0.55.3").Count, "A build within a line moves nothing a release changed.");
    }

    private static async Task<ApplyUpgrade.Response> Apply(Gate gate)
    {
        Result<ApplyUpgrade.Response> result = await gate.Send<ApplyUpgrade.Command, ApplyUpgrade.Response>(new ApplyUpgrade.Command());
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    /// <summary>A governed solution on Azure DevOps, as 0.51 left it: pipelines at the root, a team's pool and steps.</summary>
    private sealed class Repo : IDisposable
    {
        private Repo(string root) => Root = root;

        public string Root { get; }

        public static async Task<Repo> AsAnOlderReleaseLeftIt(string pinned = Older)
        {
            Repo repo = new(Path.Combine(Path.GetTempPath(), "sdlc-apply-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(repo.Root);
            repo.Git($"init -q -b {Integration}");
            repo.Git("config user.email suite@example.invalid");
            repo.Git("config user.name Suite");
            repo.Git("config core.autocrlf false");

            Directory.CreateDirectory(Path.Combine(repo.Root, "src"));
            await File.WriteAllTextAsync(Path.Combine(repo.Root, "src", "App.cs"), "class App { }\n");
            Directory.CreateDirectory(Path.Combine(repo.Root, ".sdlc"));
            await File.WriteAllTextAsync(
                SolutionDocument.PathIn(repo.Root),
                $"---\norigin: adopted\nplatform: azure-devops\nintegration_branch: {Integration}\n---\n");

            // What this release writes, then made into what an older one wrote.
            using (var gate = Gate.For(repo.Root))
            {
                Result<Initialise.Response> adopted = await gate.Send<Initialise.Command, Initialise.Response>(new Initialise.Command());
                Assert.IsTrue(adopted.IsSuccess, string.Join("; ", adopted.Errors.Select(error => error.Message)));
            }

            string nl = Environment.NewLine;
            string verify = Path.Combine(repo.Root, ".azuredevops", "sdlc-verify.yml");
            string text = (await File.ReadAllTextAsync(verify)).ReplaceLineEndings(nl);
            text = text
                .Replace("sdlc verify-chain", "sdlc verify-chain --old", StringComparison.Ordinal)
                .Replace("steps:" + nl, "steps:" + nl + "  - script: echo the team's own" + nl + "    displayName: the team's own" + nl, StringComparison.Ordinal);
            File.Delete(verify);
            await File.WriteAllTextAsync(Path.Combine(repo.Root, "azure-pipelines.yml"), text);

            string factory = FactoryPipeline.Text(Platform.AzureDevOps, Integration)
                .Replace("vmImage: ubuntu-latest", "name: Team-Pool", StringComparison.Ordinal);
            int sitrep = factory.IndexOf("            - script: |" + nl + "                git fetch origin", StringComparison.Ordinal);
            Assert.IsTrue(sitrep > 0);
            factory = factory[..sitrep] + "            - script: echo ${{ parameters.cycle }}" + nl + "              displayName: build the queue" + nl + nl + factory[sitrep..];
            await File.WriteAllTextAsync(Path.Combine(repo.Root, "azure-pipelines.factory.yml"), factory);

            string upgrade = Path.Combine(repo.Root, ".azuredevops", "sdlc-upgrade.yml");
            File.Move(upgrade, Path.Combine(repo.Root, "azure-pipelines.upgrade.yml"));

            string manifest = Path.Combine(repo.Root, ".config", "dotnet-tools.json");
            await File.WriteAllTextAsync(manifest, (await File.ReadAllTextAsync(manifest)).Replace(Release.Version, pinned, StringComparison.Ordinal));

            repo.Git("add -A");
            repo.Git("commit -q -m seed");
            return repo;
        }

        /// <summary>What <c>dotnet tool update --local</c> leaves: the pin moved in place, not committed.</summary>
        public async Task Install(string release, bool alsoAddingTool = false)
        {
            string manifest = Path.Combine(Root, ".config", "dotnet-tools.json");
            string text = (await File.ReadAllTextAsync(manifest)).Replace("\"" + Older + "\"", "\"" + release + "\"", StringComparison.Ordinal);

            if (alsoAddingTool)
            {
                text = text.Replace("\"tools\": {", "\"tools\": {\n    \"dotnet-ef\": { \"version\": \"10.0.0\", \"commands\": [ \"dotnet-ef\" ] },", StringComparison.Ordinal);
            }

            Assert.IsTrue(PinnedRelease.PinnedIn(text) == release && (!alsoAddingTool || text.Contains("dotnet-ef", StringComparison.Ordinal)), text);
            await File.WriteAllTextAsync(manifest, text);
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
            try
            {
                foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A scratch directory something still has open. Nothing here depends on it going.
            }
            catch (UnauthorizedAccessException)
            {
                // Nothing here depends on it going.
            }
        }
    }
}
