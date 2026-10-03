using FluentResults;
using Sdlc.Core;
using Sdlc.Core.Constraints;

namespace Sdlc.Tests;

/// <summary>
/// Asserts the shipped library itself, not the parser.
/// </summary>
/// <remarks>
/// CONSTRAINTS.md promises that a constraint missing a clause is invalid rather than untidy and that
/// the suite fails on it. This is that promise. Without it the rule would be prose about a check
/// nothing performs, which is the failure the framework is built to avoid.
/// </remarks>
[TestClass]
public sealed class ShippedLibraryTests
{
    private static IReadOnlyList<Constraint> Library { get; set; } = [];

    [ClassInitialize]
    public static void Load(TestContext context)
    {
        string text = File.ReadAllText(RepoPaths.Library);
        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(text, RepoPaths.Library);

        Assert.IsTrue(
            result.IsSuccess,
            $"The shipped library does not parse: {string.Join("; ", result.Errors.Select(e => e.Message))}");

        Library = result.Value;
    }

    [TestMethod]
    public void Every_shipped_constraint_carries_all_four_parts()
    {
        foreach (Constraint constraint in Library)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(constraint.Statement),
                $"{constraint.Id} has no statement.");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(constraint.AppliesTo),
                $"{constraint.Id} has no 'Applies to' clause.");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(constraint.Forbids),
                $"{constraint.Id} has no 'Forbids' clause.");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(constraint.Why),
                $"{constraint.Id} has no 'Why' clause.");
        }
    }

    [TestMethod]
    public void No_shipped_constraint_restates_its_statement_as_its_forbids_clause()
    {
        foreach (Constraint constraint in Library)
        {
            Assert.IsFalse(
                string.Equals(constraint.Statement, constraint.Forbids, StringComparison.OrdinalIgnoreCase),
                $"{constraint.Id}'s Forbids clause restates its statement. CON_11 forbids exactly that: "
                + "a clause that names nothing illegal rules nothing out.");
        }
    }

    [TestMethod]
    public void Every_shipped_constraint_is_global_because_this_is_the_global_library()
    {
        foreach (Constraint constraint in Library)
        {
            Assert.AreEqual(
                ConstraintTier.Global,
                constraint.Id.Tier,
                $"{constraint.Id} is in the global library. A profile rule belongs in a profile package "
                + "and a solution rule in the solution.");
        }
    }

    [TestMethod]
    public void The_shipped_ids_are_a_gapless_sequence_from_one()
    {
        int[] numbers = [.. Library.Select(constraint => constraint.Id.Number).Order()];

        Assert.AreEqual(1, numbers[0], "The sequence starts at CON_01.");

        for (int index = 1; index < numbers.Length; index++)
        {
            Assert.AreEqual(
                numbers[index - 1] + 1,
                numbers[index],
                $"CON_{numbers[index - 1] + 1:D2} is missing. A removed constraint leaves a gap on purpose, "
                + "and when that happens this assertion is what gets relaxed - with the commit that made "
                + "the gap named here.");
        }
    }

    [TestMethod]
    public void No_shipped_constraint_names_a_language_framework_or_package()
    {
        // Stack neutrality is what lets the whole library survive a change of profile or host
        // untouched. These are the names most likely to creep in from the default stack.
        string[] stackWords =
        [
            "C#", ".NET", "dotnet", "Blazor", "MAUI", "Aspire", "MSTest", "EF Core",
            "Azure", "SQL Server", "Redis", "Terraform", "Playwright", "FluentResults",
        ];

        foreach (Constraint constraint in Library)
        {
            string text = string.Join(
                ' ',
                constraint.Statement,
                constraint.AppliesTo,
                constraint.Forbids,
                constraint.Why);

            foreach (string word in stackWords)
            {
                Assert.IsFalse(
                    text.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"{constraint.Id} names '{word}'. Global constraints bind solutions on any stack, "
                    + "so stack specifics belong in the profile and in analyzers.");
            }
        }
    }

    [TestMethod]
    public void The_library_holds_the_fourteen_constraints_iteration_four_carries()
    {
        Assert.AreEqual(
            14,
            Library.Count,
            "Nine came across from iteration 3 unchanged, CON_10 was rebound to a gate refusal, and "
            + "CON_12, CON_13 and CON_14 were added in iteration 4. A deliberate addition updates this "
            + "number; an accident fails here.");
    }

    [TestMethod]
    public void A_rebound_constraint_keeps_its_id_and_gains_a_revision()
    {
        // CON_10 used to govern an accepted finding's reason. Nothing is accepted at the gate any more,
        // and the rule it states applies unchanged to a refusal's reason -- so it is a revision rather
        // than a new id, and a finding that cited the earlier wording stays readable against it.
        Constraint rebound = Library.Single(constraint => constraint.Id.Number == 10);

        Assert.AreEqual(2, rebound.Id.Revision);
        Assert.AreEqual("CON_10.2", rebound.Id.ToString());
        Assert.IsNull(
            rebound.SupersededBy,
            "The forbids clause did not change, so this is the same constraint better worded rather "
            + "than a replacement.");
    }

    [TestMethod]
    public void No_constraint_mentions_accepting_or_waiving_a_finding()
    {
        // A blocking finding cannot be waived by any mechanism. A constraint still describing one would
        // be the rule's last hiding place, and the library is what an interrogation is handed.
        foreach (Constraint constraint in Library)
        {
            string text = constraint.Statement + " " + constraint.AppliesTo + " " + constraint.Forbids;

            foreach (string word in new[] { "accepted finding", "waive", "waived", "accepted_findings" })
            {
                Assert.IsFalse(
                    text.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"{constraint.Id} still speaks of '{word}'.");
            }
        }
    }

    [TestMethod]
    public async Task The_binary_carries_the_library_and_it_is_the_one_in_this_repository()
    {
        // The packed tool shipped no library at all, so an installed gate answered `help` and
        // `version` and nothing else, both generated pipelines installed a binary whose one
        // non-bypassable check could never pass, and the turn-end gate failed open. The library is now
        // an embedded resource, which cannot be left out of a package. D-41.
        //
        // Null means "the copy the binary carries". Asserting it against the file on disk is what
        // catches the resource being dropped from the project file or embedded under another name --
        // both of which leave every other test passing, because every other test reads the file.
        FileConstraintDocuments documents = new(libraryPath: null, solutionRoot: Path.GetTempPath());

        IReadOnlyList<ConstraintDocument> sources = await documents.Enumerate(TestContext.CancellationToken);
        ConstraintDocument global = sources.Single(source => source.Tier == ConstraintTier.Global);

        Assert.AreEqual(
            await File.ReadAllTextAsync(RepoPaths.Library, TestContext.CancellationToken),
            global.Text,
            "The embedded library is not the one at the root of this repository.");

        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(global.Text, global.Source);

        Assert.IsTrue(parsed.IsSuccess, string.Join("; ", parsed.Errors.Select(error => error.Message)));
        Assert.AreEqual(Library.Count, parsed.Value.Count, "The same rules, however they were read.");
    }

    [TestMethod]
    public async Task The_embedded_library_names_the_release_rather_than_a_path()
    {
        // `sdlc constraints` prints where each tier was read from, and for an installed tool the
        // honest answer is not a path -- it would be one inside the dotnet tool store, which tells a
        // reader less than the release does. Which is also why the member is called Source: a
        // property named Path holding something that is not one gets passed to File.Exists.
        FileConstraintDocuments documents = new(libraryPath: null, solutionRoot: Path.GetTempPath());

        IReadOnlyList<ConstraintDocument> sources = await documents.Enumerate(TestContext.CancellationToken);
        string source = sources.Single(entry => entry.Tier == ConstraintTier.Global).Source;

        StringAssert.Contains(source, "embedded");
        StringAssert.Contains(source, Release.Version);
        Assert.IsFalse(File.Exists(source), "It is deliberately not a path.");
    }

    [TestMethod]
    public async Task A_library_file_still_wins_over_the_embedded_copy()
    {
        // The lookup order is SDLC_CONSTRAINTS, then beside the gate, then up the tree, then the
        // embedded copy. Keeping the file ahead of the resource is what keeps this repository's own
        // loop: editing CONSTRAINTS.md and running the build output takes effect with no rebuild.
        string path = Path.Combine(Path.GetTempPath(), "sdlc-library", Guid.NewGuid().ToString("N") + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            # Rules

            ## CON_01 — Only this one

            **Applies to:** every artifact

            **Forbids:** nothing at all

            **Why:** a file supplied it, so the embedded copy must not be the one that binds

            """,
            TestContext.CancellationToken);

        try
        {
            FileConstraintDocuments documents = new(path, Path.GetTempPath());

            IReadOnlyList<ConstraintDocument> sources =
                await documents.Enumerate(TestContext.CancellationToken);
            ConstraintDocument global = sources.Single(source => source.Tier == ConstraintTier.Global);

            Assert.AreEqual(path, global.Source);
            StringAssert.Contains(global.Text, "Only this one");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;
}
