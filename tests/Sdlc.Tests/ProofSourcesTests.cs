using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Findings;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// A proof that lives in code the change never touched is handed to the verify whole, from the build's commit
/// (D-168).
/// </summary>
[TestClass]
public sealed class ProofSourcesTests
{
    private const string Before = "0123456789abcdef0123456789abcdef01234567";
    private const string After = "fedcba9876543210fedcba9876543210fedcba98";

    [TestMethod]
    public async Task A_test_the_patch_never_mentions_is_handed_over_from_the_file_that_defines_it()
    {
        // MarketSense's cycle _59: a criterion guarding existing behaviour, proven by a test the change left
        // alone, so its assertion was in no evidence and the Met could quote nothing.
        using CycleWorkspace workspace = Recorded(proofs: 2);
        FakeRepository git = Git("+public void Thing_does_the_thing_1() { Assert.IsTrue(rolled); }\n");
        git.Containing["Thing_does_the_thing_2"] = ["tests/OldTests.cs"];
        git.Files[After + ":tests/OldTests.cs"] = "public void Thing_does_the_thing_2()\n{\n    Assert.AreEqual(\"AAPL\", chart.Symbol);\n}\n";
        git.Containing["Thing_does_the_thing_1"] = ["tests/NewTests.cs"];

        string? sources = await ProofSources.Rebuild(workspace.Cycle, git, workspace.Root, CancellationToken.None);

        Assert.IsNotNull(sources);
        StringAssert.Contains(sources, "===== tests/OldTests.cs, lines 1-4 =====");
        StringAssert.Contains(sources, "Assert.AreEqual(\"AAPL\", chart.Symbol);");
        StringAssert.Contains(sources, After);
        Assert.IsFalse(sources.Contains("tests/NewTests.cs", StringComparison.Ordinal), "A test the patch shows is read from the patch.");
    }

    [TestMethod]
    public async Task A_helper_a_proof_names_by_path_is_handed_over_and_a_file_the_change_created_is_not()
    {
        using CycleWorkspace workspace = Recorded(proofs: 1);
        Name(workspace, "1. `Thing_does_the_thing_1` asserts through `tests/Support/Dice.cs` and `tests/Support/New.cs`.");
        FakeRepository git = Git("+public void Thing_does_the_thing_1() { Dice.AssertRolled(roll); }\n");
        git.Files[Before + ":tests/Support/Dice.cs"] = "old";
        git.Files[After + ":tests/Support/Dice.cs"] = "public static void AssertRolled(int roll) => Assert.IsTrue(roll is >= 1 and <= 6);\n";
        git.Files[After + ":tests/Support/New.cs"] = "whole in the patch already";

        string? sources = await ProofSources.Rebuild(workspace.Cycle, git, workspace.Root, CancellationToken.None);

        Assert.IsNotNull(sources);
        StringAssert.Contains(sources, "Assert.IsTrue(roll is >= 1 and <= 6)");
        Assert.IsFalse(sources.Contains("tests/Support/New.cs", StringComparison.Ordinal), "A file the change created is whole in the patch.");
    }

    [TestMethod]
    public async Task A_name_too_common_to_place_and_one_found_nowhere_are_said_rather_than_guessed()
    {
        using CycleWorkspace workspace = Recorded(proofs: 2);
        FakeRepository git = Git("+nothing named\n");
        git.Containing["Thing_does_the_thing_1"] = ["a/One.cs", "a/Two.cs", "a/Three.cs", "a/Four.cs"];

        string? sources = await ProofSources.Rebuild(workspace.Cycle, git, workspace.Root, CancellationToken.None);

        Assert.IsNotNull(sources);
        StringAssert.Contains(sources, "`Thing_does_the_thing_1` is in 4 files, too many to say which defines it");
        StringAssert.Contains(sources, "`Thing_does_the_thing_2` is in no file at this commit.");
        Assert.IsFalse(sources.Contains("=====", StringComparison.Ordinal), "No file is handed over on a guess.");
    }

