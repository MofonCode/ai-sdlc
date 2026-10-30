using FluentResults;
using Sdlc.Core.Artifacts;

namespace Sdlc.Tests;

/// <summary>
/// The one reader of an artifact's frontmatter, which the schema, the chain, the status panel and the
/// sitrep all ask.
/// </summary>
/// <remarks>
/// The YAML cases are held here rather than through a cycle workspace, because the bug they guard was
/// in the reading: a line scan that saw quotes a parser would have dropped, a comment as a key, and a
/// key inside a block scalar as a second one (F-06). VerifyChainTests keeps one quoted outcome, to
/// hold that the chain still asks this reader.
/// </remarks>
[TestClass]
public sealed class FrontmatterTests
{
    [TestMethod]
    [DataRow("outcome: pass", DisplayName = "plain")]
    [DataRow("outcome: \"pass\"", DisplayName = "double-quoted")]
    [DataRow("outcome: 'pass'", DisplayName = "single-quoted")]
    [DataRow("outcome:   pass  ", DisplayName = "padded")]
    public void A_key_reads_as_its_value_however_the_yaml_spells_the_scalar(string yaml)
    {
        Assert.AreEqual("pass", Frontmatter.Key(yaml, "outcome"));
    }

    [TestMethod]
    public void A_comment_is_not_a_key()
    {
        Assert.AreEqual(
            "pass",
            Frontmatter.Key("# outcome: blocked -- a note somebody left\noutcome: pass", "outcome"));
    }

    [TestMethod]
    public void A_key_inside_a_block_scalar_is_part_of_that_value()
    {
        // The line scan took the first line beginning `outcome:` after trimming, so a block scalar
        // mentioning one decided whether the cycle could merge.
        const string yaml = "note: |\n  outcome: blocked\n  which this is not\noutcome: pass";

        Assert.AreEqual("pass", Frontmatter.Key(yaml, "outcome"));
    }

    [TestMethod]
    [DataRow("release: 1", DisplayName = "absent")]
    [DataRow("outcome:", DisplayName = "empty")]
    [DataRow("outcome: \"  \"", DisplayName = "blank")]
    [DataRow("outcome: [unclosed", DisplayName = "not valid YAML")]
    public void A_key_with_nothing_to_read_is_null_rather_than_a_value(string yaml)
    {
        Assert.IsNull(Frontmatter.Key(yaml, "outcome"));
    }

    [TestMethod]
    public void Yaml_that_will_not_parse_is_a_failure_that_says_so()
    {
        Result<IReadOnlyDictionary<string, string?>> keys = Frontmatter.Keys("outcome: [unclosed");

        Assert.IsTrue(keys.IsFailed);
        StringAssert.Contains(keys.Errors[0].Message, "not valid YAML");
    }

    [TestMethod]
    public void A_document_splits_into_its_frontmatter_and_its_prose()
    {
        Result<FrontmatterDocument> read = Frontmatter.Read("---\r\noutcome: pass\r\n---\r\n# Verify\r\n", "verify.md");

        Assert.IsTrue(read.IsSuccess);
        Assert.AreEqual("outcome: pass", read.Value.Yaml, "The fences are not part of the YAML, and CRLF reads as LF.");
        Assert.AreEqual("# Verify\n", read.Value.Body);
    }

    [TestMethod]
    [DataRow("# Verify\n---\noutcome: pass\n---\n", "no frontmatter", DisplayName = "prose before the fence")]
    [DataRow("---\noutcome: pass\n", "never closes", DisplayName = "unclosed")]
    public void A_document_without_a_whole_frontmatter_is_refused_and_named(string text, string expected)
    {
        Result<FrontmatterDocument> read = Frontmatter.Read(text, "verify.md");

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "verify.md");
        StringAssert.Contains(read.Errors[0].Message, expected);
    }
}
