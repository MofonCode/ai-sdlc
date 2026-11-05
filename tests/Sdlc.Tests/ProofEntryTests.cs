using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;

namespace Sdlc.Tests;

/// <summary>
/// What a plan's proof entries and file rows commit to, read the way verify-chain reads them.
/// </summary>
/// <remarks>
/// cycle-02 verified a pass with every test green and could not open its pull request: the chain read a
/// browser API and a package out of two proof entries' prose as tests that had to pass, and four project
/// names out of one file row's description as files that were never delivered. The entries below are
/// cycle-02's, verbatim, so the case that broke is the case held.
/// </remarks>
[TestClass]
public sealed class ProofEntryTests
{
    private const string Id = "20261018_1103_50";

    [TestMethod]
    [DataRow(
        "`A_roll_changes_the_die_once_with_nothing_animating` installs, by init script, a `MutationObserver`",
        "A_roll_changes_the_die_once_with_nothing_animating")]
    [DataRow(
        "`The_page_has_no_axe_violations_in_any_state` runs axe-core through `Deque.AxeCore.Playwright` with",
        "The_page_has_no_axe_violations_in_any_state")]
    [DataRow(
        "`Each_face_draws_as_many_pips_as_the_number_and_no_digit` (bUnit, one case per face 1 to 6) renders",
        "Each_face_draws_as_many_pips_as_the_number_and_no_digit")]
    [DataRow(
        "`Page_shows_a_die_and_a_roll_button` opens `/` in a fresh context and, before any press, asserts:",
        "Page_shows_a_die_and_a_roll_button")]
    public void Only_the_name_an_entry_opens_with_is_a_test(string entry, string test)
    {
        CollectionAssert.AreEqual(
            new[] { test },
            DeclaredFiles.TestsIn(entry).ToArray(),
            "What the prose after the name backticks is formatting, not a commitment.");
    }

    [TestMethod]
    [DataRow("`Rolls_within_range`, `Rolls_are_independent` assert the draw.")]
    [DataRow("`Rolls_within_range` and `Rolls_are_independent` assert the draw.")]
    [DataRow("`Rolls_within_range`, and `Rolls_are_independent` assert the draw.")]
    [DataRow("`Rolls_within_range` & `Rolls_are_independent` assert the draw.")]
    public void Several_names_an_entry_opens_with_are_each_a_test(string entry)
    {
        CollectionAssert.AreEqual(
            new[] { "Rolls_are_independent", "Rolls_within_range" },
            DeclaredFiles.TestsIn(entry).ToArray());
    }

    [TestMethod]
    [DataRow("A test named `Rolls_within_range` asserts it.")]
    [DataRow("Covered by the integration suite.")]
    [DataRow("`tests/RollTests.cs` holds `Rolls_within_range`.")]
    public void An_entry_that_opens_with_prose_or_a_path_names_no_test(string entry)
    {
        Assert.AreEqual(0, DeclaredFiles.TestsIn(entry).Count);
    }

    [TestMethod]
    public void A_plan_whose_proof_entry_opens_with_prose_is_refused_at_plan_time()
    {
        // The chain finds this after the build, when correcting one sentence costs a reopen and a second
        // approval. The schema runs wherever the plan is checked, so it is found while it is free to fix.
        ArtifactValidation check = Validate(Plan(
            "1. `Rolls_within_range` draws 10,000 times.",
            "2. A test named `Rolls_are_independent` asserts the draws differ."));

        SchemaViolation[] proofs = [.. check.Violations.Where(v => v.Where == "Proof")];

        Assert.AreEqual(1, proofs.Length, string.Join("; ", check.Violations.Select(v => v.Where + " " + v.Problem)));
        StringAssert.Contains(proofs[0].Problem, "entry 2");
    }

