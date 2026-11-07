using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sdlc.Core.Hooks;

/// <summary>
/// Whether the skills a session loaded are the release of the binary that runs where it started.
/// </summary>
/// <remarks>
/// <para><strong>Why this exists.</strong> MarketSense's steer session ran an interrogate skill newer than its
/// binary. The commands the skill named were not there, so it wrote reader scripts of its own and ran the rounds
/// with those (D-131). A skill and a binary from different releases is not a fault either can detect from
/// inside, because each is internally consistent; this compares them once, as the session starts.</para>
/// <para><strong>It says so, and never refuses.</strong> A session started for something else -- reading,
/// a question, an upgrade -- should not be blocked by the thing it may be about to fix.</para>
/// <para><strong>The release's numbers only.</strong> A preview binary carries a label the plugin manifest
/// does not, so <c>0.37.0-preview.4</c> and <c>0.37.0</c> are one release here.</para>
/// </remarks>
public static class PluginRelease
{
    /// <summary>What to tell the session, or null when the two are one release or the plugin cannot be read.</summary>
    /// <param name="pluginRoot">The plugin's directory, as the host passes it to a hook.</param>
    /// <param name="binary">This binary's release.</param>
    /// <returns>The warning.</returns>
    public static string? Mismatch(string? pluginRoot, string binary)
    {
        ArgumentNullException.ThrowIfNull(binary);

        string? plugin = Version(pluginRoot);

        if (plugin is null
            || string.Equals(PinnedRelease.Core(plugin), PinnedRelease.Core(binary), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"The ai-sdlc skills loaded in this session are release {plugin}, and the sdlc binary this " +
            $"directory runs is {binary}. A skill from one release driving a binary from another is how a cycle " +
            "ended up running rounds through scripts nobody wrote down (D-131). Before running any ai-sdlc skill, " +
            "tell the person, and do not work around a command the binary does not have. Starting the session " +
            "with 'sdlc claude' instead of 'claude' loads the skills the binary carries, which are always its own " +
            "release.";
    }

    /// <summary>What to tell the session and the person when the binary is a local build, or null when it is not.</summary>
    /// <remarks>
    /// Said every session rather than once, because a variable left set from last week is the way this goes wrong:
    /// every solution on the machine would be judged by a build nobody published, and nothing else would say so
    /// until a pull request was refused (D-135).
    /// </remarks>
    /// <param name="binary">This binary's release.</param>
    /// <returns>The notice.</returns>
    public static string? LocalBuild(string binary)
    {
        ArgumentNullException.ThrowIfNull(binary);

        if (!PinnedRelease.IsLocal(binary))
        {
            return null;
        }

        string from = PinnedRelease.DevBuild() is { } devBuild ? $" from {devBuild}" : string.Empty;

        return $"The sdlc binary running here is {binary}, a local build{from}, not the release this directory pins. " +
            "Every round it writes is marked local, and no cycle a local build judged can open a pull request: it is a " +
            $"test run, done again on a published release before it ships (D-135). Unset {PinnedRelease.DevBuildVariable} " +
            "to run the pinned release.";
    }

    private static string? Version(string? pluginRoot)
    {
        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            return null;
        }

        foreach (string candidate in (string[])[Path.Combine(pluginRoot, ".claude-plugin", "plugin.json"), Path.Combine(pluginRoot, "plugin.json")])
        {
            try
            {
                if (File.Exists(candidate)
                    && JsonNode.Parse(File.ReadAllText(candidate))?["version"] is JsonValue value
                    && value.TryGetValue(out string? version)
                    && !string.IsNullOrWhiteSpace(version))
                {
                    return version;
                }
            }
            catch (JsonException)
            {
                // A manifest the host itself could not have loaded. Nothing to compare.
            }
            catch (IOException)
            {
            }
        }

        return null;
    }
}
