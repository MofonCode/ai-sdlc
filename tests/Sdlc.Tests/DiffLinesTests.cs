using Sdlc.Core.Evidence;

namespace Sdlc.Tests;

/// <summary>
/// D-222: a verify round reads the diff with each hunk line numbered, and a citation whose quote is on other lines
/// than it names is a slip the binary lists.
/// </summary>
[TestClass]
public sealed class DiffLinesTests
{
    private const string Patch =
        "diff --git a/tests/DrawTests.cs b/tests/DrawTests.cs\n"
        + "--- a/tests/DrawTests.cs\n"
        + "+++ b/tests/DrawTests.cs\n"
        + "@@ -10,3 +10,4 @@ public sealed class DrawTests\n"
        + "     var first = service.Draw();\n"
        + "-    var second = first;\n"
        + "+    var second = service.Draw();\n"
        + "+    Assert.AreNotEqual(first.Value, second.Value);\n"
        + "     return;\n";

    [TestMethod]
    public void Each_hunk_line_carries_its_number_the_new_files_for_an_added_or_unchanged_line_and_the_old_files_for_a_removed_one()
    {
        string numbered = DiffLines.Numbered(Patch);

        StringAssert.Contains(numbered, "@@ -10,3 +10,4 @@", "Headers stand as they are.");
        StringAssert.Contains(numbered, " 10    |     var first = service.Draw();");
        StringAssert.Contains(numbered, "-11    |     var second = first;");
        StringAssert.Contains(numbered, "+11    |     var second = service.Draw();");
        StringAssert.Contains(numbered, "+12    |     Assert.AreNotEqual(first.Value, second.Value);");
        StringAssert.Contains(numbered, " 13    |     return;");
    }

    [TestMethod]
    public void A_removed_line_that_reads_like_a_files_header_is_still_a_line_of_its_hunk()
    {
        // A hunk ends when its header's counts are spent, not at the next line that looks like a header.
        string patch = "--- a/q.sql\n+++ b/q.sql\n@@ -1,2 +1,1 @@\n--- a comment\n select 1;\n";

        string numbered = DiffLines.Numbered(patch);

        StringAssert.Contains(numbered, "-1     | -- a comment");
        StringAssert.Contains(numbered, " 1     | select 1;");
    }

    [TestMethod]
    public void A_patch_with_no_hunk_header_is_left_as_it_stands()
    {
        Assert.AreEqual("+Assert.AreEqual(1, roll);\n", DiffLines.Numbered("+Assert.AreEqual(1, roll);\n"));
    }

    [TestMethod]
    public void A_citation_whose_quote_is_on_the_line_it_names_is_no_slip_and_one_off_by_one_names_where_the_quote_is()
    {
        string verify =
            "---\ncycle: 20260929_1456_32\noutcome: pass\n---\n\n# Draws\n\n## Criteria\n\n"
            + "1. Met. `Two_requests_differ` passed; `tests/DrawTests.cs` +12: `Assert.AreNotEqual(first.Value, second.Value)`.\n"
            + "2. Met. In `DrawTests.cs`, +13 asserts `Assert.AreNotEqual(first.Value, ...)` over two draws.\n"
            + "3. Met. +11–12 hold `var second = service.Draw();`, and the old -11 `var second = first;` is gone.\n"
            + "\n## Open\n";

        IReadOnlyList<DiffLines.Slip> slips = DiffLines.Slips(verify, Patch);

        DiffLines.Slip slip = slips.Single();
        Assert.AreEqual(2, slip.Criterion);
        Assert.AreEqual("+13", slip.Cited);
        Assert.AreEqual("+12 in tests/DrawTests.cs", slip.Found);
        StringAssert.Contains(slip.ToString(), "criterion 2: +13 quotes `Assert.AreNotEqual(first.Value, ...)`, which is at +12 in tests/DrawTests.cs");
    }

    [TestMethod]
    public void A_quote_found_nowhere_a_hunk_reference_and_a_number_in_code_are_not_slips()
    {
        // A quote the diff does not hold is CON_22's, and the evidence check lists it already.
        string verify =
            "## Criteria\n\n"
            + "1. Met. +12: `Assert.IsTrue(nothing.Like.This)`.\n"
            + "2. Met. The hunk -10,3 touches only `var first = service.Draw();` lines.\n"
            + "3. Met. `Assert.AreEqual(-1, value)` is not a citation.\n";

        Assert.AreEqual(0, DiffLines.Slips(verify, Patch).Count);
    }

    [TestMethod]
    public void A_citation_followed_by_a_fenced_block_quotes_its_first_line()
    {
        string verify = "## Criteria\n\n1. Met. The check is at +10–11:\n   ```\n   Assert.AreNotEqual(first.Value, second.Value);\n   ```\n";

        Assert.AreEqual("+12 in tests/DrawTests.cs", DiffLines.Slips(verify, Patch).Single().Found);
    }
}
