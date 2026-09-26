using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// The check that would have caught the gap: nothing was invocable.
/// </summary>
/// <remarks>
/// Every stage of this framework was built, tested and committed while the plugin manifest
/// did not exist, so not one component could be reached by name. Worse, the dispatcher
/// generated a prompt instructing an agent to run eight <c>/sdlc:…</c> commands, and those
/// commands were strings inside a string — no compiler, no analyzer and no schema sees
/// them, so the whole chain read as complete.
/// <para>
/// That is <c>unresolvable-reference</c>, the most common finding class v01 measured,
/// appearing in the tooling built to detect it. It is checkable, so it is checked here:
/// the manifest must exist and name the plugin, and every <c>/sdlc:…</c> reference in
/// shipped tooling must resolve to a workflow or a skill that actually ships.
/// </para>
/// </remarks>
[TestClass]
public sealed class PluginManifestTests
{
    /// <summary>
    /// The plugin name, which is also the command namespace. Hardcoded deliberately: it is
    /// the one string that couples the manifest to every reference in the shipped tooling,
    /// and deriving it from the manifest would let a rename pass silently.
    /// </summary>
    private const string PluginName = "sdlc";

    private static string Root => AppContext.BaseDirectory;

    private static string ManifestPath =>
        Path.Combine(Root, ".claude-plugin", "plugin.json");

    private static JsonElement Manifest()
    {
        Assert.IsTrue(File.Exists(ManifestPath),
            ".claude-plugin/plugin.json is missing. Without it nothing in this repository " +
            "is invocable: no workflow becomes a command and no skill loads.");

        return JsonDocument.Parse(File.ReadAllText(ManifestPath)).RootElement.Clone();
    }

