using System.Globalization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Artifacts;

/// <summary>One thing wrong with an artifact's shape.</summary>
/// <param name="Where">Which part of the file: a key, a section, or the document.</param>
/// <param name="Problem">What is wrong with it, and what is expected instead.</param>
public sealed record SchemaViolation(string Where, string Problem);

/// <summary>What the schema made of one artifact.</summary>
/// <param name="Kind">Which artifact was checked.</param>
/// <param name="Violations">Everything wrong with its shape, in reading order.</param>
public sealed record ArtifactValidation(ArtifactKind Kind, IReadOnlyList<SchemaViolation> Violations)
{
    /// <summary>Whether the artifact's shape is sound.</summary>
    public bool IsValid => Violations.Count == 0;
}

/// <summary>
/// The shape every artifact has to have: an envelope, a body, and nothing else.
/// </summary>
/// <remarks>
/// <para><strong>The envelope is an allowlist of two keys plus one section.</strong>
/// <c>framework_version</c> and <c>cycle</c> in the frontmatter, and a <c>## Open</c> section.
/// Everything else in the file -- any other key, any other section -- is body. Stated as an allowlist
/// rather than a list of body parts because the point is that a claim cannot hide in the envelope: the
/// constraints bind the body, so a field that escaped into the envelope would escape them.
/// </para>
/// <para><strong><c>## Open</c> is required even when empty.</strong> Without the section there is no
/// way to tell "nothing is open" from "the author forgot", and those need different responses.
/// </para>
/// <para><strong>An unexpected section or key is a violation.</strong> A section exists because
/// something reads it -- a constraint, a mechanical check, a downstream skill, or a person at the gate
/// -- and decoration costs attention at every interrogation. A solution wanting to carry a ticket
/// number will get a declared extension, where the solution says which extra keys it has and this
/// validates against that declaration. Until then, extra is wrong rather than tolerated, because a
/// field nothing declared is a field nothing reads.
/// </para>
/// </remarks>
public static class ArtifactSchema
{
    /// <summary>The frontmatter keys that belong to the envelope, on every artifact alike.</summary>
    public static IReadOnlyList<string> EnvelopeKeys { get; } = ["framework_version", "cycle"];

    /// <summary>The section that holds what an artifact deliberately does not assert.</summary>
    public const string OpenSection = "Open";

