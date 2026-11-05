using System.Globalization;

namespace Sdlc.Core.Cycles;

/// <summary>Which artifact in a cycle is meant.</summary>
public enum ArtifactKind
{
    /// <summary>Why the change exists. The problem, not a solution.</summary>
    Intent,

    /// <summary>What done means, as criteria that can be checked.</summary>
    Spec,

    /// <summary>How: the files it will touch, and the proof committed to per criterion.</summary>
    Plan,

    /// <summary>Whether the change did the spec justice.</summary>
    Verify,
}

/// <summary>
/// Where a cycle's files live.
/// </summary>
/// <remarks>
/// A cycle is one governed change: an id, a directory, and a branch. This resolves paths and does not
/// create anything — a cycle's directory is made by the skill that writes the first artifact into it,
/// because an empty directory needs a placeholder to survive git and nothing ever removes the
/// placeholder once real files arrive.
/// </remarks>
/// <param name="Id">The cycle's id.</param>
/// <param name="SolutionRoot">The root of the solution the cycle belongs to.</param>
public sealed record Cycle(CycleId Id, string SolutionRoot)
{
    /// <summary>
    /// The framework version every artifact and ledger declares.
    /// </summary>
    /// <remarks>
    /// One constant, because the envelope requires the field on every artifact and a value written in
    /// three places is three values. It says which format the file was written in, so a later reader
    /// can tell a file it cannot parse from one it should not try to.
    /// </remarks>
    public const string FrameworkVersion = "1.0.0";

    /// <summary>The framework's directory inside a solution.</summary>
    public const string SdlcDirectoryName = ".sdlc";

    /// <summary>The directory holding every cycle, inside <see cref="SdlcDirectoryName"/>.</summary>
    public const string CyclesDirectoryName = "cycles";

    /// <summary>
    /// The file saying what this solution is and where its code came from.
    /// </summary>
    /// <remarks>
    /// Not a cycle's file, and here because this is where the framework's directory layout is named.
    /// It carries three keys and no more: the origin that records where the code came from, the
    /// standard that says which conventions new work follows, and the platform that says whether a
    /// pull-request check governs a merge at all.
    /// </remarks>
    public const string SolutionFileName = "solution.md";

    /// <summary>
    /// The file declaring what this solution is built from.
    /// </summary>
    /// <remarks>
    /// Not a cycle's file either, and named here for the same reason as the one above. It is one of the
    /// two rule sources a plan is read against, which is why it is a person's to write: see
    /// <see cref="Solutions.WriteTechStackDraft"/>.
    /// </remarks>
    public const string TechStackFileName = "techstack.md";

    /// <summary>
    /// The three documents the planning approval covers, in the order they are written and read.
    /// </summary>
    /// <remarks>
    /// Approved as one act, so recorded as one act: the approval entry carries a hash for each of
    /// these three and nothing else.
    /// </remarks>
    public static IReadOnlyList<ArtifactKind> PlanningDocuments { get; } =
        [ArtifactKind.Intent, ArtifactKind.Spec, ArtifactKind.Plan];

    /// <summary>
    /// The artifact an interrogation of this one judges it against, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Derived from the chain order rather than declared, because the order is fixed: the spec is
    /// derived from the intent and the plan from the spec. An intent has no upstream, which is why this
    /// is nullable rather than a lookup that throws.
    /// </remarks>
    /// <param name="kind">The artifact being interrogated.</param>
    /// <returns>The artifact above it in the chain, or null for the first.</returns>
    public static ArtifactKind? UpstreamOf(ArtifactKind kind)
    {
        int position = PlanningDocuments.ToList().IndexOf(kind);
        return position > 0 ? PlanningDocuments[position - 1] : null;
    }

    /// <summary>The cycle's own directory.</summary>
    /// <remarks>
    /// Built from segments rather than from one slash-separated string, so the separators are the
    /// platform's throughout. A path shown to a person with mixed separators - a backslash before the
    /// framework directory and a forward slash after it - reads as a bug whether or not it resolves.
    /// </remarks>
    public string Directory =>
        Path.Combine(SolutionRoot, SdlcDirectoryName, CyclesDirectoryName, Id.ToString());

    /// <summary>The append-only ledger of this cycle's gate decisions.</summary>
    public string ApprovalPath => Path.Combine(Directory, "approval.md");

    /// <summary>
    /// Where a drafted gate decision waits for a person to rename it onto <see cref="ApprovalPath"/>.
    /// </summary>
    /// <remarks>
    /// <para><strong>A different file name, deliberately.</strong> The hook's rules match on the ledger's
    /// name, so a draft that shared it could not be written at all — and a draft the binary cannot write
    /// is the clipboard flow again. This name is not protected and records nothing: a draft nobody
    /// renamed is a file nobody renamed.</para>
    /// <para><strong>The decision is in the name</strong> because the rename is the act. A person moving
    /// <c>approval.denied.draft.md</c> onto the ledger types the word <c>denied</c> on the way, where a
    /// single <c>approval.draft.md</c> would have let an approval and a denial look identical at the
    /// moment of recording them.</para>
    /// </remarks>
    /// <param name="decision">The decision's name in the ledger, as <c>ApprovalLedger.NameOf</c> spells it.</param>
    /// <returns>The path of the draft for that decision.</returns>
    public string ApprovalDraftPath(string decision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decision);

