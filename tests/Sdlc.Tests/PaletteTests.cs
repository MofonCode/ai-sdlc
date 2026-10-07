using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Hooks;
using Sdlc.Core.Palettes;

namespace Sdlc.Tests;

/// <summary>
/// The register: the roles a solution's surfaces use, and the one place their values live.
/// </summary>
/// <remarks>
/// The failure to guard against here is not a wrong colour -- nobody is judged against one. It is the
/// palette becoming a second place the truth lives, which happens two ways: a tool inventing values
/// nobody chose, and a blank file that cannot say whether it is unfinished or deliberately empty. Both
/// end with somebody trusting the copy in the stylesheet instead.
/// </remarks>
[TestClass]
public sealed class PaletteTests
{
    [TestMethod]
    public void A_palette_reads_its_roles_its_values_and_whether_it_was_settled()
    {
        Palette palette = Read(
            """
            {
              "settled": true,
              "roles": { "surface": "#FFFFFF", "cancel": null }
            }
            """);

        Assert.IsTrue(palette.Settled);
        Assert.AreEqual(2, palette.Slots.Count);
        Assert.AreEqual("#FFFFFF", palette.Slots[0].Value);
        Assert.IsNull(palette.Slots[1].Value, "A declared role with no value is a slot, not an absence.");
    }

    [TestMethod]
    public void A_blank_run_of_values_is_not_a_value()
    {
        // A role filled with spaces reads as filled to anything comparing strings, and as empty to
        // anybody looking at the page. One of those has to win, and it is the person's reading.
        Palette palette = Read("""{ "roles": { "surface": "   " } }""");

        Assert.IsNull(palette.Slots.Single().Value);
        Assert.AreEqual("surface", palette.Unfilled.Single());
    }

    [TestMethod]
    public void A_file_that_is_not_json_is_refused_and_names_itself()
    {
        Result<Palette> read = Palette.Read("{ this is not json", "palette.json");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(string.Join("; ", read.Errors.Select(e => e.Message)), "palette.json");
    }

    [TestMethod]
    public void A_palette_with_no_roles_object_declares_nothing_and_is_refused()
    {
        Result<Palette> read = Palette.Read("""{ "settled": true }""", "palette.json");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(string.Join("; ", read.Errors.Select(e => e.Message)), "declares nothing");
    }

    [TestMethod]
    public void What_is_written_is_what_is_read_back()
    {
        // The proposer prints this file and a person places it, so a shape the reader would reject is a
        // proposal nobody can use. One definition rather than two is what keeps that true.
        Palette written = Read(Palette.Write(Palette.Empty()));

        Assert.IsFalse(written.Settled);
        Assert.AreEqual(Palette.Skeleton.Count, written.Slots.Count);
        CollectionAssert.AreEqual(
            Palette.Skeleton.ToList(),
            written.Slots.Select(slot => slot.Role).ToList());
    }

    [TestMethod]
    public void The_file_is_spelled_the_way_a_front_end_spells_json()
    {
        // This file is imported by a stylesheet as well as read by the gate, so it is camel case rather
        // than the snake case the findings rounds use -- those are the framework's own format and nothing
        // outside reads them. The proposal's own instructions say "settled", so the emitted key has to.
        string json = Palette.Write(Palette.Empty());

        StringAssert.Contains(json, "\"settled\"");
        StringAssert.Contains(json, "\"roles\"");
    }

    [TestMethod]
    public void A_hand_edited_file_works_whichever_case_somebody_typed()
    {
        Palette palette = Read("""{ "Settled": true, "Roles": { "surface": "#FFF" } }""");

        Assert.IsTrue(palette.Settled);
        Assert.AreEqual("#FFF", palette.Slots.Single().Value);
    }

    [TestMethod]
    public void A_skeleton_is_short_on_purpose_and_every_role_is_one_a_surface_must_choose()
    {
        // A long skeleton is a guess with the authority of a default. The roles a solution actually needs
        // are the ones it adds, so the test that matters is that nothing here is a colour's name.
        foreach (string role in Palette.Skeleton)
        {
            foreach (string colour in new[] { "blue", "red", "green", "grey", "gray", "white", "black" })
            {
                Assert.IsFalse(
                    role.Contains(colour, StringComparison.OrdinalIgnoreCase),
                    $"'{role}' names a colour rather than a role, so a rebrand would have to rename it.");
            }
        }

        Assert.IsTrue(Palette.Skeleton.Contains("font-body"), "A typeface drifts the way a colour does.");
    }

