using Sdlc.Core.Constraints;
using Sdlc.Core.Decisions;
using Sdlc.Core.Drafts;
using Sdlc.Core.Proposals;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Convergence (D-104): proposals a person marks, drafted as byte copies with only those lines applied.
/// </summary>
[TestClass]
public sealed class ConvergenceTests
{
    private const string Rules = """
        # This solution's own constraints

        ## LOC_01 — Every screen is held to the accessibility row

        **Applies to:** a spec.

        **Forbids:** a screen without it.

        **Why:** every later screen copies the first.

        ## LOC_02 — Every failure is recorded

        **Applies to:** a spec.

        **Forbids:** a silent failure.

        **Why:** it costs money.

        ## Considered and rejected

        - a rule about colours.
        """;

    private const string Proposals = """
        # Convergence proposals

        ## Rules

        ### [x] add — A secret never appears in source
        **Applies to:** a plan.

        **Forbids:** a credential value in any committed file.

        **Why:** a leaked key costs money.
        > Evidence: 20261021_0118_04 F_701, F_801, F_1201

        ### [x] supersede LOC_02 — Every failure is recorded through the one failure record
        **Applies to:** a spec and a plan.

        **Forbids:** a failure path that writes its own record.

        **Why:** each new mechanism was a new failure path.

        ### [ ] reword LOC_01 — Every screen meets the accessibility row in full
        **Applies to:** a spec.

        **Forbids:** less.

        **Why:** the same.

        ## Stack

        ### [x] set fake data and seeding — Bogus, with a fixed seed
        ### [x] set cache — none
        """;

    [TestMethod]
    public void A_proposal_file_reads_every_heading_and_which_were_accepted()
    {
        IReadOnlyList<DraftConvergence.Proposal> read = DraftConvergence.Parse(Proposals).Value;

        Assert.AreEqual(5, read.Count);
        Assert.AreEqual(4, read.Count(proposal => proposal.Accepted));
        Assert.IsFalse(read[0].Body.Contains("Evidence", StringComparison.Ordinal), "Evidence is for the person, not the record.");
        Assert.AreEqual("LOC_02", read[1].Target);
    }

    [TestMethod]
    public void The_framework_notes_and_the_accounting_are_read_by_the_person_and_never_drafted()
    {
        // The skill writes what goes to the framework and what it chose not to propose into the same file,
        // so the review in a fresh process can look for an item with no answer (D-112). Neither is an entry
        // in any of the three files, so a heading under either must never reach a draft.
        IReadOnlyList<DraftConvergence.Proposal> read = DraftConvergence.Parse(
            Proposals
            + "\n## For the framework\n\n### CON_08 — verify spent twelve rounds on its own wording\n> cycle 2, F_03\n"
            + "\n## Not proposed\n\n- criterion 15 — already recorded as DEC_09\n").Value;

        Assert.AreEqual(5, read.Count, "A heading outside Rules, Decisions and Stack was read as a proposal.");
    }

    [TestMethod]
    public void There_is_no_remove()
    {
        FluentResults.Result<IReadOnlyList<DraftConvergence.Proposal>> parsed = DraftConvergence.Parse("## Rules\n\n### [x] remove LOC_01 — gone\n");

        Assert.IsTrue(parsed.IsFailed);
        StringAssert.Contains(parsed.Errors[0].Message, "There is no remove");
    }

    [TestMethod]
    public void Accepted_rules_are_inserted_and_the_superseded_one_keeps_its_place()
    {
        List<DraftConvergence.Proposal> accepted = [.. DraftConvergence.Parse(Proposals).Value
            .Where(proposal => proposal.Accepted && proposal.Record == DraftConvergence.Record.Rules)];
        List<string> assigned = [];

        string draft = DraftConvergence.ApplyRules(Rules, accepted, assigned).Value;
        IReadOnlyList<Constraint> rules = ConstraintParser.Parse(draft, "draft").Value;

        CollectionAssert.AreEqual(new[] { "LOC_03", "LOC_04" }, assigned);
        Assert.AreEqual(4, rules.Count, "Nothing removed: the superseded rule is still on the page.");
        Assert.AreEqual("LOC_04", rules.Single(rule => rule.Id.Number == 2).SupersededBy?.ToString());
        Assert.IsTrue(
            draft.IndexOf("## LOC_04", StringComparison.Ordinal) < draft.IndexOf("## Considered and rejected", StringComparison.Ordinal),
            "New rules go above the refusals, which the parser does not read.");
        Assert.IsTrue(draft.StartsWith(Rules[..Rules.IndexOf("## LOC_02", StringComparison.Ordinal)], StringComparison.Ordinal), "Everything before the change is the same bytes.");
    }

