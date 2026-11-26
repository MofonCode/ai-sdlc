using Sdlc.Core.Chain;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The upgrade pipeline (D-201): a run a person starts that moves the pin, runs adoption as the new release, and opens
/// the pull request the upgrade check accepts.
/// </summary>
[TestClass]
public sealed class UpgradePipelineTests
{
    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_is_valid_yaml_installs_the_release_and_hands_the_move_to_it(Platform platform)
    {
        // D-213: the file is the one part of a move an older release writes, so it carries nothing that could go
        // stale. Everything after the install is the new release's.
        string pipeline = UpgradePipeline.Text(platform, "development");

        YamlDotNet.RepresentationModel.YamlStream stream = [];
        stream.Load(new StringReader(pipeline));
        Assert.AreEqual(1, stream.Documents.Count);

        int pin = pipeline.IndexOf("dotnet tool update --local", StringComparison.Ordinal);
        int apply = pipeline.IndexOf("dotnet sdlc upgrade apply --push", StringComparison.Ordinal);

        Assert.IsTrue(pin > 0 && apply > pin, "The move runs as the release the manifest now pins.");

        // A run queued minutes after a release found it in the download listing but not installable yet.
        int listed = pipeline.IndexOf("registration5-gz-semver2/mofoncode.sdlc.cli/$release.json", StringComparison.Ordinal);
        Assert.IsTrue(listed > 0 && pin > listed, "It waits for the release to be listed for install before installing it.");
        StringAssert.Contains(pipeline, "seq 1 20", "The wait is bounded.");
        StringAssert.Contains(pipeline, "not yet listed it for install. Run this again later.");
        int known = pipeline.IndexOf("nuget.org has no sdlc $release.", StringComparison.Ordinal);
        Assert.IsTrue(known > 0 && listed > known, "A version nuget.org does not hold is refused before the wait, not after it.");

        foreach (string stale in (string[])["dotnet sdlc init", "sdlc factory --keep", "git add", "git commit", "git push", "pr create"])
        {
            Assert.IsFalse(pipeline.Contains(stale, StringComparison.Ordinal), stale + " is the release's to run, not the file's.");
        }

        StringAssert.Contains(pipeline, "never run", "D-32: a pipeline nobody has run says so.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_runs_only_when_a_person_starts_it(Platform platform)
    {
        // A release is taken on purpose: no schedule, no trigger on a push, nothing watching nuget.org.
        string pipeline = UpgradePipeline.Text(platform, "development");

        Assert.IsFalse(pipeline.Contains("schedule:", StringComparison.OrdinalIgnoreCase), "No schedule on GitHub.");
        Assert.IsFalse(pipeline.Contains("schedules:", StringComparison.OrdinalIgnoreCase), "No schedule on Azure DevOps.");
        Assert.IsFalse(pipeline.Contains("cron", StringComparison.OrdinalIgnoreCase));

        if (platform == Platform.GitHub)
        {
            StringAssert.Contains(pipeline, "workflow_dispatch:");
            Assert.IsFalse(pipeline.Contains("  push:", StringComparison.Ordinal));
        }
        else
        {
            StringAssert.Contains(pipeline, "trigger: none");
            StringAssert.Contains(pipeline, "pr: none");
        }
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void The_pipeline_itself_is_a_file_an_upgrade_may_change(Platform platform)
    {
        // What the move stages is held in ApplyUpgradeTests, against a real repository.
        Assert.IsTrue(VerifyUpgrade.Carries([UpgradePipeline.PathOf(platform)]));
    }

    [TestMethod]
    public void On_GitHub_the_pull_request_is_opened_with_an_apps_token_and_no_credential_is_in_the_file()
    {
        // A pull request opened with GITHUB_TOKEN starts no workflow, so the check would never run on it.
        string pipeline = UpgradePipeline.Text(Platform.GitHub, "development");

        StringAssert.Contains(pipeline, "actions/create-github-app-token");
        StringAssert.Contains(pipeline, "secrets.SDLC_UPGRADE_APP_KEY");
        StringAssert.Contains(pipeline, "GH_TOKEN: ${{ steps.token.outputs.token }}");
        Assert.IsFalse(pipeline.Contains("secrets.GITHUB_TOKEN", StringComparison.Ordinal));
    }

    [TestMethod]
    public void The_agent_is_named_by_a_platform_variable_never_in_the_file()
    {
        // D-201, 0.51: an organization with no Microsoft-hosted job runs it on its own pool, which the file cannot hold.
        string azure = UpgradePipeline.Text(Platform.AzureDevOps, "development");
        string gitHub = UpgradePipeline.Text(Platform.GitHub, "development");

        StringAssert.Contains(azure, "name: $(SDLC_AGENT_POOL)", "Macro syntax: a template expression cannot see a variable set on the pipeline.");
        StringAssert.Contains(azure, "vmImage: ubuntu-latest", "Read only when the variable names a Microsoft-hosted pool.");
        StringAssert.Contains(gitHub, "runs-on: ${{ vars.SDLC_RUNNER || 'ubuntu-latest' }}");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void A_template_changed_by_hand_is_not_an_upgrade(Platform platform)
    {
        // The pipeline is the framework's whole, with no team steps beside it, so it is compared exactly.
        string path = UpgradePipeline.PathOf(platform).Replace('\\', '/');
        string template = UpgradePipeline.Text(platform, "development");
        SolutionDocument document = new(SolutionOrigin.Adopted, SolutionStandard.Existing, platform, "development");

        FluentResults.Result<string> expected = VerifyUpgrade.Expected(path, template, document, null);

        Assert.IsTrue(VerifyUpgrade.Written(path, expected, template).Passed);
        Assert.IsFalse(VerifyUpgrade.Written(path, expected, template.Replace("timeout", "timeout-x", StringComparison.Ordinal)).Passed);
    }
}