    [TestMethod]
    public void A_plan_whose_proof_entries_each_open_with_a_name_raises_nothing_about_proof()
    {
        ArtifactValidation check = Validate(Plan(
            "1. `Rolls_within_range` draws 10,000 times and asserts a `Face` of 1 to 6.",
            "2. `Rolls_are_independent` asserts the draws differ."));

        Assert.IsFalse(check.Violations.Any(v => v.Where == "Proof"));
    }

    [TestMethod]
    public void A_proof_table_gives_each_criterion_one_entry_opening_with_every_test_its_rows_name()
    {
        // D-130. The table is the closed list a reader judges, one row per case the criterion states; the
        // chain still reads one entry per criterion, and it now opens with every row's test.
        string plan = Plan(
            "| Criterion | Case | Test | Asserts |",
            "|---|---|---|---|",
            "| 1 | chromium | `Rolls_within_range` | a face of 1 to 6 |",
            "| 1 | webkit | `Rolls_within_range_on_webkit` | a face of 1 to 6 |",
            "| 2 | two requests | `Rolls_are_independent` | the draws differ |");

        IReadOnlyDictionary<int, string> entries = ArtifactSections.NumberedItems(plan, "Proof");

        Assert.AreEqual(2, entries.Count);
        CollectionAssert.AreEqual(
            new[] { "Rolls_within_range", "Rolls_within_range_on_webkit" },
            DeclaredFiles.TestsIn(entries[1]).ToArray());
        StringAssert.Contains(entries[1], "webkit: a face of 1 to 6");
        Assert.IsFalse(Validate(plan).Violations.Any(v => v.Where == "Proof"));
    }

    [TestMethod]
    public void A_proof_table_row_naming_no_test_is_refused_even_when_its_criterion_has_another()
    {
        ArtifactValidation check = Validate(Plan(
            "| Criterion | Case | Test | Asserts |",
            "|---|---|---|---|",
            "| 1 | chromium | `Rolls_within_range` | a face of 1 to 6 |",
            "| 1 | webkit | covered by the chromium run | a face of 1 to 6 |"));

        SchemaViolation[] proofs = [.. check.Violations.Where(v => v.Where == "Proof")];

        Assert.AreEqual(1, proofs.Length, string.Join("; ", check.Violations.Select(v => v.Where + " " + v.Problem)));
        StringAssert.Contains(proofs[0].Problem, "criterion 1 (webkit)");
    }

    [TestMethod]
    public void Only_the_first_cell_of_a_files_row_declares()
    {
        // cycle-02's row, verbatim in shape: the description names the four projects the solution file
        // adds, and each came back UNDELIVERED.
        IReadOnlyList<string> declared = DeclaredFiles.Read("""
            # a plan

            ## Files

            | File | Change |
            |---|---|
            | `Cycle02.slnx` | adds `Cycle02.Ui`, `Cycle02.Web`, `Cycle02.Ui.Tests` and `Cycle02.EndToEnd.Tests` |
            | `src/Cycle02.Ui/Dice/RollPanel.razor` | new |

            - `src/Cycle02.Web/Program.cs`, registering `src/Cycle02.Ui/UiServices.cs`

            ## Open
            """);

        CollectionAssert.AreEqual(
            new[] { "Cycle02.slnx", "src/Cycle02.Ui/Dice/RollPanel.razor", "src/Cycle02.Ui/UiServices.cs", "src/Cycle02.Web/Program.cs" },
            declared.ToArray(),
            "A row declares its first cell; a list item has no cells to tell apart, so it is read whole.");
    }

    private static ArtifactValidation Validate(string markdown)
    {
        Assert.IsTrue(CycleId.TryParse(Id, out CycleId? cycle));
        return ArtifactSchema.Validate(ArtifactKind.Plan, markdown, cycle.Value);
    }

    private static string Plan(params string[] proofs) =>
        string.Join(
            '\n',
            [
                "---",
                "cycle: " + Id,
                "---",
                string.Empty,
                "# a plan",
                string.Empty,
                "## Proof",
                string.Empty,
                .. proofs,
                string.Empty,
                "## Open",
                string.Empty,
            ]);
}
