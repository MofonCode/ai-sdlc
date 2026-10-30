using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Hooks;

namespace Sdlc.Tests;

/// <summary>
/// The envelope-and-body shape every artifact has to have.
/// </summary>
/// <remarks>
/// The schema is the mechanical half of the governance split: it decides shape, and the constraints
/// decide reasoning. So what matters here is that the line between them holds -- that the schema says
/// nothing about whether a section is any good, and everything about whether it is there.
/// </remarks>
[TestClass]
public sealed class ArtifactSchemaTests
{
    private const string Id = "20260929_1456_32";

    [TestMethod]
    public void A_well_formed_intent_has_nothing_wrong_with_its_shape()
    {
        ArtifactValidation check = Validate(ArtifactKind.Intent, Intent());

        Assert.IsTrue(check.IsValid, Describe(check));
    }

    [TestMethod]
    public void A_well_formed_artifact_of_every_kind_passes()
    {
        foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
        {
            ArtifactValidation check = Validate(kind, WellFormed(kind));

            Assert.IsTrue(check.IsValid, $"{Cycle.NameOf(kind)}: {Describe(check)}");
        }
    }

    [TestMethod]
    public void An_artifact_with_no_open_section_is_rejected_even_when_nothing_is_open()
    {
        // The whole reason the section is required. Without it there is no way to tell "nothing is open"
        // from "the author forgot", and those two need different responses.
        ArtifactValidation check = Validate(
            ArtifactKind.Spec,
            """
            ---
            framework_version: 1.0.0
            cycle: 20260929_1456_32
            ---

            # a spec

            ## Acceptance criteria

            1. asking for the page returns one roll.

            ## Out of scope

            Nothing.
            """);

        Assert.IsFalse(check.IsValid);
        Assert.IsTrue(check.Violations.Any(v => v.Where == "## Open"), Describe(check));
        StringAssert.Contains(
            check.Violations.Single(v => v.Where == "## Open").Problem,
            "required even when empty");
    }

    [TestMethod]
    public void Every_missing_section_is_reported_rather_than_the_first()
    {
        // An author told only about the first missing section pays for a run per defect.
        ArtifactValidation check = Validate(
            ArtifactKind.Intent,
            """
            ---
            framework_version: 1.0.0
            cycle: 20260929_1456_32
            type: bug fix
            ---

            # an intent

            ## Problem

            Rolling twice returns the same number.

            ## Open
            """);

        Assert.AreEqual(3, check.Violations.Count, Describe(check));
        Assert.IsTrue(check.Violations.All(v => v.Problem == "is missing."), Describe(check));
    }

    [TestMethod]
    public void A_section_the_artifact_does_not_carry_is_a_violation()
    {
        // A section exists because something reads it. Decoration costs attention at every
        // interrogation, which is the cost this keeps out.
        ArtifactValidation check = Validate(
            ArtifactKind.Spec,
            WellFormed(ArtifactKind.Spec) + "\n\n## Notes\n\nSome thoughts.\n");

        Assert.IsFalse(check.IsValid);
        StringAssert.Contains(
            check.Violations.Single(v => v.Where == "## Notes").Problem,
            "not a section a spec carries");
    }

    [TestMethod]
    public void A_frontmatter_key_nothing_declared_is_a_violation()
    {
        // Until a solution can declare the extra keys it carries, extra is wrong rather than tolerated:
        // a field nothing declared is a field nothing reads.
        ArtifactValidation check = Validate(
            ArtifactKind.Spec,
            WellFormed(ArtifactKind.Spec).Replace(
                "cycle: " + Id,
                "cycle: " + Id + "\nticket: PROJ-4821",
                StringComparison.Ordinal));

        Assert.IsFalse(check.IsValid);
        StringAssert.Contains(
            check.Violations.Single(v => v.Where == "ticket").Problem,
            "nothing declared is a field nothing reads");
    }

    [TestMethod]
    public void A_cycle_key_that_disagrees_with_the_path_is_a_violation()
    {
        // The duplication is the point: a file copied out of one cycle into another keeps the id it was
        // written under, and nothing but this comparison would notice.
        ArtifactValidation check = Validate(
            ArtifactKind.Spec,
            WellFormed(ArtifactKind.Spec).Replace(
                "cycle: " + Id,
                "cycle: 20260101_0900_00",
                StringComparison.Ordinal));

        Assert.IsFalse(check.IsValid);
        StringAssert.Contains(
            check.Violations.Single(v => v.Where == "cycle").Problem,
            "One of the two is wrong");
    }

