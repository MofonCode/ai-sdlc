using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Adoption, in its three modes.
/// </summary>
/// <remarks>
/// The expensive mistakes here are both silent. Misreading a new solution as adopted loses every default
/// it was meant to start from; rewriting a file a person owns loses what only that file knew. So most of
/// what follows is about what adoption does <em>not</em> touch.
/// </remarks>
[TestClass]
public sealed class InitTests
{
    [TestMethod]
    public async Task A_repository_with_no_code_is_scaffolded()
    {
        using var solution = Scratch.Create();

        Initialise.Response report = await Adopt(solution, tracked: ["README.md", "LICENSE", ".gitignore"]);

        Assert.AreEqual(Initialise.InitMode.Greenfield, report.Mode);
        Assert.AreEqual(
            SolutionOrigin.Scaffolded,
            report.Origin,
            "A repository holding a README and a licence has no solution in it yet. Reading it as adopted "
            + "would silently drop every default the profile was meant to supply.");
    }

    [TestMethod]
    public async Task A_repository_that_already_holds_code_is_onboarded()
    {
        using var solution = Scratch.Create();

        Initialise.Response report = await Adopt(solution, tracked: ["README.md", "src/App/Program.cs"]);

        Assert.AreEqual(Initialise.InitMode.Onboard, report.Mode);
        Assert.AreEqual(SolutionOrigin.Adopted, report.Origin);
    }

    [TestMethod]
    public async Task A_governed_solution_is_updated_and_keeps_the_origin_it_was_given()
    {
        // Recomputing the origin would turn a historical fact into something that moves: a scaffolded
        // solution would read as adopted the moment it had code in it, which is every solution after its
        // first cycle.
        using var solution = Scratch.Create();

        await Adopt(solution, tracked: []);
        Initialise.Response again = await Adopt(solution, tracked: ["src/App/Program.cs", "src/App/Roll.cs"]);

        Assert.AreEqual(Initialise.InitMode.Update, again.Mode);
        Assert.AreEqual(SolutionOrigin.Scaffolded, again.Origin);
    }

    [TestMethod]
    public async Task Update_never_rewrites_a_file_only_a_person_writes()
    {
        // The rule needs no list of exclusions: update writes what the framework owns, and what it owns
        // is everything not protected.
        using var solution = Scratch.Create();
        await Adopt(solution, tracked: []);

        string path = SolutionDocument.PathIn(solution.Root);
        await File.WriteAllTextAsync(path, "---\nprofile: mofoncode\norigin: adopted\n---\n\n# mine\n");

        Initialise.Response report = await Adopt(solution, tracked: []);

        Assert.AreEqual(
            Initialise.InitChange.Kept,
            report.Actions.Single(action => action.Path.EndsWith(Cycle.SolutionFileName, StringComparison.Ordinal)).Change);

        StringAssert.Contains(await File.ReadAllTextAsync(path), "profile: mofoncode");
        StringAssert.Contains(await File.ReadAllTextAsync(path), "# mine");
    }

    [TestMethod]
    public async Task Update_refreshes_what_the_framework_owns()
    {
        using var solution = Scratch.Create();
        await Adopt(solution, tracked: []);

        string readme = Path.Combine(solution.Root, Cycle.SdlcDirectoryName, "README.md");
        await File.WriteAllTextAsync(readme, "stale, from an older framework version\n");

        Initialise.Response report = await Adopt(solution, tracked: []);

        Assert.AreEqual(
            Initialise.InitChange.Updated,
            report.Actions.Single(action => action.Path.EndsWith("README.md", StringComparison.Ordinal)).Change,
            "Without this, a solution's scaffolding rots across framework versions with nothing to fix it.");

        StringAssert.Contains(await File.ReadAllTextAsync(readme), "intent -> spec -> plan");
    }

    [TestMethod]
    public async Task An_existing_agents_file_is_printed_rather_than_overwritten()
    {
        // Rewriting a repository's instruction file is the one destructive thing adoption could do, and
        // the existing one usually holds things nothing else knows.
        using var solution = Scratch.Create();
        string path = Path.Combine(solution.Root, Initialise.AgentsFileName);
        await File.WriteAllTextAsync(path, "instructions nobody should lose\n");

        Initialise.Response report = await Adopt(solution, tracked: ["src/App/Program.cs"]);

        Assert.AreEqual(
            Initialise.InitChange.Printed,
            report.Actions.Single(action => action.Path == Initialise.AgentsFileName).Change);

        Assert.AreEqual("instructions nobody should lose\n", await File.ReadAllTextAsync(path));
        Assert.IsTrue(report.Printed.ContainsKey(Initialise.AgentsFileName));
    }

