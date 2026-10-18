using System.Text.RegularExpressions;
using Sdlc.Cli;
using Sdlc.Core.Perspectives;

namespace Sdlc.Tests;

/// <summary>
/// The two documents that are copies of something else on purpose, each held to what it copies.
/// </summary>
/// <remarks>
/// The cheat sheet is one cycle as a line per step, and the perspectives page tabulates the perspectives.
/// Both are accepted duplications, and AGENTS.md says accepted duplications are paid for mechanically
/// rather than promised. Until this file neither was: a renamed skill, a removed command or a new
/// perspective would have left either one wrong with the suite green.
/// </remarks>
[TestClass]
public sealed class DocumentCopyTests
{
    private static readonly string Root = RepoPaths.Root;

    private static string CheatSheet => File.ReadAllText(Path.Combine(Root, "docs", "cheat-sheet.md"));

    [TestMethod]
    [DataRow("README.md")]
    [DataRow("docs/README.md")]
    public void The_cheat_sheet_is_named_where_a_person_starts(string index)
    {
        // The index-parity tests cover the four document folders; the cheat sheet sits beside them, so a
        // row dropped from either index left it unreachable and nothing failed.
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(Root, index)),
            "cheat-sheet.md",
            index + " no longer links the cheat sheet.");
    }

    [TestMethod]
    public void Every_skill_the_cheat_sheet_names_is_a_skill_on_disk()
    {
        string[] named = [.. Regex.Matches(CheatSheet, @"/sdlc:(?<name>[a-z-]+)")
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)];

        Assert.IsTrue(named.Length > 0, "The cheat sheet names no skill, so this checked nothing.");

        foreach (string name in named)
        {
            Assert.IsTrue(
                Directory.Exists(Path.Combine(Root, "skills", name)),
                $"The cheat sheet names /sdlc:{name}, and there is no such skill.");
        }
    }

    [TestMethod]
    public void Every_command_the_cheat_sheet_names_is_one_the_binary_has()
    {
        // Matched in a code span only, so prose is not mistaken for a command.
        string[] named = [.. Regex.Matches(CheatSheet, @"`sdlc (?<verb>[a-z-]+)(?: (?<sub>[a-z]+))?")
            .Select(match => match.Groups["sub"].Success && Lists(match.Groups["verb"].Value + " " + match.Groups["sub"].Value)
                ? match.Groups["verb"].Value + " " + match.Groups["sub"].Value
                : match.Groups["verb"].Value)
            .Distinct(StringComparer.Ordinal)];

        Assert.IsTrue(named.Length > 0, "The cheat sheet names no command, so this checked nothing.");

        foreach (string verb in named)
        {
            Assert.IsTrue(Lists(verb), $"The cheat sheet names 'sdlc {verb}', and 'sdlc help' lists no such command.");
        }
    }

    // Whether the usage text has a line opening with the command, which is how it lists one.
    private static bool Lists(string command) =>
        Regex.IsMatch(Usage.Text, @"^[ \t]+" + Regex.Escape(command) + @"(?:[ \t]|$)", RegexOptions.Multiline);

    [TestMethod]
    public void The_perspectives_table_names_every_written_perspective_as_it_is_switched_on()
    {
        Dictionary<string, string> rows = PerspectiveRows();
        IReadOnlyList<Perspective> written = PerspectiveLibrary.All().Value;

        foreach (Perspective perspective in written)
        {
            Assert.IsTrue(
                rows.TryGetValue(perspective.Name, out string? onWhen),
                $"'{perspective.Name}' is a perspective the binary carries, and the page's table does not name it.");

            Assert.AreEqual(
                perspective.Always,
                string.Equals(onWhen, "always", StringComparison.Ordinal),
                $"The table says '{perspective.Name}' is on '{onWhen}', and its document says always: {perspective.Always}.");
        }
    }

    [TestMethod]
    public void The_perspectives_table_names_nothing_the_binary_or_the_interview_does_not()
    {
        // A perspective not written yet still has a name: the intent interview's last question brings it.
        // So the table's names are the written ones and the ones the interview names, and no others.
        string intent = File.ReadAllText(Path.Combine(Root, "skills", "intent", "SKILL.md"));
        HashSet<string> written = [.. PerspectiveLibrary.All().Value.Select(perspective => perspective.Name)];

        foreach (string name in PerspectiveRows().Keys)
        {
            Assert.IsTrue(
                written.Contains(name) || intent.Contains("`" + name + "`", StringComparison.Ordinal),
                $"The table names '{name}', which neither the binary carries nor the intent interview brings.");
        }
    }

    // Each row of the table under "## Which there are": the name in its first cell, and its last cell.
    private static Dictionary<string, string> PerspectiveRows()
    {
        string page = File.ReadAllText(Path.Combine(Root, "docs", "concepts", "perspectives.md"));
        int start = page.IndexOf("## Which there are", StringComparison.Ordinal);
        int end = page.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        Assert.IsTrue(start >= 0 && end > start, "The perspectives page has no 'Which there are' section.");

        Dictionary<string, string> rows = new(StringComparer.Ordinal);

        foreach (Match row in Regex.Matches(
            page[start..end],
            @"^\| `(?<name>[a-z-]+)` \|.*\| (?<when>[^|]+) \|\s*$",
            RegexOptions.Multiline))
        {
            rows[row.Groups["name"].Value] = row.Groups["when"].Value.Trim();
        }

        Assert.IsTrue(rows.Count > 0, "The table has no rows this could read, so this checked nothing.");
        return rows;
    }
}
