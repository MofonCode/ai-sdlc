using System.Diagnostics;
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
    private static readonly string[] _onlyHooks = ["hooks"];

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
            _onlyHooks,
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

    // ------------------------------------------------------------------ injectors

    private static string RunScript(string name)
    {
        ProcessStartInfo info = new("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        foreach (string argument in new[]
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", RepoPaths.At("tools", name),
        })
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start powershell.exe.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(0, process.ExitCode,
            $"{name} exited {process.ExitCode}. A failed injection abandons the skill. stderr: {stderr.Trim()}");

        return stdout;
    }

    [TestMethod]
    [DataRow("Get-Constraints.ps1", "CON_01")]
    [DataRow("Get-StackDefaults.ps1", "MofonCode.Core")]
    public void AnInjectorEmitsItsContentUncorrupted(string script, string expected)
    {
        // Windows PowerShell 5.1 assumes the system codepage when Get-Content reads a file with no BOM,
        // so an em dash round-trips as mojibake and a rule reaches the model corrupted. Both injectors
        // shipped with that defect: every interrogation was reading mangled constraint headings until a
        // -Encoding UTF8 on the read fixed it. Nothing caught it, because the text stayed readable.
        string output = RunScript(script);

        StringAssert.Contains(output, expected, StringComparison.Ordinal);

        Assert.IsFalse(
            output.Contains("\u00e2\u0080", StringComparison.Ordinal),
            $"{script} emitted mojibake - a UTF-8 sequence decoded as the system codepage. " +
            "Read the source with -Encoding UTF8.");

        StringAssert.Contains(output, "\u2014", StringComparison.Ordinal,
            $"{script} emitted no em dash at all, so this test is no longer checking anything.");
    }

    [TestMethod]
    public void EveryToolScriptIsExercisedByATest()
    {
        // Reachability is not coverage, and this repository had only the first. The reachability test
        // above asserts that something invokes each script; it says nothing about whether a test ever
        // runs one. Compare-DiffToPlan.ps1 satisfied reachability with no test at all, and shipped
        // taking its base from HEAD at run time - so one ordinary git commit during a build emptied the
        // diff and reported success with an undeclared file in it.
        //
        // There is no line-coverage number here on purpose: the C# in this repository is the test suite,
        // and the thing being covered is markdown and PowerShell. What matters is whether each shipped
        // script is exercised, and that is answerable directly.
        string[] testBodies = [.. Directory
            .EnumerateFiles(RepoPaths.At("tests", "Sdlc.Tests"), "*.cs")
            .Select(File.ReadAllText)];

        string[] scripts = [.. Directory.EnumerateFiles(RepoPaths.At("tools"), "*.ps1")];

        List<string> untested = [];

        foreach (string script in scripts)
        {
            string name = Path.GetFileName(script);

            if (testBodies.Any(body => body.Contains(name, StringComparison.Ordinal)))
            {
                continue;
            }

            // A dot-sourced library is covered through its callers, but only if a test actually runs
            // one of them. Otherwise "it is a library" is a way of never testing anything.
            bool coveredThroughACaller = scripts
                .Where(other => other != script)
                .Any(other =>
                    File.ReadAllText(other).Contains(name, StringComparison.Ordinal)
                    && testBodies.Any(body =>
                        body.Contains(Path.GetFileName(other), StringComparison.Ordinal)));

            if (!coveredThroughACaller)
            {
                untested.Add(name);
            }
        }

        Assert.AreEqual(
            0,
            untested.Count,
            "These scripts ship with no test exercising them, directly or through a caller: "
            + string.Join(", ", untested)
            + ". A guardrail nobody runs is indistinguishable from one that works.");
    }

    [TestMethod]
    public void EveryScriptReadsFilesWithAnExplicitEncoding()
    {
        // The rule above was enforced only for the two injectors, by observing their output. That left
        // every other script unchecked, and three of them were reading without -Encoding UTF8: the
        // settled-artifact gate, the artifact-shape gate, and the one that decides whether a cycle may
        // land. Their reads stay readable when mangled, so nothing would have surfaced it.
        List<string> unencoded = [];

        foreach (string script in Directory.EnumerateFiles(RepoPaths.At("tools"), "*.ps1"))
        {
            string[] lines = File.ReadAllLines(script);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                if (!line.Contains("Get-Content", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!line.Contains("-Encoding UTF8", StringComparison.Ordinal))
                {
                    unencoded.Add($"{Path.GetFileName(script)}:{i + 1}");
                }
            }
        }

        Assert.AreEqual(
            0,
            unencoded.Count,
            "These reads assume the system codepage, so a file with no BOM comes back mangled: "
            + string.Join(", ", unencoded)
            + ". Pass -Encoding UTF8.");
    }

    [TestMethod]
    public void TheStackDefaultsBlockIsMarkedAndSubstantial()
    {
        // The injector extracts between markers and throws without them, which abandons the skill rather
        // than letting a plan choose project shapes against nothing.
        string techstack = File.ReadAllText(RepoPaths.At("docs", "techstack.md"));

        StringAssert.Contains(techstack, "<!-- stack-defaults:begin -->", StringComparison.Ordinal);
        StringAssert.Contains(techstack, "<!-- stack-defaults:end -->", StringComparison.Ordinal);

        string output = RunScript("Get-StackDefaults.ps1");

        // The two defaults a plan most often gets wrong: orchestration nothing needs, and a library
        // named where a seam was wanted.
        StringAssert.Contains(output, "Aspire", StringComparison.Ordinal);
        StringAssert.Contains(output, "Mediator", StringComparison.Ordinal);
        StringAssert.Contains(output, "FluentResults", StringComparison.Ordinal);

        Assert.IsFalse(output.Contains("How this repository is built", StringComparison.Ordinal),
            "The injector leaked the section about building this repository, which is none of a " +
            "solution's business.");
    }

    [TestMethod]
    public void EveryToolScriptIsReachedFromSomewhere()
    {
        // A script nothing invokes is either dead or a guardrail somebody forgot to wire up, and the
        // second is indistinguishable from protection that works. Three things reach a script: the hook
        // manifest, a skill that runs or injects it, and another script that dot-sources it.
        string manifest = File.ReadAllText(RepoPaths.At("hooks", "hooks.json"));

        string[] skillBodies = [.. Directory
            .EnumerateFiles(RepoPaths.Skills, "SKILL.md", SearchOption.AllDirectories)
            .Select(File.ReadAllText)];

        string[] scripts = [.. Directory.EnumerateFiles(RepoPaths.At("tools"), "*.ps1")];

        foreach (string script in scripts)
        {
            string name = Path.GetFileName(script);

            bool sourced = scripts
                .Where(other => other != script)
                .Any(other => File.ReadAllText(other).Contains(name, StringComparison.Ordinal));

            bool reached = manifest.Contains(name, StringComparison.Ordinal)
                || skillBodies.Any(b => b.Contains(name, StringComparison.Ordinal))
                || sourced;

            Assert.IsTrue(
                reached,
                $"tools/{name} is named by hooks.json, no skill, and no other script. Nothing runs it.");
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