    [TestMethod]
    public async Task Adoption_refuses_outside_a_git_repository()
    {
        // Without git, build cannot record the diff verify derives from and the gates key on a branch that
        // cannot exist -- so every hook would permit everything while the artifacts still looked governed.
        using var solution = Scratch.Create();

        using Gate gate = Adopter(solution, new FakeRepository { Repository = false });

        Result<Initialise.Response> result = await gate
            .Send<Initialise.Command, Initialise.Response>(new Initialise.Command("dotnet-default", Platform.GitHub));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(result.Errors[0].Message, "git init");
        Assert.IsFalse(
            Directory.Exists(Path.Combine(solution.Root, Cycle.SdlcDirectoryName)),
            "A refusal that had already written half the scaffolding would be worse than one that wrote none.");
    }

    [TestMethod]
    public async Task A_scaffolded_techstack_carries_the_profile_defaults_and_an_adopted_one_does_not()
    {
        // Reading a repository to guess what it is built from would write a guess down as a declaration,
        // and the first plan to depart from a wrong guess would be judged against it.
        using var fresh = Scratch.Create();
        await Adopt(fresh, tracked: []);

        using var existing = Scratch.Create();
        await Adopt(existing, tracked: ["src/App/Program.cs"]);

        string scaffolded = await File.ReadAllTextAsync(
            Path.Combine(fresh.Root, Cycle.SdlcDirectoryName, "techstack.md"));

        string adopted = await File.ReadAllTextAsync(
            Path.Combine(existing.Root, Cycle.SdlcDirectoryName, "techstack.md"));

        StringAssert.Contains(scaffolded, "MSTest on Microsoft.Testing.Platform");
        Assert.IsFalse(adopted.Contains("MSTest", StringComparison.Ordinal));
        StringAssert.Contains(adopted, "Left empty on purpose");
    }

    [TestMethod]
    [DataRow(Platform.GitHub, ".github/workflows/pull-request.yml")]
    [DataRow(Platform.AzureDevOps, "azure-pipelines.yml")]
    public async Task Each_platform_gets_its_own_pipeline(Platform platform, string expected)
    {
        using var solution = Scratch.Create();

        Initialise.Response report = await Adopt(solution, tracked: [], platform: platform);

        Assert.IsTrue(
            report.Actions.Any(action => action.Path.Replace('\\', '/') == expected),
            string.Join("; ", report.Actions.Select(a => a.Path)));
    }

    [TestMethod]
    public void Every_pipeline_runs_verify_chain_and_thresholds_nothing()
    {
        // The guarantee lives in the binary, so each pipeline is about thirty lines wrapping one command.
        // And coverage is displayed rather than enforced: a threshold gets met by testing what is easy.
        foreach (Platform platform in Enum.GetValues<Platform>())
        {
            string pipeline = Templates.Pipeline(platform);

            StringAssert.Contains(pipeline, "sdlc verify-chain", platform.ToString());

            // Comment lines are excluded, because the comment that explains why there is no threshold
            // says the word. A check that banned it everywhere would force the explanation out.
            string yaml = string.Join(
                Environment.NewLine,
                pipeline
                    .ReplaceLineEndings()
                    .Split(Environment.NewLine)
                    .Where(line => !line.TrimStart().StartsWith('#')));

            foreach (string word in (string[])["threshold", "fail_under", "minimum", "badge"])
            {
                Assert.IsFalse(
                    yaml.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"{platform} enforces coverage ('{word}'), which is a non-goal.");
            }
        }
    }

