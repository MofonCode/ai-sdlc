using Sdlc.Core.Artifacts;

namespace Sdlc.Tests;

/// <summary>
/// What canonicalisation must and must not hide.
/// </summary>
/// <remarks>
/// The hash is the whole of what makes an approval bind to bytes, so both directions matter: it has to
/// survive what a platform does on its own, and it has to move for anything a person did.
/// </remarks>
[TestClass]
public sealed class ContentHashTests
{
    [TestMethod]
    public void A_crlf_file_and_an_lf_file_hash_the_same()
    {
        // .gitattributes normalises to LF in the repository and checks out native, so the same
        // committed file is CRLF on Windows and LF elsewhere. A hash that moved would refuse every
        // cycle on a teammate's machine.
        Assert.AreEqual(
            ContentHash.Of("one\ntwo\nthree\n"),
            ContentHash.Of("one\r\ntwo\r\nthree\r\n"));
    }

    [TestMethod]
    public void Trailing_whitespace_on_a_line_does_not_change_the_hash()
    {
        Assert.AreEqual(
            ContentHash.Of("a line\nanother\n"),
            ContentHash.Of("a line   \nanother\t\n"));
    }

    [TestMethod]
    public void A_file_ending_in_one_newline_and_one_ending_in_several_hash_the_same()
    {
        Assert.AreEqual(
            ContentHash.Of("content\n"),
            ContentHash.Of("content\n\n\n\n"));
    }

    [TestMethod]
    public void A_file_with_no_closing_newline_hashes_as_one_that_has_it()
    {
        Assert.AreEqual(ContentHash.Of("content\n"), ContentHash.Of("content"));
    }

    [TestMethod]
    public void A_changed_word_changes_the_hash()
    {
        Assert.AreNotEqual(ContentHash.Of("the roll is impartial\n"), ContentHash.Of("the roll is fair\n"));
    }

    [TestMethod]
    public void Leading_whitespace_changes_the_hash_because_indentation_is_content()
    {
        // Markdown gives a leading space meaning - a nested list item, a code block - so stripping it
        // would let a real change pass as formatting.
        Assert.AreNotEqual(ContentHash.Of("- a point\n"), ContentHash.Of("    - a point\n"));
    }

    [TestMethod]
    public void A_blank_line_in_the_middle_changes_the_hash_because_it_separates_paragraphs()
    {
        Assert.AreNotEqual(ContentHash.Of("one\ntwo\n"), ContentHash.Of("one\n\ntwo\n"));
    }

    [TestMethod]
    public void The_hash_is_lower_case_hex_of_the_expected_length()
    {
        string hash = ContentHash.Of("anything");

        Assert.AreEqual(64, hash.Length);
        Assert.IsTrue(hash.All(c => char.IsAsciiDigit(c) || (c is >= 'a' and <= 'f')), hash);
    }

    [TestMethod]
    public void An_abbreviation_is_twelve_characters_so_two_can_be_compared_by_eye()
    {
        string hash = ContentHash.Of("anything");

        Assert.AreEqual(12, ContentHash.Abbreviate(hash).Length);
        StringAssert.StartsWith(hash, ContentHash.Abbreviate(hash));
    }

    [TestMethod]
    public void An_abbreviation_of_something_already_short_is_returned_whole()
    {
        Assert.AreEqual("missing", ContentHash.Abbreviate("missing"));
    }

    [TestMethod]
    public void Canonicalisation_leaves_an_empty_document_empty_rather_than_a_bare_newline()
    {
        Assert.AreEqual(string.Empty, ContentHash.Canonicalise("\n\n   \n"));
    }
}
