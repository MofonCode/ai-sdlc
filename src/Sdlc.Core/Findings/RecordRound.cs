using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Findings;

/// <summary>
/// Writes one interrogation round: what the readers found, with everything only the binary can know
/// stamped beside it (D-101).
/// </summary>
/// <remarks>
/// <strong>A round was a file the session wrote, and the session was the wrong author for half of
/// it.</strong> In a real cycle the round number, the time, and the hash of what was judged were all
/// typed by the agent running the loop: three stages' rounds carried the same second, and seven plan
/// rounds each judged a different spec while the spec was still moving. Nothing refused any of it,
/// because nothing wrote the round but the thing being checked. The findings are still the readers'
/// and the dispositions still the session's (D-49) -- this owns the envelope and the order.
/// </remarks>
public static class RecordRound
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddRecordRoundRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>The round as written.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Artifact">What was judged.</param>
    /// <param name="Written">The round as the ledger now holds it: what it raised and what it moved.</param>
    /// <param name="Path">The ledger it was written to.</param>
    /// <param name="Standing">Every finding still open after it.</param>
    /// <param name="Converted">How many round files from before D-107 this write folded in and removed.</param>
    public sealed record Response(
        Cycle Cycle,
        ArtifactKind Artifact,
        FindingsRound Written,
        string Path,
        IReadOnlyList<Finding> Standing,
        int Converted = 0);

    /// <summary>Writes the next round for one artifact.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Artifact">What was judged.</param>
    /// <param name="From">
    /// A JSON file holding the readers' <c>findings</c>, and <c>conflicts</c> when there are any, with
    /// the dispositions the session recorded. Any envelope field in it is ignored: those are this
    /// command's to write.
    /// </param>
    public sealed record Command(CycleId Id, ArtifactKind Artifact, string From) : IRequest<Response>;

    /// <summary>
    /// Whether every document above one is settled: interrogated as it stands now, against the
    /// upstream as it stands now, with nothing blocking left open.
    /// </summary>
    /// <remarks>
    /// Shared with the status panel's next-step, so the panel never offers a round this would refuse.
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="kind">The artifact about to be interrogated.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>Each reason the chain above is not settled; empty when it is.</returns>
    public static async Task<Result<IReadOnlyList<string>>> UnsettledAbove(
        Cycle cycle,
        ArtifactKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        List<string> reasons = [];
        IReadOnlyList<ArtifactKind> above = kind == ArtifactKind.Verify
            ? Cycle.PlanningDocuments
            : [.. Cycle.PlanningDocuments.TakeWhile(document => document != kind)];

        foreach (ArtifactKind document in above)
        {
            string path = cycle.PathOf(document);
            string name = Cycle.FileNameOf(document);

            if (!File.Exists(path))
            {
                reasons.Add(string.Create(CultureInfo.InvariantCulture, $"{name} does not exist"));
                continue;
            }

            Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
                .ReadHistory(cycle, document, cancellationToken)
                .ConfigureAwait(false);

            if (history.IsFailed)
            {
                return Result.Fail<IReadOnlyList<string>>(history.Errors);
            }

            if (history.Value.Count == 0)
            {
                reasons.Add(string.Create(CultureInfo.InvariantCulture, $"{name} has no interrogation round"));
                continue;
            }

            FindingsRound latest = history.Value[^1];
            string now = await ContentHash.OfFile(path, cancellationToken).ConfigureAwait(false);

            if (latest.SubjectSha256 is null)
            {
                reasons.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} round {latest.Round} does not record which {name} it judged"));
            }
            else if (!string.Equals(latest.SubjectSha256, now, StringComparison.Ordinal))
            {
                reasons.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} changed after its round {latest.Round}: that round judged {ContentHash.Abbreviate(latest.SubjectSha256)}, and it is now {ContentHash.Abbreviate(now)}"));
            }

            if (Cycle.UpstreamOf(document) is { } upstream)
            {
                string upstreamNow = await ContentHash
                    .OfFile(cycle.PathOf(upstream), cancellationToken)
                    .ConfigureAwait(false);

                if (!string.Equals(latest.UpstreamSha256, upstreamNow, StringComparison.Ordinal))
                {
                    reasons.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{name} round {latest.Round} judged it against a {Cycle.FileNameOf(upstream)} that has since changed"));
                }
            }

            IReadOnlyList<Finding> blockers = FindingsReader.StandingBlockers(history.Value);

            if (blockers.Count > 0)
            {
                reasons.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} has {blockers.Count} open blocking finding{(blockers.Count == 1 ? string.Empty : "s")}: {string.Join(", ", blockers.Select(blocker => blocker.Id))}"));
            }
        }

        return Result.Ok<IReadOnlyList<string>>(reasons);
    }

    /// <summary>
    /// The refusal for a blocker this round raises with no gist, or one longer than a glance.
    /// </summary>
    /// <remarks>
    /// <para>Only a blocker the ledger has not seen: one carried forward keeps whatever it was raised
    /// with, so a cycle that began before gists is not sent back over findings nobody can re-raise.</para>
    /// <para>Here rather than in <see cref="FindingsRound.Validate"/>, which also reads every ledger already
    /// written -- a rule there would make every round recorded before it unreadable.</para>
    /// <para><strong>Why it is required.</strong> A blocker reached the person as an id, a rule and a
    /// statement of five hundred to eighteen hundred characters, so what somebody at the gate learned was
    /// "CON_15 is blocking". The reader that raised it is the one that knows what set it off.</para>
    /// </remarks>
    /// <param name="round">The round as given.</param>
    /// <param name="ledger">What the artifact's ledger already holds.</param>
    /// <param name="source">The file it was read from, for the message.</param>
    /// <returns>The refusal, or null when every new blocker carries a gist.</returns>
    internal static string? Ungisted(FindingsRound round, FindingsLedger ledger, string source)
    {
        HashSet<string> known = [.. ledger.Findings.Select(finding => finding.Id)];

        List<string> wanting =
        [
            .. round.Findings
                .Where(finding => finding.Severity == Severity.Blocking && !known.Contains(finding.Id))
                .Where(finding => string.IsNullOrWhiteSpace(finding.Gist)
                    || finding.Gist.Length > Finding.GistLimit
                    || finding.Gist.Contains('\n', StringComparison.Ordinal))
                .Select(finding => finding.Id),
        ];

        return wanting.Count == 0
            ? null
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: blocking finding{(wanting.Count == 1 ? string.Empty : "s")} {string.Join(", ", wanting)} {(wanting.Count == 1 ? "carries" : "carry")} no usable gist. A blocker raised now carries `gist`: one plain sentence, at most {Finding.GistLimit} characters on one line, saying what in the document set it off -- it is what a person at the gate reads first. Ask the reader that raised it; do not write it here.");
    }

    /// <summary>
    /// One hash over the evidence a verification may cite, so a verify round says which run it read.
    /// </summary>
    /// <remarks>
    /// Evidence is re-recorded after each remediation, and in a real cycle it was recorded five times
    /// under ten verify rounds that all recorded the same plan hash. Which run a round judged could
    /// only be recovered from git.
    /// <para><strong>Over what is committed, and nothing else</strong> (D-92). The pull-request check
    /// recomputes this on a clean checkout, where the build log is not, so a hash over it could never
    /// match there. Rounds recorded before carried it; <paramref name="withBuildLog"/> recomputes theirs.</para>
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <param name="withBuildLog">Whether to include the build log, as rounds recorded before 0.32.0 did.</param>
    /// <returns>The hash, or null when nothing has been recorded.</returns>
    public static async Task<string?> EvidenceHash(
        Cycle cycle,
        CancellationToken cancellationToken,
        bool withBuildLog = false)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        string[] names = withBuildLog
            ? [RecordEvidence.SummaryFileName, RecordEvidence.DiffFileName, RecordEvidence.BuildLogFileName]
            : [RecordEvidence.SummaryFileName, RecordEvidence.DiffFileName];
        StringBuilder listing = new();

        foreach (string name in names)
        {
            string path = Path.Combine(cycle.EvidenceDirectory, name);

            if (File.Exists(path))
            {
                string hash = await ContentHash.OfFile(path, cancellationToken).ConfigureAwait(false);
                listing.Append(name).Append(' ').Append(hash).Append('\n');
            }
        }

        return listing.Length == 0
            ? null
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(listing.ToString())));
    }

    /// <summary>
    /// A hash per second-level section, so the next round can say which sections moved.
    /// </summary>
    /// <param name="markdown">The artifact.</param>
    /// <returns>Each heading mapped to the hash of its lines.</returns>
    public static IReadOnlyDictionary<string, string> SectionHashes(string markdown) =>
        ArtifactSections.Read(markdown).ToDictionary(
            section => section.Key,
            section => ContentHash.Of(string.Join('\n', section.Value)),
            StringComparer.Ordinal);

    /// <summary>
    /// The sections that changed since a round, by heading, including sections added since.
    /// </summary>
    /// <param name="round">The earlier round.</param>
    /// <param name="markdown">The artifact as it is now.</param>
    /// <returns>The headings, in document order; every heading when the round recorded none.</returns>
    public static IReadOnlyList<string> ChangedSince(FindingsRound round, string markdown)
    {
        ArgumentNullException.ThrowIfNull(round);

        IReadOnlyDictionary<string, string> now = SectionHashes(markdown);

        return round.SectionSha256 is not { } before
            ? [.. now.Keys]
            : [.. now.Where(section => !before.TryGetValue(section.Key, out string? was)
                || !string.Equals(was, section.Value, StringComparison.Ordinal)).Select(section => section.Key)];
    }

    /// <summary>
    /// Every id the cycle's document ledgers hold, so a new suggestion's id is free across the cycle.
    /// </summary>
    private static async Task<Result<HashSet<string>>> LedgerIds(Cycle cycle, CancellationToken cancellationToken)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (ArtifactKind kind in Enum.GetValues<ArtifactKind>())
        {
            Result<LedgerOnDisk> read = await FindingsReader.ReadLedger(cycle, kind, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<HashSet<string>>();
            }

            ids.UnionWith(read.Value.Ledger.Findings.Select(finding => finding.Id));
        }

        return Result.Ok(ids);
    }

    private sealed class Handler(SolutionRoot solution, TimeProvider clock) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            string artifact = cycle.PathOf(request.Artifact);
            string name = Cycle.FileNameOf(request.Artifact);

            if (!File.Exists(artifact))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no {name}. A round judges a document that exists."));
            }

            if (!File.Exists(request.From))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no file at {request.From} holding the round's findings."));
            }

            // The order is the chain's: a round about a document is only worth recording once everything
            // above it has been read as it stands and nothing there still blocks. A spec round judged
            // against an intent that is still moving is a clean reading of a document that is about to
            // stop existing -- seven of those happened in one cycle, run in parallel to save time.
            Result<IReadOnlyList<string>> unsettled = await UnsettledAbove(cycle, request.Artifact, cancellationToken)
                .ConfigureAwait(false);

            if (unsettled.IsFailed)
            {
                return Result.Fail<Response>(unsettled.Errors);
            }

            if (unsettled.Value.Count > 0)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id}: a {Cycle.NameOf(request.Artifact)} round waits for everything above it to be settled, and it is not:{Environment.NewLine}{string.Join(Environment.NewLine, unsettled.Value.Select(reason => "  " + reason))}{Environment.NewLine}Interrogate from the top down, one document at a time: each round reads the one above as it stands."));
            }

            string json = await File.ReadAllTextAsync(request.From, cancellationToken).ConfigureAwait(false);
            Result<FindingsRound> given = FindingsReader.Read(json, request.From);

            if (given.IsFailed)
            {
                return Result.Fail<Response>(given.Errors);
            }

            Result<LedgerOnDisk> onDisk = await FindingsReader
                .ReadLedger(cycle, request.Artifact, cancellationToken)
                .ConfigureAwait(false);

            if (onDisk.IsFailed)
            {
                return Result.Fail<Response>(onDisk.Errors);
            }

            FindingsLedger ledger = onDisk.Value.Ledger;

            if (Ungisted(given.Value, ledger, request.From) is { } ungisted)
            {
                return Result.Fail<Response>(ungisted);
            }

            string text = await File.ReadAllTextAsync(artifact, cancellationToken).ConfigureAwait(false);
            // A verify round's upstream is the plan, which commits to the proof per criterion -- the
            // hash the status panel compares a verify round against.
            ArtifactKind? above = request.Artifact == ArtifactKind.Verify
                ? ArtifactKind.Plan
                : Cycle.UpstreamOf(request.Artifact);

            string? upstream = above is { } judgedAgainst
                ? await ContentHash.OfFile(cycle.PathOf(judgedAgainst), cancellationToken).ConfigureAwait(false)
                : null;

            FindingsRound written = given.Value with
            {
                FrameworkVersion = Cycle.FrameworkVersion,
                Cycle = cycle.Id.ToString(),
                Artifact = name,
                UpstreamSha256 = upstream,
                SubjectSha256 = ContentHash.Of(text),
                EvidenceSha256 = request.Artifact == ArtifactKind.Verify
                    ? await EvidenceHash(cycle, cancellationToken).ConfigureAwait(false)
                    : null,
                SectionSha256 = SectionHashes(text),
                InterrogatedAt = clock.GetUtcNow(),
                Round = ledger.Rounds.Count == 0 ? 1 : ledger.Rounds[^1].Round + 1,
                EntrySha256 = null,
            };

            Result<SuggestionLedger> suggestions = await FindingsReader
                .ReadSuggestions(cycle, cancellationToken)
                .ConfigureAwait(false);

            if (suggestions.IsFailed)
            {
                return Result.Fail<Response>(suggestions.Errors);
            }

            Result<HashSet<string>> taken = await LedgerIds(cycle, cancellationToken).ConfigureAwait(false);

            if (taken.IsFailed)
            {
                return Result.Fail<Response>(taken.Errors);
            }

            // Blockers stay in the document's ledger, which the approval anchors; suggestions go to the
            // cycle's, each concern once (D-115). A suggestion this document's ledger already holds was
            // raised before that, and stays where its history is.
            HashSet<string> here = [.. ledger.Findings.Select(finding => finding.Id)];
            List<Finding> toLedger = [];
            List<Finding> toSuggestions = [];

            foreach (Finding finding in written.Findings)
            {
                if (finding.Severity == Severity.Blocking && suggestions.Value.Find(finding.Id) is not null)
                {
                    return Result.Fail<Response>(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{request.From}: {finding.Id} is a suggestion, and this round calls it blocking. A blocker raised for the same concern takes an F_ id of its own, and the suggestion is withdrawn in the same round."));
                }

                (finding.Severity == Severity.Blocking || here.Contains(finding.Id) ? toLedger : toSuggestions).Add(finding);
            }

            // Merged rather than appended verbatim (D-107): a new id is a finding, a moved one a
            // transition, and one restated where it already stands is nothing -- so a session copying
            // every finding forward costs the ledger nothing, where it once cost a file the size of the
            // whole history every round.
            Result<FindingsLedger> appended = ledger.Append(written with { Findings = toLedger });

            if (appended.IsFailed)
            {
                return Result.Fail<Response>(appended.Errors);
            }

            Result<SuggestionLedger> recorded = suggestions.Value.Record(request.Artifact, written.Round, toSuggestions, taken.Value);

            if (recorded.IsFailed)
            {
                return Result.Fail<Response>(recorded.Errors);
            }

            // The document's ledger first: a suggestion naming a round its ledger does not hold is refused
            // on read, so the order that can be interrupted safely is this one.
            string path = onDisk.Value.Path;
            Directory.CreateDirectory(cycle.FindingsDirectory);
            await File.WriteAllTextAsync(path, FindingsReader.Write(appended.Value), cancellationToken).ConfigureAwait(false);

            if (toSuggestions.Count > 0)
            {
                await File.WriteAllTextAsync(cycle.SuggestionsLedgerPath, FindingsReader.Write(recorded.Value), cancellationToken).ConfigureAwait(false);
            }

            // A cycle still holding round files is converted by this write: the ledger now carries every
            // one of them, chained, so they go in the same step. Left behind, they would make the next
            // read refuse a directory holding both shapes.
            foreach (string legacy in onDisk.Value.RoundFiles)
            {
                File.Delete(legacy);
            }

            Result<IReadOnlyList<FindingsRound>> merged = FindingsReader.WithSuggestions(
                appended.Value.Views(),
                recorded.Value,
                request.Artifact,
                cycle.SuggestionsLedgerPath);

            if (merged.IsFailed)
            {
                return Result.Fail<Response>(merged.Errors);
            }

            IReadOnlyList<FindingsRound> views = merged.Value;

            return Result.Ok(new Response(
                cycle,
                request.Artifact,
                views[^1],
                path,
                [.. FindingsReader.Standing(views).Where(finding => finding.Disposition == Disposition.Open)],
                onDisk.Value.RoundFiles.Count));
        }
    }
}

