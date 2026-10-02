using FluentResults;
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

        InitReport report = await Adopt(solution, tracked: ["README.md", "LICENSE", ".gitignore"]);

        Assert.AreEqual(InitMode.Scaffold, report.Mode);
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

        InitReport report = await Adopt(solution, tracked: ["README.md", "src/App/Program.cs"]);

        Assert.AreEqual(InitMode.Onboard, report.Mode);
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
        InitReport again = await Adopt(solution, tracked: ["src/App/Program.cs", "src/App/Roll.cs"]);

        Assert.AreEqual(InitMode.Update, again.Mode);
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

        InitReport report = await Adopt(solution, tracked: []);

        Assert.AreEqual(
            InitChange.Kept,
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

        InitReport report = await Adopt(solution, tracked: []);

        Assert.AreEqual(
            InitChange.Updated,
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
        string path = Path.Combine(solution.Root, InitHandler.AgentsFileName);
        await File.WriteAllTextAsync(path, "instructions nobody should lose\n");

        InitReport report = await Adopt(solution, tracked: ["src/App/Program.cs"]);

        Assert.AreEqual(
            InitChange.Printed,
            report.Actions.Single(action => action.Path == InitHandler.AgentsFileName).Change);

        Assert.AreEqual("instructions nobody should lose\n", await File.ReadAllTextAsync(path));
        Assert.IsTrue(report.Printed.ContainsKey(InitHandler.AgentsFileName));
    }

    [TestMethod]
    public async Task Adoption_refuses_outside_a_git_repository()
    {
        // Without git, build cannot record the diff verify derives from and the gates key on a branch that
        // cannot exist -- so every hook would permit everything while the artifacts still looked governed.
        using var solution = Scratch.Create();

        Result<InitReport> result = await new InitHandler(solution.Root, new FakeRepository { Repository = false })
            .Handle(new InitCommand("dotnet-default", Platform.GitHub), CancellationToken.None);

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

        InitReport report = await Adopt(solution, tracked: [], platform: platform);

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

        InitReport report = await Adopt(solution, tracked: []);

        Assert.IsTrue(
            File.Exists(Path.Combine(solution.Root, Cycle.SdlcDirectoryName, "hooks", "pre-push")),
            string.Join("; ", report.Actions.Select(action => action.Path)));

        Assert.IsFalse(
            Directory.Exists(Path.Combine(solution.Root, ".git", "hooks")),
            "Adoption must not write into .git, which is not tracked.");

        StringAssert.Contains(InitRenderer.Render(report), "git config core.hooksPath .sdlc/hooks");
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

        InitReport report = await Adopt(solution, tracked: []);
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

    private static async Task<InitReport> Adopt(
        Scratch solution,
        IReadOnlyList<string> tracked,
        Platform platform = Platform.GitHub)
    {
        Result<InitReport> result = await new InitHandler(solution.Root, new FakeRepository { Tracked = tracked })
            .Handle(new InitCommand("dotnet-default", platform), CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }


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