    [TestMethod]
    public void A_reword_keeps_the_id_and_takes_the_next_revision()
    {
        DraftConvergence.Proposal reword = new(DraftConvergence.Record.Rules, DraftConvergence.Operation.Reword, "LOC_01", "Every screen meets the row in full", "**Applies to:** a spec.\n\n**Forbids:** less.\n\n**Why:** the same.", Accepted: true);

        string draft = DraftConvergence.ApplyRules(Rules, [reword], []).Value;

        StringAssert.Contains(draft, "## LOC_01.2 — Every screen meets the row in full");
        Assert.AreEqual(2, ConstraintParser.Parse(draft, "draft").Value.Count);
    }

    [TestMethod]
    public void A_decision_is_added_or_superseded_and_never_reworded()
    {
        const string record = """
            # Decisions

            ## DEC_01 — One AppHost

            **Decided:** one run.

            **Because:** simpler.

            **Instead of:** many runs.

            **From:** 20261021_0118_04.
            """;

        DraftConvergence.Proposal supersede = new(DraftConvergence.Record.Decisions, DraftConvergence.Operation.Supersede, "DEC_01", "One AppHost, two profiles", "**Decided:** two profiles.\n\n**Because:** tests collide.\n\n**Instead of:** one profile.\n\n**From:** 20261024_1205_42.", Accepted: true);
        List<string> assigned = [];

        string draft = DraftConvergence.ApplyDecisions(record, [supersede], assigned).Value;
        IReadOnlyList<Decision> decisions = DecisionParser.Parse(draft, "draft").Value;

        CollectionAssert.AreEqual(new[] { "DEC_02" }, assigned);
        Assert.AreEqual("DEC_02", decisions.Single(decision => decision.Id.Number == 1).SupersededBy?.ToString());
        Assert.IsTrue(DraftConvergence.ApplyDecisions(record, [supersede with { Operation = DraftConvergence.Operation.Reword }], []).IsFailed);
    }

    [TestMethod]
    public void A_stack_row_is_changed_in_place_or_added_to_the_defaults_and_none_is_an_answer()
    {
        const string stack = """
            # What this solution is built from

            ## Defaults

            | Concern | Choice |
            |---|---|
            | language, framework | C#, .NET |
            | cache | Redis |

            ## Rules
            """;

        List<DraftConvergence.Proposal> accepted = [.. DraftConvergence.Parse(Proposals).Value
            .Where(proposal => proposal.Accepted && proposal.Record == DraftConvergence.Record.Stack)];

        string draft = DraftConvergence.ApplyStack(stack, accepted).Value;
        IReadOnlyList<StackTable.Row> rows = StackTable.Rows(draft);

        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual("none", rows.Single(row => row.Concern == "cache").Choice);
        Assert.AreEqual("Bogus, with a fixed seed", rows.Single(row => row.Concern == "fake data and seeding").Choice);
        Assert.IsTrue(draft.IndexOf("fake data", StringComparison.Ordinal) < draft.IndexOf("## Rules", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Drafting_writes_one_draft_per_record_and_names_the_renames()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "constraints.md"), Rules);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Root, ".sdlc", "techstack.md"), "## Defaults\n\n| Concern | Choice |\n|---|---|\n| cache | Redis |\n");
        string proposals = Path.Combine(workspace.Root, "proposed.md");
        await File.WriteAllTextAsync(proposals, Proposals);

        using var gate = Gate.For(workspace.Root);
        DraftResult drafted = (await gate.Send<DraftConvergence.Command, DraftResult>(new DraftConvergence.Command(proposals))).Value;

        Assert.IsTrue(drafted.MayDraft, drafted.Instructions);
        Assert.IsTrue(File.Exists(Path.Combine(workspace.Root, ".sdlc", "constraints.draft.md")));
        Assert.IsTrue(File.Exists(Path.Combine(workspace.Root, ".sdlc", "techstack.draft.md")));
        StringAssert.Contains(drafted.Instructions, "Move-Item -Force .sdlc/constraints.draft.md .sdlc/constraints.md");
        StringAssert.Contains(drafted.Entry!, "1 proposal(s) not accepted.");
    }
}
