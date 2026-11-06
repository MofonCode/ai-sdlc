using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core;
using Sdlc.Core.Chain;
using Sdlc.Core.Cycles;
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