    /// <summary>
    /// The body sections one kind of artifact carries, in the order they are written.
    /// </summary>
    /// <param name="kind">Which artifact.</param>
    /// <returns>Its sections, without the open region.</returns>
    public static IReadOnlyList<string> BodySectionsOf(ArtifactKind kind) => kind switch
    {
        // Problem, because CON_01 forbids an intent that names a solution and no observable problem.
        // Wanted outcomes, because it is CON_16's left-hand side. Fixed decisions, because CON_01 routes
        // a decision already taken here and steer writes into it. Must not change, because CON_16 names
        // it and it is the outcome most often dropped.
        ArtifactKind.Intent =>
            ["Problem", "Wanted outcomes", "Fixed decisions", "Must not change"],

        // Acceptance criteria, read by five constraints and by verify. Out of scope, read by CON_05 and
        // one of the three destinations an intent's open item can be disposed of into.
        ArtifactKind.Spec => ["Acceptance criteria", "Out of scope"],

        // Shape and departures holds the decision, judged by CON_06 and CON_15. Files holds the
        // estimate, which nothing judges. Proof is CON_06's subject and verify's criterion table. Risk
        // is read by the person at the gate, which is a consumer like any other.
        ArtifactKind.Plan => ["Shape and departures", "Files", "Proof", "Risk"],

        // One section. Verify's findings are data like every other artifact's, in findings/verify.json,
        // so what belongs in the document is the criterion-by-criterion adjudication.
        ArtifactKind.Verify => ["Criteria"],

        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// The frontmatter keys one kind of artifact carries in its body.
    /// </summary>
    /// <remarks>
    /// These are claims rather than bookkeeping, which is why they are body rather than envelope. A
    /// change's type decides how its spec derives criteria; a plan's tier is a claim about blast radius
    /// chosen by reversibility. Both are arguable, and anything arguable has to be somewhere the
    /// constraints reach.
    /// </remarks>
    /// <param name="kind">Which artifact.</param>
    /// <returns>Its body keys, which may be none.</returns>
    public static IReadOnlyList<string> BodyKeysOf(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Intent => ["type"],
        ArtifactKind.Plan => ["tier"],
        ArtifactKind.Verify => ["outcome"],
        _ => [],
    };

    /// <summary>
    /// The frontmatter keys one kind of artifact may carry and need not.
    /// </summary>
    /// <remarks>
    /// An intent may record the lead it began from (D-89) — so the cycle's pull request can link and close the
    /// item — the release branch a fix is for (D-88), and the perspectives beyond the three always on that the
    /// change needs (D-90), named in the interview so the approval's hash covers them. Most intents have neither, so neither is required;
    /// both are checked when present, because a malformed one would be a link to nothing.
    /// </remarks>
    /// <param name="kind">Which artifact.</param>
    /// <returns>Its optional keys, which may be none.</returns>
    public static IReadOnlyList<string> OptionalKeysOf(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Intent => ["lead", "release", "perspectives"],
        _ => [],
    };

    /// <summary>The values a change type may take.</summary>
    public static IReadOnlyList<string> ChangeTypes { get; } =
        ["new solution", "feature addition", "feature change", "bug fix"];

    /// <summary>The values a plan's blast radius may take, from most to least reversible.</summary>
    public static IReadOnlyList<string> Tiers { get; } = ["local", "contract", "data"];

    /// <summary>The values a verify outcome may take.</summary>
    public static IReadOnlyList<string> Outcomes { get; } = ["pass", "blocked", "escalated"];

    /// <summary>
    /// Checks one artifact's shape.
    /// </summary>
    /// <remarks>
    /// Reports everything wrong rather than the first thing. A person fixing an artifact wants the list,
    /// not a sequence of runs -- and an author told only about a missing fence will not learn that three
    /// sections are missing too.
    /// </remarks>
    /// <param name="kind">Which artifact this is meant to be.</param>
    /// <param name="markdown">The file's contents.</param>
    /// <param name="cycle">The cycle the file's path puts it in.</param>
    /// <returns>What is wrong with its shape, which may be nothing.</returns>
    public static ArtifactValidation Validate(ArtifactKind kind, string markdown, CycleId cycle)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<SchemaViolation> violations = [];

        Result<FrontmatterDocument> read = Frontmatter.Read(markdown, Cycle.FileNameOf(kind));

        if (read.IsFailed)
        {
            // Phrased here rather than passed through, because a Where and a Problem are read as one
            // sentence and the splitter's message is written to stand alone.
            violations.Add(new SchemaViolation(
                "the document",
                read.Errors[0].Message.Contains("never closes", StringComparison.Ordinal)
                    ? "opens its frontmatter and never closes it. Add a '---' fence after it."
                    : "has no frontmatter. It must open with a '---' fence."));

            // Without frontmatter there is nothing to check the keys against, but the sections are still
            // readable and still worth reporting in the same pass.
            CheckSections(kind, markdown, violations);
            return new ArtifactValidation(kind, violations);
        }

        CheckKeys(kind, read.Value.Yaml, cycle, violations);
        CheckSections(kind, markdown, violations);
        CheckDeclarations(kind, markdown, violations);
        CheckProofs(kind, markdown, violations);

        return new ArtifactValidation(kind, violations);
    }

