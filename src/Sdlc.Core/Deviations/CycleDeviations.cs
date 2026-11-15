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

/// <summary>Where one deviation stands.</summary>
public enum DeviationStanding
{
    /// <summary>
    /// Recorded by the build and in force: it leads the pull request, and merging it is how a person accepts it
    /// (D-181).
    /// </summary>
    Recorded,

    /// <summary>Accepted by a deviations entry in the approval ledger, as a person answered before D-181.</summary>
    Accepted,

    /// <summary>
    /// Changed at the person's request, or declined by a ledger entry from before D-181: it no longer gives leave,
    /// and the build redid that part.
    /// </summary>
    Declined,

    /// <summary>Recorded before the approval that stands, so a reopen took it in; it stays for the harvest.</summary>
    History,
}

/// <summary>One deviation and where it stands.</summary>
/// <param name="Deviation">The deviation.</param>
/// <param name="Standing">Where it stands.</param>
/// <param name="Note">The person's words, when they asked for it to be changed or declined it.</param>
public sealed record DeviationState(Deviation Deviation, DeviationStanding Standing, string? Note);

/// <summary>Three or more deviations bending the same approved text: the loop going round, not converging.</summary>
/// <param name="Document">The document they quote.</param>
/// <param name="Quote">The text they bend.</param>
/// <param name="Ids">The deviations, in the order recorded.</param>
public sealed record DeviationCircle(string Document, string Quote, IReadOnlyList<string> Ids);

/// <summary>A cycle's deviations, each with where it stands, and everything wrong with them.</summary>
/// <param name="Recorded">Whether the cycle has a <c>deviations.md</c> at all.</param>
/// <param name="States">Every entry the file records, choices included, in its order.</param>
/// <param name="Problems">What is wrong: a file that does not parse, a quote the approved text does not hold, a
/// file another cycle claims, a criterion narrowed, an accepted narrowing with no successor written.</param>
/// <param name="Circles">Approved text bent three times or more (D-65, carried to deviations).</param>
public sealed record DeviationsState(
    bool Recorded,
    IReadOnlyList<DeviationState> States,
    IReadOnlyList<string> Problems,
    IReadOnlyList<DeviationCircle> Circles)
{
    /// <summary>A cycle with no deviations.</summary>
    public static DeviationsState None { get; } = new(false, [], [], []);

    /// <summary>The entries since the approval that stands.</summary>
    public IReadOnlyList<DeviationState> Current => [.. States.Where(state => state.Standing != DeviationStanding.History)];

    /// <summary>The deviations that bend approved text and still give leave: recorded or accepted.</summary>
    public IReadOnlyList<DeviationState> InForce => [.. Current.Where(state => state.Deviation.Bends && state.Standing != DeviationStanding.Declined)];

    /// <summary>The choices the build made that the approved text left open, and the person has not asked to change.</summary>
    public IReadOnlyList<DeviationState> Choices => [.. Current.Where(state => !state.Deviation.Bends && state.Standing != DeviationStanding.Declined)];

    /// <summary>The entries the person asked to change, or declined, since the approval that stands.</summary>
    public IReadOnlyList<DeviationState> Declined => [.. States.Where(state => state.Standing == DeviationStanding.Declined)];
}

/// <summary>
/// Reads a cycle's deviations: which are recorded, changed at the person's request, or history, and what is wrong
/// with them.
/// </summary>
/// <remarks>
/// <para><strong>Worked out on every read, never stored.</strong> The file says what the build did and, on a
/// <c>Changed:</c> line, what the person asked to be done instead. Nothing waits for an answer before the pull
/// request: merging it is the acceptance (D-181). A deviations entry in the approval ledger from before D-181 is
/// still read, held to the hash of the text it answered.</para>
/// <para><strong>History is what a reopen left behind.</strong> A deviation answered before the approval that
/// stands now -- the reopen's draft answers every recorded one, taking it in -- no longer bends anything and is
/// not checked; it stays in the file for the harvest.</para>
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
            return new DeviationsState(true, [], [.. parsed.Errors.Select(error => error.Message)], []);
        }

        ApprovalLedger? ledger = null;
        if (File.Exists(cycle.ApprovalPath))
        {
            Result<ApprovalLedger> read = ApprovalLedger.Read(
                await File.ReadAllTextAsync(cycle.ApprovalPath, cancellationToken).ConfigureAwait(false),
                cycle.ApprovalPath);

            ledger = read.IsSuccess ? read.Value : null;
        }

        List<DeviationState> states = Stand(parsed.Value, ledger);
        List<string> problems = await Problems(cycle, states, cancellationToken).ConfigureAwait(false);

        return new DeviationsState(true, states, problems, Circles(states));
    }

    // Each deviation: a Changed line is the person's request and decides; otherwise an answer before the standing
    // approval makes it history, and an answer after it from before D-181 decides while it holds to the hash it
    // answered. Everything else is recorded, and the pull request is where it is accepted.
    private static List<DeviationState> Stand(IReadOnlyList<Deviation> deviations, ApprovalLedger? ledger)
    {
        int standing = ledger?.StandingIndex ?? -1;
        Dictionary<string, DeviationAnswer> before = new(StringComparer.Ordinal);
        Dictionary<string, DeviationAnswer> after = new(StringComparer.Ordinal);

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
        }

        List<DeviationState> states = [];

        foreach (Deviation deviation in deviations)
        {
            if (before.TryGetValue(deviation.Id, out DeviationAnswer? earlier) && earlier.Answered)
            {
                states.Add(new DeviationState(deviation, DeviationStanding.History, earlier.Note));
                continue;
            }

            if (deviation.Changed is { } changed)
            {
                states.Add(new DeviationState(deviation, DeviationStanding.Declined, changed));
                continue;
            }

            DeviationStanding stands = after.TryGetValue(deviation.Id, out DeviationAnswer? answer)
                && string.Equals(answer.Sha256, deviation.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? answer.Accepted ? DeviationStanding.Accepted
                        : answer.Declined ? DeviationStanding.Declined
                        : DeviationStanding.Recorded
                    : DeviationStanding.Recorded;

            states.Add(new DeviationState(deviation, stands, stands == DeviationStanding.Declined ? answer!.Note : null));
        }

        return states;
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

            // A choice quotes nothing, so there is no approved text to hold it to; the readers judge it against the
            // decisions and the plan's purpose (D-180).
            if (!deviation.Bends)
            {
                continue;
            }

            // What done means is the spec's, and no deviation changes it (D-179): a criterion narrowed by the build,
            // which no person accepted before D-181, is something the cycle may not do on its own.
            if (deviation.Kind == DeviationKind.Criterion && state.Standing == DeviationStanding.Recorded)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{deviation.Id} narrows a criterion, and a deviation never changes what done means (D-179). Meet the criterion, or escalate so a person can reopen the plan."));
            }

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
            .Where(state => state.Standing != DeviationStanding.History && state.Deviation.Bends)
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
