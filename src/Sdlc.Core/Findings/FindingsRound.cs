using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Perspectives;

namespace Sdlc.Core.Findings;

/// <summary>One interrogation of one artifact.</summary>
/// <remarks>
/// <strong>It had an <c>IsClean</c> property and nothing but tests ever asked it.</strong> Whether an
/// artifact is clear is not a question about one round: a finding keeps its id across rounds, so a
/// blocker opened in round one and answered in round two is not open because round one said it was.
/// <see cref="FindingsReader.StandingBlockers"/> is the fold that answers it, and it says in its own
/// remarks why there must be exactly one — the gate and the status panel have to agree on what "open"
/// means, and the one a person reads would not have been the one that refuses the build.
/// </remarks>
/// <param name="FrameworkVersion">The version that produced it.</param>
/// <param name="Cycle">The cycle's id.</param>
/// <param name="Artifact">The artifact's path, as the round recorded it.</param>
/// <param name="UpstreamSha256">
/// The canonical hash of the artifact above, as this round saw it. Null on an intent, which has no
/// upstream.
/// </param>
/// <param name="InterrogatedAt">When the round ran.</param>
/// <param name="Round">Which round this is, counting from one.</param>
/// <param name="Findings">What it found. An empty list is a clean round.</param>
/// <param name="Conflicts">
/// Rules this reader found cannot both be satisfied, if any. Absent and empty mean the same thing
/// here, unlike <paramref name="Findings"/>: a reader that found no conflict is the overwhelming
/// default, and requiring the array would put a key on every round that is empty in almost all of
/// them -- the decoration <c>CON_11</c> exists to refuse. With Findings the distinction is worth a
/// rule, because "nothing is wrong" and "the author forgot" need different responses.
/// </param>
/// <param name="SubjectSha256">
/// The canonical hash of the document this round judged (D-101). Without it a round could say what it
/// was judged against and not what it judged, so a document edited after its clean round was caught by
/// nothing but the person approving the bytes.
/// </param>
/// <param name="EvidenceSha256">On a verify round, one hash over the evidence the verification may cite.</param>
/// <param name="SectionSha256">
/// A hash per second-level section of the judged document, so the next round's perspective readers can be
/// told which sections moved and raise suggestions about those alone.
/// </param>
/// <param name="Note">
/// What moved above this document since its last round and what this document did about it, when a
/// steer carried a change down. A spec that did not change for a corrected intent says why here, so the
/// next reader of the history sees the decision rather than an unexplained gap.
/// </param>
public sealed record FindingsRound(
    [property: JsonPropertyName("framework_version")] string? FrameworkVersion,
    string? Cycle,
    string? Artifact,
    [property: JsonPropertyName("upstream_sha256")] string? UpstreamSha256,
    [property: JsonPropertyName("interrogated_at")] DateTimeOffset? InterrogatedAt,
    int Round,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<Conflict>? Conflicts = null,
    [property: JsonPropertyName("subject_sha256")] string? SubjectSha256 = null,
    [property: JsonPropertyName("evidence_sha256")] string? EvidenceSha256 = null,
    [property: JsonPropertyName("section_sha256")] IReadOnlyDictionary<string, string>? SectionSha256 = null,
    string? Note = null);

/// <summary>Reads interrogation rounds off disk.</summary>
public static class FindingsReader
{
    private static readonly Lazy<HashSet<string>> _perspectives = new(() => PerspectiveLibrary.All() is { IsSuccess: true } all
        ? [.. all.Value.Select(perspective => perspective.Name)]
        : []);

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions _writing = new(_options)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Writes one round as the file a reader reads back.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>Its JSON, with absent fields left out.</returns>
    public static string Write(FindingsRound round) => JsonSerializer.Serialize(round, _writing) + "\n";

    /// <summary>
    /// Reads one round.
    /// </summary>
    /// <param name="json">The round's JSON.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The round, or a failure naming the file.</returns>
    public static Result<FindingsRound> Read(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        try
        {
            FindingsRound? round = JsonSerializer.Deserialize<FindingsRound>(json, _options);

            if (round is null)
            {
                return Result.Fail<FindingsRound>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: the file holds no round."));
            }

            // A round with no findings array is not a clean round; it is a file that forgot to say.
            // Treating the two alike would let a malformed round read as nothing to answer.
            if (round.Findings is null)
            {
                return Result.Fail<FindingsRound>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: no findings array. A clean round records an empty one rather than omitting it."));
            }

            // Every blocking finding cites exactly one constraint (§7, D-30), and nothing enforced it,
            // so a blocker with no `cites` was accepted and the gate rendered the citation as
            // whitespace. It is mechanically decidable, so by §2.3 it belongs here rather than in a
            // prompt -- and an uncited blocker is unarguable, which is the property §7.1 says the
            // citation buys: a person at the gate cannot judge whether it is cheap or expensive
            // without the rule behind it. F-11.
            foreach (Finding blocking in round.Findings.Where(finding => finding.Severity == Severity.Blocking))
            {
                if (blocking.Cites is null || string.IsNullOrWhiteSpace(blocking.Cites.Id))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: blocking finding {blocking.Id} cites no constraint. A blocking finding names the one rule it rests on, because that is what a person at the gate weighs."));
                }

                if (!ConstraintId.TryParse(blocking.Cites.Id, out _))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: blocking finding {blocking.Id} cites '{blocking.Cites.Id}', which is not a constraint id. Expected a form like CON_04 or LOC_02, with an optional revision such as CON_07.2."));
                }
            }

