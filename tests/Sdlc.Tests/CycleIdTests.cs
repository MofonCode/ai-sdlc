using Sdlc.Core.Cycles;

namespace Sdlc.Tests;

[TestClass]
public sealed class CycleIdTests
{
    [TestMethod]
    public void A_well_formed_id_parses_to_the_moment_it_names()
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));

        Assert.AreEqual(new DateTimeOffset(2026, 9, 29, 14, 56, 32, TimeSpan.Zero), id.Value.Opened);
    }

    [TestMethod]
    public void An_id_round_trips_through_its_own_text()
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));

        Assert.AreEqual("20260929_1456_32", id.Value.ToString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("20260929")]
    [DataRow("20260929_1456")]
    [DataRow("20260929-1456-32")]
    [DataRow("20260929_1456_32_1")]
    [DataRow("2026929_1456_32")]
    [DataRow("20260929_1456_3")]
    public void A_malformed_id_does_not_parse(string? text)
    {
        Assert.IsFalse(CycleId.TryParse(text, out CycleId? id));
        Assert.IsNull(id);
    }

    [TestMethod]
    [DataRow("20261332_1456_32")]
    [DataRow("20260229_1456_32")]
    [DataRow("20260929_2599_32")]
    [DataRow("20260929_1456_99")]
    public void A_shape_that_names_no_real_moment_does_not_parse(string text)
    {
        // The pattern alone would accept month thirteen and minute ninety-nine, and an id that parses
        // to nothing would sort and compare in ways nobody intended.
        Assert.IsFalse(CycleId.TryParse(text, out CycleId? id), $"'{text}' names no moment.");
        Assert.IsNull(id);
    }

    [TestMethod]
    public void Ids_sort_chronologically_as_text_which_is_why_they_are_written_this_way_round()
    {
        string[] ids = ["20260929_1456_32", "20260101_0900_00", "20260929_0901_05", "20271231_2359_59"];

        Assert.IsTrue(ids.All(id => CycleId.TryParse(id, out _)));

        string[] byText = [.. ids.Order(StringComparer.Ordinal)];
        string[] byMoment =
        [
            .. ids
                .Select(id => CycleId.TryParse(id, out CycleId? parsed) ? parsed.Value : default)
                .OrderBy(id => id.Opened)
                .Select(id => id.ToString()),
        ];

        CollectionAssert.AreEqual(byText, byMoment);
    }

    [TestMethod]
    public void Allocation_reads_the_clock_it_is_given_and_truncates_to_the_second()
    {
        // Truncation matters: an id carrying milliseconds would not round-trip through its own
        // directory name, so the id in a path and the id in memory would differ.
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 9, 5, 7, 884, TimeSpan.Zero));

        var id = CycleId.Allocate(clock);

        Assert.AreEqual("20261001_0905_07", id.ToString());
        Assert.AreEqual(0, id.Opened.Millisecond);
    }

    [TestMethod]
    public void Allocation_round_trips_through_parsing()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 9, 5, 7, TimeSpan.Zero));

        var allocated = CycleId.Allocate(clock);

        Assert.IsTrue(CycleId.TryParse(allocated.ToString(), out CycleId? reparsed));
        Assert.AreEqual(allocated, reparsed.Value);
    }

    [TestMethod]
    public void The_branch_name_carries_the_id()
    {
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));

        Assert.AreEqual("cycle/20260929_1456_32", id.Value.BranchName());
    }

    [TestMethod]
    [DataRow("cycle/20260929_1456_32")]
    [DataRow("refs/heads/cycle/20260929_1456_32")]
    [DataRow("  cycle/20260929_1456_32  ")]
    [DataRow("cycle/20260929_1456_32/plan")]
    [DataRow("refs/heads/cycle/20260929_1456_32/build")]
    [DataRow("cycle/20260929_1456_32/build-unattended")]
    [DataRow("cycle/20260929_1456_32/forward")]
    public void A_cycle_branch_is_read_however_a_platform_spells_it(string branch)
    {
        // Both generated pipelines hand one of these straight to 'sdlc verify-chain': GitHub's
        // head_ref is the bare branch and Azure DevOps reports the fully qualified reference. Both
        // were refused as malformed ids, so the pull-request check could not run as shipped.
        Assert.IsTrue(CycleId.TryParseBranch(branch, out CycleId? id));
        Assert.AreEqual("20260929_1456_32", id.Value.ToString());
    }

    [TestMethod]
    [DataRow("master")]
    [DataRow("feature/cycle/20260929_1456_32")]
    [DataRow("cycles/20260929_1456_32")]
    [DataRow("cycle/not-an-id")]
    [DataRow("20260929_1456_32")]
    [DataRow("cycle/20260929_1456_32/biuld")]
    [DataRow("cycle/20260929_1456_32/")]
    [DataRow("cycle/20260929_1456_32/build/extra")]
    [DataRow(null)]
    public void A_branch_that_is_not_a_cycle_branch_is_not_read_as_one(string? branch)
    {
        // A bare id is deliberately not a branch. The question this answers is "is this a cycle
        // branch", and the turn-end gate asks it to find out whether it has any business at all.
        Assert.IsFalse(CycleId.TryParseBranch(branch, out CycleId? id));
        Assert.IsNull(id);
    }

    [TestMethod]
    [DataRow(CycleBranchKind.Plan, "cycle/20260929_1456_32/plan")]
    [DataRow(CycleBranchKind.Build, "cycle/20260929_1456_32/build")]
    [DataRow(CycleBranchKind.BuildUnattended, "cycle/20260929_1456_32/build-unattended")]
    [DataRow(CycleBranchKind.Forward, "cycle/20260929_1456_32/forward")]
    public void Each_kind_of_work_has_its_own_branch_under_the_cycle(CycleBranchKind kind, string expected)
    {
        // D-85: everything about one cycle sits under cycle/<id>/, and the name round-trips, because the
        // factory's skip rule reads back the branches it and a person pushed.
        Assert.IsTrue(CycleId.TryParse("20260929_1456_32", out CycleId? id));

        Assert.AreEqual(expected, id.Value.BranchName(kind));
        Assert.IsTrue(CycleBranch.TryParse(expected, out CycleBranch? read));
        Assert.AreEqual(kind, read.Kind);
        Assert.AreEqual(expected, read.ToString());
    }
}
