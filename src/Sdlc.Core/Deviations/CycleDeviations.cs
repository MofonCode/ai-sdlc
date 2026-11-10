using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Approvals;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Deviations;

/// <summary>Where one deviation stands with the person who signs it.</summary>
public enum DeviationStanding
{
    /// <summary>Nobody has answered it, or its text changed after it was answered.</summary>
    Pending,

    /// <summary>Accepted: it is part of what the cycle promises.</summary>
    Accepted,

    /// <summary>Declined, with a reason: the build redoes that part.</summary>
    Declined,

    /// <summary>Answered before the approval that stands now, so a reopen folded it in or gave it as its reason.</summary>
    History,
}

/// <summary>One deviation and where it stands.</summary>
/// <param name="Deviation">The deviation.</param>
/// <param name="Standing">Where it stands.</param>
/// <param name="Note">The person's note, when they wrote one.</param>
/// <param name="Changed">Whether it was answered and its text has moved since, which leaves it pending.</param>
public sealed record DeviationState(Deviation Deviation, DeviationStanding Standing, string? Note, bool Changed);

/// <summary>Three or more deviations bending the same approved text: the loop going round, not converging.</summary>
/// <param name="Document">The document they quote.</param>
/// <param name="Quote">The text they bend.</param>
/// <param name="Ids">The deviations, in the order recorded.</param>
public sealed record DeviationCircle(string Document, string Quote, IReadOnlyList<string> Ids);

/// <summary>A cycle's deviations, each with where it stands, and everything wrong with them.</summary>
/// <param name="Recorded">Whether the cycle has a <c>deviations.md</c> at all.</param>
/// <param name="States">Every deviation the file records, in its order.</param>
/// <param name="Problems">What is wrong: a file that does not parse, a quote the approved text does not hold, a
/// file another cycle claims, an accepted narrowing with no successor written.</param>
/// <param name="AcceptedBySetting">Whether <c>approval: none</c> accepts every deviation, because no person signs.</param>
/// <param name="LastAnswered">When the latest deviations entry since the standing approval was placed.</param>
/// <param name="Circles">Approved text bent three times or more (D-65, carried to deviations).</param>
public sealed record DeviationsState(
    bool Recorded,
    IReadOnlyList<DeviationState> States,
    IReadOnlyList<string> Problems,
    bool AcceptedBySetting,
    DateTimeOffset? LastAnswered,
    IReadOnlyList<DeviationCircle> Circles)
{
    /// <summary>A cycle with no deviations.</summary>
    public static DeviationsState None { get; } = new(false, [], [], false, null, []);

    /// <summary>The deviations since the approval that stands: pending, accepted or declined.</summary>
    public IReadOnlyList<DeviationState> Current => [.. States.Where(state => state.Standing != DeviationStanding.History)];

    /// <summary>The deviations nobody has answered.</summary>
    public IReadOnlyList<DeviationState> Pending => [.. States.Where(state => state.Standing == DeviationStanding.Pending)];

    /// <summary>The deviations a person declined since the approval that stands.</summary>
    public IReadOnlyList<DeviationState> Declined => [.. States.Where(state => state.Standing == DeviationStanding.Declined)];
}

/// <summary>
/// Reads a cycle's deviations against its approval ledger: which are pending, accepted, declined or history,
/// and what is wrong with them.
/// </summary>
/// <remarks>
/// <para><strong>Worked out on every read, never stored.</strong> The file says what the build did; the ledger
/// says what a person answered, holding each answer to the hash of the text it answered.</para>
/// <para><strong>History is what a reopen left behind.</strong> A deviation answered before the approval that
/// stands now was folded into the amended plan or given as the reopen's reason, so it no longer bends anything
/// and is not checked; it stays in the file for mining.</para>
/// </remarks>
public static class CycleDeviations
{
    /// <summary>How many deviations bending the same approved text make a circle (D-65's halt).</summary>
    public const int Circle = 3;

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCycleDeviationsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks for a cycle's deviations.</summary>
    /// <param name="Id">The cycle.</param>
    public sealed record Query(CycleId Id) : IRequest<Response>;

