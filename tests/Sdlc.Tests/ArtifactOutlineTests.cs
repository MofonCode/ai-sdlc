using Sdlc.Core.Artifacts;

namespace Sdlc.Tests;

[TestClass]
public sealed class ArtifactOutlineTests
{
    [TestMethod]
    public void The_first_heading_is_what_the_artifact_is_about()
    {
        ArtifactOutline outline = ArtifactOutlineReader.Read("""
            ---
            cycle: 20260929_1456_32
            ---

            # Players with no die have no way to get an impartial roll

            ## Problem

            ## Who
            """);

        Assert.AreEqual("Players with no die have no way to get an impartial roll", outline.Headline);
        CollectionAssert.AreEqual(new[] { "Problem", "Who" }, outline.Sections.ToArray());
    }

    [TestMethod]
    public void A_heading_inside_frontmatter_is_yaml_and_not_a_heading()
    {
        // `# a comment` in frontmatter is a YAML comment. Reading it as the headline would put the
        // document's metadata in the panel where its subject belongs.
        ArtifactOutline outline = ArtifactOutlineReader.Read("""
            ---
            # this is a yaml comment
            cycle: 20260929_1456_32
            ---

            # The real heading
            """);

        Assert.AreEqual("The real heading", outline.Headline);
    }

    [TestMethod]
    public void A_document_with_no_frontmatter_still_yields_its_headings()
    {
        ArtifactOutline outline = ArtifactOutlineReader.Read("# A heading\n\n## A section\n");

        Assert.AreEqual("A heading", outline.Headline);
        Assert.AreEqual(1, outline.Sections.Count);
    }

    [TestMethod]
    public void A_document_with_no_headings_yields_an_empty_outline_rather_than_failing()
    {
        ArtifactOutline outline = ArtifactOutlineReader.Read("---\ncycle: 20260929_1456_32\n---\n\nJust prose.\n");

        Assert.IsNull(outline.Headline);
        Assert.AreEqual(0, outline.Sections.Count);
    }

    [TestMethod]
    public void Only_the_first_first_level_heading_is_taken_as_the_headline()
    {
        ArtifactOutline outline = ArtifactOutlineReader.Read("# First\n\n# Second\n");

        Assert.AreEqual("First", outline.Headline);
    }

    [TestMethod]
    public void Deeper_headings_are_not_sections()
    {
        ArtifactOutline outline = ArtifactOutlineReader.Read("# H\n\n## A section\n\n### Not a section\n");

        CollectionAssert.AreEqual(new[] { "A section" }, outline.Sections.ToArray());
    }

    [TestMethod]
    public void Crlf_and_lf_read_the_same()
    {
        ArtifactOutline lf = ArtifactOutlineReader.Read("# H\n\n## A\n");
        ArtifactOutline crlf = ArtifactOutlineReader.Read("# H\r\n\r\n## A\r\n");

        Assert.AreEqual(lf.Headline, crlf.Headline);
        CollectionAssert.AreEqual(lf.Sections.ToArray(), crlf.Sections.ToArray());
    }

    [TestMethod]
    public void A_description_counts_the_sections_and_names_them()
    {
        ArtifactOutline outline = new("H", ["Problem", "Who", "Done"]);

        Assert.AreEqual("3 sections: Problem, Who, Done", ArtifactOutlineReader.Describe(outline));
    }

    [TestMethod]
    public void A_description_of_nothing_says_so_rather_than_printing_a_bare_zero()
    {
        Assert.AreEqual("no sections yet", ArtifactOutlineReader.Describe(ArtifactOutline.Empty));
    }

    [TestMethod]
    public async Task A_file_that_does_not_exist_yields_an_empty_outline()
    {
        ArtifactOutline outline = await ArtifactOutlineReader.ReadFile(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.md"),
            CancellationToken.None);

        Assert.AreEqual(ArtifactOutline.Empty, outline);
    }
}
