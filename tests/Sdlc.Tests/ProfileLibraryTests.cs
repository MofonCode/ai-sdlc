using FluentResults;
using Sdlc.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// Asserts the profile tier: the stack rules the framework ships, and that a solution is judged
/// against exactly the ones its declared stack switches on.
/// </summary>
/// <remarks>
/// <para>The tier was wired and empty for three iterations. <c>FileConstraintDocuments</c> took a
/// profile path, nothing ever passed it, no profile shipped a document, and so
/// <c>sdlc constraints</c> printed an identical set on every profile — while the migration drafter,
/// the component dictionary and the first-cycle guide all told a person the set would change. That is
/// the failure this file exists to keep closed: a tier that resolves to nothing looks exactly like a
/// tier whose document was dropped from the build.</para>
/// <para>The global library has its own suite in <c>ShippedLibraryTests</c>. The assertions here are
/// deliberately the mirror of two of those: every rule is in the profile tier rather than the global
/// one, and every rule <em>does</em> name a language, framework or package — because a profile rule
/// that names none had no reason not to be global.</para>
/// </remarks>
[TestClass]
public sealed class ProfileLibraryTests
{
    private const string Profile = "dotnet-default";

    private static IReadOnlyList<Constraint> Library { get; set; } = [];

    [ClassInitialize]
    public static void Load(TestContext context)
    {
        string path = RepoPaths.ProfileLibrary(Profile);
        Result<IReadOnlyList<Constraint>> result = ConstraintParser.Parse(File.ReadAllText(path), path);

        Assert.IsTrue(
            result.IsSuccess,
            $"The {Profile} library does not parse: {string.Join("; ", result.Errors.Select(e => e.Message))}");

        Library = result.Value;
    }

    [TestMethod]
    public void Every_profile_constraint_carries_all_four_parts()
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
    public void Every_rule_here_is_a_profile_rule()
    {
        foreach (Constraint constraint in Library)
        {
            Assert.AreEqual(
                ConstraintTier.Profile,
                constraint.Id.Tier,
                $"{constraint.Id} is in a profile library. A rule that binds every stack belongs in the "
                + "global library, and a rule that binds one solution belongs in that solution.");
        }
    }