    [TestMethod]
    public async Task The_pre_push_hook_is_written_but_not_installed()
    {
        // A hook written into .git/hooks would exist on one machine and silently not on anybody else's, and
        // a check that is absent where nobody looks is worse than no check. So it lands under .sdlc, which
        // is tracked, and the report says how to point git at it.
        using var solution = Scratch.Create();

        Initialise.Response report = await Adopt(solution, tracked: []);

        Assert.IsTrue(
            File.Exists(Path.Combine(solution.Root, Cycle.SdlcDirectoryName, "hooks", "pre-push")),
            string.Join("; ", report.Actions.Select(action => action.Path)));

        Assert.IsFalse(
            Directory.Exists(Path.Combine(solution.Root, ".git", "hooks")),
            "Adoption must not write into .git, which is not tracked.");

        StringAssert.Contains(InitRenderer.Render(report), "git config core.hooksPath .sdlc/hooks");
    }

    [TestMethod]
    public void The_solution_readme_shows_the_format_version_being_stamped()
    {
        // The readme's example envelope is the shape a person checks their own artifact against, so a
        // version it hardcodes is a version that is right until the format moves and wrong everywhere
        // afterwards -- in a file already written into every solution that adopted before the change.
        // It was a literal '1.0.0' for exactly as long as that was also the constant's value.
        StringAssert.Contains(
            Templates.Readme(),
            "framework_version: " + Cycle.FrameworkVersion,
            "The solution readme's example envelope does not show the version being stamped into "
            + "artifacts. Interpolate Cycle.FrameworkVersion rather than writing the number.");
    }

    [TestMethod]
    public void Every_command_the_solution_readme_names_is_one_the_binary_has()
    {
        // The readme goes into every governed solution and is the first place somebody looks. A command it
        // names and the binary does not have sends them to a usage error, and the usage text is the one
        // place they would not think to look for the answer.
        string usage = File.ReadAllText(Path.Combine(
            RepoPaths.Root, "src", "Sdlc.Cli", "Usage.cs"));

        IEnumerable<string> named = System.Text.RegularExpressions.Regex
            .Matches(Templates.Readme(), @"sdlc (?<verb>[a-z-]+(?: [a-z]+)?)")
            .Select(match => match.Groups["verb"].Value)
            .Distinct(StringComparer.Ordinal);

        foreach (string verb in named)
        {
            // The stage skills are named deliberately, and the binary answers those by explaining itself.
            if (Sdlc.Cli.Stages.IsSkill(verb.Split(' ')[0]))
            {
                continue;
            }

            StringAssert.Contains(
                usage,
                verb,
                $"The solution readme names 'sdlc {verb}', which the usage text does not list.");
        }
    }

