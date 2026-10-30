using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// What the declared stack's rows read as, out of a file a person owns.
/// </summary>
/// <remarks>
/// The reader is deliberately tolerant. Adoption creates the file and a person edits it from then on,
/// so it will carry prose, extra tables and concerns worded unlike any profile's — and a reader that
/// called a hand-edited file broken would teach somebody to stop reading what the gate says. What it
/// has to get right is the two cases <c>sdlc doctor</c> acts on: a row that declares nothing, and no
/// rows at all.
/// </remarks>
[TestClass]
public sealed class StackTableTests
{
    [TestMethod]
    public void The_header_and_the_divider_are_not_rows()
    {
        IReadOnlyList<StackTable.Row> rows = StackTable.Rows(
            "| Concern | Choice |\n|---|---|\n| api | ASP.NET Core |\n");

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("api", rows[0].Concern);
        Assert.AreEqual("ASP.NET Core", rows[0].Choice);
    }

    [TestMethod]
    public void A_row_with_no_choice_is_read_as_a_row_that_declares_nothing()
    {
        // The case that matters. An adopted solution's file opens with four of these and an
        // instruction to fill them in; an empty row is honest about declaring nothing, and a stack
        // file of nothing but empty rows leaves CON_15 judging every plan against no default at all.
        IReadOnlyList<StackTable.Row> rows = StackTable.Rows(
            "| Concern | Choice |\n|---|---|\n| language, framework | |\n| testing | |\n");

        Assert.AreEqual(2, rows.Count);
        Assert.IsTrue(rows.All(row => string.IsNullOrWhiteSpace(row.Choice)));
    }

    [TestMethod]
    public void Prose_and_headings_between_tables_are_skipped_rather_than_confusing_it()
    {
        // The real file has notes under its table, and the profile's notes carry pipe characters
        // nowhere near a table. Keying on the shape of a row rather than on position is what makes
        // that safe.
        IReadOnlyList<StackTable.Row> rows = StackTable.Rows(
            """
            # What this solution is built from

            Some prose about departing from a default.

            ## Defaults

            | Concern | Choice |
            |---|---|
            | api | ASP.NET Core |

            ## Notes worth keeping

            **An Aspire AppHost is conditional.** More than one process.

            | Concern | Choice |
            |---|---|
            | cache | Redis |
            """);

        CollectionAssert.AreEqual(
            new[] { "api", "cache" },
            rows.Select(row => row.Concern).ToArray(),
            "Both tables, and nothing from the prose between them.");
    }

    [TestMethod]
    public void A_three_column_table_is_not_read_as_declared_rows()
    {
        // A person may keep a third column -- a note, a date, who decided. Reading two of its three
        // cells would report a choice that is not the choice, so a wider table is left alone.
        IReadOnlyList<StackTable.Row> rows = StackTable.Rows(
            "| Concern | Choice | Why |\n|---|---|---|\n| api | ASP.NET Core | it is what we know |\n");

        Assert.AreEqual(0, rows.Count);
    }

    [TestMethod]
    public void A_document_with_no_table_declares_nothing()
    {
        Assert.AreEqual(0, StackTable.Rows("# What this solution is built from\n\nNothing yet.\n").Count);
    }
}