    [TestMethod]
    public async Task A_solution_with_no_palette_is_reported_rather_than_refused()
    {
        // Correct for a solution with no front-end surface. Nothing here decides which it is: a check
        // that claimed to verify a UI solution has a palette would have to decide what a UI is.
        using var workspace = CycleWorkspace.Create();

        ReadPalette.Response response = await ReadFrom(workspace);

        Assert.IsFalse(response.Recorded);
        StringAssert.Contains(PaletteRenderer.Render(response), "No palette.");
    }

    [TestMethod]
    public async Task An_unsettled_palette_says_it_is_a_skeleton_rather_than_an_answer()
    {
        // Not decided yet and deliberately none read identically in a file of empty values, and guessing
        // wrong in either direction wastes a conversation.
        using var workspace = CycleWorkspace.Create();
        Write(workspace, Palette.Write(Palette.Empty()));

        string rendered = PaletteRenderer.Render(await ReadFrom(workspace));

        StringAssert.Contains(rendered, "Not settled");
        StringAssert.Contains(rendered, "deliberate rather than unfinished");
    }

    [TestMethod]
    public async Task A_settled_palette_with_an_empty_role_reads_as_settled()
    {
        using var workspace = CycleWorkspace.Create();
        Write(workspace, """{ "settled": true, "roles": { "surface": "#FFF", "cancel": null } }""");

        ReadPalette.Response response = await ReadFrom(workspace);

        Assert.IsTrue(response.Palette!.Settled);
        Assert.IsFalse(response.Palette.IsComplete);
        StringAssert.Contains(PaletteRenderer.Render(response), "Settled.");
    }

    [TestMethod]
    public async Task The_proposal_prints_a_skeleton_when_there_is_none_and_never_a_value()
    {
        using var workspace = CycleWorkspace.Create();

        string proposed = PaletteRenderer.Propose(await ReadFrom(workspace));

        StringAssert.Contains(proposed, Palette.FileName);
        StringAssert.Contains(proposed, "\"surface\": null", "A role is proposed; a value is not.");
        Assert.IsFalse(
            proposed.Contains('#', StringComparison.Ordinal),
            "A tool choosing a brand's colour is the one proposal here nobody asked for.");
    }

    [TestMethod]
    public async Task The_proposal_names_the_empty_roles_in_a_palette_that_exists()
    {
        using var workspace = CycleWorkspace.Create();
        Write(workspace, """{ "settled": false, "roles": { "surface": "#FFF", "cancel": null } }""");

        string proposed = PaletteRenderer.Propose(await ReadFrom(workspace));

        StringAssert.Contains(proposed, "already has a palette");
        StringAssert.Contains(proposed, "cancel");
        Assert.IsFalse(proposed.Contains("surface", StringComparison.Ordinal), "A filled role is not a gap.");
    }

    [TestMethod]
    public async Task A_complete_palette_has_nothing_to_propose()
    {
        using var workspace = CycleWorkspace.Create();
        Write(workspace, """{ "settled": true, "roles": { "surface": "#FFF" } }""");

        string proposed = PaletteRenderer.Propose(await ReadFrom(workspace));

        StringAssert.Contains(proposed, "nothing to propose");
        StringAssert.Contains(proposed, "declares the file", "Changing a value is ordinary declared work.");
    }

    [TestMethod]
    [DataRow("C:/solution/.sdlc/palette.json")]
    [DataRow(".sdlc/palette.json")]
    public void A_palette_is_not_protected_and_that_asymmetry_is_deliberate(string path)
    {
        // The rule file and the decision record are protected because a stage is judged against one and
        // reads the other for alignment, so an agent writing either could satisfy a rule by doing so.
        // Nothing reads a colour that way, so there is no loop to close -- and a hook rule nothing needs
        // would be a rule somebody later carves an exception into.
        Assert.IsFalse(ProtectedPaths.IsProtected(path, out _));
        Assert.IsFalse(ProtectedPaths.CommandTouchesProtected("echo x > .sdlc/palette.json", out _));
    }

    private static async Task<ReadPalette.Response> ReadFrom(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ReadPalette.Response> result = await gate
            .Send<ReadPalette.Query, ReadPalette.Response>(new ReadPalette.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    private static Palette Read(string json)
    {
        Result<Palette> read = Palette.Read(json, "palette.json");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        return read.Value;
    }

    private static void Write(CycleWorkspace workspace, string json) =>
        CycleWorkspace.WriteRaw(
            Path.Combine(workspace.Root, Cycle.SdlcDirectoryName, Palette.FileName),
            json);
}
