using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Drafts;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// The three drafters that print a file for a person to place.
/// </summary>
/// <remarks>
/// Each exists because a tool never writes the file it describes, which is what lets every hook rule
/// stay absolute. So the same thing is worth asserting of all of them: they write nothing, and what
/// they print is correct enough that a person pasting it does not then have to reason about it.
/// </remarks>
[TestClass]
public sealed class DraftTests
{
    [TestMethod]
    public async Task Abandoning_prints_a_record_that_parses_once_a_reason_is_written()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();

        DraftResult draft = await Abandon(workspace);

        Assert.IsTrue(draft.MayDraft, draft.Instructions);
        Assert.IsFalse(
            File.Exists(workspace.Cycle.AbandonedPath),
            "A drafter writes nothing. One that also wrote the file would need a flag saying when, and "
            + "that flag is the exception the hook rules are written to have nowhere to live.");

        Result<AbandonRecord> empty = AbandonRecord.Read(draft.Entry!, "abandoned.md");

        Assert.IsTrue(
            empty.IsFailed,
            "The reason is left empty on purpose, and an unfilled record has to fail rather than stand.");

        Result<AbandonRecord> filled = AbandonRecord.Read(
            draft.Entry! + "The headline change turned out to be unreachable, and no success criterion "
            + "failed without it.",
            "abandoned.md");

