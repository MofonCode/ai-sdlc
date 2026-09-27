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

    private static string AgentsMd => File.ReadAllText(RepoPaths.At("AGENTS.md"));

    private static string[] DocFiles => [.. Directory
        .EnumerateFiles(RepoPaths.Docs, "*.md")
        .Select(p => "docs/" + Path.GetFileName(p))
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
