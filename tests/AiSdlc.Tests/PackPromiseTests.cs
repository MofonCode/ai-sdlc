using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiSdlc.Tests;

/// <summary>
/// A pack's skill tells <c>init</c> what to write into a scaffolded repository's charter. This
/// checks that what it promises there is what its own manifest emits.
/// </summary>
/// <remarks>
/// The pack's skill is otherwise forbidden from restating what <c>scaffold.json</c> declares,
/// because two lists with nothing comparing them drift and one of them quietly stops being
/// true. That is exactly what happened here: the charter line committed every scaffolded
/// repository to a per-module layout with an Application project, while the web kind emitted
/// one solution, six projects and no Application anywhere. Every repo this pack created was
/// told in its own charter to follow a structure its own scaffold had not given it, and
/// spec-writer reads that charter as binding.
/// <para>
/// The charter line is allowed to exist because this check exists. It reads the promise out of
/// SKILL.md and the emitted projects out of the manifest, per kind, and fails naming the
/// project and the document that left it out.
/// </para>
/// <para>
/// Like the other document-against-document checks here, it never reads a scaffolded
/// repository: a repo created last month records what the pack promised then, and validating
/// it against today's manifest would make history fail.
/// </para>
/// </remarks>
[TestClass]
public sealed class PackPromiseTests
{
    /// <summary>The manifest every pack ships, beside its own SKILL.md.</summary>
    private const string ScaffoldManifest = "scaffold.json";

    /// <summary>The plugin that is the engine; every other plugin is a stack pack.</summary>
    private const string EnginePlugin = "sdlc-core";

    /// <summary>
    /// The charter line, one per kind. The capture is the kind name; what follows on that line
    /// and its continuations is the promise.
    /// </summary>
    private static readonly Regex PromiseLine = new(
        @"^-\s+Solution layout\s+\(`(?<kind>[A-Za-z0-9_-]+)`\s+kind\)\s*:",
        RegexOptions.Compiled);

    /// <summary>A promised project, written as <c>`&lt;dir&gt;/{Solution}.&lt;Role&gt;`</c>.</summary>
    private static readonly Regex PromisedProject = new(
        @"`(?<dir>[A-Za-z0-9_-]+)/\{Solution\}\.(?<role>[A-Za-z][A-Za-z0-9]*)`",
        RegexOptions.Compiled);