        Assert.IsTrue(filled.IsSuccess, string.Join("; ", filled.Errors.Select(e => e.Message)));
        Assert.AreEqual("Peter Almanzar", filled.Value.By);
        StringAssert.Contains(filled.Value.Reason, "unreachable");
    }

    [TestMethod]
    public async Task Abandoning_a_cycle_that_is_already_abandoned_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        workspace.Abandon();

        DraftResult draft = await Abandon(workspace);

        Assert.IsFalse(draft.MayDraft);
        StringAssert.Contains(
            draft.Instructions,
            "terminal",
            "Abandonment is not a state to re-enter, and a second record would overwrite the first "
            + "reason, which is the only thing the cycle produced.");
    }

    [TestMethod]
    public async Task Abandoning_a_cycle_with_no_directory_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        Assert.IsTrue(CycleId.TryParse("20991231_2359_59", out CycleId? absent));

        using var gate = Gate.For(workspace.Root);

        Result<DraftResult> result = await gate
            .Send<AbandonText.Query, DraftResult>(
                new AbandonText.Query(absent.Value, "Peter Almanzar"));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value.MayDraft);
        StringAssert.Contains(result.Value.Instructions, "nothing to abandon");
    }

    [TestMethod]
    public async Task The_first_solution_constraint_is_LOC_01()
    {
        using var workspace = CycleWorkspace.Create();

        DraftResult draft = await Constraint(workspace);

        Assert.IsTrue(draft.MayDraft, draft.Instructions);
        StringAssert.Contains(draft.Entry!, "## LOC_01");
        StringAssert.Contains(draft.Instructions, "first solution constraint");
        StringAssert.Contains(
            draft.Instructions,
            "no analyzer, schema or hook could decide it",
            "The admission tests belong where somebody is about to add a rule, not only in the library.");
    }

    [TestMethod]
    public async Task A_new_id_goes_past_the_highest_ever_used_rather_than_into_a_gap()
    {
        // Ids are assigned once and never reused. A gap is the record of a constraint that was removed,
        // so filling it would make a commit citing the old id read as the new rule.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolutionConstraints(1, 4);

        DraftResult draft = await Constraint(workspace);

        StringAssert.Contains(draft.Entry!, "## LOC_05");
        Assert.IsFalse(
            draft.Entry!.Contains("LOC_02", StringComparison.Ordinal),
            "LOC_02 and LOC_03 are gaps, not vacancies.");
    }

    [TestMethod]
    public async Task A_constraints_file_that_cannot_be_parsed_is_reported_rather_than_worked_around()
    {
        // Allocating an id from a document this cannot read risks handing back one already taken, which
        // is the single mistake the monotonic rule exists to make impossible.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, "constraints.md"),
            "## LOC_01 - a rule with no clauses at all\n");

        using var gate = Gate.For(workspace.Root);

        Result<DraftResult> result = await gate
            .Send<ConstraintText.Query, DraftResult>(new ConstraintText.Query());

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(string.Join("; ", result.Errors.Select(e => e.Message)), "LOC_01");
    }

    [TestMethod]
    public async Task The_solution_draft_is_written_to_disk_for_a_rename()
    {
        // A block to paste is a block that does not get pasted: a real adoption left this file on its
        // shipped placeholder, and every plan in that solution was written against a document saying
        // nothing. The rename is one command and leaves no step to forget.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            """
            ---
            profile: dotnet-default
            origin: scaffolded
            standard: profile
            platform: local
            ---

            # What this solution is

            Replace this with a paragraph a newcomer could read.

            """);

        DraftResult draft = await Solution(workspace);

        Assert.IsTrue(draft.MayDraft, draft.Instructions);

        string path = Path.Combine(
            workspace.Root,
            Cycle.SdlcDirectoryName,
            WriteSolutionDraft.DraftFileName);

        Assert.IsTrue(File.Exists(path), "The draft is written, not printed for a person to retype.");

        string written = await File.ReadAllTextAsync(path);

        StringAssert.Contains(
            written,
            "platform: local",
            "The keys are carried through as recorded rather than recomputed. A draft that defaulted "
            + "the platform would reclassify a local solution as GitHub by being renamed, and doctor "
            + "would then report a broken install rather than a choice.");

        Assert.IsFalse(
            written.Contains(WriteSolutionDraft.Placeholder, StringComparison.Ordinal),
            "The placeholder is replaced, or renaming the draft changes nothing worth doing.");
    }

    [TestMethod]
    public async Task A_solution_draft_carries_prose_somebody_already_wrote()
    {
        // This is what makes the rename safe rather than destructive. Without it, the one command this
        // exists to offer would cost a person the paragraph they had written.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            SolutionDocument.PathIn(workspace.Root),
            """
            ---
            profile: dotnet-default
            origin: adopted
            standard: existing
            platform: github
            ---

            # What this solution is

            A dice roller for tabletop games, held to the house style.

            """);

        DraftResult draft = await Solution(workspace);

        Assert.IsTrue(draft.MayDraft, draft.Instructions);

        string written = await File.ReadAllTextAsync(Path.Combine(
            workspace.Root,
            Cycle.SdlcDirectoryName,
            WriteSolutionDraft.DraftFileName));

        StringAssert.Contains(written, "A dice roller for tabletop games, held to the house style.");
        StringAssert.Contains(written, "origin: adopted");
        StringAssert.Contains(written, "standard: existing");
    }

    [TestMethod]
    public async Task Drafting_a_solution_document_that_does_not_exist_is_refused()
    {
        using var workspace = CycleWorkspace.Create();

        DraftResult draft = await Solution(workspace);

        Assert.IsFalse(draft.MayDraft, "There is nothing to carry the keys and the prose through from.");
        StringAssert.Contains(draft.Instructions, "not been adopted");
    }

    [TestMethod]
    public async Task A_profile_migration_prints_both_keys_and_leaves_the_origin_alone()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution("dotnet-default", SolutionOrigin.Adopted);

        DraftResult draft = await Profile(workspace, "mofoncode");

        Assert.IsTrue(draft.MayDraft, draft.Instructions);

        Result<SolutionDocument> read = SolutionDocument.Read(
            "---\n" + draft.Entry + "---\n",
            "solution.md");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        Assert.AreEqual("mofoncode", read.Value.Profile);
        Assert.AreEqual(
            SolutionOrigin.Adopted,
            read.Value.Origin,
            "The origin is a historical fact. Printing one key and leaving a person to preserve the "
            + "other by hand is how it gets lost.");
    }

    [TestMethod]
    public async Task Migrating_to_the_profile_already_in_use_is_refused()
    {
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution("dotnet-default", SolutionOrigin.Scaffolded);

        DraftResult draft = await Profile(workspace, "dotnet-default");

        Assert.IsFalse(draft.MayDraft);
        StringAssert.Contains(draft.Instructions, "already on");
    }

    [TestMethod]
    public async Task A_profile_migration_in_a_repository_with_no_solution_file_says_to_run_init()
    {
        using var workspace = CycleWorkspace.Create();

        using var gate = Gate.For(workspace.Root);

        Result<DraftResult> result = await gate
            .Send<ProfileText.Query, DraftResult>(new ProfileText.Query("mofoncode"));

        Assert.IsTrue(result.IsFailed);
        StringAssert.Contains(string.Join("; ", result.Errors.Select(e => e.Message)), "sdlc init");
    }

    [TestMethod]
    public void A_solution_document_with_an_unknown_origin_is_rejected()
    {
        // The origin decides whether a profile's defaults bind or the existing standard wins, so a value
        // the reader does not know must not quietly become one it does.
        Result<SolutionDocument> read = SolutionDocument.Read(
            "---\nprofile: dotnet-default\norigin: inherited\n---\n",
            "solution.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "scaffolded or adopted");
    }

    [TestMethod]
    public void Every_drafted_block_renders_with_what_to_do_about_it()
    {
        // A skill shows this verbatim, so its shape is a contract. A block with no instructions beside it
        // is a thing to paste somewhere a person has to guess.
        string rendered = DraftRenderer.Render(DraftResult.Drafted("profile: x\n", "Open the file."));

        StringAssert.Contains(rendered, "Place this:");
        StringAssert.Contains(rendered, "profile: x");
        StringAssert.Contains(rendered, "Open the file.");
    }

    [TestMethod]
    public async Task The_abandonment_record_is_stamped_with_the_clock_the_container_supplied()
    {
        // The reason the handler takes a TimeProvider. This record is the only thing a dropped cycle
        // leaves behind, and 'at' is what places it in the history -- so the format is worth pinning
        // rather than asserting around.
        using var workspace = CycleWorkspace.Create();
        using var gate = Gate.For(
            workspace.Root,
            services => services.AddSingleton<TimeProvider>(
                new FakeClock(new DateTimeOffset(2026, 10, 3, 14, 7, 9, 512, TimeSpan.Zero))));

        DraftResult draft = Ok(await gate.Send<AbandonText.Query, DraftResult>(
            new AbandonText.Query(workspace.Cycle.Id, "Peter Almanzar")));

        StringAssert.Contains(draft.Entry, "at: 2026-10-03T14:07:09Z");
    }

    private static async Task<DraftResult> Abandon(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        return Ok(await gate.Send<AbandonText.Query, DraftResult>(
            new AbandonText.Query(workspace.Cycle.Id, "Peter Almanzar")));
    }

    private static async Task<DraftResult> Constraint(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        return Ok(await gate.Send<ConstraintText.Query, DraftResult>(new ConstraintText.Query()));
    }

    [TestMethod]
    public async Task A_stack_draft_is_a_byte_copy_of_the_file_it_is_for()
    {
        // The whole guarantee of the rename. Nothing is merged and nothing is regenerated, so placing the
        // draft applies what somebody changed in it and cannot cost them anything else.
        using var workspace = CycleWorkspace.Create();

        string path = Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);
        const string Declared = "# What this solution is built from\n\n| Concern | Choice |\n|---|---|\n| api | ASP.NET Core |\n";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, Declared);

        DraftResult draft = await TechStack(workspace);

        Assert.IsTrue(draft.MayDraft, draft.Instructions);

        string draftPath = Path.Combine(
            workspace.Root,
            Cycle.SdlcDirectoryName,
            WriteTechStackDraft.DraftFileName);

        Assert.IsTrue(File.Exists(draftPath), "The draft is written, not printed for a person to retype.");
        Assert.AreEqual(
            Declared,
            await File.ReadAllTextAsync(draftPath),
            "A byte copy. Anything else would make the rename able to drop a row nobody meant to lose.");

        StringAssert.Contains(draft.Instructions, WriteTechStackDraft.DraftFileName);
        StringAssert.Contains(
            draft.Instructions,
            "CON_15",
            "The instructions say what belongs in a change here, because the commonest wrong reason to "
            + "edit this file is a package one cycle happened to need.");
    }

    [TestMethod]
    public async Task A_stack_draft_refuses_where_the_solution_was_never_adopted()
    {
        // Adoption creates it from the profile's template, and that is the one moment anything but a
        // person writes it. With no file there is nothing to copy, and inventing one here would be the
        // tool declaring a stack.
        using var workspace = CycleWorkspace.Create();

        DraftResult draft = await TechStack(workspace);

        Assert.IsFalse(draft.MayDraft);
        StringAssert.Contains(draft.Instructions, "has not been adopted");
    }

    private static async Task<DraftResult> Solution(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<DraftResult> result = await gate
            .Send<WriteSolutionDraft.Command, DraftResult>(new WriteSolutionDraft.Command());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static async Task<DraftResult> TechStack(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        return Ok(await gate
            .Send<WriteTechStackDraft.Command, DraftResult>(new WriteTechStackDraft.Command()));
    }

    private static async Task<DraftResult> Profile(CycleWorkspace workspace, string profile)
    {
        using var gate = Gate.For(workspace.Root);

        return Ok(await gate.Send<ProfileText.Query, DraftResult>(new ProfileText.Query(profile)));
    }

    private static DraftResult Ok(Result<DraftResult> result)
    {
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
