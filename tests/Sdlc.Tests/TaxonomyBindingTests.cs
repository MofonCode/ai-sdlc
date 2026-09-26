using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// Binds the findings schema to the charter that owns the taxonomy.
/// </summary>
/// <remarks>
/// <b>The charter says nothing but the findings schema may restate these values.</b> It then
/// had no way to notice when the schema stopped agreeing with it. The only binding check tied
/// <c>interrogate.js</c>'s copies to the <b>schema enum</b>, so the chain ran
/// schema → workflow with the charter dangling off the side: the charter could drift from both
/// and every test would still pass.
/// <para>
/// This is INV-03 applied to the document that declares INV-03 — no second representation
/// without, in the same change, the check that fails when they disagree. The check existed and
/// pointed at the wrong one of the two.
/// </para>
/// <para>
/// <b>Both directions, deliberately.</b> A value in the schema that the charter never declared
/// is an ungoverned category. A value the charter declares that the schema rejects is worse: a
/// finding that the charter says is legal cannot be written down, so it disappears. Asserting
/// set equality catches both; asserting "every schema value appears somewhere in the charter"
/// catches neither reliably, because a passing mention in prose satisfies it.
/// </para>
/// <para>
/// Each vocabulary is read from the section that DECLARES it rather than from the whole
/// document, so a value merely discussed elsewhere cannot stand in for a declaration.
/// </para>
/// </remarks>
[TestClass]
public sealed class TaxonomyBindingTests
{
    private static string RepoRoot => Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..");

