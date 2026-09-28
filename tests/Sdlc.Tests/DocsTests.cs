using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// The documentation model claims three properties: every doc is indexed exactly once, every index
/// row points at a file that exists, and every doc has a freshness signal. A claim nobody checks is
/// how documentation rots while looking maintained.
/// </summary>
[TestClass]
public sealed class DocsTests
{
    private const int MaxInstructionLines = 200;
    private const string FootnoteHeading = "## Constraints cited here";

    private static string AgentsMd => File.ReadAllText(RepoPaths.At("AGENTS.md"));

    private static string[] DocFiles => [.. Directory
        .EnumerateFiles(RepoPaths.Docs, "*.md", SearchOption.AllDirectories)
        .Select(p => "docs/" + Path.GetRelativePath(RepoPaths.Docs, p).Replace('\\', '/'))
        .OrderBy(p => p, StringComparer.Ordinal)];

    private static string[] IndexedPaths => [.. Regex
        .Matches(AgentsMd, @"^\|\s*`(?<path>docs/[^`]+)`\s*\|", RegexOptions.Multiline, TimeSpan.FromSeconds(2))
        .Select(m => m.Groups["path"].Value)
        .OrderBy(p => p, StringComparer.Ordinal)];

    [TestMethod]
    public void EveryDocIsIndexedExactlyOnce()
    {
        CollectionAssert.AreEqual(
            DocFiles,
            IndexedPaths,
            "Every file under docs/ appears in the AGENTS.md index exactly once, and every row " +
            "points at a file that exists. A doc nobody indexes will never be opened; a row " +
            "pointing nowhere sends a reader to a dead end.");
    }

    [TestMethod]
    public void EveryDocDeclaresAFreshnessSignal()
    {
        foreach (string doc in DocFiles)
        {
            string text = File.ReadAllText(RepoPaths.At(doc.Split('/')));

            Assert.IsTrue(
                text.StartsWith("---", StringComparison.Ordinal),
                $"{doc} has no frontmatter, so it has no freshness signal.");

            int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
            string frontmatter = text[3..(end > 0 ? end : text.Length)];

            bool covers = frontmatter.Contains("covers:", StringComparison.Ordinal);
            bool exempt = frontmatter.Contains("freshness: exempt", StringComparison.Ordinal);

            Assert.IsTrue(
                covers ^ exempt,
                $"{doc} must declare exactly one of covers: (it documents source, and is checked " +
                "against it) or freshness: exempt with a reason (it records decisions).");

            if (exempt)
            {
                Assert.IsTrue(
                    frontmatter.Contains("reason:", StringComparison.Ordinal),
                    $"{doc} claims exemption without a reason.");
            }
        }
    }

    [TestMethod]
    public void EveryCoversGlobPointsSomewhere()
    {
        foreach (string doc in DocFiles)
        {
            string text = File.ReadAllText(RepoPaths.At(doc.Split('/')));
            int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
            string frontmatter = text[3..(end > 0 ? end : text.Length)];

            foreach (Match m in Regex.Matches(frontmatter, @"^\s*-\s*""(?<glob>[^""]+)""",
                RegexOptions.Multiline, TimeSpan.FromSeconds(2)))
            {
                string glob = m.Groups["glob"].Value;
                string directory = glob.Contains('/', StringComparison.Ordinal)
                    ? glob[..glob.LastIndexOf('/')].Replace("**", string.Empty, StringComparison.Ordinal).TrimEnd('/')
                    : string.Empty;
                string pattern = glob[(glob.LastIndexOf('/') + 1)..];

                string searchRoot = string.IsNullOrEmpty(directory)
                    ? RepoPaths.Root
                    : RepoPaths.At(directory.Split('/'));

                Assert.IsTrue(
                    Directory.Exists(searchRoot) && Directory.EnumerateFiles(searchRoot, pattern,
                        SearchOption.AllDirectories).Any(),
                    $"{doc} covers '{glob}', which matches nothing. A doc measured against files " +
                    "that do not exist can never be stale, which makes the check a decoration.");
            }
        }
    }

