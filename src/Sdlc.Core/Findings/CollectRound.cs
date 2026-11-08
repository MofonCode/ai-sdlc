using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Hooks;
using Sdlc.Core.Perspectives;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>
/// Reads every reader's answer for one round and drafts the round file (D-117).
/// </summary>
/// <remarks>
/// <para>The session parsed each reader's output, carried restatements, numbered new ids and wrote the
/// round by hand, at whatever context it had reached, and one session wrote throwaway scripts to do it.
/// Every one of those steps is a lookup: what the reconciler named, the ledgers already hold, and the next
/// free id is a maximum. So the binary does them, and the readers still judge.</para>
/// <para><strong>The dispositions of earlier findings stay the session's</strong> (D-49). Whether a finding
/// the readers did not raise again was answered or withdrawn comes from the refiner's report, so this lists
/// those findings and leaves them out of the draft — where silence keeps each one open.</para>
/// </remarks>
public static class CollectRound
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCollectRoundRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>The round as drafted.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Artifact">The artifact judged.</param>
    /// <param name="Path">Where the draft was written.</param>
    /// <param name="Raised">Each finding the draft carries, under the id it will be recorded with.</param>
    /// <param name="Restated">Each reader's id the reconciler named a restatement, and the id it is carried under.</param>
    /// <param name="Demoted">A perspective's blockers made suggestions, because they cite no rule it lists.</param>
    /// <param name="Conflicts">The conflicts the draft carries.</param>
    /// <param name="NotRaised">Findings still open on this document that no reader raised again.</param>
    /// <param name="Usage">What each reader cost.</param>
    public sealed record Response(
        Cycle Cycle,
        ArtifactKind Artifact,
        string Path,
        IReadOnlyList<Finding> Raised,
        IReadOnlyDictionary<string, string> Restated,
        IReadOnlyList<string> Demoted,
        IReadOnlyList<Conflict> Conflicts,
        IReadOnlyList<Finding> NotRaised,
        IReadOnlyList<ReaderUsage> Usage);

    /// <summary>Drafts one round from the answers in its directory.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Artifact">The artifact judged.</param>
    /// <param name="Directory">The round's directory, where <c>sdlc round prompt</c> wrote the prompts.</param>
    /// <param name="Host">The host the readers ran on, as the session names it, or null.</param>
    public sealed record Command(CycleId Id, ArtifactKind Artifact, string Directory, string? Host = null) : IRequest<Response>;

    private static readonly JsonSerializerOptions _writing = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed record Draft(
        IReadOnlyList<Finding> Findings,
        IReadOnlyList<Conflict>? Conflicts,
        string? Host,
        IReadOnlyList<ReaderUsage> Usage);

    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            string? host = null;

            if (request.Host is { } named)
            {
                if (!HookProtocol.TryParseHost(named, out HookHost? parsed))
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"'{named}' is not a host. One of: {string.Join(", ", SupportedHosts.Spellings())}."));
                }

                host = SupportedHosts.Of(parsed.Value).Spelling;
            }

            Result<ActivePerspectives.Response> active = await mediator
                .Send<ActivePerspectives.Query, ActivePerspectives.Response>(new ActivePerspectives.Query(request.Id), cancellationToken)
                .ConfigureAwait(false);

            if (active.IsFailed)
            {
                return active.ToResult<Response>();
            }

            IReadOnlyList<string> readers = ReaderAnswers.Blind(request.Artifact, active.Value.Perspectives.Where(p => p.Written).Select(p => p.Name));
            List<Finding> raised = [];
            List<Conflict> conflicts = [];
            List<string> demoted = [];
            List<ReaderUsage> usage = [];

            // The adjudicator ran before this round, for this round: what it cost is this round's.
            if (request.Artifact == ArtifactKind.Verify
                && File.Exists(ReaderAnswers.AnswerPath(request.Directory, ReaderAnswers.Adjudicate)))
            {
                Result<ReaderAnswer> verdict = await ReaderAnswers.Read(request.Directory, ReaderAnswers.Adjudicate, cancellationToken).ConfigureAwait(false);

                if (verdict.IsSuccess)
                {
                    usage.Add(verdict.Value.Usage);
                }
            }

            foreach (string reader in readers)
            {
                Result<ReaderAnswer> answer = await ReaderAnswers.Read(request.Directory, reader, cancellationToken).ConfigureAwait(false);

                if (answer.IsFailed)
                {
                    return answer.ToResult<Response>();
                }

                Result<ReadBody> body = ReaderAnswers.FindingsOf(answer.Value, reader);

                if (body.IsFailed)
                {
                    return body.ToResult<Response>();
                }

                if (Ungisted(body.Value.Findings) is { Count: > 0 } wanting)
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"The {reader} reader raised blocking finding{(wanting.Count == 1 ? string.Empty : "s")} {string.Join(", ", wanting)} with no usable gist: one plain sentence, at most {Finding.GistLimit} characters on one line. Start it again; do not write one here."));
                }

                // A rule judged item by item is answered item by item, or the reader goes again (D-119).
                if (reader == ReaderAnswers.Rules)
                {
                    Result<Constraints.EffectiveConstraints.Response> set = await mediator
                        .Send<Constraints.EffectiveConstraints.Query, Constraints.EffectiveConstraints.Response>(
                            new Constraints.EffectiveConstraints.Query(Cycle.NameOf(request.Artifact)),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (set.IsFailed)
                    {
                        return set.ToResult<Response>();
                    }

                    IReadOnlyList<(Constraints.Constraint Rule, IReadOnlyDictionary<int, string> Items)> judged = await JudgedItems.In(cycle, set.Value, cancellationToken).ConfigureAwait(false);

                    if (JudgedItems.Unanswered(judged, body.Value.Rows, body.Value.Findings) is { } unanswered)
                    {
                        return Result.Fail<Response>(unanswered + " Start it again; do not write the rows here.");
                    }
                }

                raised.AddRange(body.Value.Findings);
                conflicts.AddRange(body.Value.Conflicts);
                demoted.AddRange(body.Value.Demoted);
                usage.Add(answer.Value.Usage);
            }

            Result<IReadOnlyList<FindingsRound>> history = await FindingsReader.ReadHistory(cycle, request.Artifact, cancellationToken).ConfigureAwait(false);

            if (history.IsFailed)
            {
                return history.ToResult<Response>();
            }

            Result<SuggestionLedger> suggestions = await FindingsReader.ReadSuggestions(cycle, cancellationToken).ConfigureAwait(false);

            if (suggestions.IsFailed)
            {
                return suggestions.ToResult<Response>();
            }

            Result<HashSet<string>> taken = await RecordRound.LedgerIds(cycle, cancellationToken).ConfigureAwait(false);

            if (taken.IsFailed)
            {
                return taken.ToResult<Response>();
            }

            IReadOnlyList<Finding> standingHere = FindingsReader.Standing(history.Value);
            ReconcilerBody reconciled = new([], []);

            // Nothing raised, nothing to reconcile: a reconciler asked about an empty round answers nothing.
            if (raised.Count > 0)
            {
                Result<ReaderAnswer> answer = await ReaderAnswers.Read(request.Directory, ReaderAnswers.Reconcile, cancellationToken).ConfigureAwait(false);

                if (answer.IsFailed)
                {
                    return answer.ToResult<Response>();
                }

                Result<ReconcilerBody> body = ReaderAnswers.Body<ReconcilerBody>(answer.Value);

                if (body.IsFailed)
                {
                    return body.ToResult<Response>();
                }

                reconciled = body.Value;
                usage.Add(answer.Value.Usage);
            }

            Apply applied = Reconcile(raised, reconciled, standingHere, suggestions.Value);
            Dictionary<string, string> renamed = Number(applied, history.Value, suggestions.Value, taken.Value);

            List<Finding> findings = [];

            foreach (Finding finding in applied.Findings)
            {
                string id = renamed.GetValueOrDefault(finding.Id, finding.Id);
                string statement = finding.Statement;

                if (applied.Contradicts.TryGetValue(finding.Id, out string? other))
                {
                    statement += string.Create(CultureInfo.InvariantCulture, $" Contradicts `{renamed.GetValueOrDefault(other, other)}`.");
                }
                else if (applied.RunsInto.TryGetValue(finding.Id, out string? rule))
                {
                    statement += string.Create(CultureInfo.InvariantCulture, $" Runs into `{rule}`.");
                }

                findings.Add(finding with { Id = id, Statement = statement });
            }

            findings.AddRange(applied.Carried);
            conflicts.AddRange(applied.Conflicts);

            HashSet<string> mentioned = [.. findings.Select(finding => finding.Id)];
            List<Finding> notRaised = [.. standingHere.Where(finding => finding.Disposition == Disposition.Open && !mentioned.Contains(finding.Id))];

            Directory.CreateDirectory(request.Directory);
            string path = Path.Combine(request.Directory, ReaderAnswers.RoundFileName);
            Draft draft = new(findings, conflicts.Count == 0 ? null : conflicts, host, usage);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(draft, _writing) + "\n", cancellationToken).ConfigureAwait(false);

            return Result.Ok(new Response(
                cycle,
                request.Artifact,
                path,
                findings,
                applied.Restated.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                [.. demoted.Select(id => renamed.GetValueOrDefault(id, id))],
                conflicts,
                notRaised,
                usage));
        }

        private static List<string> Ungisted(IEnumerable<Finding> findings) =>
        [
            .. findings
                .Where(finding => finding.Severity == Severity.Blocking)
                .Where(finding => string.IsNullOrWhiteSpace(finding.Gist)
                    || finding.Gist.Length > Finding.GistLimit
                    || finding.Gist.Contains('\n', StringComparison.Ordinal))
                .Select(finding => finding.Id),
        ];
    }

    /// <summary>The reconciler's answer applied to this round's findings.</summary>
    private sealed record Apply(
        List<Finding> Findings,
        List<Finding> Carried,
        Dictionary<string, string> Restated,
        Dictionary<string, string> Contradicts,
        Dictionary<string, string> RunsInto,
        List<Conflict> Conflicts);

    /// <summary>
    /// Carries each restatement under the id that stands, and settles each pair that cannot both be
    /// answered by the reconciler's table (D-108, D-64).
    /// </summary>
    private static Apply Reconcile(
        IReadOnlyList<Finding> raised,
        ReconcilerBody reconciled,
        IReadOnlyList<Finding> standingHere,
        SuggestionLedger suggestions)
    {
        var byId = raised.ToDictionary(finding => finding.Id, StringComparer.Ordinal);
        var open = standingHere
            .Where(finding => finding.Disposition == Disposition.Open)
            .ToDictionary(finding => finding.Id, StringComparer.Ordinal);

        List<Finding> carried = [];
        Dictionary<string, string> restated = new(StringComparer.Ordinal);
        HashSet<string> dropped = new(StringComparer.Ordinal);

        foreach (Restatement restatement in reconciled.Restatements ?? [])
        {
            if (restatement.Finding is not { } id
                || restatement.Standing is not { } standing
                || !byId.TryGetValue(id, out Finding? finding)
                || restated.ContainsKey(id))
            {
                continue;
            }

            // Standing on this document: carried as the ledger holds it, which records nothing. Raised again
            // as a blocker where it stands as a suggestion: the blocker stays new and the suggestion is
            // withdrawn here, since a concern is not both.
            if (open.TryGetValue(standing, out Finding? here))
            {
                if (finding.Severity == Severity.Blocking && here.Severity == Severity.Suggestion)
                {
                    carried.Add(here with { Disposition = Disposition.Withdrawn });
                    restated[id] = standing + " (withdrawn: raised again as a blocker)";
                    continue;
                }

                if (carried.All(other => other.Id != standing))
                {
                    carried.Add(here);
                }

                restated[id] = standing;
                dropped.Add(id);
                continue;
            }

            // An open suggestion another document raised: the same concern found again, carried under its id
            // and recorded against this document (D-115). Not as a blocker: a suggestion only moves on the
            // documents that raised it, so the blocker stays new.
            if (suggestions.Find(standing) is { IsOpen: true } elsewhere && finding.Severity == Severity.Suggestion)
            {
                if (carried.All(other => other.Id != standing))
                {
                    carried.Add(new Finding(
                        elsewhere.Id,
                        Severity.Suggestion,
                        Disposition.Open,
                        elsewhere.Cites,
                        elsewhere.Statement,
                        elsewhere.Locus,
                        elsewhere.Remedy,
                        Perspective: elsewhere.Perspective,
                        Gist: elsewhere.Gist));
                }

                restated[id] = standing;
                dropped.Add(id);
            }

            // Anything else names an id that does not stand: unsure, so the finding is new.
        }

        List<Finding> findings = [.. raised.Where(finding => !dropped.Contains(finding.Id))];
        Dictionary<string, string> contradicts = new(StringComparer.Ordinal);
        Dictionary<string, string> runsInto = new(StringComparer.Ordinal);
        List<Conflict> conflicts = [];

        foreach (Pair pair in reconciled.Pairs ?? [])
        {
            if (pair.Between is not [string first, string second])
            {
                continue;
            }

            int a = findings.FindIndex(finding => finding.Id == first);
            int b = findings.FindIndex(finding => finding.Id == second);

            if (a < 0 || b < 0)
            {
                continue;
            }

            bool firstCites = findings[a].Cites?.Id is { Length: > 0 };
            bool secondCites = findings[b].Cites?.Id is { Length: > 0 };

            if (!firstCites && !secondCites)
            {
                findings[a] = findings[a] with { Severity = Severity.Suggestion };
                findings[b] = findings[b] with { Severity = Severity.Suggestion };
                contradicts[first] = second;
                contradicts[second] = first;
            }
            else if (firstCites != secondCites)
            {
                int loser = firstCites ? b : a;
                findings[loser] = findings[loser] with { Severity = Severity.Suggestion };
                runsInto[findings[loser].Id] = (firstCites ? findings[a] : findings[b]).Cites!.Id!;
            }
            else if (!string.Equals(findings[a].Cites!.Id, findings[b].Cites!.Id, StringComparison.Ordinal))
            {
                conflicts.Add(new Conflict(
                    [findings[a].Cites!, findings[b].Cites!],
                    pair.Why ?? "Each remedy excludes the other.",
                    findings[a].Locus));
            }
        }

        return new Apply(findings, carried, restated, contradicts, runsInto, conflicts);
    }

    /// <summary>
    /// Every new id, numbered past what the ledgers hold under its prefix: a blocker's past this document's,
    /// a suggestion's past the cycle's (D-108, D-115).
    /// </summary>
    private static Dictionary<string, string> Number(
        Apply applied,
        IReadOnlyList<FindingsRound> history,
        SuggestionLedger suggestions,
        HashSet<string> taken)
    {
        HashSet<string> blockers = [.. history.SelectMany(round => round.Findings).Select(finding => finding.Id)];
        HashSet<string> suggestionIds = [.. suggestions.Suggestions.Select(suggestion => suggestion.Id), .. taken];
        Dictionary<string, string> renamed = new(StringComparer.Ordinal);

        foreach (Finding finding in applied.Findings)
        {
            int dot = finding.Id.LastIndexOf('.');
            string prefix = dot < 0 ? string.Empty : finding.Id[..(dot + 1)];
            bool blocking = finding.Severity == Severity.Blocking;
            HashSet<string> pool = blocking ? blockers : suggestionIds;
            string kind = blocking ? "F_" : "S_";

            int highest = pool
                .Where(known => known.StartsWith(prefix + kind, StringComparison.Ordinal))
                .Select(known => int.TryParse(known[(prefix.Length + 2)..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : 0)
                .DefaultIfEmpty(0)
                .Max();

            string id = string.Create(CultureInfo.InvariantCulture, $"{prefix}{kind}{highest + 1:D2}");
            pool.Add(id);
            renamed[finding.Id] = id;
        }

        return renamed;
    }
}
