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

    /// <summary>
    /// A directory holding a build of the binary that nobody published. Set, the global binary hands every command
    /// to that build instead of the release the directory pins, and the build calls itself local (D-135).
    /// </summary>
    public const string DevBuildVariable = "SDLC_DEV_BUILD";

    /// <summary>
    /// The prerelease label of a build nobody published: <c>0.36.7-local</c>, or <c>0.36.7-local.20261106-0930</c>
    /// for a pack. A round it writes proves a change to the framework and never reaches a pull request (D-135).
    /// </summary>
    public const string LocalLabel = "local";

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

    /// <summary>The version a manifest's text pins, or null when it names no release or does not parse.</summary>
    /// <param name="text">The manifest's text, or null when there is none.</param>
    /// <returns>The pinned version.</returns>
    public static string? PinnedIn(string? text)
    {
        if (text is null)
        {
            return null;
        }

        try
        {
            return Version(JsonNode.Parse(text));
        }
        catch (JsonException)
        {
            return null;
        }
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

    /// <summary>Whether a release is a build nobody published: its prerelease label starts <c>local</c>.</summary>
    /// <param name="version">A release such as <c>0.36.7-local.20261106-0930</c>.</param>
    /// <returns>True for a local build.</returns>
    public static bool IsLocal(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        int plus = version.IndexOf('+', StringComparison.Ordinal);
        string bare = plus < 0 ? version : version[..plus];
        int dash = bare.IndexOf('-', StringComparison.Ordinal);

        if (dash < 0)
        {
            return false;
        }

        string label = bare[(dash + 1)..];
        int dot = label.IndexOf('.', StringComparison.Ordinal);
        return string.Equals(dot < 0 ? label : label[..dot], LocalLabel, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A release as a local build calls itself: unchanged when it already says so.</summary>
    /// <param name="version">The stamped release, such as <c>0.36.7</c>.</param>
    /// <returns>Such as <c>0.36.7-local</c>.</returns>
    public static string AsLocal(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return IsLocal(version) ? version : Core(version) + "-" + LocalLabel;
    }

    /// <summary>The directory <see cref="DevBuildVariable"/> names, in full, or null when it is not set.</summary>
    /// <returns>The directory.</returns>
    public static string? DevBuild() =>
        Environment.GetEnvironmentVariable(DevBuildVariable) is { Length: > 0 } named
            ? Path.GetFullPath(named.Trim())
            : null;

    /// <summary>Whether a directory is the dev build or inside it, so the build does not hand on to itself.</summary>
    /// <param name="directory">The directory a binary runs from.</param>
    /// <param name="devBuild">The directory <see cref="DevBuildVariable"/> names.</param>
    /// <returns>True when the one is the other or under it.</returns>
    public static bool IsInside(string directory, string devBuild)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(devBuild);

        string from = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(devBuild)) + Path.DirectorySeparatorChar;

        return from.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>sdlc.dll</c> a dev build directory holds: the one at its top, or the only one beneath it.
    /// </summary>
    /// <remarks>
    /// Two layouts are meant. A build's output, <c>src/Sdlc.Cli/bin/Debug/net10.0</c>, holds it at the top. A
    /// <c>dotnet tool install --tool-path</c> of a local pack holds it several folders down, under <c>.store</c>.
    /// More than one is refused rather than guessed between: the build that writes a cycle's rounds is the one
    /// thing here that has to be the one meant.
    /// </remarks>
    /// <param name="devBuild">The directory.</param>
    /// <returns>The path to its <c>sdlc.dll</c>, or why there is none.</returns>
    public static Result<string> DevBuildEntry(string devBuild)
    {
        ArgumentNullException.ThrowIfNull(devBuild);

        if (!Directory.Exists(devBuild))
        {
            return Result.Fail($"{DevBuildVariable} names {devBuild}, which is not a directory. Point it at a build's output, such as src/Sdlc.Cli/bin/Debug/net10.0, or unset it to run the release this directory pins.");
        }

        string top = Path.Combine(devBuild, "sdlc.dll");

        if (File.Exists(top))
        {
            return top;
        }

        string[] found = [.. Directory.EnumerateFiles(devBuild, "sdlc.dll", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

        return found.Length switch
        {
            1 => found[0],
            0 => Result.Fail($"{DevBuildVariable} names {devBuild}, which holds no sdlc.dll. Build ai-sdlc and point it at the output, or unset it."),
            _ => Result.Fail($"{DevBuildVariable} names {devBuild}, which holds {found.Length} builds of sdlc.dll: {string.Join(", ", found)}. Point it at the one meant."),
        };
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
