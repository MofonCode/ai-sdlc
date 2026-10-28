using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace Sdlc.Core.Findings;

/// <summary>One move of one finding: raised, answered, withdrawn, or raised again (D-107).</summary>
/// <param name="Round">The round that made it.</param>
/// <param name="Disposition">Where the finding stood after it.</param>
/// <param name="By">Who recorded it, when the round said.</param>
/// <param name="At">When, when the round said.</param>
public sealed record Transition(
    int Round,
    Disposition Disposition,
    string? By = null,
    DateTimeOffset? At = null);

/// <summary>One finding, stored once, with every move it made (D-107).</summary>
/// <remarks>
/// Everything but <paramref name="Transitions"/> is fixed when the finding is first raised. A finding's
/// disposition as of a round is its last transition at or before it, and the first transition is the
/// raise.
/// </remarks>
/// <param name="Id">The id it keeps for the life of the cycle.</param>
/// <param name="Severity">Blocking or a suggestion.</param>
/// <param name="Cites">The one rule it rests on.</param>
/// <param name="Statement">What is wrong.</param>
/// <param name="Locus">Where.</param>
/// <param name="Remedy">What would fix it.</param>
/// <param name="Perspective">The perspective whose reader raised it, if one did.</param>
/// <param name="Transitions">Every move, in round order.</param>
/// <param name="Gist">The one sentence a person reads first, when the reader gave one.</param>
public sealed record LedgerFinding(
    string Id,
    Severity Severity,
    Citation? Cites,
    string Statement,
    string? Locus,
    string? Remedy,
    string? Perspective,
    IReadOnlyList<Transition> Transitions,
    string? Gist = null);

/// <summary>One round's entry: what it judged, against what, and its place in the chain (D-107).</summary>
/// <param name="Round">Which round, counting from one.</param>
/// <param name="FrameworkVersion">The version that wrote it.</param>
/// <param name="InterrogatedAt">When it ran.</param>
/// <param name="UpstreamSha256">The artifact above, as the round saw it (D-21).</param>
/// <param name="SubjectSha256">The document judged (D-101).</param>
/// <param name="EvidenceSha256">On a verify round, the evidence it read.</param>
/// <param name="SectionSha256">A hash per second-level section of the document judged.</param>
/// <param name="Conflicts">The rules this round's reader found cannot both be satisfied.</param>
/// <param name="Note">What a steer said moved above the document.</param>
/// <param name="PreviousSha256">The entry before this one's hash. Null on round one.</param>
/// <param name="EntrySha256">This entry's hash, over itself, what it raised, what it moved, and the previous hash.</param>
public sealed record LedgerRound(
    int Round,
    [property: JsonPropertyName("framework_version")] string? FrameworkVersion,
    [property: JsonPropertyName("interrogated_at")] DateTimeOffset? InterrogatedAt,
    [property: JsonPropertyName("upstream_sha256")] string? UpstreamSha256 = null,
    [property: JsonPropertyName("subject_sha256")] string? SubjectSha256 = null,
    [property: JsonPropertyName("evidence_sha256")] string? EvidenceSha256 = null,
    [property: JsonPropertyName("section_sha256")] IReadOnlyDictionary<string, string>? SectionSha256 = null,
    IReadOnlyList<Conflict>? Conflicts = null,
    string? Note = null,
    [property: JsonPropertyName("previous_sha256")] string? PreviousSha256 = null,
    [property: JsonPropertyName("entry_sha256")] string? EntrySha256 = null);

