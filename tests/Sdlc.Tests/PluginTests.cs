using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The manifests decide whether the plugin loads at all. v02 shipped a hooks.json whose event keys
/// sat outside the <c>hooks</c> object, so nothing loaded, and it went green through CI because the
/// validation step was advisory. These assertions are not advisory.
/// </summary>
[TestClass]
public sealed class PluginTests
{
    private static readonly string[] OnlyHooks = ["hooks"];

    private static JsonDocument Read(string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(RepoPaths.At(relativePath.Split('/'))));

    [TestMethod]
    public void PluginManifestIdentifiesThePlugin()
    {
        using JsonDocument manifest = Read(".claude-plugin/plugin.json");

        Assert.AreEqual("sdlc", manifest.RootElement.GetProperty("name").GetString());
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(manifest.RootElement.GetProperty("version").GetString()),
            "A version is what a findings document records to make its verdict reproducible.");
    }

    [TestMethod]
    public void MarketplaceEntryAgreesWithThePluginManifest()
    {
        using JsonDocument manifest = Read(".claude-plugin/plugin.json");
        using JsonDocument marketplace = Read(".claude-plugin/marketplace.json");

        string pluginName = manifest.RootElement.GetProperty("name").GetString()!;

        string[] listed = [.. marketplace.RootElement.GetProperty("plugins").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString()!)];

        CollectionAssert.Contains(
            listed,
            pluginName,
            "The marketplace does not list the plugin this repository ships, so installing it by " +
            "name would fail.");
    }

    [TestMethod]
    public void HookEventsAreNestedInsideTheHooksObject()
    {
        using JsonDocument hooks = Read("hooks/hooks.json");

        string[] topLevel = [.. hooks.RootElement.EnumerateObject().Select(p => p.Name)];

        CollectionAssert.AreEqual(
            OnlyHooks,
            topLevel,
            "Every event must sit inside the hooks object. An event key at the top level makes the " +
            "whole plugin fail to load, silently.");
    }

    [TestMethod]
    public void EveryHookScriptTheManifestNamesExists()
    {
        using JsonDocument hooks = Read("hooks/hooks.json");

        int checked_ = 0;

        foreach (JsonProperty ev in hooks.RootElement.GetProperty("hooks").EnumerateObject())
        {
            foreach (JsonElement matcher in ev.Value.EnumerateArray())
            {
                foreach (JsonElement hook in matcher.GetProperty("hooks").EnumerateArray())
                {
                    Assert.IsTrue(hook.TryGetProperty("type", out _), $"A {ev.Name} hook has no type.");

                    if (!hook.TryGetProperty("args", out JsonElement args))
                    {
                        continue;
                    }

                    foreach (JsonElement arg in args.EnumerateArray())
                    {
                        string value = arg.GetString()!;

                        if (!value.Contains("${CLAUDE_PLUGIN_ROOT}", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string relative = value.Replace("${CLAUDE_PLUGIN_ROOT}/", string.Empty, StringComparison.Ordinal);

                        Assert.IsTrue(
                            File.Exists(RepoPaths.At(relative.Split('/'))),
                            $"A {ev.Name} hook names {relative}, which does not exist. A hook whose " +
                            "script is missing protects nothing while appearing to.");

                        checked_++;
                    }
                }
            }
        }

        Assert.IsTrue(checked_ > 0, "No hook scripts were checked, so this test proves nothing.");
    }

    [TestMethod]
    public void EveryToolScriptIsReachedFromSomewhere()
    {
        // A script nothing invokes is either dead or a guardrail somebody forgot to wire up, and the
        // second is indistinguishable from protection that works. Two things reach a script: the
        // hook manifest, and a skill that injects its output at load time.
        string manifest = File.ReadAllText(RepoPaths.At("hooks", "hooks.json"));

        string[] skillBodies = [.. Directory
            .EnumerateFiles(RepoPaths.Skills, "SKILL.md", SearchOption.AllDirectories)
            .Select(File.ReadAllText)];

        foreach (string script in Directory.EnumerateFiles(RepoPaths.At("tools"), "*.ps1"))
        {
            string name = Path.GetFileName(script);

            bool reached = manifest.Contains(name, StringComparison.Ordinal)
                || skillBodies.Any(b => b.Contains(name, StringComparison.Ordinal));

            Assert.IsTrue(
                reached,
                $"tools/{name} is named by neither hooks.json nor any skill. Nothing runs it.");
        }
    }

    [TestMethod]
    public void EveryInjectedScriptASkillNamesExists()
    {
        // A load-time injection that cannot run abandons the whole skill invocation. That is the
        // behaviour we want when rules are missing, and a disaster when the cause is a typo.
        foreach (string file in Directory.EnumerateFiles(RepoPaths.Skills, "SKILL.md", SearchOption.AllDirectories))
        {
            string body = File.ReadAllText(file);

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                body,
                @"\$\{CLAUDE_PLUGIN_ROOT\}/(?<path>[^\s""`]+)",
                System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(2)))
            {
                string relative = match.Groups["path"].Value;

                Assert.IsTrue(
                    File.Exists(RepoPaths.At(relative.Split('/'))),
                    $"{Path.GetFileName(Path.GetDirectoryName(file))} references {relative}, which " +
                    "does not exist. A failed injection abandons the skill.");
            }
        }
    }
}
