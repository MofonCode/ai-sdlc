using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Perspectives;

namespace Sdlc.Tests;

/// <summary>
/// Perspectives: methods that steer authoring and judge in a fresh reader of their own (D-90).
/// </summary>
[TestClass]
public sealed class PerspectiveTests
{
    [TestMethod]
    public void Every_shipped_perspective_reads_and_speaks_to_every_stage()
    {
        // A perspective with nothing to say at a stage is a stage where it neither steers nor judges, which
        // is what D-90 rejected a label for.
        Result<IReadOnlyList<Perspective>> all = PerspectiveLibrary.All();

        Assert.IsTrue(all.IsSuccess, string.Join("; ", all.Errors.Select(e => e.Message)));
        Assert.IsTrue(all.Value.Count > 0, "No perspective is embedded, so every check below passes against nothing.");

        foreach (Perspective perspective in all.Value)
        {
            foreach (string stage in Perspective.Stages)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(perspective.Cover[stage]), perspective.Name + " covers nothing at " + stage);
                Assert.IsFalse(string.IsNullOrWhiteSpace(perspective.Questions[stage]), perspective.Name + " asks nothing at " + stage);
            }

            Assert.IsFalse(string.IsNullOrWhiteSpace(perspective.Method), perspective.Name + " has no method.");
        }
    }

    [TestMethod]
    public void Security_is_written_and_always_on()
    {
        Result<Perspective> security = PerspectiveLibrary.Find("security");

        Assert.IsTrue(security.IsSuccess, string.Join("; ", security.Errors.Select(e => e.Message)));
        Assert.IsTrue(security.Value.Always);
        CollectionAssert.Contains(Perspective.AlwaysOn.ToList(), "security");
        StringAssert.Contains(security.Value.Method, "trust boundaries");
    }

    [TestMethod]
    public void An_author_gets_the_short_list_and_a_reader_gets_the_method()
    {
        Perspective security = PerspectiveLibrary.Find("security").Value;

        string cover = ActivePerspectives.RenderStage(security, "spec", cover: true);
        string reader = ActivePerspectives.RenderStage(security, "spec", cover: false);

        Assert.IsFalse(cover.Contains("## Method", StringComparison.Ordinal), "Six full methods in one authoring prompt is the dilution D-90 avoids.");
        StringAssert.Contains(reader, "## Method");
        StringAssert.Contains(reader, "## Ask of this spec");
    }

    [TestMethod]
    public async Task A_cycle_gets_the_three_always_on_and_what_its_intent_names()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            workspace.Cycle.PathOf(ArtifactKind.Intent),
            "---\nframework_version: 1.0.0\ncycle: " + workspace.Cycle.Id + "\ntype: feature addition\nperspectives: accessibility, ui-ux\n---\n\n# x\n");
        using var gate = Gate.For(workspace.Root);

        Result<ActivePerspectives.Response> result = await gate
            .Send<ActivePerspectives.Query, ActivePerspectives.Response>(new ActivePerspectives.Query(workspace.Cycle.Id));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        CollectionAssert.AreEqual(
            new[] { "security", "operability", "maintainability", "accessibility", "ui-ux" },
            result.Value.Perspectives.Select(p => p.Name).ToArray());
        Assert.IsTrue(result.Value.Perspectives.Single(p => p.Name == "security").Written);
        StringAssert.Contains(ActivePerspectives.Render(result.Value), "not written yet");
    }

    [TestMethod]
    public void A_perspective_is_switched_on_by_a_declared_file_matching_its_patterns()
    {
        Result<Perspective> read = Perspective.Read(
            "---\nname: ui-ux\nalways: false\nareas: ui\nfiles: \"*.razor, Pages/\"\n---\n\n# UI\n\nWhy.\n\n## Method\n\nm\n\n## Cover this\n\n"
            + string.Concat(Perspective.Stages.Select(s => "### " + s + "\n\n- c\n\n"))
            + "## Questions\n\n"
            + string.Concat(Perspective.Stages.Select(s => "### " + s + "\n\n- q\n\n"))
            + "## Rules\n\nnone\n\n## Evidence\n\ne\n",
            "ui-ux.md");

        Assert.IsTrue(read.IsSuccess, string.Join("; ", read.Errors.Select(e => e.Message)));
        Assert.IsTrue(read.Value.Matches("src/Web/Components/Dice.razor"));
        Assert.IsTrue(read.Value.Matches("src\\Web\\Pages\\Index.cshtml"));
        Assert.IsFalse(read.Value.Matches("src/Domain/Die.cs"));
    }

    [TestMethod]
    public void A_document_missing_a_stage_is_rejected_and_names_it()
    {
        Result<Perspective> read = Perspective.Read(
            "---\nname: x\n---\n\n# X\n\n## Method\n\nm\n\n## Cover this\n\n### intent\n\n- c\n\n## Questions\n\n### intent\n\n- q\n\n## Rules\n\nr\n\n## Evidence\n\ne\n",
            "x.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "spec");
    }
}
