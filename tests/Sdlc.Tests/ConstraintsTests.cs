using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// The global constraints are the framework's product, so the properties the concept claims about
/// them are asserted rather than trusted. CON-11 says every constraint must forbid something;
/// these tests are what stop that from being a sentence nobody checks.
/// </summary>
[TestClass]
public sealed class ConstraintsTests
{
    private static readonly Regex Heading = new(
        @"^## (?<id>CON-(?<n>\d{2,})) — (?<statement>.+)$",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(2));

    // Words that would make a global constraint judge a solution on a stack it never chose.
    private static readonly string[] StackWords =
    [
        "dotnet", ".net", "csharp", "c#", "msbuild", "nuget", "mstest", "xunit", "nunit",
        "blazor", "maui", "aspire", "asp.net", "sql server", "cosmos", "ml.net",
        "python", "pytest", "npm", "node", "typescript", "javascript", "java", "gradle",
    ];

    private static string Text => File.ReadAllText(RepoPaths.At("constraints.md"));

    private static List<(string Id, int Number, string Statement, string Body)> Parse()
    {
        string text = Text;
        MatchCollection matches = Heading.Matches(text);
        List<(string, int, string, string)> result = [];

        for (int i = 0; i < matches.Count; i++)
        {
            Match m = matches[i];
            int start = m.Index + m.Length;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;

            result.Add((
                m.Groups["id"].Value,
                int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture),
                m.Groups["statement"].Value.Trim(),
                text[start..end]));
        }

        return result;
    }

    [TestMethod]
    public void ConstraintsParse()
    {
        Assert.IsTrue(Parse().Count >= 6,
            "Fewer than six constraints means the adversarial enforcement mode is not carrying " +
            "its weight, and the concept says to narrow it rather than keep the machinery.");
    }

    [TestMethod]
    public void EveryConstraintForbidsSomething()
    {
        foreach ((string id, _, _, string body) in Parse())
        {
            Assert.IsTrue(
                body.Contains("**Forbids:**", StringComparison.Ordinal),
                $"{id} has no Forbids clause. CON-11 forbids that.");

            int index = body.IndexOf("**Forbids:**", StringComparison.Ordinal);
            string clause = body[(index + "**Forbids:**".Length)..];
            int terminator = clause.IndexOf("\n\n", StringComparison.Ordinal);
            clause = (terminator >= 0 ? clause[..terminator] : clause).Trim();

            Assert.IsTrue(
                clause.Length >= 40,
                $"{id}'s Forbids clause is too short to name what becomes illegal: '{clause}'.");
        }
    }

    [TestMethod]
    public void EveryConstraintSaysWhatItAppliesTo()
    {
        foreach ((string id, _, _, string body) in Parse())
        {
            Assert.IsTrue(
                body.Contains("**Applies to:**", StringComparison.Ordinal),
                $"{id} does not say which artifact it applies to, so no interrogation knows when to check it.");
        }
    }

    [TestMethod]
    public void IdsAreUniqueAndAscending()
    {
        List<(string Id, int Number, string Statement, string Body)> parsed = Parse();

        int[] numbers = [.. parsed.Select(c => c.Number)];

        CollectionAssert.AllItemsAreUnique(numbers, "Constraint IDs are assigned once and never reused.");

        for (int i = 1; i < numbers.Length; i++)
        {
            Assert.IsTrue(
                numbers[i] > numbers[i - 1],
                $"CON-{numbers[i]:D2} appears after CON-{numbers[i - 1]:D2}. IDs are a monotonic sequence.");
        }
    }

    [TestMethod]
    public void GlobalConstraintsAreStackNeutral()
    {
        // A global constraint that names a stack puts every solution on another stack in violation
        // of a rule that never applied to it.
        foreach ((string id, _, string statement, string body) in Parse())
        {
            string haystack = statement + body;

            foreach (string word in StackWords)
            {
                Assert.IsFalse(
                    haystack.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"{id} mentions '{word}'. Global constraints are stack-neutral; stack specifics " +
                    "belong in docs/techstack.md and in analyzers.");
            }
        }
    }

    [TestMethod]
    public void SupersessionsNameAnExistingConstraint()
    {
        HashSet<string> ids = [.. Parse().Select(c => c.Id)];

        MatchCollection references = Regex.Matches(
            Text,
            @"Superseded-by:\s*(?<id>CON-\d{2,})",
            RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(2));

        foreach (Match reference in references)
        {
            string target = reference.Groups["id"].Value;
            Assert.IsTrue(ids.Contains(target), $"Superseded-by names {target}, which does not exist.");
        }
    }
}