    [TestMethod]
    public void EveryEnforcedByCitationNamesATestThatExists()
    {
        // docs/conventions.md claims that each rule a machine can check names the test that checks it.
        // That claim is the whole reason the document is worth more than advice, so it is verified here:
        // a citation pointing at a renamed or deleted test would leave a convention looking enforced
        // while nothing enforced it.
        //
        // Only the Enforced-by paragraphs are scanned. Elsewhere the document backticks ordinary
        // identifiers - PSCustomObject, WarningsNotAsErrors - which are not tests and never were.
        string conventions = File.ReadAllText(RepoPaths.At("docs", "conventions.md"));

        string[] paragraphs = [.. Regex
            .Matches(conventions, @"Enforced by:(?<body>.*?)(?:\r?\n\r?\n|$)",
                RegexOptions.Singleline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(2))
            .Select(m => m.Groups["body"].Value)];

        Assert.IsTrue(paragraphs.Length >= 15,
            $"Only {paragraphs.Length} Enforced-by paragraphs found; the document is meant to be dense with them.");

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        Type[] classes = [.. typeof(DocsTests).Assembly.GetTypes()
            .Where(t => t.Namespace == "Sdlc.Tests")];

        List<string> missing = [];
        int checked_ = 0;

        foreach (string paragraph in paragraphs)
        {
            foreach (Match match in Regex.Matches(paragraph,
                @"`(?:(?<type>[A-Za-z]+Tests)\.)?(?<name>[A-Z][A-Za-z0-9]{8,})`",
                RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(2)))
            {
                string name = match.Groups["name"].Value;
                string typeName = match.Groups["type"].Value;
                checked_++;

                IEnumerable<Type> candidates = string.IsNullOrEmpty(typeName)
                    ? classes
                    : classes.Where(c => c.Name == typeName);

                if (!candidates.Any(c => c.GetMethod(name, Any) is not null))
                {
                    missing.Add(match.Value);
                }
            }
        }

        Assert.IsTrue(checked_ >= 25, $"Only {checked_} citations were checked.");

        Assert.AreEqual(0, missing.Count,
            "conventions.md cites tests that do not exist: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EverySkillAppearsInAGuide() 
    {
        // A skill nobody documented is one nobody will run. The guides are where a person looks, so a
        // new skill that reaches none of them has shipped invisible.
        string guides = string.Concat(Directory
            .EnumerateFiles(Path.Combine(RepoPaths.Docs, "guides"), "*.md")
            .Select(File.ReadAllText));

        List<string> missing = [.. Directory
            .EnumerateDirectories(RepoPaths.Skills)
            .Select(d => Path.GetFileName(d)!)
            .Where(name => !guides.Contains($"/sdlc:{name}", StringComparison.Ordinal))];

        Assert.AreEqual(0, missing.Count,
            "These skills are named in no guide: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryCitedConstraintExistsAndIsFootnoted()
    {
        // A document citing a constraint should not send a reader away to find out what it says, and a
        // citation naming one that no longer exists is worse than none. Both are checkable.
        //
        // Lines where the id is a value rather than a reference are skipped: a JSON field, a YAML
        // frontmatter entry. Glossing or footnoting those would be describing data.
        string constraints = File.ReadAllText(RepoPaths.Constraints);

        HashSet<string> defined = [.. Regex
            .Matches(constraints, @"^## (?<id>CON_[0-9]{2,})", RegexOptions.Multiline, TimeSpan.FromSeconds(2))
            .Select(m => m.Groups["id"].Value)];

        Assert.IsTrue(defined.Count >= 6, "constraints.md defined too few constraints to check against.");

        List<string> problems = [];
        int citations = 0;

        foreach (string file in Directory.EnumerateFiles(RepoPaths.Docs, "*.md", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            string name = Path.GetFileName(file);

            int split = text.IndexOf(FootnoteHeading, StringComparison.Ordinal);
            string body = split >= 0 ? text[..split] : text;
            string footnote = split >= 0 ? text[split..] : string.Empty;

            HashSet<string> cited = [];

            foreach (string line in body.Split('\n'))
            {
                // An id inside quotes, or after a YAML key, is data.
                if (line.Contains('\"', StringComparison.Ordinal) || line.Contains("cites:", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match m in Regex.Matches(line, @"CON_[0-9]{2}", RegexOptions.None, TimeSpan.FromSeconds(2)))
                {
                    cited.Add(m.Value);
                }
            }

            foreach (string id in cited)
            {
                citations++;

                if (!defined.Contains(id))
                {
                    problems.Add($"{name} cites {id}, which constraints.md does not define");
                }
                else if (!footnote.Contains(id, StringComparison.Ordinal))
                {
                    problems.Add($"{name} cites {id} but its footnote omits it");
                }
            }
        }

        Assert.IsTrue(citations >= 10, $"Only {citations} citations found across the docs.");

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void GuidesNameTheRealPluginVersion()
    {
        // A guide that states a version has to state the one that ships. The first-cycle guide said
        // 0.1.0 across a whole release, which quietly told a reader they were verifying something other
        // than what they had installed.
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(RepoPaths.At(".claude-plugin", "plugin.json")));

        string shipped = manifest.RootElement.GetProperty("version").GetString()!;

        List<string> wrong = [];
        int claims = 0;

        foreach (string file in Directory.EnumerateFiles(Path.Combine(RepoPaths.Docs, "guides"), "*.md"))
        {
            string text = File.ReadAllText(file);

            foreach (Match match in Regex.Matches(
                text,
                @"Verified as of `(?<v>[0-9]+\.[0-9]+\.[0-9]+)`",
                RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(2)))
            {
                claims++;
                string claimed = match.Groups["v"].Value;

                if (claimed != shipped)
                {
                    wrong.Add($"{Path.GetFileName(file)} claims {claimed}, shipped is {shipped}");
                }
            }
        }

        Assert.IsTrue(claims > 0, "No guide states a verified-as-of version, so this test proves nothing.");

        Assert.AreEqual(0, wrong.Count, string.Join("; ", wrong));
    }

    [TestMethod]
    public void InstructionFilesStayShortEnoughToBeFollowed()
    {
        // Longer instruction files cost more context and are followed less. The limit is the
        // documented guidance, asserted so it cannot drift quietly.
        foreach (string file in new[] { "AGENTS.md", "CLAUDE.md" })
        {
            int lines = File.ReadAllLines(RepoPaths.At(file)).Length;

            Assert.IsTrue(
                lines <= MaxInstructionLines,
                $"{file} is {lines} lines, past the {MaxInstructionLines} line target. Move " +
                "detail behind a described pointer in the docs index.");
        }
    }
}