        return Path.Combine(Directory, "approval." + decision + ".draft.md");
    }

    /// <summary>Why the cycle stopped, when it did. Its presence is what makes abandonment terminal.</summary>
    public string AbandonedPath => Path.Combine(Directory, "abandoned.md");

    /// <summary>Where interrogation rounds are written.</summary>
    public string FindingsDirectory => Path.Combine(Directory, "findings");

    /// <summary>Where the build records what happened.</summary>
    public string EvidenceDirectory => Path.Combine(Directory, "evidence");

    /// <summary>The file name an artifact is written to, without a directory.</summary>
    /// <param name="kind">The artifact.</param>
    /// <returns>The file name, such as <c>intent.md</c>.</returns>
    public static string FileNameOf(ArtifactKind kind) => NameOf(kind) + ".md";

    /// <summary>
    /// An artifact's name with the right article in front of it.
    /// </summary>
    /// <remarks>
    /// Because "a intent" appears in a message a person reads, and a tool that cannot spell its own
    /// vocabulary reads as one that has not been looked at.
    /// </remarks>
    /// <param name="kind">Which artifact.</param>
    /// <returns>The name, articled.</returns>
    public static string DescribeKind(ArtifactKind kind) =>
        (kind == ArtifactKind.Intent ? "an " : "a ") + NameOf(kind);

    /// <summary>The artifact's name as it is written and spoken.</summary>
    /// <param name="kind">The artifact.</param>
    /// <returns>The name, such as <c>intent</c>.</returns>
    public static string NameOf(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Intent => "intent",
        ArtifactKind.Spec => "spec",
        ArtifactKind.Plan => "plan",
        ArtifactKind.Verify => "verify",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown artifact."),
    };

    /// <summary>Reads an artifact name back to its kind.</summary>
    /// <param name="name">The name, in any casing.</param>
    /// <param name="kind">The artifact, when this returns true.</param>
    /// <returns>True when the name is one of the artifacts.</returns>
    public static bool TryParseKind(string? name, out ArtifactKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (ArtifactKind candidate in Enum.GetValues<ArtifactKind>())
        {
            if (string.Equals(NameOf(candidate), name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>The path an artifact is written to.</summary>
    /// <param name="kind">The artifact.</param>
    /// <returns>The full path.</returns>
    public string PathOf(ArtifactKind kind) => Path.Combine(Directory, FileNameOf(kind));

    /// <summary>
    /// The path of an artifact's findings ledger, every round of its interrogation in one file (D-107).
    /// </summary>
    /// <param name="kind">The artifact that was interrogated.</param>
    /// <returns>The full path.</returns>
    public string FindingsLedgerPath(ArtifactKind kind) =>
        Path.Combine(FindingsDirectory, NameOf(kind) + ".json");

    /// <summary>
    /// The path of the cycle's suggestions ledger, every suggestion its readers raised against any of its
    /// documents, each concern once (D-115).
    /// </summary>
    public string SuggestionsLedgerPath => Path.Combine(FindingsDirectory, Findings.SuggestionLedger.FileName);

    /// <summary>
    /// The path a round file was written to before D-107, one per round.
    /// </summary>
    /// <remarks>
    /// <strong>Nothing in the binary writes one any more.</strong> Cycles opened before the ledger still
    /// hold them, <see cref="Findings.FindingsReader.ReadLedger(Cycle, ArtifactKind, CancellationToken)"/>
    /// reads them by glob, and the next round converts them. So this is where the old naming convention is
    /// stated, and the suite writes its legacy fixtures through it: if the convention moved and the glob
    /// did not, the tests would keep passing against files the reader no longer finds.
    /// </remarks>
    /// <param name="kind">The artifact that was interrogated.</param>
    /// <param name="round">The round number, counting from one.</param>
    /// <returns>The full path.</returns>
    public string FindingsPath(ArtifactKind kind, int round)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);

        return Path.Combine(
            FindingsDirectory,
            string.Create(CultureInfo.InvariantCulture, $"{NameOf(kind)}.r{round}.json"));
    }

    /// <summary>
    /// The release branch the intent says this cycle's fix is for, or null when it names none (D-88).
    /// </summary>
    /// <remarks>
    /// A release-targeted cycle is built by hand from that branch and back into it, and carried forward into
    /// the integration branch by a pull request of its own, so the factory passes it over and
    /// <c>sdlc branch</c> starts its build from the release.
    /// </remarks>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The branch, or null.</returns>
    public async Task<string?> ReleaseTarget(CancellationToken cancellationToken)
    {
        string intent = PathOf(ArtifactKind.Intent);

        if (!File.Exists(intent))
        {
            return null;
        }

        FluentResults.Result<Artifacts.FrontmatterDocument> read = Artifacts.Frontmatter.Read(
            await File.ReadAllTextAsync(intent, cancellationToken).ConfigureAwait(false),
            intent);

        return read.IsSuccess && Artifacts.Frontmatter.Key(read.Value.Yaml, "release") is { } release
            && !string.IsNullOrWhiteSpace(release)
                ? release.Trim()
                : null;
    }

    /// <summary>Whether the cycle's directory exists yet.</summary>
    public bool Exists => System.IO.Directory.Exists(Directory);

    /// <summary>
    /// Whether the cycle has been abandoned.
    /// </summary>
    /// <remarks>
    /// Abandonment is terminal: a cycle with this file never builds, even if it also holds an
    /// approval. Changing your mind means a new cycle, because resurrection would need rules for
    /// which of two hand-written files wins and the ordering of those is not something to rest a gate
    /// on.
    /// </remarks>
    public bool IsAbandoned => File.Exists(AbandonedPath);
}