    /// <summary>
    /// Command names a workflow contributes. A plugin workflow is named by its
    /// <c>meta.name</c>, not by its filename, so the filename is not the answer.
    /// </summary>
    private static HashSet<string> WorkflowCommands()
    {
        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root, "workflows"), "*.js"))
        {
            Match m = Regex.Match(
                SourceText.StripJavaScriptComments(File.ReadAllText(file)),
                @"export\s+const\s+meta\s*=\s*\{[^}]*?\bname:\s*'(?<name>[^']+)'",
                RegexOptions.Singleline);

            Assert.IsTrue(m.Success,
                $"{Path.GetFileName(file)} has no literal meta.name, so it contributes no " +
                "command at all — Claude Code drops a workflow whose meta is not a pure literal");

            Assert.IsTrue(names.Add(m.Groups["name"].Value),
                $"two workflows both declare meta.name '{m.Groups["name"].Value}'; " +
                "one would shadow the other and which one is undefined");
        }

        return names;
    }

    /// <summary>
    /// Command names a skill contributes: a directory under <c>skills/</c> holding a
    /// <c>SKILL.md</c>. A loose markdown file there is documentation, not a skill.
    /// </summary>
    private static HashSet<string> SkillCommands()
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        string dir = Path.Combine(Root, "skills");

        if (!Directory.Exists(dir))
        {
            return names;
        }

        foreach (string sub in Directory.EnumerateDirectories(dir))
        {
            if (File.Exists(Path.Combine(sub, "SKILL.md")))
            {
                names.Add(Path.GetFileName(sub));
            }
        }

        return names;
    }

    /// <summary>
    /// Every <c>/sdlc:…</c> reference in shipped tooling, as (file, command) pairs.
    /// </summary>
    /// <remarks>
    /// Read from comment-stripped code, per <see cref="SourceText"/>. A comment saying
    /// "deliberately not <c>/sdlc:intent</c>, that skill does not exist yet" must not be
    /// read as a reference to it — the note explaining an absence is not the absence.
    /// <para>
    /// Scoped to executable tooling and <c>SKILL.md</c> instructions, which are the files
    /// whose text reaches an agent as something to run. Prose markdown is excluded: a
    /// README writing <c>/sdlc:&lt;name&gt;</c> as a placeholder is not a broken reference,
    /// and a check that says it is gets switched off.
    /// </para>
    /// </remarks>
    private static List<(string File, string Command)> References()
    {
        List<(string, string)> found = [];
        Regex pattern = new("/" + PluginName + @":(?<cmd>[a-z0-9][a-z0-9-]*)", RegexOptions.None);

        void Scan(string path, Func<string, string> strip)
        {
            string code = strip(File.ReadAllText(path));
            foreach (Match m in pattern.Matches(code))
            {
                found.Add((Path.GetFileName(path), m.Groups["cmd"].Value));
            }
        }

        foreach (string f in Directory.EnumerateFiles(Path.Combine(Root, "tools"), "*.ps1"))
        {
            Scan(f, SourceText.StripPowerShellComments);
        }

        foreach (string f in Directory.EnumerateFiles(Path.Combine(Root, "workflows"), "*.js"))
        {
            Scan(f, SourceText.StripJavaScriptComments);
        }

        string skills = Path.Combine(Root, "skills");
        if (Directory.Exists(skills))
        {
            foreach (string f in Directory.EnumerateFiles(skills, "SKILL.md", SearchOption.AllDirectories))
            {
                Scan(f, static s => s);
            }
        }

        return found;
    }

    [TestMethod]
    public void TheManifestNamesThePlugin()
    {
        JsonElement m = Manifest();

        Assert.AreEqual(PluginName, m.GetProperty("name").GetString(),
            "the plugin name IS the command namespace: every component is addressed as " +
            $"/{PluginName}:<name>, and renaming the plugin breaks every reference in the " +
            "shipped tooling without any compiler noticing");
    }

    [TestMethod]
    public void TheManifestNameIsAValidIdentifier()
    {
        string? name = Manifest().GetProperty("name").GetString();

        Assert.IsNotNull(name);
        Assert.IsTrue(Regex.IsMatch(name, "^[a-z0-9]+(-[a-z0-9]+)*$"),
            $"'{name}' is not kebab-case; the name is used verbatim in command names");
    }

    [TestMethod]
    public void TheManifestCarriesASemanticVersion()
    {
        // Distribution is item 13, but the version has to be right from the first publish:
        // a marketplace decides whether an install is an update by comparing this string.
        string? version = Manifest().GetProperty("version").GetString();

        Assert.IsNotNull(version);
        Assert.IsTrue(Regex.IsMatch(version, @"^\d+\.\d+\.\d+"),
            $"'{version}' is not a semantic version");
    }

    [TestMethod]
    public void TheHookConfigLivesWhereAnUndeclaredPluginLooks()
    {
        // Same reasoning as workflows below: `hooks/hooks.json` at the plugin root is loaded
        // automatically, so the manifest declares no `hooks` field and there is one place for
        // the path to be rather than two.
        string hooks = Path.Combine(Root, "hooks", "hooks.json");
        Assert.IsTrue(File.Exists(hooks),
            "no hooks/hooks.json, so the INV-08 gate is configured nowhere and never runs");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(hooks));
        Assert.IsTrue(doc.RootElement.TryGetProperty("PreToolUse", out JsonElement pre),
            "the gate must run BEFORE the edit; PostToolUse would report a charter already changed");

        Assert.IsTrue(pre.GetArrayLength() > 0, "PreToolUse declares no handler");

        string matcher = pre[0].GetProperty("matcher").GetString()!;
        foreach (string tool in new[] { "Edit", "Write" })
        {
            Assert.IsTrue(matcher.Contains(tool, StringComparison.Ordinal),
                $"the matcher must cover {tool}, or that tool can amend the charter unchecked: {matcher}");
        }

        if (m_ManifestHasHooksField(Manifest()))
        {
            Assert.Fail("the manifest declares a hooks field as well as hooks/hooks.json, " +
                "which is a second place for the path to drift");
        }
    }

    private static bool m_ManifestHasHooksField(JsonElement manifest) =>
        manifest.TryGetProperty("hooks", out _);

    [TestMethod]
    public void EveryHookCommandPointsAtAScriptThatExists()
    {
        // THE SILENT-FAILURE CASE. A hook whose script has been renamed does not fail closed:
        // powershell exits non-zero on a missing file, which is not the exit code that blocks,
        // so the protection would simply stop existing while hooks.json still looked correct.
        //
        // ${CLAUDE_PLUGIN_ROOT} resolves to the plugin root at runtime, so it resolves to Root
        // here — which is exactly the substitution being verified.
        string hooks = Path.Combine(Root, "hooks", "hooks.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(hooks));

        int checked_ = 0;
        foreach (JsonProperty evt in doc.RootElement.EnumerateObject())
        {
            foreach (JsonElement matcher in evt.Value.EnumerateArray())
            {
                foreach (JsonElement handler in matcher.GetProperty("hooks").EnumerateArray())
                {
                    if (!handler.TryGetProperty("args", out JsonElement args))
                    {
                        continue;
                    }

                    foreach (JsonElement arg in args.EnumerateArray())
                    {
                        string value = arg.GetString() ?? string.Empty;
                        if (!value.Contains("CLAUDE_PLUGIN_ROOT", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string relative = value
                            .Replace("${CLAUDE_PLUGIN_ROOT}", string.Empty, StringComparison.Ordinal)
                            .TrimStart('/');

                        string resolved = Path.Combine(Root, relative);
                        Assert.IsTrue(File.Exists(resolved),
                            $"{evt.Name} references '{value}', which resolves to a file that does " +
                            "not exist. The hook would not run and nothing would say so.");
                        checked_++;
                    }
                }
            }
        }

        Assert.IsTrue(checked_ > 0,
            "no hook command referenced a bundled script, so this test verified nothing");
    }

    [TestMethod]
    public void WorkflowsLiveWhereTheManifestSaysTheyDo()
    {
        // The manifest deliberately omits the `workflows` field because Claude Code already
        // scans `workflows/` at the plugin root, and a redundant declaration is a second
        // place for the path to drift. That omission is only safe while the scripts are
        // actually there, which is what this asserts. A declaration that disagrees with the
        // default is worse than either, so it is rejected outright.
        JsonElement m = Manifest();

        Assert.IsTrue(Directory.EnumerateFiles(Path.Combine(Root, "workflows"), "*.js").Any(),
            "no workflow scripts at workflows/, the only place an undeclared plugin looks");

        if (m.TryGetProperty("workflows", out JsonElement declared))
        {
            Assert.AreEqual("./workflows/", declared.GetString(),
                "if the field is declared at all it must agree with where the scripts are");
        }
    }

    [TestMethod]
    public void EveryStageOfTheChainContributesACommand()
    {
        // Workflows and skills both contribute /sdlc:<name>, so what matters is the UNION.
        // Which side a stage falls on is SkillTests' and WorkflowScriptTests' business; here
        // the only question is whether the stage is reachable at all.
        HashSet<string> commands = WorkflowCommands();
        commands.UnionWith(SkillCommands());

        foreach (string stage in new[]
                 {
                     "intent", "spec", "interrogate", "repair", "plan", "build", "verify",
                     "retro", "run", "init", "absorb-retro", "grill", "maintain",
                 })
        {
            Assert.IsTrue(commands.Contains(stage),
                $"nothing contributes '{stage}', so /{PluginName}:{stage} resolves to nothing");
        }
    }

    [TestMethod]
    public void EveryReferencedCommandResolves()
    {
        List<(string File, string Command)> refs = References();

        // The guard that keeps this test honest. If the regex, the stripping or the copy to
        // the output directory breaks, `refs` goes empty and every assertion below passes
        // vacuously — which is the exact shape of the failure being checked for.
        Assert.IsTrue(refs.Count > 0,
            $"found no /{PluginName}: references at all. The dispatcher's prompt contains " +
            "several, so this means the scan is broken, not that the tooling is clean");

        HashSet<string> resolvable = WorkflowCommands();
        resolvable.UnionWith(SkillCommands());

        List<string> dangling = [.. refs
            .Where(r => !resolvable.Contains(r.Command))
            .Select(r => $"{r.File} → /{PluginName}:{r.Command}")
            .Distinct(StringComparer.Ordinal)];

        Assert.AreEqual(0, dangling.Count,
            "these references resolve to nothing that ships, so an agent told to run them " +
            "fails at the first step:\n  " + string.Join("\n  ", dangling) +
            $"\nShipped: {string.Join(", ", resolvable.OrderBy(x => x, StringComparer.Ordinal))}");
    }

    [TestMethod]
    public void SkillsAreDirectoriesHoldingASkillFile()
    {
        // A skill is a directory with a SKILL.md. A stray .md at the top of skills/ is
        // documentation and contributes no command; a directory WITHOUT a SKILL.md is the
        // real hazard, because it looks like a skill in the tree and loads as nothing.
        string dir = Path.Combine(Root, "skills");

        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string sub in Directory.EnumerateDirectories(dir))
        {
            Assert.IsTrue(File.Exists(Path.Combine(sub, "SKILL.md")),
                $"skills/{Path.GetFileName(sub)}/ has no SKILL.md, so it loads as nothing");
        }
    }
}
