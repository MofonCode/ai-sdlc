using System.Text.Json.Nodes;
using FluentResults;
using Sdlc.Core;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// The release a solution pins, how the global binary finds it, and the check that the skills match (D-131).
/// </summary>
[TestClass]
public sealed class PinnedReleaseTests
{
    [TestMethod]
    public void A_directory_below_the_manifest_pins_what_it_names()
    {
        using var scratch = Scratch.Create();
        scratch.Write(".config/dotnet-tools.json", PinnedRelease.Pin(null, "0.37.0").Value);
        string nested = scratch.Directory("src/App");

        Assert.AreEqual("0.37.0", PinnedRelease.Find(nested));
    }

    [TestMethod]
    public void A_root_manifest_that_does_not_name_the_package_stops_the_search()
    {
        // dotnet stops at isRoot, so this must too: a parent's pin is not this solution's.
        using var scratch = Scratch.Create();
        scratch.Write(".config/dotnet-tools.json", PinnedRelease.Pin(null, "0.37.0").Value);
        scratch.Write("inner/.config/dotnet-tools.json", """{ "version": 1, "isRoot": true, "tools": { "dotnet-ef": { "version": "9.0.0", "commands": ["dotnet-ef"] } } }""");

        Assert.IsNull(PinnedRelease.Find(scratch.Directory("inner/src")));
    }

    [TestMethod]
    public void The_package_is_found_however_its_id_is_cased()
    {
        using var scratch = Scratch.Create();
        scratch.Write("dotnet-tools.json", """{ "version": 1, "tools": { "MofonCode.Sdlc.Cli": { "version": "0.38.1", "commands": ["sdlc"] } } }""");

        Assert.AreEqual("0.38.1", PinnedRelease.Find(scratch.Root));
    }

    [TestMethod]
    public void Pinning_keeps_every_other_tool_and_replaces_only_its_own_entry()
    {
        const string existing = """
            { "version": 1, "isRoot": true, "tools": {
              "dotnet-ef": { "version": "9.0.0", "commands": ["dotnet-ef"] },
              "MofonCode.Sdlc.Cli": { "version": "0.36.0", "commands": ["sdlc"] } } }
            """;

        JsonObject tools = JsonNode.Parse(PinnedRelease.Pin(existing, "0.37.0").Value)!["tools"]!.AsObject();

        Assert.AreEqual("9.0.0", tools["dotnet-ef"]!["version"]!.GetValue<string>());
        Assert.AreEqual("0.37.0", tools["mofoncode.sdlc.cli"]!["version"]!.GetValue<string>());
        Assert.HasCount(2, tools, "The old casing of the entry was replaced, not kept beside the new one.");
    }

    [TestMethod]
    public void A_manifest_that_does_not_parse_is_left_alone()
    {
        Result<string> pinned = PinnedRelease.Pin("{ \"tools\": { // a comment }", "0.37.0");

        Assert.IsTrue(pinned.IsFailed);
    }

    [TestMethod]
    [DataRow("0.37.0-preview.12", "0.37.0")]
    [DataRow("0.37.0+abc123", "0.37.0")]
    [DataRow("0.37.0", "0.37.0")]
    public void The_core_of_a_release_drops_its_label(string version, string core)
    {
        Assert.AreEqual(core, PinnedRelease.Core(version));
    }

    [TestMethod]
    [DataRow("0.36.7-local", true)]
    [DataRow("0.36.7-local.20261106-0930", true)]
    [DataRow("0.36.7-LOCAL+abc123", true)]
    [DataRow("0.36.7", false)]
    [DataRow("0.36.7-preview", false)]
    [DataRow("0.36.7-preview.local", false)]
    [DataRow("0.36.7-localhost", false)]
    public void A_local_build_is_named_by_its_first_label(string version, bool local)
    {
        Assert.AreEqual(local, PinnedRelease.IsLocal(version));
    }

    [TestMethod]
    [DataRow("0.36.7", "0.36.7-local")]
    [DataRow("0.36.7-preview", "0.36.7-local")]
    [DataRow("0.36.7-local.20261106-0930", "0.36.7-local.20261106-0930")]
    public void A_dev_build_calls_itself_local_and_keeps_a_label_that_already_says_so(string stamped, string reported)
    {
        Assert.AreEqual(reported, PinnedRelease.AsLocal(stamped));
    }

    [TestMethod]
    public void A_dev_build_is_found_at_the_top_of_its_directory_or_alone_beneath_it()
    {
        using var output = Scratch.Create();
        output.Write("sdlc.dll", string.Empty);
        output.Write("other/sdlc.dll", string.Empty);

        Assert.AreEqual(Path.Combine(output.Root, "sdlc.dll"), PinnedRelease.DevBuildEntry(output.Root).Value);

        using var installed = Scratch.Create();
        installed.Write(".store/mofoncode.sdlc.cli/0.36.7-local/tools/net10.0/any/sdlc.dll", string.Empty);

        StringAssert.EndsWith(PinnedRelease.DevBuildEntry(installed.Root).Value, "sdlc.dll");
    }

