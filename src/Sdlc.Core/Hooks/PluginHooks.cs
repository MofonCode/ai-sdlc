using System.Text.Json;
using System.Text.Json.Nodes;
using Sdlc.Core.Init;

namespace Sdlc.Core.Hooks;

/// <summary>
/// Whether the plugin's copy of a hook steps aside for the one adoption wrote into the solution (D-172).
/// </summary>
/// <remarks>
/// <para><strong>Two registrations, and the host runs both.</strong> Adoption writes the framework's hooks into
/// the solution's settings, so a solution opened without the plugin is still governed (D-40), and the plugin
/// ships the same hooks, so a repository not yet adopted is guarded too. Claude Code merges identical hooks
/// across settings files but never a plugin's with a settings file's, so in every adopted solution each check ran
/// twice and every turn-end message was said twice.</para>
/// <para><strong>The plugin's copy says where it came from</strong>, with <c>--source plugin</c>, which is the
/// marker D-40 asked for. When the solution's own configuration runs the framework's hook for the same event, the
/// plugin's copy answers with nothing and the settings copy speaks alone; where it does not, the plugin's copy
/// is the guard and speaks. Read on every call, so a solution adopted mid-session is handed over at once.</para>
/// </remarks>
public static class PluginHooks
{
    /// <summary>The option the plugin's hook commands pass.</summary>
    public const string SourceOption = "source";

    /// <summary>The value they pass it.</summary>
    public const string PluginSource = "plugin";

    /// <summary>
    /// Whether the configuration adoption writes, in the solution holding <paramref name="directory"/>, runs the
    /// framework's hook for this event.
    /// </summary>
    /// <param name="directory">Where the hook runs: the solution or a directory inside it.</param>
    /// <param name="host">The host.</param>
    /// <param name="turnEnd">True for the turn end, false for the tool call.</param>
    /// <returns>True when the settings copy will answer, so the plugin's should not.</returns>
    public static bool AnsweredBySettings(string directory, HookHost host, bool turnEnd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        SupportedHost described = SupportedHosts.Of(host);
        string? eventName = turnEnd ? described.TurnEndEvent : described.PreToolEvent;

        if (eventName is null)
        {
            return false;
        }

        HashSet<string> wanted = Commands(JsonNode.Parse(Templates.HookConfig(host))?[described.EventsKey]?[eventName]);

        for (DirectoryInfo? at = new(Path.GetFullPath(directory)); at is not null; at = at.Parent)
        {
            string path = Path.Combine(at.FullName, described.ConfigPath);

            if (File.Exists(path))
            {
                return Holds(path, described.EventsKey, eventName, wanted);
            }

            // The repository's root is as far as a solution reaches.
            if (Directory.Exists(Path.Combine(at.FullName, ".git")) || File.Exists(Path.Combine(at.FullName, ".git")))
            {
                return false;
            }
        }

        return false;
    }

    private static bool Holds(string path, string eventsKey, string eventName, HashSet<string> wanted)
    {
        try
        {
            HashSet<string> present = Commands(JsonNode.Parse(File.ReadAllText(path))?[eventsKey]?[eventName]);
            return wanted.Count > 0 && wanted.IsSubsetOf(present);
        }
        catch (JsonException)
        {
            // A settings file the host cannot read either registers nothing, so the plugin's copy is the guard.
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // Every command string under one event, whatever shape the host nests it in.
    private static HashSet<string> Commands(JsonNode? node)
    {
        HashSet<string> found = new(StringComparer.Ordinal);
        Collect(node, found);
        return found;
    }

    private static void Collect(JsonNode? node, HashSet<string> found)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    Collect(item, found);
                }

                break;

            case JsonObject entry:
                foreach ((string key, JsonNode? value) in entry)
                {
                    if (key is "command" or "bash" or "powershell" && value is JsonValue text && text.TryGetValue(out string? command))
                    {
                        found.Add(command);
                    }
                    else
                    {
                        Collect(value, found);
                    }
                }

                break;
        }
    }
}