    [TestMethod]
    public void A_missing_envelope_key_is_reported_on_every_kind()
    {
        foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
        {
            ArtifactValidation check = Validate(
                kind,
                WellFormed(kind).Replace("framework_version: 1.0.0\n", string.Empty, StringComparison.Ordinal));

            Assert.IsTrue(
                check.Violations.Any(v => v.Where == "framework_version"),
                $"{Cycle.NameOf(kind)} did not require it: {Describe(check)}");
        }
    }

    [TestMethod]
    [DataRow(ArtifactKind.Intent, "type", "refactor")]
    [DataRow(ArtifactKind.Plan, "tier", "medium")]
    [DataRow(ArtifactKind.Verify, "outcome", "green")]
    public void A_body_key_outside_its_allowed_values_is_a_violation(
        ArtifactKind kind,
        string key,
        string wrong)
    {
        string text = WellFormed(kind);
        string line = text.Split('\n').Single(l => l.StartsWith(key + ":", StringComparison.Ordinal));

        ArtifactValidation check = Validate(kind, text.Replace(line, key + ": " + wrong, StringComparison.Ordinal));

        Assert.IsFalse(check.IsValid);
        StringAssert.Contains(check.Violations.Single(v => v.Where == key).Problem, "Expected one of");
    }

    [TestMethod]
    public void A_document_with_no_frontmatter_still_has_its_sections_checked()
    {
        // Two runs to learn two things is the cost of stopping at the first failure, and a missing fence
        // is exactly the defect most likely to come with others.
        ArtifactValidation check = Validate(
            ArtifactKind.Spec,
            """
            # a spec with no frontmatter

            ## Acceptance criteria

            1. something observable.
            """);

        Assert.IsTrue(check.Violations.Any(v => v.Where == "the document"), Describe(check));
        Assert.IsTrue(check.Violations.Any(v => v.Where == "## Out of scope"), Describe(check));
        Assert.IsTrue(check.Violations.Any(v => v.Where == "## Open"), Describe(check));
    }

    [TestMethod]
    public void The_envelope_is_the_same_two_keys_on_every_artifact()
    {
        // D-07's test for where a field goes, asserted. If a key were universal in name and not in
        // meaning, or carried on some artifacts and not others, it would not belong here.
        CollectionAssert.AreEqual(new[] { "framework_version", "cycle" }, ArtifactSchema.EnvelopeKeys.ToArray());

        foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
        {
            Assert.IsFalse(
                ArtifactSchema.BodyKeysOf(kind).Intersect(ArtifactSchema.EnvelopeKeys, StringComparer.Ordinal).Any(),
                $"{Cycle.NameOf(kind)} claims an envelope key as a body key.");
        }
    }

    [TestMethod]
    public void No_artifact_lists_the_open_region_among_its_body_sections()
    {
        // The open region is the one part the constraints do not bind, so a kind listing it as body would
        // make it judged on that artifact and not on the others.
        foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
        {
            Assert.IsFalse(
                ArtifactSchema.BodySectionsOf(kind).Contains(ArtifactSchema.OpenSection, StringComparer.Ordinal),
                $"{Cycle.NameOf(kind)} lists the open region as body.");
        }
    }

    [TestMethod]
    [DynamicData(nameof(ProtectedNames))]
    public void A_plan_that_declares_a_file_only_a_person_writes_is_refused(string name)
    {
        // Such a plan cannot be carried out as written: the build is denied the write at the tool call, so
        // the file never changes and the evidence comparison reports it as undelivered -- which means "the
        // blast radius was wider than the change" and sends the author after an edit they forgot rather
        // than at a declaration that was never executable.
        string plan = WellFormed(ArtifactKind.Plan)
            .Replace("## Files", "## Files\n\n| `.sdlc/" + name + "` | what changes in it |", StringComparison.Ordinal);

        ArtifactValidation check = Validate(ArtifactKind.Plan, plan);

        Assert.IsFalse(check.IsValid, Describe(check));
        Assert.IsTrue(
            check.Violations.Any(violation => violation.Where == "Files"),
            "The violation names the section, because that is where the fix goes. " + Describe(check));
        StringAssert.Contains(Describe(check), name);
    }

