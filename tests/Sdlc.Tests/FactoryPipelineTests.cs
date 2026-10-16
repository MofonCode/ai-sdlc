using FluentResults;
using Sdlc.Core.Factory;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The factory's pipeline (D-84): what it runs, in what order, and what it never does.
/// </summary>
[TestClass]
public sealed class FactoryPipelineTests
{
    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_is_valid_yaml_and_proves_the_gate_before_it_builds(Platform platform)
    {
        string pipeline = FactoryPipeline.Text(platform, "development", "9.9.9");

        YamlDotNet.RepresentationModel.YamlStream stream = [];
        stream.Load(new StringReader(pipeline));
        Assert.AreEqual(1, stream.Documents.Count);

        int smoke = pipeline.IndexOf("the gate refuses", StringComparison.Ordinal);
        int build = pipeline.IndexOf("/sdlc:batch-build --pr --unattended", StringComparison.Ordinal);
        int sitrep = pipeline.IndexOf("sdlc sitrep", StringComparison.Ordinal);

        Assert.IsTrue(smoke > 0 && build > smoke, "The gate is proved before the agent is trusted with anything.");
        Assert.IsTrue(sitrep > build, "The run ends with the sitrep.");
        StringAssert.Contains(pipeline, "--version 9.9.9", "The gate it installs is pinned.");
        StringAssert.Contains(pipeline, "never run", "D-32: a pipeline nobody has run says so.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_signs_in_with_either_credential_and_proves_it_before_the_gate(Platform platform)
    {
        // The gate check passes when the agent writes nothing, and an agent that cannot sign in writes
        // nothing -- so the sign-in is proved first, or a broken credential would prove the gate.
        string pipeline = FactoryPipeline.Text(platform, "development", "9.9.9");

        int signIn = pipeline.IndexOf("Reply with the single word ready", StringComparison.Ordinal);
        int gate = pipeline.IndexOf(".sdlc/approval.md containing", StringComparison.Ordinal);

        Assert.IsTrue(signIn > 0 && gate > signIn, "The credential is proved before the gate is.");

        foreach (string claude in pipeline.ReplaceLineEndings().Split(Environment.NewLine + Environment.NewLine)
            .Where(step => step.Contains("claude -p", StringComparison.Ordinal)))
        {
            StringAssert.Contains(claude, "ANTHROPIC_API_KEY:", "Every step that runs the agent maps both secrets.");
            StringAssert.Contains(claude, "CLAUDE_CODE_OAUTH_TOKEN:", "Every step that runs the agent maps both secrets.");
            StringAssert.Contains(claude, "unset CLAUDE_CODE_OAUTH_TOKEN", "And leaves the agent exactly one.");
        }
    }

    [TestMethod]
    [DataRow(Platform.GitHub, "timeout-minutes", "concurrency")]
    [DataRow(Platform.AzureDevOps, "timeoutInMinutes", "Exclusive lock")]
    public void It_has_a_time_limit_and_runs_one_at_a_time(Platform platform, string limit, string lockSetting)
    {
        string pipeline = FactoryPipeline.Text(platform, "development", "9.9.9");

        StringAssert.Contains(pipeline, limit, "The job's timeout is the spend ceiling, since remediation has none.");
        StringAssert.Contains(pipeline, lockSetting, "Two runs must not pick the same cycles.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_never_merges_or_pushes_to_the_integration_branch(Platform platform)
    {
        string[] steps =
        [
            .. FactoryPipeline.Text(platform, "development", "9.9.9")
                .ReplaceLineEndings()
                .Split(Environment.NewLine)
                .Where(line => !line.TrimStart().StartsWith('#')),
        ];

        Assert.IsFalse(steps.Any(line => line.Contains("git merge", StringComparison.Ordinal)
            || line.Contains("pr merge", StringComparison.Ordinal)
            || line.Contains("--auto-complete", StringComparison.Ordinal)
            || line.Contains("git push", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task A_local_solution_is_refused_rather_than_given_a_pipeline_nothing_runs()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(SolutionDocument.PathIn(workspace.Root), "---\norigin: adopted\nplatform: local\n---\n");
        using var gate = Gate.For(workspace.Root);

        Result<WriteFactoryPipeline.Response> result = await gate
            .Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command());

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "local");
    }

    [TestMethod]
    public async Task It_is_written_for_the_recorded_platform_and_integration_branch()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: azure-devops\nintegration_branch: integration\n---\n");
        using var gate = Gate.For(workspace.Root);

        Result<WriteFactoryPipeline.Response> result = await gate
            .Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.AreEqual("azure-pipelines.factory.yml", result.Value.Path);
        StringAssert.Contains(
            await File.ReadAllTextAsync(Path.Combine(workspace.Root, result.Value.Path)),
            "include: [integration]");
    }
}
