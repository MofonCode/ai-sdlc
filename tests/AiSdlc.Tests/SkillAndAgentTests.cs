namespace AiSdlc.Tests;

/// <summary>
/// Skill and agent definitions: the frontmatter that decides whether they load, and the
/// bodies that decide whether their instructions can actually be followed.
/// </summary>
/// <remarks>
/// Frontmatter validation alone is not enough, and this repository learned that the hard
/// way: a manifest-check command once shipped with literal backslash-n characters in it and
/// a closing fence followed by prose, which swallowed the rest of its section. The skill
/// loaded perfectly and simply instructed the agent to run a command that cannot execute.
/// </remarks>
[TestClass]
public sealed class SkillAndAgentTests
{
    private static readonly char Backslash = (char)92;

    private static IEnumerable<string> Definitions() =>
        Marketplace.SkillFiles().Concat(Marketplace.AgentFiles());

    [TestMethod]
    public void EveryDefinition_HasFrontmatterAsItsFirstLine()
    {
        foreach (string file in Definitions())
        {
            Assert.IsNotNull(
                Frontmatter.Read(file),
                Marketplace.Rel(file) + " has no frontmatter as its first line, so it is never loaded");
        }
    }

    [TestMethod]
    public void EveryDefinition_DeclaresNameAndDescription()
    {
        foreach (string file in Definitions())
        {
            IReadOnlyDictionary<string, string> fm = Frontmatter.Read(file)
                ?? throw new AssertFailedException(Marketplace.Rel(file) + " has no frontmatter");

            foreach (string key in new[] { "name", "description" })
            {
                Assert.IsTrue(
                    fm.ContainsKey(key) && fm[key].Length > 0,
                    Marketplace.Rel(file) + " frontmatter is missing `" + key + "`");
            }
        }
    }

    [TestMethod]
    public void EverySkillName_MatchesItsDirectory()
    {
        foreach (string file in Marketplace.SkillFiles())
        {
            IReadOnlyDictionary<string, string> fm = Frontmatter.Read(file)
                ?? throw new AssertFailedException(Marketplace.Rel(file) + " has no frontmatter");

            string directory = Path.GetFileName(Path.GetDirectoryName(file)!);
            Assert.AreEqual(
                directory,
                fm.GetValueOrDefault("name"),
                Marketplace.Rel(file) + " frontmatter name does not match its directory");
        }
    }

    [TestMethod]
    public void EveryAgentName_MatchesItsFilename()
    {
        foreach (string file in Marketplace.AgentFiles())
        {
            IReadOnlyDictionary<string, string> fm = Frontmatter.Read(file)
                ?? throw new AssertFailedException(Marketplace.Rel(file) + " has no frontmatter");

            Assert.AreEqual(
                Path.GetFileNameWithoutExtension(file),
                fm.GetValueOrDefault("name"),
                Marketplace.Rel(file) + " frontmatter name does not match its filename");
        }
    }

    [TestMethod]
    public void EveryBody_HasBalancedCodeFences()
    {
        List<string> problems = [];

        foreach (string file in Definitions())
        {
            string[] lines = File.ReadAllLines(file);
            bool inFence = false;
            int openedAt = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!inFence)
                {
                    inFence = true;
                    openedAt = i + 1;
                }
                else if (trimmed == "```")
                {
                    inFence = false;
                }
                else
                {
                    // A closing fence with trailing text does not close the block. Everything
                    // below it is swallowed into the code block and never read as instructions.
                    problems.Add(
                        $"{Marketplace.Rel(file)}:{i + 1} closing fence has trailing text; "
                        + $"it does not close the block opened at line {openedAt}");
                    inFence = false;
                }
            }

            if (inFence)
            {
                problems.Add($"{Marketplace.Rel(file)} unclosed code fence opened at line {openedAt}");
            }
        }

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void NoBody_ContainsALiteralBackslashN()
    {
        List<string> problems = [];
        string needle = Backslash + "n";

        foreach (string file in Definitions())
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                // A literal backslash-n is never intended in a command or in prose. It is the
                // signature of a string written through one escaping layer too many. Table
                // rows are exempt because a table may legitimately discuss one.
                if (lines[i].Contains(needle, StringComparison.Ordinal)
                    && !lines[i].TrimStart().StartsWith('|'))
                {
                    problems.Add($"{Marketplace.Rel(file)}:{i + 1} {lines[i].Trim()}");
                }
            }
        }

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }
}