    [TestMethod]
    public void A_plan_may_declare_a_file_whose_name_matches_a_protected_one_outside_the_framework_directory()
    {
        // The schema refuses exactly what the hook denies, by asking the hook -- and the hook requires an
        // .sdlc segment. A techstack.md at the repository root is an ordinary file to both, and two
        // different answers to "is this protected" would eventually disagree.
        string plan = WellFormed(ArtifactKind.Plan)
            .Replace("## Files", "## Files\n\n| `docs/techstack.md` | what changes in it |", StringComparison.Ordinal);

        ArtifactValidation check = Validate(ArtifactKind.Plan, plan);

        Assert.IsTrue(check.IsValid, Describe(check));
    }

    [TestMethod]
    public void An_intent_naming_a_protected_file_is_not_judged_on_it()
    {
        // The rule is about a plan's Files section, which is a declaration of what a build will touch. An
        // intent has no such section and no such promise, so prose mentioning a file is just prose.
        string intent = Intent()
            .Replace("## Open", "## Open\n\nWhether `.sdlc/techstack.md` should declare bUnit.", StringComparison.Ordinal);

        ArtifactValidation check = Validate(ArtifactKind.Intent, intent);

        Assert.IsTrue(check.IsValid, Describe(check));
    }

    // Driven off the list rather than written out, so a file added to it is refused in a plan without
    // anybody remembering this test -- the written-out rows had already missed decisions.md.
    public static IEnumerable<object[]> ProtectedNames =>
        ProtectedPaths.Names.Select(name => new object[] { name });

    private static ArtifactValidation Validate(ArtifactKind kind, string markdown)
    {
        Assert.IsTrue(CycleId.TryParse(Id, out CycleId? cycle));
        return ArtifactSchema.Validate(kind, markdown, cycle.Value);
    }

    private static string Describe(ArtifactValidation check) =>
        check.Violations.Count == 0
            ? "nothing wrong"
            : string.Join("; ", check.Violations.Select(v => v.Where + " " + v.Problem));

    private static string Intent() => WellFormed(ArtifactKind.Intent);

    /// <summary>An artifact of the given kind with every part the schema asks for.</summary>
    private static string WellFormed(ArtifactKind kind)
    {
        List<string> lines =
        [
            "---",
            "framework_version: 1.0.0",
            "cycle: " + Id,
        ];

        foreach (string key in ArtifactSchema.BodyKeysOf(kind))
        {
            lines.Add(key + ": " + key switch
            {
                "type" => "bug fix",
                "tier" => "local",
                "outcome" => "pass",
                _ => throw new InvalidOperationException("unknown body key " + key),
            });
        }

        lines.AddRange("---", string.Empty, "# a " + Cycle.NameOf(kind), string.Empty);

        foreach (string section in ArtifactSchema.BodySectionsOf(kind))
        {
            lines.AddRange("## " + section, string.Empty, "Something said here.", string.Empty);
        }

        lines.Add("## " + ArtifactSchema.OpenSection);

        return string.Join('\n', lines);
    }

    [TestMethod]
    public void A_section_appearing_twice_is_a_violation()
    {
        // Two "## Acceptance criteria" sections validated cleanly. The bodies merge in
        // ArtifactSections.Read and NumberedItems then lets the first mention of each number win, so
        // criterion 1 under the second heading was silently discarded and verify-chain's proof matching
        // never saw it. A criterion nothing proves and nothing reports is what §5.4's second mechanical
        // check exists to catch. F-14.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSpec(criteria: 2);

        string path = workspace.Cycle.PathOf(ArtifactKind.Spec);
        string spec = File.ReadAllText(path);

        File.WriteAllText(
            path,
            spec.Replace(
                "## " + ArtifactSchema.OpenSection,
                "## Acceptance criteria\n\n1. A criterion nothing will ever prove.\n\n## "
                    + ArtifactSchema.OpenSection,
                StringComparison.Ordinal));

        ArtifactValidation validation = ArtifactSchema.Validate(
            ArtifactKind.Spec,
            File.ReadAllText(path),
            workspace.Cycle.Id);

        Assert.IsTrue(
            validation.Violations.Any(violation =>
                violation.Where.Contains("Acceptance criteria", StringComparison.Ordinal)
                && violation.Problem.Contains("more than once", StringComparison.Ordinal)),
            string.Join("; ", validation.Violations.Select(v => v.Where + " " + v.Problem)));
    }
}