    /// <summary>
    /// Refuses a plan whose proof entry opens with no test name.
    /// </summary>
    /// <remarks>
    /// <c>verify-chain</c> reads a proof's tests from the backticked names its entry opens with (see
    /// <see cref="Evidence.DeclaredFiles.TestsIn"/>), and an entry that opens with prose commits to nothing
    /// it can check. Here as well as there, because the chain runs after the build: by then the plan is
    /// approved, and correcting one sentence costs a reopen and a second approval. Here it costs a line.
    /// </remarks>
    /// <param name="kind">Which artifact this is.</param>
    /// <param name="markdown">The file's contents.</param>
    /// <param name="violations">The list to add to.</param>
    private static void CheckProofs(
        ArtifactKind kind,
        string markdown,
        List<SchemaViolation> violations)
    {
        if (kind != ArtifactKind.Plan)
        {
            return;
        }

        foreach ((int number, string entry) in ArtifactSections
            .NumberedItems(markdown, "Proof")
            .OrderBy(item => item.Key))
        {
            if (Evidence.DeclaredFiles.TestsIn(entry).Count > 0)
            {
                continue;
            }

            violations.Add(new SchemaViolation(
                "Proof",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"entry {number} does not open with a test name in backticks. verify-chain reads the names an entry opens with, and only those, so this one commits to nothing it can check.")));
        }
    }

    /// <summary>
    /// Refuses a plan that declares a file no agent may write.
    /// </summary>
    /// <remarks>
    /// <para>Such a plan cannot be carried out as written: the build is denied the write at the tool call,
    /// so the file never changes and the evidence comparison reports it as <c>UNDELIVERED</c> -- which
    /// means "the plan's blast radius was wider than the change" and sends the author looking for an edit
    /// they forgot rather than at a declaration that was never executable. Same symptom, wrong diagnosis.
    /// </para>
    /// <para>Here rather than in the comparison, and here rather than as a constraint. It is mechanically
    /// decidable, which by §2.3 puts it in a schema rather than a prompt; and the schema runs before the
    /// build, so the mistake is caught at the one moment it is still free to fix.</para>
    /// <para><strong>It refuses exactly what the hook denies, by asking the hook.</strong> Two lists of
    /// protected paths would eventually disagree, and the one that mattered would be whichever the author
    /// did not read. So a path only counts here if <see cref="Hooks.ProtectedPaths.IsProtected"/> says a
    /// write to it would be refused -- which also means a <c>techstack.md</c> outside <c>.sdlc</c> is an
    /// ordinary file to both of them.</para>
    /// </remarks>
    /// <param name="kind">Which artifact this is.</param>
    /// <param name="markdown">The file's contents.</param>
    /// <param name="violations">The list to add to.</param>
    private static void CheckDeclarations(
        ArtifactKind kind,
        string markdown,
        List<SchemaViolation> violations)
    {
        if (kind != ArtifactKind.Plan)
        {
            return;
        }

        foreach (string declared in Evidence.DeclaredFiles.Read(markdown))
        {
            if (!Hooks.ProtectedPaths.IsProtected(declared, out Hooks.ProtectedFile? file))
            {
                continue;
            }

            violations.Add(new SchemaViolation(
                "Files",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"declares '{declared}', which only a person writes. {file!.Reason}")));
        }
    }

    private static void CheckKeys(
        ArtifactKind kind,
        string yaml,
        CycleId cycle,
        List<SchemaViolation> violations)
    {
        // Through Frontmatter.Keys, which is the one reader -- the schema accepting a quoted scalar
        // while the chain check compared raw text after `outcome:` is F-06, and the fix is that there
        // is nowhere left for a second reading to live.
        Result<IReadOnlyDictionary<string, string?>> read = Frontmatter.Keys(yaml);

        if (read.IsFailed)
        {
            violations.Add(new SchemaViolation(
                "the frontmatter",
                string.Join(" ", read.Errors.Select(error => error.Message))));
            return;
        }

        IReadOnlyDictionary<string, string?> keys = read.Value;

        foreach (string required in EnvelopeKeys.Concat(BodyKeysOf(kind)))
        {
            if (!keys.TryGetValue(required, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                violations.Add(new SchemaViolation(required, "is missing, and every artifact carries it."));
            }
        }

        // The cycle key duplicates what the path already says, and this is the whole reason it is there:
        // a file copied out of one cycle into another keeps the id it was written under, and nothing but
        // this comparison would notice.
        if (keys.TryGetValue("cycle", out string? declared)
            && !string.IsNullOrWhiteSpace(declared)
            && !string.Equals(declared.Trim(), cycle.ToString(), StringComparison.Ordinal))
        {
            violations.Add(new SchemaViolation(
                "cycle",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"says '{declared.Trim()}' and the file sits in {cycle}. One of the two is wrong.")));
        }

        CheckValue(kind, keys, "type", ChangeTypes, violations);
        CheckValue(kind, keys, "tier", Tiers, violations);
        CheckValue(kind, keys, "outcome", Outcomes, violations);

        if (OptionalKeysOf(kind).Contains("lead", StringComparer.Ordinal)
            && keys.TryGetValue("lead", out string? lead)
            && !string.IsNullOrWhiteSpace(lead)
            && !Leads.LeadReference.TryParse(lead, out _, out string? problem))
        {
            violations.Add(new SchemaViolation("lead", problem));
        }

        foreach (string key in keys.Keys)
        {
            if (!EnvelopeKeys.Contains(key, StringComparer.Ordinal)
                && !BodyKeysOf(kind).Contains(key, StringComparer.Ordinal)
                && !OptionalKeysOf(kind).Contains(key, StringComparer.Ordinal))
            {
                violations.Add(new SchemaViolation(
                    key,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"is not a key {Cycle.DescribeKind(kind)} carries. A field nothing declared is a field nothing reads.")));
            }
        }
    }

    private static void CheckValue(
        ArtifactKind kind,
        IReadOnlyDictionary<string, string?> keys,
        string key,
        IReadOnlyList<string> allowed,
        List<SchemaViolation> violations)
    {
        if (!BodyKeysOf(kind).Contains(key, StringComparer.Ordinal)
            || !keys.TryGetValue(key, out string? value)
            || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            violations.Add(new SchemaViolation(
                key,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"is '{value.Trim()}'. Expected one of: {string.Join(", ", allowed)}.")));
        }
    }

    private static void CheckSections(
        ArtifactKind kind,
        string markdown,
        List<SchemaViolation> violations)
    {
        IReadOnlyList<string> found = ArtifactOutlineReader.Read(markdown).Sections;
        IReadOnlyList<string> expected = BodySectionsOf(kind);

        foreach (string section in expected)
        {
            if (!found.Contains(section, StringComparer.Ordinal))
            {
                violations.Add(new SchemaViolation(
                    "## " + section,
                    "is missing."));
            }
        }

        if (!found.Contains(OpenSection, StringComparer.Ordinal))
        {
            violations.Add(new SchemaViolation(
                "## " + OpenSection,
                "is missing. It is required even when empty, because an absent section cannot be told "
                + "apart from nothing being open."));
        }

        // A heading appearing twice. ArtifactSections.Read accumulates the two bodies rather than
        // replacing, and NumberedItems then lets the first mention of each number win -- so criterion 1
        // in a second "## Acceptance criteria" is silently discarded and verify-chain's proof matching
        // never sees it. A criterion nothing proves and nothing reports is what §5.4's second
        // mechanical check exists to catch. F-14.
        foreach (string duplicated in found
            .GroupBy(section => section, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            violations.Add(new SchemaViolation(
                "## " + duplicated,
                "appears more than once. The two bodies merge, and the first mention of a numbered item "
                + "wins -- so items under the second heading are silently dropped rather than proved."));
        }

        foreach (string section in found.Distinct(StringComparer.Ordinal))
        {
            if (!expected.Contains(section, StringComparer.Ordinal)
                && !string.Equals(section, OpenSection, StringComparison.Ordinal))
            {
                violations.Add(new SchemaViolation(
                    "## " + section,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"is not a section {Cycle.DescribeKind(kind)} carries. Expected: {string.Join(", ", expected)}, {OpenSection}.")));
            }
        }
    }
}
