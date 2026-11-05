using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace Sdlc.Core;

/// <summary>
/// The release a solution pins in its local tool manifest, and where the restored copy of it lives.
/// </summary>
/// <remarks>
/// <para><strong>The manifest is the pin, and the branch carries it.</strong> A cycle's worktree is cut from the
/// integration branch, so it holds the manifest that branch held, and a bump reaches the cycle only when a person
/// merges the integration branch into it (D-131). Nothing here decides which release is right; it reads which one
/// was chosen.</para>
/// <para><strong>Found the way <c>dotnet</c> finds it.</strong> Upward from the working directory, the first
/// <c>.config/dotnet-tools.json</c> or <c>dotnet-tools.json</c> that names this package, stopping at a manifest
/// marked <c>isRoot</c>. A second rule for finding it would one day disagree with <c>dotnet tool restore</c>
/// about which version a directory pins.</para>
/// </remarks>
public static class PinnedRelease
{
    /// <summary>The package the binary is published as.</summary>
    public const string PackageId = "MofonCode.Sdlc.Cli";

    // NuGet lowercases a package id in a manifest key and in the global packages folder, by its own rule rather
    // than by culture, so it is written out rather than converted.
    private const string LowerId = "mofoncode.sdlc.cli";

    /// <summary>Where adoption writes the manifest, relative to the solution root.</summary>
    public const string ManifestPath = ".config/dotnet-tools.json";

    /// <summary>
    /// Set on a process the global binary handed on to, holding the global binary's release. The pinned binary
    /// never hands on again, whatever it reads, so two manifests can never bounce a command between them.
    /// </summary>
    public const string HandedOnVariable = "SDLC_HANDED_ON";

    /// <summary>The version a directory pins, or null when no manifest above it names the package.</summary>
    /// <param name="directory">Where to start looking.</param>
    /// <returns>The pinned version.</returns>
    public static string? Find(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        for (DirectoryInfo? at = new(directory); at is not null; at = at.Parent)
        {
            foreach (string candidate in (string[])[Path.Combine(at.FullName, ".config", "dotnet-tools.json"), Path.Combine(at.FullName, "dotnet-tools.json")])
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                JsonNode? manifest = Parse(candidate);
                string? version = Version(manifest);

                if (version is not null)
                {
                    return version;
                }

                if (manifest?["isRoot"]?.GetValueKind() == JsonValueKind.True)
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>A manifest pinning one release: a new one, or an existing one with this package's entry set.</summary>
    /// <remarks>
    /// A solution may already keep other local tools in the manifest, so only this package's entry is the
    /// framework's. A file that does not parse is not rewritten, because a relaxed parse would drop whatever it
    /// could not read on the way back out; the caller prints the entry instead, as it does for the host settings.
    /// </remarks>
    /// <param name="existing">The manifest as it stands, or null when there is none.</param>
    /// <param name="version">The release.</param>
    /// <returns>The file's text, or why it could not be written.</returns>
    public static Result<string> Pin(string? existing, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        JsonObject manifest;

        if (existing is null)
        {
            manifest = new JsonObject { ["version"] = 1, ["isRoot"] = true, ["tools"] = new JsonObject() };
        }
        else
        {
            try
            {
                manifest = JsonNode.Parse(existing) as JsonObject
                    ?? throw new JsonException("not an object");
            }
            catch (JsonException)
            {
                return Result.Fail($"{ManifestPath} is not JSON this can read, so it was left as it is. Add the {PackageId} entry below to its tools by hand.");
            }
        }

        if (manifest["tools"] is not JsonObject tools)
        {
            tools = new JsonObject();
            manifest["tools"] = tools;
        }

        foreach (string id in tools.Select(entry => entry.Key).Where(id => string.Equals(id, PackageId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            tools.Remove(id);
        }

        tools[LowerId] = new JsonObject
        {
            ["version"] = version,
            ["commands"] = new JsonArray("sdlc"),
            ["rollForward"] = false,
        };

        return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    /// The entry point of a restored release, or null when <c>dotnet tool restore</c> has not fetched it on this
    /// machine.
    /// </summary>
    /// <remarks>
    /// A restored local tool is an ordinary package in the global packages folder, so it is started as
    /// <c>dotnet &lt;entry&gt;</c> rather than through <c>dotnet tool run</c>, whose own options would sit in front
    /// of the arguments being forwarded.
    /// </remarks>
    /// <param name="version">The release.</param>
    /// <returns>The path to its <c>sdlc.dll</c>.</returns>
    public static string? Restored(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        string tools = Path.Combine(packages, LowerId, Lower(version), "tools");

        return Directory.Exists(tools)
            ? Directory.EnumerateFiles(tools, "sdlc.dll", SearchOption.AllDirectories).Order(StringComparer.Ordinal).LastOrDefault()
            : null;
    }

    /// <summary>The major, minor and patch of a release, with any prerelease label and build metadata dropped.</summary>
    /// <param name="version">A release such as <c>0.37.0-preview.12</c>.</param>
    /// <returns>Such as <c>0.37.0</c>.</returns>
    public static string Core(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        int end = version.IndexOfAny(['-', '+']);
        return end < 0 ? version : version[..end];
    }

    // A version is ASCII letters, digits, dots and dashes, so lowering it needs no culture and no analyzer's worry.
    private static string Lower(string version) =>
        string.Concat(version.Select(c => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c));

    private static JsonNode? Parse(string path)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            // dotnet refuses the file too, so there is no pin here for either of us to follow.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? Version(JsonNode? manifest)
    {
        if (manifest?["tools"] is not JsonObject tools)
        {
            return null;
        }

        foreach ((string id, JsonNode? entry) in tools)
        {
            if (string.Equals(id, PackageId, StringComparison.OrdinalIgnoreCase)
                && entry?["version"]?.GetValueKind() == JsonValueKind.String)
            {
                return entry["version"]!.GetValue<string>();
            }
        }

        return null;
    }
}
