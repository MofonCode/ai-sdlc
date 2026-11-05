using Sdlc.Core.Constraints;

namespace Sdlc.Tests;

[TestClass]
public sealed class ConstraintIdTests
{
    [TestMethod]
    [DataRow("CON_01", ConstraintTier.Global, 1, 1)]
    [DataRow("CON_12", ConstraintTier.Global, 12, 1)]
    [DataRow("PRO_03", ConstraintTier.Profile, 3, 1)]
    [DataRow("LOC_07", ConstraintTier.Solution, 7, 1)]
    [DataRow("CON_01.2", ConstraintTier.Global, 1, 2)]
    [DataRow("LOC_104.11", ConstraintTier.Solution, 104, 11)]
    public void A_well_formed_id_parses_to_its_tier_number_and_revision(
        string text,
        ConstraintTier tier,
        int number,
        int revision)
    {
        Assert.IsTrue(ConstraintId.TryParse(text, out ConstraintId? id));
        Assert.AreEqual(tier, id.Value.Tier);
        Assert.AreEqual(number, id.Value.Number);
        Assert.AreEqual(revision, id.Value.Revision);
    }

    [TestMethod]
    public void An_id_without_a_suffix_is_revision_one()
    {
        Assert.IsTrue(ConstraintId.TryParse("CON_04", out ConstraintId? id));
        Assert.AreEqual(1, id.Value.Revision);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    [DataRow("CON01")]
    [DataRow("CON_1")]
    [DataRow("XXX_01")]
    [DataRow("CON_01.")]
    [DataRow("CON_01.a")]
    [DataRow("con_01")]
    [DataRow("CON_01.2.3")]
    public void A_malformed_id_does_not_parse(string? text)
    {
        Assert.IsFalse(ConstraintId.TryParse(text, out ConstraintId? id));
        Assert.IsNull(id);
    }

    [TestMethod]
    public void Revision_one_renders_without_a_suffix_so_the_common_case_reads_as_written()
    {
        Assert.IsTrue(ConstraintId.TryParse("CON_01", out ConstraintId? id));
        Assert.AreEqual("CON_01", id.Value.ToString());
    }

    [TestMethod]
    public void A_later_revision_renders_with_its_suffix_so_a_citation_names_the_wording()
    {
        Assert.IsTrue(ConstraintId.TryParse("CON_01.3", out ConstraintId? id));
        Assert.AreEqual("CON_01.3", id.Value.ToString());
    }

    [TestMethod]
    public void A_number_below_ten_renders_padded_so_ids_sort_as_text()
    {
        Assert.IsTrue(ConstraintId.TryParse("CON_07", out ConstraintId? id));
        Assert.AreEqual("CON_07", id.Value.ToString());
    }

    [TestMethod]
    public void Two_revisions_of_one_rule_are_not_equal_because_a_citation_names_the_wording()
    {
        Assert.IsTrue(ConstraintId.TryParse("CON_01", out ConstraintId? first));
        Assert.IsTrue(ConstraintId.TryParse("CON_01.2", out ConstraintId? second));
        Assert.AreNotEqual(first.Value, second.Value);
        Assert.AreEqual(first.Value.Number, second.Value.Number);
    }
}