    [TestMethod]
    public async Task A_scaffolded_solution_gets_the_stack_files_and_an_adopted_one_gets_none()
    {
        // D-33: the default profile is carried by the binary, so adoption needs no feed and no
        // network. And origin decides whether it binds - an adopted repository keeps its own
        // conventions, because the profile proposes a shape for new work and never rewrites one that
        // predates it.
        string[] stack =
        [
            ".editorconfig",
            "Directory.Build.props",
            "Directory.Packages.props",
            "global.json",
            Path.Combine("tests", "Directory.Build.props"),
        ];

        using var fresh = Scratch.Create();
        await Adopt(fresh, tracked: []);

        foreach (string file in stack)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(fresh.Root, file)),
                file + " is what makes a stated convention enforceable, so a scaffolded solution starts with it.");
        }

        using var existing = Scratch.Create();
        await Adopt(existing, tracked: ["src/App/Program.cs"]);

        foreach (string file in stack)
        {
            Assert.IsFalse(
                File.Exists(Path.Combine(existing.Root, file)),
                file + " would rewrite the conventions of a codebase that predates the framework.");
        }
    }

    [TestMethod]
    public async Task Adoption_writes_no_code_and_no_projects()
    {
        // D-35. Adoption writes the files that say how code is to be written; the code itself arrives
        // through 'sdlc scaffold' behind an approved plan. Writing it here made the solution file, the
        // projects and the reference slice the only things in a governed repository with no intent, no
        // spec, no plan and no approval behind them -- which is the promise the whole design rests on.
        using var solution = Scratch.Create();

        Initialise.Response report = await Adopt(solution, tracked: []);

        Assert.AreEqual(SolutionOrigin.Scaffolded, report.Origin, "The case where it would be tempting.");

        string[] code =
        [
            .. Directory.EnumerateFiles(solution.Root, "*.cs", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(solution.Root, "*.csproj", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(solution.Root, "*.slnx", SearchOption.AllDirectories),
        ];

        Assert.AreEqual(
            0,
            code.Length,
            "Adoption left code on disk: " + string.Join(", ", code) + ". It belongs to a cycle.");
    }

    [TestMethod]
    public async Task A_stack_file_a_person_amended_is_not_rewritten_by_update()
    {
        // These are a person's to amend. Update refreshes what the framework owns, and it does not own
        // the rules a team has since argued about.
        using var solution = Scratch.Create();
        await Adopt(solution, tracked: []);

        string path = Path.Combine(solution.Root, ".editorconfig");
        await File.WriteAllTextAsync(path, "root = true\n\n# ours now\n");

        Initialise.Response report = await Adopt(solution, tracked: []);

        Assert.AreEqual(
            Initialise.InitChange.Kept,
            report.Actions.Single(action => action.Path == ".editorconfig").Change);

        StringAssert.Contains(await File.ReadAllTextAsync(path), "# ours now");
    }

    [TestMethod]
    public void The_generated_stack_is_well_formed_and_carries_the_pair_that_silently_breaks_a_run()
    {
        // The runner needs EnableMSTestRunner and the global.json test section together. With one of
        // them, 'dotnet test' exits zero having run nothing, which reads as a clean run - and a trial
        // cycle lost its whole verification to exactly that. So both halves are asserted here, where a
        // reader can see they are a pair.
        Assert.IsNotNull(XDocument.Parse(Templates.BuildProps()));
        Assert.IsNotNull(XDocument.Parse(Templates.PackageProps()));
        Assert.IsNotNull(JsonNode.Parse(Templates.GlobalJson()));

        StringAssert.Contains(Templates.BuildProps(), "<EnableMSTestRunner>true</EnableMSTestRunner>");
        Assert.AreEqual(
            "Microsoft.Testing.Platform",
            JsonNode.Parse(Templates.GlobalJson())!["test"]!["runner"]!.GetValue<string>());

        // The element, not the word: the file names the property in a comment warning against it, and
        // asserting on the bare name catches the warning rather than the mistake.
        Assert.IsFalse(
            Templates.BuildProps().Contains("<TestingPlatformDotnetTestSupport>", StringComparison.Ordinal),
            "That is the .NET 9 mechanism, and setting it instead of EnableMSTestRunner is what "
            + "produced the silent zero-test pass.");
    }

    [TestMethod]
    public void A_test_project_can_write_its_first_test_under_the_generated_policy()
    {
        // Found by running it rather than by reading it: with the analyzer policy and no relaxations,
        // CA1707 refuses Rolls_within_one_and_six and CA1305 refuses a bare ToString() in an
        // assertion, so a scaffolded solution could not write the test its own plan named. A profile
        // shipping the policy without these would make every consumer rediscover the same five.
        string props = Templates.TestBuildProps();

        Assert.IsNotNull(XDocument.Parse(props));

        foreach (string rule in (string[])["CA1707", "CA1515", "CA1307", "CA1305", "CA2007"])
        {
            StringAssert.Contains(props, rule, rule + " is relaxed for test code, and says why.");
        }

        StringAssert.Contains(
            props,
            "GetPathOfFileAbove",
            "A Directory.Build.props in a subdirectory shadows the one above it, so without the import "
            + "a test project silently loses the framework, the analyzer level and warnings-as-errors.");
    }

    [TestMethod]
    public void The_generated_editorconfig_says_what_does_not_enforce_its_naming_rules()
    {
        // The rules are there to guide an editor, and EnforceCodeStyleInBuild does not cover IDE1006.
        // A file that stated them without saying so would be the directory that appears enforced and
        // is not, in the one place a reader goes to find out what binds them.
        string text = Templates.EditorConfig();

        StringAssert.Contains(text, "dotnet_naming_rule.private_fields_are_underscore_camel");
        StringAssert.Contains(text, "dotnet_naming_rule.private_methods_are_camel");
        StringAssert.Contains(text, "dotnet_diagnostic.IDE0055.severity = error");

        StringAssert.Contains(text, "IDE1006");
        StringAssert.Contains(text, "architecture test");
    }

    [TestMethod]
    public void Each_generated_hook_config_is_valid_json()
    {
        // These templates are assembled a line at a time, so a stray comma is a settings file the
        // host rejects -- and a hook configuration that fails to parse is a hook that never runs,
        // which permits everything while the directory still looks governed.
        foreach (string host in (string[])["claude", "copilot"])
        {
            string text = Templates.HookConfig(host);

            Assert.IsNotNull(
                JsonNode.Parse(text),
                host + "'s hook configuration is not valid JSON:\n" + text);
        }
    }

    [TestMethod]
    public void The_turn_end_is_configured_for_claude_and_deliberately_absent_for_copilot()
    {
        // Copilot's hook surface has a pre-tool event only. Writing a plausible-looking turn-end
        // block for it would be the failure this whole design is written against: a check that looks
        // like enforcement and cannot fire. 'sdlc hook' refuses that combination by name.
        JsonNode claude = JsonNode.Parse(Templates.HookConfig("claude"))!;

        StringAssert.Contains(
            claude["hooks"]!["Stop"]!.AsArray()[0]!["hooks"]!.AsArray()[0]!["command"]!.GetValue<string>(),
            "--event turn-end");

        JsonNode copilot = JsonNode.Parse(Templates.HookConfig("copilot"))!;

        Assert.IsNull(
            copilot["hooks"]!.AsObject().FirstOrDefault(entry =>
                entry.Key.Contains("stop", StringComparison.OrdinalIgnoreCase)
                || entry.Key.Contains("turn", StringComparison.OrdinalIgnoreCase)).Value,
            "Copilot has no turn-end event to listen to, so nothing may claim one.");
    }

    [TestMethod]
    public void The_pre_push_hook_has_unix_line_endings_and_ignores_branches_that_are_not_cycles()
    {
        // CRLF in a shell script fails on Linux with a message about the interpreter, which is a confusing
        // way to learn that a hook was written on Windows.
        string hook = Templates.PrePushHook();

        Assert.IsFalse(
            hook.Contains('\r'),
            "The hook carries a carriage return, which fails on Linux as an interpreter error.");
        StringAssert.StartsWith(hook, "#!/bin/sh");

        StringAssert.Contains(
            hook,
            "refs/heads/cycle/*",
            "A developer pushing a spike should not meet a governance hook at all.");

        StringAssert.Contains(
            hook,
            "--no-verify",
            "The hook has to say it is bypassable, or somebody will mistake it for the guarantee.");

        StringAssert.Contains(
            hook,
            "not on PATH",
            "A missing binary warns visibly rather than failing closed or passing silently.");
    }

    [TestMethod]
    public void The_untested_pipeline_says_so_in_the_file_it_generates()
    {
        // D-32. A pipeline nobody has run looks exactly like a gate, and a check that appears to enforce
        // and does not is worse than one that never pretended -- so the caveat is in the generated file,
        // where the first person to adopt on this platform reads it, rather than only in our own notes.
        string ado = Templates.Pipeline(Platform.AzureDevOps);

        StringAssert.Contains(ado, "never run against a real Azure DevOps server");
        StringAssert.Contains(
            ado,
            "sdlc verify-chain",
            "The caveat must not bury the one line that matters.");

        Assert.IsFalse(
            Templates.Pipeline(Platform.GitHub).Contains("never run", StringComparison.Ordinal),
            "The GitHub pipeline has run, so claiming otherwise would be the opposite mistake.");
    }

    [TestMethod]
    public void Every_pipeline_installs_the_gate_before_it_calls_it()
    {
        // The first version of these templates called 'sdlc verify-chain' on a runner where nothing had
        // put 'sdlc' on the path. It would have failed command-not-found from the one step that is the
        // whole guarantee -- the loudest possible place to discover an install was missing, and the
        // least likely place for a reader to suspect one.
        foreach (Platform platform in Enum.GetValues<Platform>())
        {
            string pipeline = Templates.Pipeline(platform).ReplaceLineEndings();
            string[] lines = pipeline.Split(Environment.NewLine);

            int install = Array.FindIndex(
                lines,
                line => line.Contains("dotnet tool install", StringComparison.Ordinal)
                    && !line.TrimStart().StartsWith('#'));

            Assert.IsGreaterThan(
                -1,
                install,
                $"{platform} never installs the binary its last step calls.");

            // Pinned rather than latest. An unpinned governance tool can change what a repository is
            // judged by between two runs, with nobody having chosen the new rules.
            StringAssert.Contains(
                lines[install],
                "--version " + Sdlc.Core.Release.Version,
                $"{platform} does not pin the gate to the release it ships with.");

            int check = Array.FindIndex(
                lines,
                line => line.Contains("sdlc verify-chain", StringComparison.Ordinal)
                    && !line.TrimStart().StartsWith('#'));

            Assert.IsLessThan(
                check,
                install,
                $"{platform} calls the gate before installing it.");

            // The build is the expensive step, so a gate that cannot be fetched should fail before it
            // rather than after it.
            int build = Array.FindIndex(
                lines,
                line => line.Contains("dotnet build", StringComparison.Ordinal)
                    && !line.TrimStart().StartsWith('#'));

            Assert.IsLessThan(
                build,
                install,
                $"{platform} spends a build before finding out whether the gate is available.");
        }
    }

    [TestMethod]
    public void The_unpublished_package_says_so_in_the_pipeline_it_generates()
    {
        // D-32's rule applied to the step that step depends on. The package is not on a feed yet, so
        // this install does not resolve -- and the person whose first pull request fails on it reads the
        // generated file, not our notes. Writing no install step at all was the quiet version of the
        // same gap, which is the one the whole design is written against.
        foreach (Platform platform in Enum.GetValues<Platform>())
        {
            StringAssert.Contains(
                Templates.Pipeline(platform),
                "not on a public feed yet",
                $"{platform} installs a package it cannot resolve and does not say so.");
        }
    }

    [TestMethod]
    public void The_agents_file_points_at_the_constraints_rather_than_restating_them()
    {
        // A copy of the rules here would be a second place they live, and the copy is always the one that
        // goes stale.
        string agents = Templates.Agents("a solution");

        StringAssert.Contains(agents, "sdlc constraints");

        foreach (string id in (string[])["CON_01", "CON_04", "CON_07", "CON_13"])
        {
            Assert.IsFalse(
                agents.Contains(id, StringComparison.Ordinal),
                $"The instruction file restates {id}. It must point at the command instead.");
        }

        Assert.IsTrue(
            agents.ReplaceLineEndings().Split(Environment.NewLine).Length < 200,
            "The file is meant to be lean; past about 200 lines nothing reads all of it.");
    }

    [TestMethod]
    public async Task Adoption_creates_no_empty_directories()
    {
        // Nothing is created speculatively. An empty cycles directory is a thing a reader has to work out
        // the meaning of.
        using var solution = Scratch.Create();

        await Adopt(solution, tracked: []);

        Assert.IsFalse(
            Directory.Exists(Path.Combine(
                solution.Root,
                Cycle.SdlcDirectoryName,
                Cycle.CyclesDirectoryName)),
            "A cycles directory holding no cycle was created ahead of the step that owns it.");
    }

    [TestMethod]
    public async Task The_commit_message_names_every_file_adoption_wrote()
    {
        using var solution = Scratch.Create();

        Initialise.Response report = await Adopt(solution, tracked: []);
        string message = Templates.CommitMessage(report.Mode, report.Written);

        foreach (string path in report.Written)
        {
            StringAssert.Contains(message, path.Replace('\\', '/'));
        }

        StringAssert.Contains(
            message,
            "untracked",
            "The reason adoption commits at all belongs in the message: an untracked file is untracked "
            + "when the first cycle takes its diff.");
    }

    private static async Task<Initialise.Response> Adopt(
        Scratch solution,
        IReadOnlyList<string> tracked,
        Platform platform = Platform.GitHub)
    {
        using Gate gate = Adopter(solution, new FakeRepository { Tracked = tracked });

        Result<Initialise.Response> result = await gate
            .Send<Initialise.Command, Initialise.Response>(new Initialise.Command("dotnet-default", platform));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    /// <summary>The gate with git substituted, which is what decides adoption's three modes.</summary>
    private static Gate Adopter(Scratch solution, FakeRepository git) =>
        Gate.For(solution.Root, services => services.AddSingleton<IRepository>(git));


    /// <summary>A scratch directory that is not a cycle workspace, for adoption's sake.</summary>
    private sealed class Scratch : IDisposable
    {
        private Scratch(string root) => Root = root;

        public string Root { get; }

        public static Scratch Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-init", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new Scratch(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // A scratch directory left under the temp path is not worth failing a test over.
            }
        }
    }
}
