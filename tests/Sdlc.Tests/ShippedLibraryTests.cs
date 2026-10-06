using System.Globalization;
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
    public void The_library_holds_the_entries_iteration_four_carries()
    {
        Assert.AreEqual(
            18,
            Library.Count,
            "Nine came across from iteration 3 unchanged, CON_10 was rebound to a gate refusal, and "
            + "CON_12, CON_13 and CON_14 were added in iteration 4. Two supersessions followed, each "
            + "leaving its predecessor on the page, and CON_17 was placed after them -- which is why "
            + "this counts entries and the readme counts rules. CON_18 followed, asking a plan that "
            + "changes a rule the cycle is judged by to say what the rule would have cost. A deliberate "
            + "addition updates this number; an accident fails here.");

        Assert.AreEqual(
            16,
            Library.Count(rule => rule.IsInForce),
            "The number of rules that bind is the readme's claim and what 'sdlc constraints' prints. A "
            + "supersession replaces one rule with another and leaves this unchanged; only a genuine "
            + "addition or removal moves it.");
    }

    [TestMethod]
    public void A_superseded_constraint_is_kept_and_stops_binding()
    {
        // The first supersession the library has had, and the distinction it rests on: the old rule
        // stays on the page so a finding that cited it is still readable against what it was judged
        // under, and it leaves the set a plan is held to. Counting entries and counting rules are the
        // same number only while this has never happened.
        // Written against one supersession and generalised at the second, which is the lesson: this is
        // a plural thing and Single was a bet that it would stay singular for the life of the library.
        string[] pairs = [.. Library
            .Where(rule => !rule.IsInForce)
            .Select(rule => rule.Id + " -> " + rule.SupersededBy)
            .Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            new[] { "CON_04 -> CON_16", "CON_13 -> CON_15" },
            pairs,
            "Every superseded rule names its replacement, and a new supersession is added here "
            + "deliberately. A rule that stopped binding without naming what replaced it would leave "
            + "a finding that cited it readable against nothing.");
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
    public void Every_constraint_reaches_an_interrogation_except_the_one_a_person_applies()
    {
        // The effective set is matched by artifact name, so a constraint whose "Applies to" names no
        // artifact is handed to nobody -- it is not in any of the five sets `sdlc constraints --for`
        // renders, and no interrogation ever sees it. CON_10.2 is in that position by construction:
        // its subject is a gate refusal's reason, and a gate decision is a person's act rather than a
        // document. F-27.
        //
        // Declared rather than discovered. A rule nothing applies looks identical to a rule somebody
        // forgot to wire up, and CON_11 is the rule that keeps the library from becoming decoration --
        // so the human-applied set is named here and anything else drifting out of reach fails.
        string[] appliedByAPerson = ["CON_10"];

        string[] artifacts = ["intent", "spec", "plan", "diff", "verify"];
        List<string> unreachable = [];

        foreach (Constraint constraint in Library)
        {
            bool reachable = constraint.AppliesTo.Contains("every artifact", StringComparison.OrdinalIgnoreCase)
                || artifacts.Any(a => constraint.AppliesTo.Contains(a, StringComparison.OrdinalIgnoreCase));

            if (!reachable)
            {
                unreachable.Add(constraint.Id.ToString());
            }
        }

        foreach (string id in appliedByAPerson)
        {
            Assert.IsTrue(
                unreachable.Any(u => u.StartsWith(id, StringComparison.Ordinal)),
                $"{id} is declared as applied by a person, and an interrogation can now reach it. "
                + "Either it gained an artifact subject, in which case take it off that list, or its "
                + "'Applies to' clause drifted.");
        }

        List<string> undeclared = [.. unreachable
            .Where(u => !appliedByAPerson.Any(known => u.StartsWith(known, StringComparison.Ordinal)))];

        Assert.AreEqual(
            0,
            undeclared.Count,
            "These constraints are in no artifact's effective set, so no interrogation will ever be "
            + "handed them: " + string.Join(", ", undeclared)
            + ". Either name an artifact in the 'Applies to' clause, or say in the constraint that a "
            + "person applies it and add it to the list in this test. A rule nothing applies and "
            + "nothing admits to not applying is decoration, which CON_11 exists to prevent.");
    }

    [TestMethod]
    public void A_superseded_constraint_stays_on_the_page_and_out_of_force()
    {
        // The half of the id rules where a check is honest.
        //
        // A revision cannot be policed mechanically. What §7.4 asks of one is that nothing became
        // legal or illegal that was not before, which is a judgement about prose -- and the obvious
        // proxy, comparing the Forbids clause for equality, is wrong in the case that has actually
        // happened: CON_10's prohibition survived unchanged while its subject was deleted, so the
        // clause was rewritten to name what was left. A check that called that a violation would be
        // teaching somebody to phrase a real change carefully instead of catching one. F-40.
        //
        // A supersession can be policed, and it is where loss would matter. When a rule genuinely
        // changes what is forbidden, the old one keeps its id, stays on the page verbatim and gains
        // Superseded-by -- so a finding raised under it stays readable against the rule it was
        // actually judged under. Deleting that entry is a real regression and nothing else would
        // notice: the rule it pointed at is still there, the count still looks right, and only a
        // finding nobody is reading today goes unanswerable.
        List<string> dangling = [];
        List<string> stillInForce = [];

        foreach (Constraint constraint in Library)
        {
            if (constraint.SupersededBy is not { } replacement)
            {
                continue;
            }

            // The constraint it points at has to be here, or the pointer is the only record of a rule
            // nobody can read.
            if (!Library.Any(other => other.Id.Number == replacement.Number))
            {
                dangling.Add(constraint.Id + " is superseded by " + replacement + ", which is not on the page");
            }

            // And it must not still bind. A superseded rule is kept for reading, never for judging.
            if (constraint.IsInForce)
            {
                stillInForce.Add(constraint.Id.ToString());
            }
        }

        Assert.AreEqual(
            0,
            dangling.Count,
            "A Superseded-by clause points at a constraint that is not here: " + string.Join("; ", dangling)
            + ". The replacement has to be on the page, or a finding raised under the old rule leads "
            + "nowhere.");

        Assert.AreEqual(
            0,
            stillInForce.Count,
            "These constraints are superseded and still in force: " + string.Join(", ", stillInForce)
            + ". A superseded rule is kept so a past finding stays readable, never so a new one can be "
            + "raised under it.");

        // The positive control, and it is doing real work here: the library has no supersession yet,
        // so both assertions above pass over an empty set. This proves the machinery would see one --
        // if the parser stopped reading Superseded-by, the loops would stay empty for the wrong reason
        // and a deleted entry would sail through.
        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(
            """
            # Rules

            ## CON_01 — A retired rule

            **Applies to:** every artifact

            **Forbids:** what the replacement forbids, worded worse

            **Why:** kept so a finding raised under it stays readable

            **Superseded-by:** CON_02

            ## CON_02 — Its replacement

            **Applies to:** every artifact

            **Forbids:** the same thing, said properly

            **Why:** this is what binds now

            """,
            "a synthetic library");

        Assert.IsTrue(parsed.IsSuccess, string.Join("; ", parsed.Errors.Select(e => e.Message)));

        Constraint retired = parsed.Value.Single(c => c.Id.Number == 1);

        Assert.IsNotNull(
            retired.SupersededBy,
            "The parser no longer reads Superseded-by, so the assertions above would pass over an "
            + "empty set however the real library was edited.");

        Assert.IsFalse(retired.IsInForce, "A superseded constraint does not bind.");
        Assert.IsTrue(parsed.Value.Single(c => c.Id.Number == 2).IsInForce);
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
