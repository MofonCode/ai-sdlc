using System.Text.Json;

namespace AiSdlc.Tests;

/// <summary>
/// Reads the marketplace layout: which plugins exist, and where their files live.
/// </summary>
internal static class Marketplace
{
    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    /// <summary>One plugin, as the marketplace declares it and as it exists on disk.</summary>
    internal sealed record Plugin(string Name, string Directory);

    public static string MarketplaceJson => Path.Combine(Repo.Root, ".claude-plugin", "marketplace.json");

    /// <summary>Parses a JSON file, or fails the calling test with the parse error.</summary>
    public static JsonDocument Parse(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path), Lenient);
        }
        catch (JsonException e)
        {
            Assert.Fail(Rel(path) + " is not valid JSON - " + e.Message);
            throw; // unreachable; Assert.Fail throws
        }
    }

    /// <summary>Every plugin the marketplace lists whose directory exists.</summary>
    public static IReadOnlyList<Plugin> Plugins()
    {
        using JsonDocument doc = Parse(MarketplaceJson);

        List<Plugin> plugins = [];
        if (!doc.RootElement.TryGetProperty("plugins", out JsonElement entries))
        {
            return plugins;
        }

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string name = entry.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "";
            string source = entry.TryGetProperty("source", out JsonElement s) ? s.GetString() ?? "" : "";
            plugins.Add(new Plugin(name, Path.GetFullPath(Path.Combine(Repo.Root, source))));
        }

        return plugins;
    }

    /// <summary>Every SKILL.md across every plugin.</summary>
    public static IEnumerable<string> SkillFiles() => PluginFiles("skills", "SKILL.md");

    /// <summary>Every agent definition across every plugin.</summary>
    public static IEnumerable<string> AgentFiles() => PluginFiles("agents", "*.md");

    /// <summary>Every workflow script across every plugin.</summary>
    public static IEnumerable<string> WorkflowFiles() => PluginFiles("workflows", "*.js");

    private static IEnumerable<string> PluginFiles(string subdirectory, string pattern)
    {
        string plugins = Path.Combine(Repo.Root, "plugins");
        if (!Directory.Exists(plugins))
        {
            yield break;
        }

        foreach (string plugin in Directory.EnumerateDirectories(plugins))
        {
            string dir = Path.Combine(plugin, subdirectory);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            // Agents are flat; skills nest one directory deep.
            SearchOption depth = pattern == "*.md" && subdirectory == "agents"
                ? SearchOption.TopDirectoryOnly
                : SearchOption.AllDirectories;

            foreach (string file in Directory.EnumerateFiles(dir, pattern, depth))
            {
                yield return file;
            }
        }
    }

    public static string Rel(string path) =>
        Path.GetRelativePath(Repo.Root, path).Replace(Path.DirectorySeparatorChar, '/');
}
