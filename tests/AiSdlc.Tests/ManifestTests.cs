using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiSdlc.Tests;

/// <summary>
/// The marketplace, plugin and hook manifests.
/// </summary>
/// <remarks>
/// Every failure here is silent at runtime. A plugin whose directory moved simply does not
/// install; a hook pointing at a deleted file errors on every edit; a version that is valid
/// semver but was never bumped makes `plugin update` a no-op that reports success.
/// </remarks>
[TestClass]
public sealed partial class ManifestTests
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex Semver();

    // The hook command is a shell line; pull the plugin-relative path back out of it.
    [GeneratedRegex(@"\$\{CLAUDE_PLUGIN_ROOT\}/([^""\s]+)")]
    private static partial Regex PluginRootPath();

    [TestMethod]
    public void MarketplaceJson_IsValidJson()
    {
        using JsonDocument doc = Marketplace.Parse(Marketplace.MarketplaceJson);
        Assert.IsTrue(
            doc.RootElement.TryGetProperty("plugins", out _),
            "marketplace.json declares no plugins");
    }

    [TestMethod]
    public void EveryListedPlugin_HasADirectoryThatExists()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            Assert.IsTrue(
                Directory.Exists(plugin.Directory),
                $"marketplace lists \"{plugin.Name}\" at a directory that does not exist: {plugin.Directory}");
        }
    }

    [TestMethod]
    public void EveryPlugin_HasAManifestWhoseNameMatchesTheMarketplace()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            string manifestPath = Path.Combine(plugin.Directory, ".claude-plugin", "plugin.json");
            Assert.IsTrue(File.Exists(manifestPath), Marketplace.Rel(plugin.Directory) + " has no plugin.json");

            using JsonDocument manifest = Marketplace.Parse(manifestPath);
            string name = manifest.RootElement.GetProperty("name").GetString() ?? "";

            Assert.AreEqual(
                plugin.Name,
                name,
                Marketplace.Rel(manifestPath) + " name does not match its marketplace entry");
        }
    }

    [TestMethod]
    public void EveryPluginVersion_IsSemver()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            string manifestPath = Path.Combine(plugin.Directory, ".claude-plugin", "plugin.json");
            using JsonDocument manifest = Marketplace.Parse(manifestPath);
            string version = manifest.RootElement.TryGetProperty("version", out JsonElement v)
                ? v.GetString() ?? ""
                : "";

            // Plugins are cached per version. A malformed version is not merely untidy:
            // it makes an update silently do nothing.
            Assert.IsTrue(
                Semver().IsMatch(version),
                $"{Marketplace.Rel(manifestPath)} version \"{version}\" is not semver");
        }
    }

    [TestMethod]
    public void EveryHookCommand_PointsAtAFileThatExists()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            string hooksPath = Path.Combine(plugin.Directory, "hooks", "hooks.json");
            if (!File.Exists(hooksPath))
            {
                continue;
            }

            using JsonDocument hooks = Marketplace.Parse(hooksPath);
            if (!hooks.RootElement.TryGetProperty("hooks", out JsonElement events))
            {
                continue;
            }

            foreach (JsonProperty ev in events.EnumerateObject())
            {
                foreach (JsonElement group in ev.Value.EnumerateArray())
                {
                    if (!group.TryGetProperty("hooks", out JsonElement entries))
                    {
                        continue;
                    }

                    foreach (JsonElement entry in entries.EnumerateArray())
                    {
                        string command = entry.TryGetProperty("command", out JsonElement c)
                            ? c.GetString() ?? ""
                            : "";

                        Match m = PluginRootPath().Match(command);
                        if (!m.Success)
                        {
                            continue;
                        }

                        string target = Path.Combine(
                            plugin.Directory,
                            m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));

                        Assert.IsTrue(
                            File.Exists(target),
                            $"{Marketplace.Rel(hooksPath)} command points at a missing file: {m.Groups[1].Value}");
                    }
                }
            }
        }
    }
}
