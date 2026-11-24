using FluentResults;
using Sdlc.Core.Chain;
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
        string pipeline = FactoryPipeline.Text(platform, "development");

        YamlDotNet.RepresentationModel.YamlStream stream = [];
        stream.Load(new StringReader(pipeline));
        Assert.AreEqual(1, stream.Documents.Count);

        int smoke = pipeline.IndexOf("the hooks refuse", StringComparison.Ordinal);
        int build = pipeline.IndexOf("sdlc series run", StringComparison.Ordinal);
        int sitrep = pipeline.IndexOf("sdlc sitrep", StringComparison.Ordinal);

        Assert.IsTrue(smoke > 0 && build > smoke, "The hooks are proved before the agent is trusted with anything.");
        Assert.IsTrue(sitrep > build, "The run ends with the sitrep.");
        StringAssert.Contains(pipeline, "dotnet tool restore", "The sdlc it runs is the one the branch pins (D-131).");
        StringAssert.Contains(pipeline, "never run", "D-32: a pipeline nobody has run says so.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_signs_in_with_either_credential_and_proves_it_before_the_gate(Platform platform)
    {
        // The gate check passes when the agent writes nothing, and an agent that cannot sign in writes
        // nothing -- so the sign-in is proved first, or a broken credential would prove the gate.
        string pipeline = FactoryPipeline.Text(platform, "development");

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
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void A_claude_the_agent_starts_can_sign_in_and_that_is_proved_before_the_gate(Platform platform)
    {
        // Claude Code hands its commands no credential, so the fresh reader interrogate starts signed in with
        // nothing and every verify went unread. The credential goes into the agent's own settings, merged so
        // the plugin the same file records survives, and a nested sign-in is proved before anything is built.
        string pipeline = FactoryPipeline.Text(platform, "development");

        int signIn = pipeline.IndexOf("Reply with the single word ready", StringComparison.Ordinal);
        int nested = pipeline.IndexOf("could not sign in, so interrogation would have no fresh reader", StringComparison.Ordinal);
        int gate = pipeline.IndexOf(".sdlc/approval.md containing", StringComparison.Ordinal);

        Assert.IsTrue(signIn > 0 && nested > signIn && gate > nested, "The nested sign-in is proved after the agent's own and before the gate.");

        foreach (string claude in pipeline.ReplaceLineEndings().Split(Environment.NewLine + Environment.NewLine)
            .Where(step => step.Contains("claude -p", StringComparison.Ordinal)))
        {
            StringAssert.Contains(claude, "~/.claude/settings.json", "Every step that runs the agent writes the credential where a nested claude reads it.");
            StringAssert.Contains(claude, ".env = ((.env // {}) +", "Merged into the settings, not written over them.");
        }
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void Every_claude_reads_its_standard_input_from_nothing(Platform platform)
    {
        // Without it each call waits three seconds for a prompt on standard input and prints a warning.
        string[] calls =
        [
            .. FactoryPipeline.Text(platform, "development")
                .ReplaceLineEndings()
                .Split(Environment.NewLine)
                .Where(line => line.Contains("claude -p", StringComparison.Ordinal) && !line.TrimStart().StartsWith('#')),
        ];

        Assert.IsTrue(calls.Length >= 3);

        foreach (string call in calls)
        {
            StringAssert.Contains(call, "< /dev/null", call);
        }
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void An_agent_that_already_carries_the_tools_installs_nothing_and_stays_pinned(Platform platform)
    {
        // A team running its own agent may carry sdlc and Claude Code already. Each is used when it is there,
        // and sdlc only at the pinned version: any other version is replaced, because the version a pull
        // request is judged by is not the agent's to decide.
        foreach (string pipeline in new[]
        {
            FactoryPipeline.Text(platform, "development"),
            Templates.Pipeline(platform),
        })
        {
            StringAssert.Contains(pipeline, "is what runs here");
            StringAssert.Contains(pipeline, "dotnet tool install --global MofonCode.Sdlc.Cli --version \"$pinned\"");
        }

        string factory = FactoryPipeline.Text(platform, "development");

        StringAssert.Contains(factory, "= \"sdlc $pinned\"", "Only the pinned version counts as already installed.");
        StringAssert.Contains(factory, "command -v claude", "Claude Code is used when it is there.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void Every_session_the_factory_starts_carries_the_skills_of_the_release_it_pins(Platform platform)
    {
        // An installed plugin is whatever release installed it. 'sdlc claude' loads the skills packed beside
        // the pinned binary instead, so the factory installs none (D-131).
        string factory = FactoryPipeline.Text(platform, "development");

        Assert.IsFalse(factory.Contains("plugin install", StringComparison.Ordinal), "The factory installs no plugin.");
        StringAssert.Contains(factory, "sdlc series run --solution", "The series' process is started by the pinned binary, with the skills beside it.");
        StringAssert.Contains(factory, "sdlc claude -p 'Create the file .sdlc/approval.md", "The hooks proved are the pinned release's.");

        string[] bare = [.. factory.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("claude -p '/sdlc:", StringComparison.Ordinal)
                || line.StartsWith("claude -p 'Create the file", StringComparison.Ordinal))];

        Assert.IsEmpty(bare, "A session that needs the skills or the hooks starts through sdlc claude.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void A_restore_no_feed_can_answer_says_what_it_usually_means_and_fails(Platform platform)
    {
        // dotnet says only that no feed holds the version. The package is on nuget.org, so the usual cause is a
        // nuget.config that clears the default sources, and the step says so -- and still fails.
        foreach (string pipeline in new[]
        {
            FactoryPipeline.Text(platform, "development"),
            Templates.Pipeline(platform),
        })
        {
            StringAssert.Contains(pipeline, "a nuget.config that clears the default sources is the usual cause");
            StringAssert.Contains(pipeline, ">&2; exit 1; }", "The explanation is not a way past the failure.");
        }
    }

    [TestMethod]
    [DataRow(Platform.GitHub, "timeout-minutes", "concurrency")]
    [DataRow(Platform.AzureDevOps, "timeoutInMinutes", "Exclusive lock")]
    public void It_has_a_time_limit_and_runs_one_at_a_time(Platform platform, string limit, string lockSetting)
    {
        string pipeline = FactoryPipeline.Text(platform, "development");

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
            .. FactoryPipeline.Text(platform, "development")
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
    [DataRow(Platform.GitHub, "workflow_dispatch:")]
    [DataRow(Platform.AzureDevOps, "trigger: none")]
    public void A_person_starts_it_and_nothing_else_does(Platform platform, string manual)
    {
        // D-143: starting the run is the decision to spend, so there is no schedule to make it for anybody.
        string pipeline = FactoryPipeline.Text(platform, "development");

        StringAssert.Contains(pipeline, manual);
        Assert.IsFalse(pipeline.Contains("cron", StringComparison.Ordinal), "No schedule starts it.");
        Assert.IsFalse(pipeline.Contains("sdlc operate", StringComparison.Ordinal), "It builds; Operate is a person's command.");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void The_factory_runs_only_a_series_of_the_waves_asked_for_checked_before_anything_starts(Platform platform)
    {
        // The factory runs only series: the number of waves is text typed into a form, so it is refused unless it is a
        // whole number before the series starts, and the worktree 'series start' prints is where its process runs.
        string pipeline = FactoryPipeline.Text(platform, "development");

        int check = pipeline.IndexOf("*[!0-9]*) echo", StringComparison.Ordinal);
        int start = pipeline.IndexOf("series=\"$(sdlc series start --waves \"$WAVES\")\" || exit 1", StringComparison.Ordinal);
        int run = pipeline.IndexOf("sdlc series run --solution \"$series\"", StringComparison.Ordinal);

        Assert.IsTrue(check > 0 && start > check && run > start, "The number is checked, then the series starts, then it runs.");
        StringAssert.Contains(pipeline, "WAVES: ${{ ", "It arrives through the environment, never pasted into the script.");
        StringAssert.Contains(pipeline, platform == Platform.GitHub ? "      waves:" : "  - name: waves");
        Assert.IsFalse(pipeline.Contains("/sdlc:build", StringComparison.Ordinal), "No cycle is built outside a series.");
        Assert.IsFalse(pipeline.Contains("CYCLE", StringComparison.Ordinal), "There is no one cycle to ask for any more.");
        Assert.IsFalse(pipeline.Contains("series run --solution \"$series\" --permission-mode", StringComparison.Ordinal), "The series' process keeps its permission checks.");
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
        Assert.AreEqual(".azuredevops/sdlc-factory.yml", result.Value.Path);
        StringAssert.Contains(
            await File.ReadAllTextAsync(Path.Combine(workspace.Root, result.Value.Path)),
            "git switch integration");
    }

    [TestMethod]
    public async Task With_keep_a_teams_factory_that_carries_every_step_of_the_release_is_left_as_it_is()
    {
        // D-201: the upgrade pipeline runs it this way, so a team's own pool and steps survive a release that did not
        // change the framework's steps, and a rewrite that has to drop them says whose pool it was.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: azure-devops\nintegration_branch: integration\n---\n");
        string path = Path.Combine(workspace.Root, ".azuredevops", "sdlc-factory.yml");
        string template = FactoryPipeline.Text(Platform.AzureDevOps, "integration");
        int first = template.IndexOf("            - script: |", StringComparison.Ordinal);
        string team = (template[..first] + "            - task: NuGetAuthenticate@1" + Environment.NewLine + Environment.NewLine + template[first..])
            .Replace("vmImage: ubuntu-latest", "name: Team-Pool", StringComparison.Ordinal);
        Assert.AreNotEqual(template, team);
        CycleWorkspace.WriteRaw(path, team);
        using var gate = Gate.For(workspace.Root);

        WriteFactoryPipeline.Response kept = (await gate.Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command(Keep: true))).Value;

        Assert.IsTrue(kept.Kept);
        Assert.AreEqual(team, await File.ReadAllTextAsync(path), "Kept means untouched.");
        StringAssert.StartsWith(WriteFactoryPipeline.Render(kept), "Kept .azuredevops/sdlc-factory.yml");

        // Without --keep the file is the framework's, rewritten whole, and the pool it dropped is named.
        WriteFactoryPipeline.Response rewritten = (await gate.Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command())).Value;

        Assert.IsFalse(rewritten.Kept);
        Assert.AreEqual(template, await File.ReadAllTextAsync(path));
        StringAssert.Contains(WriteFactoryPipeline.Render(rewritten), "Team-Pool");
    }

    [TestMethod]
    public async Task With_keep_a_factory_an_older_release_wrote_takes_this_releases_steps_and_keeps_the_teams_own()
    {
        // Issue #75: most releases change a framework step, so keeping only an untouched file rarely kept anything. The
        // older release's version of a step is replaced, by its name; the team's pool and its own step are carried.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: azure-devops\nintegration_branch: integration\n---\n");
        string path = Path.Combine(workspace.Root, ".azuredevops", "sdlc-factory.yml");
        string template = FactoryPipeline.Text(Platform.AzureDevOps, "integration");
        int first = template.IndexOf("            - script: |", StringComparison.Ordinal);
        string own = "            - task: NuGetAuthenticate@1" + Environment.NewLine + "              displayName: sign in to the feed" + Environment.NewLine + Environment.NewLine;
        string older = (template[..first] + own + template[first..])
            .Replace("vmImage: ubuntu-latest", "name: Team-Pool", StringComparison.Ordinal)
            .Replace("git switch integration", "git checkout integration", StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(path, older);
        using var gate = Gate.For(workspace.Root);

        WriteFactoryPipeline.Response merged = (await gate.Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command(Keep: true))).Value;
        string written = await File.ReadAllTextAsync(path);

        Assert.IsFalse(merged.Kept);
        Assert.AreEqual(template.Replace("vmImage: ubuntu-latest", "name: Team-Pool", StringComparison.Ordinal), written.Replace(own, string.Empty, StringComparison.Ordinal), "The release's steps, the team's pool.");
        Assert.IsTrue(written.IndexOf("NuGetAuthenticate@1", StringComparison.Ordinal) < written.IndexOf("displayName: install sdlc and the agent", StringComparison.Ordinal), "The team's step stays where it was, before the step it preceded.");
        Assert.IsFalse(written.Contains("git checkout integration", StringComparison.Ordinal), "The older release's version of a step is replaced.");
        Assert.IsTrue(VerifyUpgrade.Shaped(".azuredevops/sdlc-factory.yml", template, older, written).Passed, "The upgrade check accepts what it wrote.");
        string report = WriteFactoryPipeline.Render(merged);
        StringAssert.StartsWith(report, "Merged .azuredevops/sdlc-factory.yml");
        StringAssert.Contains(report, "Team-Pool");
        StringAssert.Contains(report, "'sign in to the feed'");
    }

    [TestMethod]
    public async Task With_keep_a_step_an_older_release_wrote_is_left_out_rather_than_carried()
    {
        // D-212: a governed solution's 0.51 to 0.55 move carried 'build the queue', 0.51's step, which read a
        // parameter 0.55 no longer declares, and the factory it merged could not run. A step whose name a release
        // retired is left out, and so is one that reads a parameter this release does not declare.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: azure-devops\nintegration_branch: integration\n---\n");
        string path = Path.Combine(workspace.Root, ".azuredevops", "sdlc-factory.yml");
        string template = FactoryPipeline.Text(Platform.AzureDevOps, "integration");
        int sitrep = template.IndexOf("            - script: |" + Environment.NewLine + "                git fetch origin", StringComparison.Ordinal);
        Assert.IsTrue(sitrep > 0);
        string retired = "            - script: echo queued" + Environment.NewLine + "              displayName: build the queue" + Environment.NewLine + Environment.NewLine;
        string stale = "            - script: echo ${{ parameters.cycle }}" + Environment.NewLine + "              displayName: name the cycle" + Environment.NewLine + Environment.NewLine;
        string own = "            - script: echo ${{ parameters.waves }}" + Environment.NewLine + "              displayName: say how many waves" + Environment.NewLine + Environment.NewLine;
        string older = template[..sitrep] + retired + stale + own + template[sitrep..];
        CycleWorkspace.WriteRaw(path, older);
        using var gate = Gate.For(workspace.Root);

        WriteFactoryPipeline.Response merged = (await gate.Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command(Keep: true))).Value;
        string written = await File.ReadAllTextAsync(path);

        Assert.IsFalse(written.Contains("build the queue", StringComparison.Ordinal), "A retired name is left out.");
        Assert.IsFalse(written.Contains("parameters.cycle", StringComparison.Ordinal), "A step reading a parameter this release dropped could only fail.");
        StringAssert.Contains(written, "say how many waves", "A step reading a parameter this release declares is the team's, and carried.");
        string report = WriteFactoryPipeline.Render(merged);
        StringAssert.Contains(report, "'build the queue', which an older release wrote");
        StringAssert.Contains(report, "'name the cycle', which reads 'cycle', a parameter this release does not declare");
    }

    [TestMethod]
    public void No_template_this_release_writes_uses_a_step_name_a_release_retired()
    {
        // A retired name in the list and in a template would have every merge drop the release's own step.
        string[] templates =
        [
            .. Templates.Hosted.SelectMany(platform => new[]
            {
                FactoryPipeline.Text(platform, "main"),
                Templates.Pipeline(platform, "main"),
            }),
        ];

        foreach (string name in VerifyUpgrade.RetiredSteps)
        {
            Assert.IsFalse(
                templates.Any(text => text.Split('\n').Any(line => line.Trim() == "displayName: " + name || line.Trim() == "- name: " + name)),
                name);
        }
    }

    [TestMethod]
    public async Task With_keep_an_action_named_by_its_first_line_is_the_same_step_at_another_version()
    {
        // A release that moves checkout@v4 to @v5 replaces the step rather than carrying the older beside it.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: github\nintegration_branch: main\n---\n");
        string path = Path.Combine(workspace.Root, ".github", "workflows", "sdlc-factory.yml");
        string template = FactoryPipeline.Text(Platform.GitHub, "main");
        Assert.IsTrue(template.Contains("actions/checkout@v5", StringComparison.Ordinal));
        string own = "      - run: echo the team's own" + Environment.NewLine;
        int checkout = template.IndexOf("      - uses: actions/checkout@v5", StringComparison.Ordinal);
        Assert.IsTrue(checkout > 0);
        string older = (template[..checkout] + own + template[checkout..]).Replace("actions/checkout@v5", "actions/checkout@v4", StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(path, older);
        using var gate = Gate.For(workspace.Root);

        WriteFactoryPipeline.Response merged = (await gate.Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command(Keep: true))).Value;
        string written = await File.ReadAllTextAsync(path);

        Assert.IsNotNull(merged.Carried, "Merged, not rewritten.");
        Assert.IsFalse(written.Contains("checkout@v4", StringComparison.Ordinal));
        StringAssert.Contains(written, "echo the team's own");
        Assert.AreEqual(1, merged.Carried.Count, string.Join(", ", merged.Carried));
    }

    [TestMethod]
    public async Task A_factory_an_older_release_wrote_at_the_root_moves_into_the_azuredevops_folder_as_it_was()
    {
        // D-203: the team's pool and steps travel with it, and it is judged where it now lives.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            "---\norigin: adopted\nplatform: azure-devops\nintegration_branch: integration\n---\n");
        string root = Path.Combine(workspace.Root, "azure-pipelines.factory.yml");
        string team = FactoryPipeline.Text(Platform.AzureDevOps, "integration").Replace("vmImage: ubuntu-latest", "name: Team-Pool", StringComparison.Ordinal);
        CycleWorkspace.WriteRaw(root, team);
        using var gate = Gate.For(workspace.Root);

        WriteFactoryPipeline.Response moved = (await gate.Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command(Keep: true))).Value;

        Assert.IsFalse(File.Exists(root));
        Assert.AreEqual("azure-pipelines.factory.yml", moved.MovedFrom);
        Assert.IsTrue(moved.Kept);
        Assert.AreEqual(team, await File.ReadAllTextAsync(Path.Combine(workspace.Root, ".azuredevops", "sdlc-factory.yml")));
        StringAssert.Contains(WriteFactoryPipeline.Render(moved), "point the factory's pipeline in Azure DevOps at the new path");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void Both_pipelines_select_an_sdk_whether_or_not_global_json_exists(Platform platform)
    {
        // useGlobalJson and global-json-file both fail when the file is absent, and an adopted solution
        // need not have one. Each pipeline therefore pins from the file when it is there and installs the
        // current major when it is not.
        foreach (string pipeline in new[] { Templates.Pipeline(platform), FactoryPipeline.Text(platform, "development") })
        {
            string pinned = platform == Platform.GitHub ? "hashFiles('global.json') != ''" : "eq(variables.sdkPinned, 'true')";
            string unpinned = platform == Platform.GitHub ? "hashFiles('global.json') == ''" : "ne(variables.sdkPinned, 'true')";
            StringAssert.Contains(pipeline, pinned);
            StringAssert.Contains(pipeline, unpinned);
        }
    }
}
