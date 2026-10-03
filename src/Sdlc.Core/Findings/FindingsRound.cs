using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Cycles;

using Sdlc.Core.Constraints;

namespace Sdlc.Core.Findings;

/// <summary>One interrogation of one artifact.</summary>
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
public sealed record FindingsRound(
    [property: JsonPropertyName("framework_version")] string? FrameworkVersion,
    string? Cycle,
    string? Artifact,
    [property: JsonPropertyName("upstream_sha256")] string? UpstreamSha256,
    [property: JsonPropertyName("interrogated_at")] DateTimeOffset? InterrogatedAt,
    int Round,
    IReadOnlyList<Finding> Findings)
{
    /// <summary>
    /// Whether the round raised nothing that stops the artifact.
    /// </summary>
    /// <remarks>
    /// Clean <em>as of</em> the upstream it recorded. A round judged against an intent that has since
    /// changed is clean about a document that no longer exists, which is why staleness is computed
    /// against <see cref="UpstreamSha256"/> rather than inferred from this.
    /// </remarks>
    public bool IsClean => !Findings.Any(finding => finding.IsOpenBlocker);
}

/// <summary>Reads interrogation rounds off disk.</summary>
public static class FindingsReader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

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
                if (string.IsNullOrWhiteSpace(blocking.Cites))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: blocking finding {blocking.Id} cites no constraint. A blocking finding names the one rule it rests on, because that is what a person at the gate weighs."));
                }

                if (!ConstraintId.TryParse(blocking.Cites, out _))
                {
                    return Result.Fail<FindingsRound>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}: blocking finding {blocking.Id} cites '{blocking.Cites}', which is not a constraint id. Expected a form like CON_04 or LOC_02, with an optional revision such as CON_07.2."));
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
    public static IReadOnlyList<Finding> StandingBlockers(IReadOnlyList<FindingsRound> rounds)
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

        return
        [
            .. standing.Values
                .Where(finding => finding.IsOpenBlocker)
                .OrderBy(finding => finding.Id, StringComparer.Ordinal),
        ];
    }
}