    [TestMethod]
    public void EveryPackPromisesTheLayoutItScaffolds()
    {
        List<(string Name, string SkillDirectory)> packs = StackPacks().ToList();
        Assert.IsTrue(packs.Count > 0, "found no stack pack, so this check asserted nothing");

        List<string> problems = [];
        int compared = 0;

        foreach ((string name, string skillDirectory) in packs)
        {
            string manifestPath = Path.Combine(skillDirectory, ScaffoldManifest);
            string skillPath = Path.Combine(skillDirectory, "SKILL.md");
            Assert.IsTrue(File.Exists(manifestPath), name + " ships no " + ScaffoldManifest);
            Assert.IsTrue(File.Exists(skillPath), name + " ships no SKILL.md");

            Dictionary<string, SortedSet<string>> emitted = EmittedByKind(manifestPath);
            Dictionary<string, SortedSet<string>> promised = PromisedByKind(skillPath);

            Assert.IsTrue(
                emitted.Count > 0,
                Marketplace.Rel(manifestPath)
                    + " declares no kind that adds a project to a solution, so this check"
                    + " asserted nothing");

            foreach ((string kind, SortedSet<string> emits) in emitted)
            {
                if (!promised.TryGetValue(kind, out SortedSet<string>? claims))
                {
                    problems.Add(
                        "  " + name + ": " + Marketplace.Rel(skillPath)
                            + " has no charter line for the `" + kind + "` kind, which emits "
                            + string.Join(", ", emits));
                    continue;
                }

                // A line that parsed but named nothing would otherwise pass silently.
                if (claims.Count == 0)
                {
                    problems.Add(
                        "  " + name + ": the `" + kind + "` charter line in "
                            + Marketplace.Rel(skillPath)
                            + " names no <dir>/{Solution}.<Role> project, so it promises nothing");
                    continue;
                }

                compared++;

                foreach (string project in claims.Except(emits))
                {
                    problems.Add(
                        "  " + name + "/" + kind + ": " + Marketplace.Rel(skillPath)
                            + " promises " + project + ", which " + ScaffoldManifest
                            + " never adds to the solution");
                }

                foreach (string project in emits.Except(claims))
                {
                    problems.Add(
                        "  " + name + "/" + kind + ": " + ScaffoldManifest + " emits " + project
                            + ", which the charter line in " + Marketplace.Rel(skillPath)
                            + " never names");
                }
            }

            foreach (string kind in promised.Keys.Except(emitted.Keys))
            {
                problems.Add(
                    "  " + name + ": " + Marketplace.Rel(skillPath)
                        + " has a charter line for a `" + kind + "` kind that "
                        + ScaffoldManifest + " does not declare");
            }
        }

        Assert.IsTrue(
            compared > 0,
            "compared no kind against its promise, so this check asserted nothing");

        Assert.AreEqual(
            0,
            problems.Count,
            "a pack promises a layout it does not scaffold; the charter line it writes is read"
                + " as binding by every spec that follows:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems));
    }

    /// <summary>Every stack pack in the marketplace, with the directory its own skill lives in.</summary>
    private static IEnumerable<(string Name, string SkillDirectory)> StackPacks()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            if (string.Equals(plugin.Name, EnginePlugin, StringComparison.Ordinal))
            {
                continue;
            }

            yield return (plugin.Name, Path.Combine(plugin.Directory, "skills", plugin.Name));
        }
    }

    /// <summary>
    /// The <c>&lt;dir&gt;/{Solution}.&lt;Role&gt;</c> projects each kind puts in its solution.
    /// </summary>
    /// <remarks>
    /// Read from the <c>dotnet sln ... add</c> action rather than from every path the kind
    /// mentions. That action is the definitive list: a project the solution does not reference
    /// is not built, not tested, and not part of the layout anyone sees, whatever else emitted
    /// it. The solution token comes from the manifest, because which string a pack substitutes
    /// is the pack's decision and hardcoding it here would rebuild the coupling the manifest
    /// exists to remove.
    /// </remarks>
    private static Dictionary<string, SortedSet<string>> EmittedByKind(string manifestPath)
    {
        using JsonDocument document = Marketplace.Parse(manifestPath);
        JsonElement root = document.RootElement;

        string? token = Text(root, "solution_token");
        Assert.IsNotNull(
            token,
            Marketplace.Rel(manifestPath) + " declares no solution_token, so nothing here can"
                + " tell a pack's own spelling from a string this suite made up");

        Dictionary<string, SortedSet<string>> byKind = [];

        if (!root.TryGetProperty("kinds", out JsonElement kinds)
            || kinds.ValueKind != JsonValueKind.Object)
        {
            return byKind;
        }

        foreach (JsonProperty kind in kinds.EnumerateObject())
        {
            if (!kind.Value.TryGetProperty("actions", out JsonElement actions)
                || actions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            SortedSet<string> projects = [];

            foreach (JsonElement action in actions.EnumerateArray())
            {
                List<string> args = Arguments(action);
                if (Text(action, "kind") != "run"
                    || !args.Contains("sln", StringComparer.Ordinal)
                    || !args.Contains("add", StringComparer.Ordinal))
                {
                    continue;
                }

                foreach (string argument in args)
                {
                    if (ProjectIn(argument, token) is string project)
                    {
                        projects.Add(project);
                    }
                }
            }

            if (projects.Count > 0)
            {
                byKind[kind.Name] = projects;
            }
        }

        return byKind;
    }

    /// <summary>
    /// <c>&lt;dir&gt;/{Solution}.&lt;Role&gt;</c> for a project path, spelled the way the
    /// promise spells it, or null when the argument is not a project path.
    /// </summary>
    private static string? ProjectIn(string argument, string token)
    {
        Match match = Regex.Match(
            argument.Replace('\\', '/'),
            @"^(?<dir>[A-Za-z0-9_-]+)/" + Regex.Escape(token)
                + @"\.(?<role>[A-Za-z][A-Za-z0-9]*)/");

        return match.Success
            ? match.Groups["dir"].Value + "/{Solution}." + match.Groups["role"].Value
            : null;
    }

    /// <summary>The projects the skill's charter line promises, per kind.</summary>
    /// <remarks>
    /// A promise may wrap across lines; it ends at the next bullet, the next heading, a fence
    /// or a blank line. Everything between is read for project names.
    /// </remarks>
    private static Dictionary<string, SortedSet<string>> PromisedByKind(string skillPath)
    {
        string[] lines = File.ReadAllLines(skillPath);
        Dictionary<string, SortedSet<string>> byKind = [];

        for (int i = 0; i < lines.Length; i++)
        {
            Match start = PromiseLine.Match(lines[i].TrimStart());
            if (!start.Success)
            {
                continue;
            }

            SortedSet<string> projects = [];
            for (int j = i; j < lines.Length; j++)
            {
                if (j > i)
                {
                    string trimmed = lines[j].TrimStart();
                    if (trimmed.Length == 0
                        || trimmed.StartsWith('-')
                        || trimmed.StartsWith('#')
                        || trimmed.StartsWith("```", StringComparison.Ordinal))
                    {
                        break;
                    }
                }

                foreach (Match project in PromisedProject.Matches(lines[j]))
                {
                    projects.Add(
                        project.Groups["dir"].Value
                            + "/{Solution}." + project.Groups["role"].Value);
                }
            }

            byKind[start.Groups["kind"].Value] = projects;
        }

        return byKind;
    }

    /// <summary>The non-empty string arguments an action declares.</summary>
    private static List<string> Arguments(JsonElement action)
    {
        List<string> args = [];
        if (!action.TryGetProperty("args", out JsonElement declared)
            || declared.ValueKind != JsonValueKind.Array)
        {
            return args;
        }

        foreach (JsonElement argument in declared.EnumerateArray())
        {
            if (argument.ValueKind == JsonValueKind.String
                && argument.GetString() is { Length: > 0 } text)
            {
                args.Add(text);
            }
        }

        return args;
    }

    /// <summary>A non-empty string field of <paramref name="element"/>, or null.</summary>
    private static string? Text(JsonElement element, string field) =>
        element.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
                ? text
                : null;
}