    /// <summary>The cycle's deviations.</summary>
    /// <param name="Cycle">The cycle.</param>
    /// <param name="State">Its deviations and where each stands.</param>
    public sealed record Response(Cycle Cycle, DeviationsState State);

    /// <summary>Reads a cycle's deviations and where each stands.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The state; <see cref="DeviationsState.None"/> when there is no file.</returns>
    public static async Task<DeviationsState> Read(Cycle cycle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        string path = DeviationsFile.PathOf(cycle);

        if (!File.Exists(path))
        {
            return DeviationsState.None;
        }

        Result<IReadOnlyList<Deviation>> parsed = DeviationsFile.Parse(
            await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
            DeviationsFile.FileName);

        if (parsed.IsFailed)
        {
            return new DeviationsState(true, [], [.. parsed.Errors.Select(error => error.Message)], false, null, []);
        }

        ApprovalLedger? ledger = null;
        if (File.Exists(cycle.ApprovalPath))
        {
            Result<ApprovalLedger> read = ApprovalLedger.Read(
                await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken).ConfigureAwait(false),
                cycle.ApprovalPath);

            ledger = read.IsSuccess ? read.Value : null;
        }

        bool bySetting = ledger is null
            && await SolutionDocument.ApprovalOf(cycle.SolutionRoot, cancellationToken).ConfigureAwait(false) == ApprovalRequirement.None;

        (List<DeviationState> states, DateTimeOffset? answered) = Stand(parsed.Value, ledger, bySetting);
        List<string> problems = await Problems(cycle, states, cancellationToken).ConfigureAwait(false);