    [TestMethod]
    public void Every_rule_here_names_a_language_framework_or_package()
    {
        // The mirror of the global library's stack-neutrality assertion, and it is the admission test
        // for this tier rather than a tidiness check. The global set may not name a tool; this one
        // exists so that a rule needing a name has somewhere to live. A profile rule naming nothing
        // specific would bind solutions on any stack, which means it was in the wrong library -- and
        // it would be invisible, because it reads perfectly well where it is.
        string[] stackWords =
        [
            "C#", ".NET", "dotnet", "Blazor", "MAUI", "Aspire", "MSTest", "EF Core", "bUnit",
            "Azure", "MS SQL", "Redis", "Terraform", "Playwright", "FluentResults", "MofonCode",
            "WebApplicationFactory", "DbContext",
        ];

        foreach (Constraint constraint in Library)
        {
            string text = string.Join(
                ' ',
                constraint.Statement,
                constraint.AppliesTo,
                constraint.Forbids,
                constraint.Why);

            Assert.IsTrue(
                stackWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)),
                $"{constraint.Id} names nothing stack-specific, so nothing stopped it being a global "
                + "rule -- and a global rule in a profile library binds one profile's solutions and no "
                + "others, for no reason a reader could find.");
        }
    }

    [TestMethod]
    public void The_profile_ids_are_a_gapless_sequence_from_one()
    {
        int[] numbers = [.. Library.Select(constraint => constraint.Id.Number).Order()];

        Assert.AreEqual(1, numbers[0], "The sequence starts at PRO_01.");

        for (int index = 1; index < numbers.Length; index++)
        {
            Assert.AreEqual(
                numbers[index - 1] + 1,
                numbers[index],
                $"PRO_{numbers[index - 1] + 1:D2} is missing. A removed rule leaves a gap on purpose, "
                + "and when that happens this assertion is what gets relaxed, with the commit that "
                + "made the gap named here.");
        }
    }

    [TestMethod]
    public void Every_profile_rule_reaches_an_interrogation()
    {
        // The effective set is matched by artifact name, so a rule whose "Applies to" names no artifact
        // is handed to nobody. The global library has one rule in that position on purpose and declares
        // it; this library has none, and a rule drifting out of reach here would be decoration that
        // costs a reader attention at every gate and fires at none.
        string[] artifacts = ["intent", "spec", "plan", "diff", "verify"];
        List<string> unreachable = [.. Library
            .Where(rule => !rule.AppliesTo.Contains("every artifact", StringComparison.OrdinalIgnoreCase)
                && !artifacts.Any(a => rule.AppliesTo.Contains(a, StringComparison.OrdinalIgnoreCase)))
            .Select(rule => rule.Id.ToString())];

        Assert.AreEqual(
            0,
            unreachable.Count,
            "These profile rules are in no artifact's effective set, so no interrogation will ever be "
            + "handed them: " + string.Join(", ", unreachable));
    }

    [TestMethod]
    public void The_library_holds_the_three_rules_that_survived_the_admission_tests()
    {
        Assert.AreEqual(
            3,
            Library.Count,
            "Three rules passed all three admission tests when the tier was filled: a browser criterion "
            + "proven at the wrong layer, a plan naming the kernel without naming the seam, and a proof "
            + "about stored data run against an in-memory substitute. Most candidates failed the second "
            + "test -- an analyzer or a fitness function could decide them -- and that is the tier "
            + "working rather than the tier being thin. A deliberate addition updates this number.");

        Assert.AreEqual(
            Library.Count,
            Library.Count(rule => rule.IsInForce),
            "Nothing here has been superseded yet. When something is, the predecessor stays on the page "
            + "and this is where the two numbers part company.");
    }

    [TestMethod]
    public async Task A_declared_stack_with_every_rule_on_resolves_all_of_them()
    {
        // The default a first draft is written from. What the tier hands on is the embedded document,
        // so a resource dropped from the project file fails here where every test reading the file on
        // disk would still pass.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);
        workspace.WriteTechStack();

        ConstraintDocument profile = (await Resolve(workspace))
            .Single(source => source.Tier == ConstraintTier.Profile);

        CollectionAssert.AreEqual(
            Library.Select(rule => rule.Id.ToString()).ToArray(),
            Parse(profile).Select(rule => rule.Id.ToString()).ToArray(),
            "Every rule the default lists as on is one the solution is judged against.");

        StringAssert.Contains(profile.Source, "techstack.md", "The source names the file that switched them on.");
        StringAssert.Contains(profile.Source, Release.Version, "And which release carries them.");
        Assert.IsFalse(File.Exists(profile.Source), "It is deliberately not a path.");
    }

    [TestMethod]
    public async Task A_rule_the_declared_stack_switches_off_is_not_one_a_plan_is_judged_against()
    {
        // The whole of the change. A person reads three rows, sets one to false, and that rule stops
        // binding -- where a profile name in another file used to bring all three with nothing in the
        // solution saying so.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);
        workspace.WriteTechStack(
            "| language | C# |\n\n"
            + "| Rule | On | What it holds a plan to |\n|---|---|---|\n"
            + "| `PRO_01` | true | browser |\n"
            + "| `PRO_02` | false | kernel |\n"
            + "| `PRO_03` | true | stored data |\n");

        ConstraintDocument profile = (await Resolve(workspace))
            .Single(source => source.Tier == ConstraintTier.Profile);

        CollectionAssert.AreEqual(
            (string[])["PRO_01", "PRO_03"],
            Parse(profile).Select(rule => rule.Id.ToString()).ToArray());

        Assert.IsFalse(
            profile.Text.Contains(Library.Single(rule => rule.Id.Number == 2).Statement, StringComparison.Ordinal),
            "A rule that is off is absent from the document, not present and marked. The interrogation "
            + "is handed this text, and a rule on the page is a rule a reader applies.");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("| language | Python |\n")]
    [DataRow("| `PRO_01` | false | browser |\n")]
    public async Task A_declared_stack_that_switches_no_rule_on_resolves_no_profile_tier(string stack)
    {
        // Absent rather than empty, and the difference is what a person reads: an empty document would
        // print as a tier that resolved and found nothing, which is what a dropped resource looks like.
        // The first case is the open stack -- a file a person placed with nothing in it.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);
        workspace.WriteTechStack(stack);

        Assert.IsFalse((await Resolve(workspace)).Any(source => source.Tier == ConstraintTier.Profile));
    }

    [TestMethod]
    public async Task A_solution_with_no_declared_stack_resolves_no_profile_tier()
    {
        // No cycle opens in this state, so no plan is judged in it. It still must not throw: every
        // caller of this is a gate, and `sdlc doctor` is what reports the missing file by name.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);

        Assert.IsFalse((await Resolve(workspace)).Any(source => source.Tier == ConstraintTier.Profile));
    }

    [TestMethod]
    public async Task An_old_profile_line_selects_no_rules()
    {
        // Rules come from the declared stack alone (D-81). A solution document still carrying the line
        // that used to select them is held to nothing by it, whatever its stack file lists.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted, legacyProfile: Profile);

        workspace.WriteTechStack("| Concern | Choice |\n|---|---|\n| ORM | EF Core |\n");
        Assert.IsFalse(
            (await Resolve(workspace)).Any(source => source.Tier == ConstraintTier.Profile),
            "A stack that lists no rule is held to none, whatever the solution document says.");

        workspace.WriteTechStack("| ORM | EF Core |\n| `PRO_01` | true | browser |\n");
        Assert.AreEqual(
            1,
            Parse((await Resolve(workspace)).Single(source => source.Tier == ConstraintTier.Profile)).Count,
            "One row on, so one rule -- not the ones the old line names.");
    }

    [TestMethod]
    public async Task A_repository_with_no_governed_solution_resolves_no_profile_tier()
    {
        // Every caller of this is a gate about to judge something. A solution document that cannot be
        // read is a larger problem that `sdlc doctor` reports by name, and throwing here would turn it
        // into every command failing with a message about constraints instead.
        FileConstraintDocuments documents = new(RepoPaths.Library, Path.GetTempPath());
        IReadOnlyList<ConstraintDocument> sources =
            await documents.Enumerate(TestContext.CancellationToken);

        Assert.IsFalse(sources.Any(source => source.Tier == ConstraintTier.Profile));
        Assert.IsTrue(
            sources.Any(source => source.Tier == ConstraintTier.Global),
            "The global tier always resolves, which is what keeps a gate from opening with no rules.");
    }

    [TestMethod]
    public async Task A_profile_document_supplied_as_a_file_wins_over_the_embedded_one()
    {
        // The seam an external profile package would arrive through. Nothing resolves one today, so
        // this is the mechanism being kept honest rather than a path anybody takes -- and keeping the
        // parameter ahead of the resource is what makes adding that resolution a new source rather
        // than a redesign.
        string path = Path.Combine(
            Path.GetTempPath(),
            "sdlc-profile-library",
            Guid.NewGuid().ToString("N") + ".md");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """
            # A profile from somewhere else

            ## PRO_01 — Only this one

            **Applies to:** a plan, at approval

            **Forbids:** nothing at all

            **Why:** a file supplied it, so the embedded document must not be the one that binds

            """,
            TestContext.CancellationToken);

        try
        {
            using var workspace = CycleWorkspace.Create();
            workspace.WriteSolution(SolutionOrigin.Adopted);

            FileConstraintDocuments documents = new(RepoPaths.Library, workspace.Root, path);
            IReadOnlyList<ConstraintDocument> sources =
                await documents.Enumerate(TestContext.CancellationToken);

            ConstraintDocument profile = sources.Single(source => source.Tier == ConstraintTier.Profile);

            Assert.AreEqual(path, profile.Source);
            StringAssert.Contains(profile.Text, "Only this one");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task The_profile_tier_resolves_between_the_global_set_and_the_solutions_own()
    {
        // Resolution order is the whole contract of the three tiers: a solution's own rules are the
        // last word, and a profile's come after the framework's. Asserted on the documents rather than
        // on the rendered set, because this is the order everything downstream reads them in.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSolution(SolutionOrigin.Adopted);
        workspace.WriteTechStack();
        workspace.WriteSolutionConstraints(1);

        FileConstraintDocuments documents = new(RepoPaths.Library, workspace.Root);
        IReadOnlyList<ConstraintDocument> sources =
            await documents.Enumerate(TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { ConstraintTier.Global, ConstraintTier.Profile, ConstraintTier.Solution },
            sources.Select(source => source.Tier).ToArray(),
            "Global, then profile, then the solution's own.");
    }

    private async Task<IReadOnlyList<ConstraintDocument>> Resolve(CycleWorkspace workspace) =>
        await new FileConstraintDocuments(RepoPaths.Library, workspace.Root)
            .Enumerate(TestContext.CancellationToken);

    private static IReadOnlyList<Constraint> Parse(ConstraintDocument document)
    {
        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(document.Text, document.Source);

        Assert.IsTrue(parsed.IsSuccess, string.Join("; ", parsed.Errors.Select(e => e.Message)));
        return parsed.Value;
    }

    /// <summary>The test context, for the cancellation token the analysers require to be passed on.</summary>
    public TestContext TestContext { get; set; } = null!;
}