    private static string CharterPath => Path.Combine(RepoRoot, ".sdlc", "CHARTER.md");

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "schemas", "findings.schema.json");

    private static string Charter => File.ReadAllText(CharterPath);

    private static string[] SchemaEnum(string defName)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        return [.. doc.RootElement
            .GetProperty("$defs").GetProperty(defName).GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!)];
    }

    /// <summary>
    /// The text of one charter section, from its heading to the next heading of any level.
    /// </summary>
    /// <remarks>
    /// Matched on words rather than on the whole heading: the real headings carry an em-dash,
    /// and pinning a test to a dash character invites a failure that reads as a taxonomy
    /// problem when someone retypes the punctuation.
    /// </remarks>
    private static string Section(string headingPattern)
    {
        string charter = Charter;
        Match heading = Regex.Match(charter, @"(?m)^#{2,4}\s+" + headingPattern + @".*$");
        Assert.IsTrue(heading.Success,
            $"the charter has no section matching '{headingPattern}' — it is the owner, so " +
            "a missing section means the taxonomy has no declared home");

        int start = heading.Index + heading.Length;
        Match next = Regex.Match(charter[start..], @"(?m)^#{2,4}\s");
        return next.Success ? charter[start..(start + next.Index)] : charter[start..];
    }

    /// <summary>Backticked values in the first column of a markdown table.</summary>
    private static string[] FirstColumnValues(string section) =>
        [.. Regex.Matches(section, @"(?m)^\|\s*`([^`]+)`\s*\|")
            .Select(m => m.Groups[1].Value)];

    /// <summary>Every backticked token in a section, for the two declared as prose lists.</summary>
    private static string[] BacktickedValues(string section) =>
        [.. Regex.Matches(section, "`([^`]+)`").Select(m => m.Groups[1].Value).Distinct()];

    private static void AssertSameSet(string vocabulary, string[] charterValues, string[] schemaValues)
    {
        string[] onlyCharter = [.. charterValues.Except(schemaValues).Order()];
        string[] onlySchema = [.. schemaValues.Except(charterValues).Order()];

        Assert.AreEqual(0, onlySchema.Length,
            $"the schema allows {vocabulary} the charter never declared: " +
            $"{string.Join(", ", onlySchema)}. The charter owns this list.");

        Assert.AreEqual(0, onlyCharter.Length,
            $"the charter declares {vocabulary} the schema rejects: " +
            $"{string.Join(", ", onlyCharter)}. A finding the charter says is legal cannot " +
            "be written down, so it would simply be lost.");
    }

    [TestMethod]
    public void CategoriesComeFromTheCharter()
    {
        AssertSameSet("categories", FirstColumnValues(Section("Axis 1")), SchemaEnum("category"));
    }

    [TestMethod]
    public void StagesComeFromTheCharter()
    {
        // Declared as a single backticked list rather than a table, so every token in the
        // section counts. The section holds nothing else in backticks, which is what makes
        // that safe — and if prose is added there, this test failing is the correct outcome:
        // the declaration would no longer be unambiguous.
        AssertSameSet("stages", BacktickedValues(Section("Axis 2")), SchemaEnum("stage"));
    }

    [TestMethod]
    public void DetectionValuesComeFromTheCharter()
    {
        AssertSameSet("detection values", BacktickedValues(Section("Axis 3")), SchemaEnum("detection"));
    }

    [TestMethod]
    public void StatesComeFromTheCharter()
    {
        AssertSameSet("states", FirstColumnValues(Section("State")), SchemaEnum("state"));
    }

    [TestMethod]
    public void DispositionsComeFromTheCharter()
    {
        AssertSameSet("dispositions", FirstColumnValues(Section("Disposition")), SchemaEnum("disposition"));
    }

    [TestMethod]
    public void RolesComeFromTheCharter()
    {
        // Scoped to the roles table itself. The section that follows it — what `gaps` must
        // cover, by artifact kind — is a table of ARTIFACTS, and reading the whole chapter
        // would quietly add them to the role vocabulary.
        AssertSameSet("roles", FirstColumnValues(Section("Interrogator roles")), SchemaEnum("role"));
    }

    [TestMethod]
    public void TheCoverageTableListsExactlyTheCategoriesAxisOneDeclares()
    {
        // A FOURTH REPRESENTATION, AND IT IS INSIDE THE CHARTER. The Coverage table maps every
        // category to the invariant or check that can block on it. It happens to agree with
        // Axis 1 today — eleven each — and nothing whatsoever checked that.
        //
        // Both directions of drift are already live problems in this repository. A category in
        // Axis 1 but not in Coverage is a category with no blocking route, which is exactly the
        // defect `US-13` records for the form rule. A category in Coverage but not in Axis 1 is
        // an invariant guarding something nothing can be classified as.
        //
        // Found while mutation-testing this file: a mutation aimed at Axis 1 landed on Coverage
        // instead and every test still passed, which is the same evidence as a failing check.
        string[] axis1 = FirstColumnValues(Section("Axis 1"));
        string[] coverage = FirstColumnValues(Section("Coverage"));

        string[] uncovered = [.. axis1.Except(coverage).Order()];
        string[] unclassifiable = [.. coverage.Except(axis1).Order()];

        Assert.AreEqual(0, uncovered.Length,
            "the Coverage table gives no blocking route for: " + string.Join(", ", uncovered) +
            ". A category nothing can block on is decoration.");

        Assert.AreEqual(0, unclassifiable.Length,
            "Coverage names categories Axis 1 does not declare: " +
            string.Join(", ", unclassifiable) +
            ". Nothing can ever be classified as one, so the route is unreachable.");
    }

    [TestMethod]
    public void TheChainReachesTheWorkflowAndNotJustTheSchema()
    {
        // The point of the whole file. `interrogate.js` restates these values, and the test
        // that catches ITS drift compares against the schema. That is correct as far as it
        // goes, but it only governs the workflow once the schema is itself governed — so this
        // asserts the link exists rather than leaving the chain's first hop to inspection.
        //
        // Three representations, two checks, one owner: charter → schema (here) → workflow
        // (RestatedEnumsMatchTheSchemaThatOwnsThem).
        string categories = string.Join(",", SchemaEnum("category").Order());
        string charterCategories = string.Join(",", FirstColumnValues(Section("Axis 1")).Order());

        Assert.AreEqual(charterCategories, categories,
            "if this fails the chain is broken at its first hop and the workflow check is " +
            "measuring against an ungoverned list");
    }
}
