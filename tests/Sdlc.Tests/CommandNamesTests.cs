using Sdlc.Core;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The commands this release answers and the ones it retired, and the files only a person writes that still name a
/// retired one (D-224).
/// </summary>
[TestClass]
public sealed class CommandNamesTests
{
    [TestMethod]
    public void Every_retired_command_is_one_the_binary_answers()
    {
        // Read from the one switch over line.Verb, as the CONCEPT table's test reads it: a retired command is answered.
        string program = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "Sdlc.Cli", "Program.cs"));
        int dispatch = program.IndexOf("return line.Verb switch", StringComparison.Ordinal);
        int closes = program.IndexOf("_ => await Unknown", dispatch, StringComparison.Ordinal);
        Assert.IsTrue(dispatch >= 0 && closes > dispatch, "The dispatch switch has moved.");

        List<string> dispatched = ["help", "version"];

        foreach (string line in program[dispatch..closes].ReplaceLineEndings("\n").Split('\n'))
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith('"') && trimmed.Contains("=> await", StringComparison.Ordinal))
            {
                dispatched.Add(trimmed[1..trimmed.IndexOf('"', 1)]);
            }
        }

        foreach (CommandNames.Retirement retired in CommandNames.Retired)
        {
            CollectionAssert.Contains(dispatched, retired.Was.Split(' ')[0], $"'sdlc {retired.Was}' is retired and nothing answers it.");
        }
    }

    [TestMethod]
    [DataRow("propose constraints", "harvest constraints")]
    [DataRow("propose decisions --cycles x", "harvest decisions")]
    [DataRow("propose", "harvest")]
    [DataRow("mine", "harvest")]
    [DataRow("draft mining", "draft harvest <file>")]
    public void A_retired_command_is_answered_with_what_replaced_it(string typed, string now)
    {
        CommandNames.Retirement? retired = CommandNames.RetirementOf(typed);

        Assert.IsNotNull(retired);
        Assert.AreEqual(now, retired.Now);
        StringAssert.Contains(retired.Answer(), "is now 'sdlc " + now + "'");
    }

    [TestMethod]
    public void A_current_command_is_not_retired()
    {
        Assert.IsNull(CommandNames.RetirementOf("harvest constraints"));
        Assert.IsNull(CommandNames.RetirementOf("draft approval"));
    }

    [TestMethod]
    public void A_text_naming_a_retired_command_names_each_with_its_line_and_what_replaces_it()
    {
        string text = "# Rules\n\n`sdlc propose constraints`\nnames the next id. `sdlc constraints --new` too.\n`sdlc spec <id>`, `sdlc mine` and plain sdlc mine.";

        IReadOnlyList<CommandNames.Stale> stale = CommandNames.StaleIn(text);

        Assert.AreEqual(2, stale.Count, string.Join("; ", stale));
        Assert.AreEqual(3, stale[0].Line);
        Assert.AreEqual("propose constraints", stale[0].Mention);
        StringAssert.StartsWith(stale[0].Instead, "sdlc harvest constraints");
        Assert.AreEqual(5, stale[1].Line);
        Assert.AreEqual("mine", stale[1].Mention, "A stage typed at the binary is not a retired command.");
    }

    [TestMethod]
    public void The_constraints_draft_names_no_command_and_the_readme_names_the_ones_it_dropped()
    {
        // A release can rename a command and only a person can change constraints.md; the README every upgrade rewrites.
        Assert.IsFalse(Templates.Constraints().Contains("`sdlc ", StringComparison.Ordinal));
        Assert.AreEqual(0, CommandNames.StaleIn(Templates.Readme()).Count, "The README names no retired command.");
        StringAssert.Contains(Templates.Readme(), "`sdlc constraints --new`");
        StringAssert.Contains(Templates.Readme(), "`sdlc harvest constraints`");
    }
}