        return new DeviationsState(true, states, problems, bySetting, answered, Circles(states));
    }

    // Each deviation against the ledger: an answer before the standing approval makes it history; the latest
    // answer after it decides, held to the hash it answered.
    private static (List<DeviationState> States, DateTimeOffset? Answered) Stand(
        IReadOnlyList<Deviation> deviations,
        ApprovalLedger? ledger,
        bool bySetting)
    {
        int standing = ledger?.StandingIndex ?? -1;
        Dictionary<string, DeviationAnswer> before = new(StringComparer.Ordinal);
        Dictionary<string, DeviationAnswer> after = new(StringComparer.Ordinal);
        DateTimeOffset? answered = null;

        for (int index = 0; index < (ledger?.Decisions.Count ?? 0); index++)
        {
            GateEntry entry = ledger!.Decisions[index];

            if (entry.Decision != GateDecision.Deviations)
            {
                continue;
            }

            Dictionary<string, DeviationAnswer> into = index < standing ? before : after;
            foreach (DeviationAnswer answer in entry.Answers ?? [])
            {
                into[answer.Id] = answer;
            }

            if (index > standing)
            {
                answered = entry.At;
            }
        }

        List<DeviationState> states = [];

        foreach (Deviation deviation in deviations)
        {
            if (before.TryGetValue(deviation.Id, out DeviationAnswer? earlier) && earlier.Answered)
            {
                states.Add(new DeviationState(deviation, DeviationStanding.History, earlier.Note, false));
                continue;
            }

            if (bySetting)
            {
                states.Add(new DeviationState(deviation, DeviationStanding.Accepted, null, false));
                continue;
            }

            if (!after.TryGetValue(deviation.Id, out DeviationAnswer? answer))
            {
                states.Add(new DeviationState(deviation, DeviationStanding.Pending, null, false));
                continue;
            }

            bool changed = !string.Equals(answer.Sha256, deviation.Sha256, StringComparison.OrdinalIgnoreCase);
            DeviationStanding stands = changed ? DeviationStanding.Pending
                : answer.Accepted ? DeviationStanding.Accepted
                : answer.Declined ? DeviationStanding.Declined
                : DeviationStanding.Pending;

            states.Add(new DeviationState(deviation, stands, answer.Note, changed && answer.Answered));
        }

        return (states, answered);
    }

    private static async Task<List<string>> Problems(Cycle cycle, List<DeviationState> states, CancellationToken cancellationToken)
    {
        List<string> problems = [];
        Dictionary<string, string?> documents = new(StringComparer.Ordinal);
        HashSet<string> ids = [.. states.Select(state => state.Deviation.Id)];
        List<(CycleId Cycle, IReadOnlySet<string> Files)>? claims = null;

        foreach (DeviationState state in states.Where(state => state.Standing != DeviationStanding.History))
        {
            Deviation deviation = state.Deviation;

            if (!documents.TryGetValue(deviation.Document, out string? text))
            {
                string path = deviation.Kind == DeviationKind.Decision
                    ? Path.Combine(cycle.SolutionRoot, Cycle.SdlcDirectoryName, deviation.Document)
                    : Path.Combine(cycle.Directory, deviation.Document);

                text = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;
                documents[deviation.Document] = text;
            }

            if (text is null)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} quotes {deviation.Document}, which this cycle does not have."));
            }
            else if (!DeviationsFile.Holds(text, deviation.Quote))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} quotes text {deviation.Document} does not hold: '{Brief(deviation.Quote)}'. Quote the approved text word for word."));
            }

            if (deviation.Replaces is { } replaced && !ids.Contains(replaced))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} replaces {replaced}, which this file does not record."));
            }

            if (deviation.Files.Count > 0)
            {
                claims ??= await Claims(cycle, cancellationToken).ConfigureAwait(false);

                foreach ((CycleId other, IReadOnlySet<string> files) in claims)
                {
                    foreach (string file in deviation.Files.Where(files.Contains))
                    {
                        problems.Add(string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} adds {file}, which cycle {other} has claimed. Two cycles changing one file is a conflict, not a deviation: leave it to that cycle, or reopen."));
                    }
                }
            }

            if (state.Standing == DeviationStanding.Accepted
                && deviation.Kind == DeviationKind.Criterion
                && deviation.SuccessorSlug is { } slug
                && !File.Exists(Path.Combine(cycle.SolutionRoot, Cycle.SdlcDirectoryName, Successor.DirectoryName, slug + ".md")))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} is accepted and narrows a criterion, and its successor .sdlc/{Successor.DirectoryName}/{slug}.md is not written. Write it, so what was dropped is not lost."));
            }
        }

        return problems;
    }

    // The files every other open cycle's plan declares: not abandoned and not yet verified.
    private static async Task<List<(CycleId Cycle, IReadOnlySet<string> Files)>> Claims(Cycle cycle, CancellationToken cancellationToken)
    {
        List<(CycleId, IReadOnlySet<string>)> claims = [];

        foreach (Cycle other in CycleIndex.All(cycle.SolutionRoot))
        {
            string plan = other.PathOf(ArtifactKind.Plan);

            if (other.Id == cycle.Id || other.IsAbandoned || !File.Exists(plan) || File.Exists(other.PathOf(ArtifactKind.Verify)))
            {
                continue;
            }

            IReadOnlyList<string> files = DeclaredFiles.Read(await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false));
            claims.Add((other.Id, new HashSet<string>(files, StringComparer.OrdinalIgnoreCase)));
        }

        return claims;
    }

    private static List<DeviationCircle> Circles(List<DeviationState> states) =>
    [
        .. states
            .Where(state => state.Standing != DeviationStanding.History)
            .GroupBy(state => (state.Deviation.Document, Quote: DeviationsFile.Normalise(state.Deviation.Quote)))
            .Where(group => group.Count() >= Circle)
            .Select(group => new DeviationCircle(group.Key.Document, group.Key.Quote, [.. group.Select(state => state.Deviation.Id)])),
    ];

    private static string Brief(string text)
    {
        string flat = DeviationsFile.Normalise(text);
        return flat.Length <= 80 ? flat : flat[..77] + "...";
    }

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Cycle cycle = new(request.Id, solution.Path);

            if (!Directory.Exists(cycle.Directory))
            {
                return Result.Fail<Response>(string.Create(CultureInfo.InvariantCulture, $"No cycle {request.Id} in this solution."));
            }

            return Result.Ok(new Response(cycle, await Read(cycle, cancellationToken).ConfigureAwait(false)));
        }
    }
}