/// <summary>
/// Every round of one artifact's interrogation, in one file: each finding once, each round an entry,
/// the entries chained by hash (D-107).
/// </summary>
/// <remarks>
/// <para><strong>It replaced a file per round, and the reason was restatement.</strong> One cycle's session
/// copied every finding into every round: the plan's twenty-sixth round file held 798 findings, 764 of them
/// copies, and the cycle's rounds came to 17 MB. The fold never needed the copies -- the latest mention
/// already won -- so a ledger records what a round raised and what it moved, and a restatement adds
/// nothing because there is nowhere to put one.</para>
/// <para><strong>The consumers still read rounds.</strong> <see cref="Views"/> gives one
/// <see cref="FindingsRound"/> per entry, holding exactly the findings that round raised or moved, so the
/// fold, the gate, the panel and the loop guards read one shape whichever file it came from.</para>
/// <para><strong>It holds no count.</strong> The number of rounds is the length of <see cref="Rounds"/>,
/// and what was open at a round is derived from the transitions. A stored copy of either is the
/// count-beside-the-thing D-49 rejects.</para>
/// </remarks>
/// <param name="Cycle">The cycle's id.</param>
/// <param name="Artifact">The artifact's file name.</param>
/// <param name="Findings">Each finding once, in the order first raised.</param>
/// <param name="Rounds">Each round's entry, in round order.</param>
public sealed record FindingsLedger(
    string? Cycle,
    string? Artifact,
    IReadOnlyList<LedgerFinding> Findings,
    IReadOnlyList<LedgerRound> Rounds)
{
    private static readonly JsonSerializerOptions _canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>An artifact with no rounds yet.</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <param name="artifact">The artifact's file name.</param>
    /// <returns>The empty ledger.</returns>
    public static FindingsLedger Empty(string? cycle, string? artifact) => new(cycle, artifact, [], []);

    /// <summary>
    /// Builds a ledger from round files written before D-107, which restated findings freely.
    /// </summary>
    /// <remarks>
    /// Lenient where <see cref="Append"/> is strict, because those rounds were written under rules that
    /// allowed what it refuses: a finding restated in other words keeps the latest wording, which is what
    /// the fold showed while they were the record. The result is deterministic, so a ledger built in
    /// memory here and one written from the same rounds by the next <c>sdlc round</c> carry the same
    /// hashes, and an approval anchored on the first still holds against the second.
    /// </remarks>
    /// <param name="rounds">The rounds, in any order.</param>
    /// <returns>The ledger, or a failure where two rounds share a number.</returns>
    public static Result<FindingsLedger> FromRounds(IReadOnlyList<FindingsRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        FindingsRound? first = rounds.Count == 0 ? null : rounds[0];
        FindingsLedger ledger = Empty(first?.Cycle, first?.Artifact);

        foreach (FindingsRound round in rounds.OrderBy(round => round.Round))
        {
            Result<FindingsLedger> appended = ledger.Merge(round, strict: false);

            if (appended.IsFailed)
            {
                return appended;
            }

            ledger = appended.Value;
        }

        return Result.Ok(ledger.Rechained());
    }

    /// <summary>
    /// Adds the next round: new ids become findings, moved ones transitions, and restatements nothing.
    /// </summary>
    /// <param name="round">The round, with every field the binary stamps already filled in.</param>
    /// <returns>The ledger with the round chained on, or a failure naming what was refused.</returns>
    public Result<FindingsLedger> Append(FindingsRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        int next = Rounds.Count == 0 ? 1 : Rounds[^1].Round + 1;

        if (round.Round != next)
        {
            return Result.Fail<FindingsLedger>(string.Create(
                CultureInfo.InvariantCulture,
                $"{Artifact}: the next round is {next}, and this one says {round.Round}."));
        }

        Result<FindingsLedger> merged = Merge(round, strict: true);

        if (merged.IsFailed)
        {
            return merged;
        }

        FindingsLedger ledger = merged.Value;
        LedgerRound entry = ledger.Rounds[^1] with { PreviousSha256 = Rounds.Count == 0 ? null : Rounds[^1].EntrySha256 };
        LedgerRound chained = entry with { EntrySha256 = ledger.HashOf(entry) };

        return Result.Ok(ledger with { Rounds = [.. ledger.Rounds.Take(ledger.Rounds.Count - 1), chained] });
    }

    /// <summary>
    /// Checks every entry hashes to what it records and names the one before it.
    /// </summary>
    /// <remarks>
    /// <strong>This proves the ledger is consistent, not that it is the one that was approved.</strong> The
    /// binary holds no secret, so anyone who can edit the file can recompute every hash in it. What makes
    /// the chain a check is the anchor in the approval, which no agent can write (§5.2). On its own this
    /// catches an edit that was accidental or partial.
    /// </remarks>
    /// <param name="source">What to call the ledger in an error message.</param>
    /// <returns>Success, or a failure naming the first round that does not verify.</returns>
    public Result Verify(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        HashSet<int> numbers = [];
        string? previous = null;
        int last = 0;

        foreach (LedgerRound round in Rounds)
        {
            if (round.Round <= last)
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: round {round.Round} follows round {last}. Rounds are recorded in order, once each."));
            }

            if (!string.Equals(round.PreviousSha256, previous, StringComparison.Ordinal))
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: round {round.Round} does not name the round before it, so the history before it has been changed or reordered."));
            }

            if (round.EntrySha256 is null
                || !string.Equals(round.EntrySha256, HashOf(round), StringComparison.Ordinal))
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: round {round.Round} does not hash to what it records, so it, a finding it raised, or a disposition it moved has been changed since it was written. Only sdlc round writes a findings ledger."));
            }

            numbers.Add(round.Round);
            previous = round.EntrySha256;
            last = round.Round;
        }

        foreach (LedgerFinding finding in Findings)
        {
            if (finding.Transitions.Count == 0 || finding.Transitions.Any(transition => !numbers.Contains(transition.Round)))
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: finding {finding.Id} names a round the ledger does not hold, or no round at all."));
            }
        }

        return Result.Ok();
    }

    /// <summary>
    /// Each round as the findings it raised or moved, so every consumer reads one shape.
    /// </summary>
    /// <returns>One view per entry, in round order.</returns>
    public IReadOnlyList<FindingsRound> Views() =>
    [
        .. Rounds.Select(round => new FindingsRound(
            round.FrameworkVersion,
            Cycle,
            Artifact,
            round.UpstreamSha256,
            round.InterrogatedAt,
            round.Round,
            [
                .. Findings.SelectMany(finding => finding.Transitions
                    .Where(transition => transition.Round == round.Round)
                    .Select(transition => new Finding(
                        finding.Id,
                        finding.Severity,
                        transition.Disposition,
                        finding.Cites,
                        finding.Statement,
                        finding.Locus,
                        finding.Remedy,
                        transition.By,
                        transition.At,
                        finding.Perspective,
                        finding.Gist))),
            ],
            round.Conflicts,
            round.SubjectSha256,
            round.EvidenceSha256,
            round.SectionSha256,
            round.Note,
            round.EntrySha256)),
    ];

    private Result<FindingsLedger> Merge(FindingsRound round, bool strict)
    {
        if (Rounds.Any(entry => entry.Round == round.Round))
        {
            return Result.Fail<FindingsLedger>(string.Create(
                CultureInfo.InvariantCulture,
                $"{Artifact}: two rounds are numbered {round.Round}."));
        }

        List<LedgerFinding> findings = [.. Findings];
        Dictionary<string, int> index = new(StringComparer.Ordinal);

        for (int i = 0; i < findings.Count; i++)
        {
            index[findings[i].Id] = i;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (Finding finding in round.Findings)
        {
            if (strict && !seen.Add(finding.Id))
            {
                return Result.Fail<FindingsLedger>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Artifact}: round {round.Round} names {finding.Id} twice. One id is one objection."));
            }

            Transition move = new(round.Round, finding.Disposition, finding.DispositionBy, finding.DispositionAt);

            if (!index.TryGetValue(finding.Id, out int at))
            {
                index[finding.Id] = findings.Count;
                findings.Add(new LedgerFinding(
                    finding.Id,
                    finding.Severity,
                    finding.Cites,
                    finding.Statement,
                    finding.Locus,
                    finding.Remedy,
                    finding.Perspective,
                    [move],
                    finding.Gist));
                continue;
            }

            LedgerFinding known = findings[at];

            if (!SameObjection(known, finding))
            {
                if (strict)
                {
                    return Result.Fail<FindingsLedger>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Artifact}: {finding.Id} is already recorded as a different objection -- its severity, citation or statement differs from round {known.Transitions[0].Round}'s. Carry an earlier finding's id with its words unchanged, and give a new objection a new id: the next free one is {NextId(findings, finding.Id)}."));
                }

                known = known with
                {
                    Severity = finding.Severity,
                    Cites = finding.Cites,
                    Statement = finding.Statement,
                    Locus = finding.Locus ?? known.Locus,
                    Remedy = finding.Remedy ?? known.Remedy,
                    Perspective = finding.Perspective ?? known.Perspective,
                    Gist = finding.Gist ?? known.Gist,
                };
            }

            // A restatement at the disposition the finding already stands at moves nothing, so it
            // records nothing.
            findings[at] = known.Transitions[^1].Disposition == finding.Disposition
                ? known
                : known with { Transitions = [.. known.Transitions, move] };
        }

        LedgerRound entry = new(
            round.Round,
            round.FrameworkVersion,
            round.InterrogatedAt,
            round.UpstreamSha256,
            round.SubjectSha256,
            round.EvidenceSha256,
            round.SectionSha256,
            round.Conflicts is { Count: > 0 } conflicts ? conflicts : null,
            round.Note);

        return Result.Ok(this with
        {
            Cycle = Cycle ?? round.Cycle,
            Artifact = Artifact ?? round.Artifact,
            Findings = findings,
            Rounds = [.. Rounds, entry],
        });
    }

    /// <summary>
    /// The next id under the same prefix as one given, past every id the ledger holds under it.
    /// </summary>
    /// <remarks>
    /// A reader's ids are its own: a perspective reader with no history numbers from <c>F_01</c> every
    /// round, so its new objections collide with old ones (D-108). Naming the next free id makes the
    /// renumbering a lookup rather than a search.
    /// </remarks>
    private static string NextId(IEnumerable<LedgerFinding> findings, string id)
    {
        int at = id.LastIndexOf("F_", StringComparison.Ordinal);
        string prefix = at < 0 ? string.Empty : id[..at];

        int highest = findings
            .Select(finding => finding.Id)
            .Where(known => known.StartsWith(prefix + "F_", StringComparison.Ordinal))
            .Select(known => int.TryParse(known[(prefix.Length + 2)..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return string.Create(CultureInfo.InvariantCulture, $"{prefix}F_{highest + 1:D2}");
    }

    private static bool SameObjection(LedgerFinding known, Finding finding) =>
        known.Severity == finding.Severity
        && string.Equals(known.Statement, finding.Statement, StringComparison.Ordinal)
        && string.Equals(known.Cites?.Id, finding.Cites?.Id, StringComparison.Ordinal)
        && string.Equals(known.Cites?.Heading, finding.Cites?.Heading, StringComparison.Ordinal);

    private FindingsLedger Rechained()
    {
        List<LedgerRound> rounds = [];
        string? previous = null;

        foreach (LedgerRound round in Rounds)
        {
            LedgerRound linked = round with { PreviousSha256 = previous, EntrySha256 = null };
            LedgerRound chained = linked with { EntrySha256 = HashOf(linked) };
            rounds.Add(chained);
            previous = chained.EntrySha256;
        }

        return this with { Rounds = rounds };
    }

    /// <summary>
    /// The hash of one entry: itself without its own hash, the findings it first raised, the transitions it
    /// stamped, and through <see cref="LedgerRound.PreviousSha256"/> everything before it.
    /// </summary>
    /// <remarks>
    /// Computed over the parsed values in a fixed shape, not over the file's bytes, so reformatting the
    /// file moves nothing and changing a value moves the hash. Sections are sorted, because a dictionary's
    /// order is whatever the file happened to list.
    /// </remarks>
    private string HashOf(LedgerRound round)
    {
        var canonical = new
        {
            entry = round with
            {
                EntrySha256 = null,
                SectionSha256 = round.SectionSha256 is { } sections
                    ? new SortedDictionary<string, string>(sections.ToDictionary(), StringComparer.Ordinal)
                    : null,
            },
            raised = Findings
                .Where(finding => finding.Transitions.Count > 0 && finding.Transitions[0].Round == round.Round)
                .Select(finding => finding with { Transitions = [] })
                .ToList(),
            moved = Findings
                .SelectMany(finding => finding.Transitions
                    .Where(transition => transition.Round == round.Round)
                    .Select(transition => new { finding.Id, transition }))
                .OrderBy(move => move.Id, StringComparer.Ordinal)
                .ToList(),
        };

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, _canonical);

        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>The ledger as the file holds it.</summary>
    /// <param name="options">The serializer's options.</param>
    /// <returns>Its JSON.</returns>
    internal string Write(JsonSerializerOptions options) => JsonSerializer.Serialize(this, options) + "\n";
}
