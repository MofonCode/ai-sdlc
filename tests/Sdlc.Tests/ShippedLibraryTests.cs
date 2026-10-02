using FluentResults;
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
}