            // A citation carries the rule's own line beside the id, wherever one is given. Without it a
            // reader of the finding holds an index entry rather than a rule, which is the whole reason
            // the heading is frozen into the round instead of resolved later. Checked at every severity
            // and not only on blockers: a suggestion that cites is making the same claim on a rule and
            // is read in the same listing. Whether the id parses stays a blockers-only check, which is
            // how it already was.
            foreach (Finding finding in round.Findings.Where(finding => finding.Cites is not null))
            {
                if (string.IsNullOrWhiteSpace(finding.Cites!.Heading))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: finding {finding.Id} cites '{finding.Cites.Id}' and gives no heading. A citation carries the rule's one line beside its id, copied from the set the round was judged against, so that reading the finding does not mean resolving the id."));
                }
            }

            // A perspective a finding names has to be one the binary carries. Recurrence is counted by
            // perspective name, so a misspelt one would be a perspective of its own with a count of one,
            // and the real one would read as recurring less than it did.
            foreach (Finding finding in round.Findings.Where(finding => finding.Perspective is not null))
            {
                if (!_perspectives.Value.Contains(finding.Perspective!))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: finding {finding.Id} names perspective '{finding.Perspective}', which is not one the binary carries. Written so far: {string.Join(", ", _perspectives.Value.Order(StringComparer.Ordinal))}."));
                }
            }

            // A conflict is the loop's third ending (section 6), so it is checked as strictly as a
            // finding's citation. The shape rules are all mechanically decidable, which by section 2.3
            // is what puts them here rather than in the prompt.
            foreach (Conflict conflict in round.Conflicts ?? [])
            {
                string named = conflict.Ids() is { Length: > 0 } ids ? ids : "nothing";

                // Two or more. One rule cannot conflict with itself, and a single-entry conflict is a
                // finding wearing the wrong shape -- which is the confusion this whole distinction
                // exists to prevent, so it is refused rather than tolerated.
                if (conflict.Between is null || conflict.Between.Count < 2)
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: a conflict names {named}. A conflict is between two rules or more, because its whole claim is that satisfying one breaks another. One rule objecting to the artifact is a finding."));
                }

                foreach (Citation citation in conflict.Between)
                {
                    if (!ConstraintId.TryParse(citation.Id, out _))
                    {
                        return Result.Fail<FindingsRound>(string.Create(
                            CultureInfo.InvariantCulture,
                            $"{source}: a conflict names '{citation.Id}', which is not a constraint id. Expected a form like CON_04 or LOC_02, with an optional revision such as CON_07.2."));
                    }

                    if (string.IsNullOrWhiteSpace(citation.Heading))
                    {
                        return Result.Fail<FindingsRound>(string.Create(
                            CultureInfo.InvariantCulture,
                            $"{source}: a conflict names '{citation.Id}' and gives no heading. Every citation carries the rule's one line beside its id, here for the same reason as on a finding: a person reading this has to weigh two rules against each other, which they cannot do from two numbers."));
                    }
                }

                if (string.IsNullOrWhiteSpace(conflict.Statement))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: the conflict between {named} says nothing about why. A conflict with no explanation is unarguable, which is what CON_10.2 objects to in a gate refusal and what the same reasoning objects to here."));
                }
            }

            return Result.Ok(round);
        }
        catch (JsonException error)
        {
            return Result.Fail<FindingsRound>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: not valid JSON. {error.Message}"));
        }
    }

    /// <summary>
    /// Reads every round recorded for one artifact, in round order.
    /// </summary>
    /// <remarks>
    /// Rounds accumulate and are never rewritten, so the history is the record of what was challenged
    /// and what became of it.
    /// </remarks>
    /// <param name="cycle">The cycle to read.</param>
    /// <param name="kind">The artifact whose rounds to read.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The rounds, or a failure listing every file that could not be read.</returns>
    public static async Task<Result<IReadOnlyList<FindingsRound>>> ReadHistory(
        Cycle cycle,
        ArtifactKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        if (!Directory.Exists(cycle.FindingsDirectory))
        {
            return Result.Ok<IReadOnlyList<FindingsRound>>([]);
        }

        List<FindingsRound> rounds = [];
        List<IError> errors = [];

        string prefix = Cycle.NameOf(kind) + ".r";

        foreach (string path in Directory
            .EnumerateFiles(cycle.FindingsDirectory, Cycle.NameOf(kind) + ".r*.json")
            .Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Result<FindingsRound> read = Read(json, path);

            if (read.IsFailed)
            {
                errors.AddRange(read.Errors);
                continue;
            }

            rounds.Add(read.Value);
        }

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<FindingsRound>>(errors)
            : Result.Ok<IReadOnlyList<FindingsRound>>([.. rounds.OrderBy(round => round.Round)]);
    }

    /// <summary>
    /// Folds a history into the findings that still stop the artifact advancing.
    /// </summary>
    /// <remarks>
    /// A finding keeps its id across rounds, so the latest round that mentions one says where it has
    /// got to: a blocker answered in round two is not open because round one called it open. Reading
    /// only the latest round would lose a finding that round carried silently, and reading every round
    /// as a flat list would count one objection several times.
    /// <para>
    /// This lives here, and not in each caller, because the gate and the status panel must agree on
    /// what "open" means. Two implementations of that would eventually disagree, and the one a person
    /// reads would not be the one that refuses the build.
    /// </para>
    /// </remarks>
    /// <param name="rounds">Every round recorded for one artifact.</param>
    /// <returns>The open blocking findings, by id.</returns>
    public static IReadOnlyList<Finding> StandingBlockers(IReadOnlyList<FindingsRound> rounds) =>
        [.. Standing(rounds).Where(finding => finding.IsOpenBlocker)];

    /// <summary>
    /// Folds a history into the open findings that do not stop the artifact advancing.
    /// </summary>
    /// <remarks>
    /// Derived from the same fold as <see cref="StandingBlockers"/> rather than written beside it. The
    /// counts a panel shows and the findings the gate refuses on have to mean the same thing by "open",
    /// and two folds would eventually disagree about a finding carried silently through a round.
    /// </remarks>
    /// <param name="rounds">Every round recorded for one artifact.</param>
    /// <returns>The open non-blocking findings, by id.</returns>
    public static IReadOnlyList<Finding> StandingSuggestions(IReadOnlyList<FindingsRound> rounds) =>
    [
        .. Standing(rounds)
            .Where(finding => finding.Severity != Severity.Blocking
                && finding.Disposition == Disposition.Open),
    ];

    /// <summary>
    /// The conflicts that still stand: the latest round's, and no others.
    /// </summary>
    /// <remarks>
    /// A conflict has no id and no disposition, so it cannot be tracked across rounds the way a finding
    /// is. It does not need to be. A conflict is a per-round observation, and it is settled the way
    /// everything else here is settled -- by the next fresh reader's silence (D-49). A conflict declared
    /// in round two and not declared in round three means the refiner found an exit, and <c>CON_16</c>'s
    /// <c>Out of scope</c> and <c>CON_14</c>'s <c>## Open</c> exist to be exactly that.
    /// <para>
    /// So this is the latest round's list rather than a fold. Accumulating conflicts across rounds would
    /// report a resolved one forever, which is the mistake <see cref="Standing"/> exists to avoid for
    /// findings. Counting declarations over the whole history is a different question with a different
    /// answer, and it belongs to whatever decides a loop is stuck rather than here.
    /// </para>
    /// </remarks>
    /// <param name="rounds">Every round recorded for one artifact.</param>
    /// <returns>The latest round's conflicts, or nothing when there are no rounds.</returns>
    public static IReadOnlyList<Conflict> StandingConflicts(IReadOnlyList<FindingsRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        FindingsRound? latest = rounds.Count == 0
            ? null
            : rounds.OrderBy(round => round.Round).Last();

        return latest?.Conflicts ?? [];
    }

    /// <summary>
    /// Where every finding has got to, latest mention wins.
    /// </summary>
    /// <param name="rounds">Every round recorded for one artifact.</param>
    /// <returns>Each finding once, in id order.</returns>
    public static IReadOnlyList<Finding> Standing(IReadOnlyList<FindingsRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        Dictionary<string, Finding> standing = new(StringComparer.Ordinal);

        foreach (FindingsRound round in rounds.OrderBy(round => round.Round))
        {
            foreach (Finding finding in round.Findings)
            {
                standing[finding.Id] = finding;
            }
        }

        return [.. standing.Values.OrderBy(finding => finding.Id, StringComparer.Ordinal)];
    }
}