    [TestMethod]
    public async Task Every_proof_in_the_patch_hands_over_nothing_and_leaves_the_hash_as_it_was()
    {
        // A cycle that needs no sources must hash as a round recorded before them did, or every such round
        // would read as stale.
        using CycleWorkspace workspace = Recorded(proofs: 1);
        FakeRepository git = Git("+public void Thing_does_the_thing_1() { }\n");

        Assert.IsNull(await ProofSources.Rebuild(workspace.Cycle, git, workspace.Root, CancellationToken.None));
        Assert.AreEqual(
            await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root, withSources: false),
            await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root));

        git.CommittedPatch = "+nothing named\n";
        git.Containing["Thing_does_the_thing_1"] = ["tests/OldTests.cs"];
        git.Files[After + ":tests/OldTests.cs"] = "Assert.IsTrue(true);";

        Assert.AreNotEqual(
            await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root, withSources: false),
            await RecordRound.EvidenceHash(workspace.Cycle, CancellationToken.None, repository: git, root: workspace.Root),
            "The sources a verify read are part of what it judged.");
    }

    [TestMethod]
    public async Task Every_verify_reader_is_handed_the_sources_when_there_are_some_and_a_stale_copy_is_removed()
    {
        using CycleWorkspace workspace = Recorded(proofs: 1);
        workspace.WriteSpec(criteria: 1);
        workspace.WriteTestSummary(proofs: 1);
        workspace.WriteVerify("pass");
        FakeRepository git = Git("+nothing named\n");
        git.Containing["Thing_does_the_thing_1"] = ["tests/OldTests.cs"];
        git.Files[After + ":tests/OldTests.cs"] = "void Thing_does_the_thing_1()\n{\n    Assert.AreEqual(\"AAPL\", chart.Symbol);\n}\n";
        string directory = Path.Combine(workspace.Root, "round");

        string prompt = ReaderPromptsTests.Whole((await Prompt(workspace, git, directory)).Value);

        StringAssert.Contains(prompt, "<file name=\"evidence/proof-sources.txt\">");
        StringAssert.Contains(prompt, "Assert.AreEqual(\"AAPL\", chart.Symbol);");

        git.CommittedPatch = "+public void Thing_does_the_thing_1() { }\n";
        string again = ReaderPromptsTests.Whole((await Prompt(workspace, git, directory)).Value);

        Assert.IsFalse(again.Contains("proof-sources.txt\">", StringComparison.Ordinal), "A proof back in the patch needs no sources.");
        Assert.IsFalse(File.Exists(Path.Combine(workspace.Cycle.EvidenceDirectory, ProofSources.FileName)));
    }

    [TestMethod]
    public void A_named_test_is_handed_over_as_its_block_rather_than_its_file()
    {
        // Whole files took one MarketSense cycle's sources to 186 KB for every reader.
        string[] csharp =
        [
            "public sealed class RollTests",
            "{",
            "    [TestMethod]",
            "    public void Rolls()",
            "    {",
            "        Assert.IsTrue(roll is >= 1 and <= 6);",
            "",
            "        Assert.AreEqual(6, faces);",
            "    }",
            "",
            "    public void Other() { }",
            "}",
        ];
        string[] python = ["def test_rolls():", "    assert 1 <= roll <= 6", "", "def test_other():", "    pass"];

        Assert.AreEqual((3, 8, false), ProofSources.Blocks(csharp, ["Rolls"]).Single(), "The opening line, the brace, the body and its closing brace.");
        Assert.AreEqual((0, 1, false), ProofSources.Blocks(python, ["test_rolls"]).Single(), "Indentation, whatever the language.");
        Assert.AreEqual(0, ProofSources.Blocks(csharp, ["Roll"]).Count, "A whole word only.");
    }

    [TestMethod]
    public void A_block_the_limit_ends_says_it_was_cut()
    {
        string[] lines = ["void Long()", "{", .. Enumerable.Repeat("    Assert.IsTrue(true);", ProofSources.MostLinesPerBlock + 10), "}"];

        (int first, int last, bool cut) = ProofSources.Blocks(lines, ["Long"]).Single();

        Assert.AreEqual(0, first);
        Assert.AreEqual(ProofSources.MostLinesPerBlock, last);
        Assert.IsTrue(cut, "A reader told nothing would take a cut block as whole.");
    }

    [TestMethod]
    public void A_proof_entry_names_a_file_by_a_path_with_a_slash()
    {
        CollectionAssert.AreEqual(
            new[] { "tests/Support/Dice.cs" },
            DeclaredFiles.PathsIn("`Rolls` asserts through `./tests/Support/Dice.cs`, a `Dice.cs` type and `.NET`.").ToArray());
    }

    private static CycleWorkspace Recorded(int proofs)
    {
        var workspace = CycleWorkspace.Create();
        workspace.WritePlan(proofs);
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.BaseFileName), Before + "\n");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.HeadFileName), After + "\n");
        CycleWorkspace.WriteRaw(Path.Combine(workspace.Cycle.EvidenceDirectory, RecordEvidence.SummaryFileName), "{}");
        return workspace;
    }

    private static FakeRepository Git(string patch) => new() { CommittedPatch = patch };

    private static void Name(CycleWorkspace workspace, string entry)
    {
        string plan = workspace.Cycle.PathOf(ArtifactKind.Plan);
        string text = File.ReadAllText(plan);
        File.WriteAllText(plan, text.Replace("1. `Thing_does_the_thing_1` asserts it.", entry, StringComparison.Ordinal));
    }

    private static async Task<Result<ReaderPrompts.Response>> Prompt(CycleWorkspace workspace, IRepository git, string directory)
    {
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton(git));
        return await gate.Send<ReaderPrompts.Command, ReaderPrompts.Response>(
            new ReaderPrompts.Command(workspace.Cycle.Id, ArtifactKind.Verify, directory, "rules", false));
    }
}
