using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// The README lists what ships, so something has to fail when it stops being true.
/// </summary>
/// <remarks>
/// A document naming every skill, workflow and script is a second representation of the tree. The
/// charter forbids introducing one without, in the same change, the check that fails when the two
/// disagree — so this is that check, and it ships with the document rather than after it.
/// <para>
/// BOTH DIRECTIONS, because they fail differently and the second is the one that actually happens.
/// A component the README does not mention is undocumented, which is mildly bad. A component the
/// README mentions that does not exist is an instruction to run something that is not there, and
/// that is the <c>unresolvable-reference</c> class this whole framework was built to catch —
/// measured as the commonest finding in the previous version, and committed once already in this
/// one, where ten stages shipped with a dispatcher naming eight commands that resolved to nothing.
/// </para>
/// </remarks>
[TestClass]
public sealed class ReadmeTests
{
    private const string PluginName = "sdlc";

    private static string Root => AppContext.BaseDirectory;

    private static string Readme => File.ReadAllText(Path.Combine(Root, "README.md"));

    private static IEnumerable<string> ShippedSkills() =>
        Directory.EnumerateDirectories(Path.Combine(Root, "skills"))
            .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
            .Select(Path.GetFileName)
            .Select(n => n!);

    /// <summary>The name a workflow contributes is its <c>meta.name</c>, not its filename.</summary>
    private static IEnumerable<string> ShippedWorkflows()
    {
        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root, "workflows"), "*.js"))
        {
            Match m = Regex.Match(File.ReadAllText(file), @"name:\s*'(?<name>[a-z0-9][a-z0-9-]*)'");
            if (m.Success)
            {
                yield return m.Groups["name"].Value;
            }
        }
    }

    private static IEnumerable<string> ShippedScripts() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tools"))
            .Where(f => f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Select(n => n!);

    [TestMethod]
    public void TheReadmeShips()
    {
        // The guard that keeps the rest honest: if the copy to the output directory breaks, every
        // assertion below would read an empty string and pass by finding nothing to disagree with.
        Assert.IsTrue(File.Exists(Path.Combine(Root, "README.md")), "no README.md in the output");
        Assert.IsTrue(Readme.Length > 2000, "the README is too short to be the document");
    }

    [TestMethod]
    public void EverySkillAndWorkflowIsNamedWithItsCommand()
    {
        string readme = Readme;
        List<string> missing = [];

        foreach (string name in ShippedSkills().Concat(ShippedWorkflows()))
        {
            // Named with its command rather than merely mentioned, because how it is CALLED is
            // the thing a reader came for and the thing most likely to be wrong.
            if (!readme.Contains($"/{PluginName}:{name}", StringComparison.Ordinal))
            {
                missing.Add(name);
            }
        }

        Assert.AreEqual(0, missing.Count,
            "the README does not say how to call: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryCommandTheReadmeNamesResolves()
    {
        HashSet<string> shipped = new(ShippedSkills().Concat(ShippedWorkflows()), StringComparer.Ordinal);

        MatchCollection referenced = Regex.Matches(
            Readme, "/" + PluginName + @":(?<cmd>[a-z0-9][a-z0-9-]*)");

        Assert.IsTrue(referenced.Count > 0,
            "found no commands in the README at all, so this check verified nothing");

        List<string> dangling = [.. referenced
            .Select(m => m.Groups["cmd"].Value)
            .Where(c => !shipped.Contains(c))
            .Distinct(StringComparer.Ordinal)];

        Assert.AreEqual(0, dangling.Count,
            "the README tells a reader to run commands that do not ship: " +
            string.Join(", ", dangling));
    }

    [TestMethod]
    public void EveryShippedScriptIsNamed()
    {
        string readme = Readme;
        List<string> missing = [];

        foreach (string script in ShippedScripts())
        {
            if (!readme.Contains(script, StringComparison.Ordinal))
            {
                missing.Add(script);
            }
        }

        Assert.AreEqual(0, missing.Count,
            "the README's script table is missing: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryScriptTheReadmeNamesExists()
    {
        HashSet<string> shipped = new(ShippedScripts(), StringComparer.OrdinalIgnoreCase);

        MatchCollection referenced = Regex.Matches(Readme, @"\b[A-Za-z][A-Za-z0-9.\-]*\.(ps1|cs)\b");

        Assert.IsTrue(referenced.Count > 0, "found no script names in the README");

        List<string> dangling = [.. referenced
            .Select(m => m.Value)
            .Where(n => !shipped.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        Assert.AreEqual(0, dangling.Count,
            "the README names scripts that do not exist under tools/: " +
            string.Join(", ", dangling));
    }

    [TestMethod]
    public void TheSevenStagesAreNamedInOrder()
    {
        // The stage names are the playbook's, kept verbatim so the vocabulary is shared with the
        // official docs and every other tool in the space. A README that renamed one would be
        // teaching a reader a word nothing else uses.
        string readme = Readme;
        int at = 0;

        foreach (string stage in new[] { "intent", "spec", "plan", "build", "verify", "deploy", "maintain" })
        {
            int found = readme.IndexOf(stage, at, StringComparison.Ordinal);
            Assert.IsTrue(found >= 0, $"the README does not name the {stage} stage in order");
            at = found;
        }
    }
}