    [TestMethod]
    public void Two_dev_builds_in_one_directory_are_refused_rather_than_guessed_between()
    {
        // The build that writes a cycle's rounds is the one thing here that has to be the one meant.
        using var scratch = Scratch.Create();
        scratch.Write("a/sdlc.dll", string.Empty);
        scratch.Write("b/sdlc.dll", string.Empty);

        Result<string> entry = PinnedRelease.DevBuildEntry(scratch.Root);

        Assert.IsTrue(entry.IsFailed);
        StringAssert.Contains(entry.Errors[0].Message, "2 builds");
        Assert.IsTrue(PinnedRelease.DevBuildEntry(Path.Combine(scratch.Root, "missing")).IsFailed);
    }

    [TestMethod]
    public void A_directory_is_inside_the_dev_build_only_when_it_is_it_or_beneath_it()
    {
        string root = Path.Combine(Path.GetTempPath(), "sdlc-dev");

        Assert.IsTrue(PinnedRelease.IsInside(root, root));
        Assert.IsTrue(PinnedRelease.IsInside(Path.Combine(root, "plugin"), root));
        Assert.IsFalse(PinnedRelease.IsInside(root + "-other", root), "A sibling sharing a prefix is not inside.");
    }

    [TestMethod]
    public void A_local_build_is_announced_every_session_and_a_published_one_is_not()
    {
        string? local = PluginRelease.LocalBuild("0.36.7-local");

        Assert.IsNotNull(local);
        StringAssert.Contains(local, "0.36.7-local");
        StringAssert.Contains(local, PinnedRelease.DevBuildVariable);
        Assert.IsNull(PluginRelease.LocalBuild("0.36.7-preview"));
        Assert.IsNull(PluginRelease.LocalBuild("0.36.7"));
    }

    [TestMethod]
    public void Skills_from_another_release_are_named_with_both_releases()
    {
        using var scratch = Scratch.Create();
        scratch.Write(".claude-plugin/plugin.json", """{ "name": "sdlc", "version": "0.38.0" }""");

        string? mismatch = PluginRelease.Mismatch(scratch.Root, "0.37.0");

        Assert.IsNotNull(mismatch);
        StringAssert.Contains(mismatch, "0.38.0");
        StringAssert.Contains(mismatch, "0.37.0");
        StringAssert.Contains(mismatch, "do not work around");
        StringAssert.Contains(mismatch, "sdlc claude");
    }

    [TestMethod]
    [DataRow("0.37.0")]
    [DataRow("0.37.0-preview.4")]
    public void Skills_from_the_same_release_say_nothing(string binary)
    {
        using var scratch = Scratch.Create();
        scratch.Write(".claude-plugin/plugin.json", """{ "name": "sdlc", "version": "0.37.0" }""");

        Assert.IsNull(PluginRelease.Mismatch(scratch.Root, binary));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void No_plugin_directory_says_nothing(string? root)
    {
        Assert.IsNull(PluginRelease.Mismatch(root, "0.37.0"));
    }

    [TestMethod]
    public void The_binary_carries_its_own_releases_skills_and_hooks_for_a_session_to_start_with()
    {
        string plugin = Sdlc.Cli.ClaudeSession.PluginDirectory;

        Assert.IsNull(PluginRelease.Mismatch(plugin, Release.Version), "The packed plugin is another release.");

        string[] packed = [.. Directory.GetDirectories(Path.Combine(plugin, "skills")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        string[] shipped = [.. Directory.GetDirectories(Path.Combine(RepoPaths.Root, "skills")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];
        CollectionAssert.AreEqual(shipped, packed);

        Assert.AreEqual(
            File.ReadAllText(Path.Combine(RepoPaths.Root, "hooks", "hooks.json")),
            File.ReadAllText(Path.Combine(plugin, "hooks", "hooks.json")));
    }

    private sealed class Scratch : IDisposable
    {
        private Scratch(string root) => Root = root;

        public string Root { get; }

        public static Scratch Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdlc-pin", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(root);
            return new Scratch(root);
        }

        public void Write(string relative, string contents)
        {
            string path = Path.Combine(Root, relative);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
        }

        public string Directory(string relative) =>
            System.IO.Directory.CreateDirectory(Path.Combine(Root, relative)).FullName;

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A scratch directory a virus scanner still has open. Nothing here depends on it going.
            }
        }
    }
}
