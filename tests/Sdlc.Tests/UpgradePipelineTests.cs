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
    public void It_is_valid_yaml_and_moves_the_pin_before_adoption_runs_as_that_release(Platform platform)
    {
        string pipeline = UpgradePipeline.Text(platform, "development");

        YamlDotNet.RepresentationModel.YamlStream stream = [];
        stream.Load(new StringReader(pipeline));
        Assert.AreEqual(1, stream.Documents.Count);

        int pin = pipeline.IndexOf("dotnet tool update --local", StringComparison.Ordinal);
        int init = pipeline.IndexOf("sdlc init" + Environment.NewLine, StringComparison.Ordinal);
        int commit = pipeline.IndexOf("git commit", StringComparison.Ordinal);

        Assert.IsTrue(pin > 0 && init > pin, "Adoption runs as the release the manifest now pins.");
        Assert.IsTrue(commit > init, "What adoption wrote is what is committed.");
        StringAssert.Contains(pipeline, "sdlc factory --keep", "The factory's pipeline moves with the release when it must, and keeps the team's pool and steps when it need not.");
        StringAssert.Contains(pipeline, "$factory_note", "A rewrite is said in the pull request, so what the team added is put back before it merges.");
        StringAssert.Contains(pipeline, "never run", "D-32: a pipeline nobody has run says so.");
        StringAssert.Contains(pipeline, "upgrade/sdlc-$release");
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
    public void It_stages_only_what_the_upgrade_check_accepts(Platform platform)
    {
        // Anything else in the pull request and the check refuses it as a change with no chain.
        string staging = UpgradePipeline.Text(platform, "development")
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Single(line => line.Contains("git add", StringComparison.Ordinal));

        string[] listed = staging.Split(" in ")[1].Split(';')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        CollectionAssert.AreEquivalent(VerifyUpgrade.Paths.ToList(), listed, "Each path an upgrade may carry is staged when it changed, and nothing else.");

        Assert.IsFalse(staging.Contains("-A", StringComparison.Ordinal), "Nothing is staged wholesale.");
        Assert.IsTrue(VerifyUpgrade.Carries([UpgradePipeline.PathOf(platform)]), "The pipeline itself is a file an upgrade may change.");
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