/// <summary>
/// Which sections of an artifact changed since its latest round (D-101).
/// </summary>
/// <remarks>
/// What a perspective reader is told on every round after the first: raise suggestions about these
/// sections alone. Thirty suggestions a round, whether or not the document had moved, left one plan
/// with nearly eight hundred open and nobody reading any of them.
/// </remarks>
public static class ChangedSections
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddChangedSectionsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>The sections that moved.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="Artifact">The artifact.</param>
    /// <param name="Since">The round compared against, or null when there is none yet.</param>
    /// <param name="Changed">The headings that changed or were added, in document order.</param>
    public sealed record Response(Cycle Cycle, ArtifactKind Artifact, int? Since, IReadOnlyList<string> Changed);

    /// <summary>Asks which sections moved since the latest round.</summary>
    /// <param name="Id">The cycle.</param>
    /// <param name="Artifact">The artifact.</param>
    public sealed record Query(CycleId Id, ArtifactKind Artifact) : IRequest<Response>;

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);
            string artifact = cycle.PathOf(request.Artifact);

            if (!File.Exists(artifact))
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cycle {cycle.Id} has no {Cycle.FileNameOf(request.Artifact)}."));
            }

            Result<IReadOnlyList<FindingsRound>> history = await FindingsReader
                .ReadHistory(cycle, request.Artifact, cancellationToken)
                .ConfigureAwait(false);

            if (history.IsFailed)
            {
                return Result.Fail<Response>(history.Errors);
            }

            string text = await File.ReadAllTextAsync(artifact, cancellationToken).ConfigureAwait(false);

            return history.Value.Count == 0
                ? Result.Ok(new Response(cycle, request.Artifact, null, [.. RecordRound.SectionHashes(text).Keys]))
                : Result.Ok(new Response(
                    cycle,
                    request.Artifact,
                    history.Value[^1].Round,
                    RecordRound.ChangedSince(history.Value[^1], text)));
        }
    }
}
