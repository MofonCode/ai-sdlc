using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Harvest;

/// <summary>One area a closed cycle's plan proposed, kept so the three-cycle count survives its folder.</summary>
/// <param name="Name">The name the plan proposed.</param>
/// <param name="Files">The files or globs it listed.</param>
public sealed record HarvestedArea(string Name, IReadOnlyList<string> Files);

/// <summary>A suggestion a sitting declined, with why, so what became of it can still be said.</summary>
/// <param name="Id">The suggestion's id, as its cycle's ledger held it.</param>
/// <param name="Reason">Why it was declined.</param>
public sealed record HarvestDecline(string Id, string Reason);

/// <summary>
/// A suggestion a sitting answered as already handled: a rule, a decision, a successor or a file covers it, so
/// nobody declined it and nothing is left to do.
/// </summary>
/// <param name="Id">The suggestion's id, as its cycle's ledger held it.</param>
/// <param name="By">What covers it, as the proposal file wrote it: a rule or decision id, <c>successor:&lt;slug&gt;</c>, or a path.</param>
/// <param name="Reason">How it covers it.</param>
public sealed record HarvestCover(string Id, string By, string Reason);

/// <summary>
/// What one closed cycle leaves in the tree once its folder is deleted (D-152).
/// </summary>
/// <param name="Cycle">The cycle's id.</param>
/// <param name="Merge">
/// The last commit on the integration branch that touched its folder: <c>git show &lt;merge&gt;:.sdlc/cycles/&lt;id&gt;/…</c>
/// recovers any of it, and a decision's <c>From</c> naming the cycle still resolves.
/// </param>
/// <param name="Closed">When it was closed.</param>
/// <param name="Abandoned">Whether it stopped rather than passed.</param>
/// <param name="Declared">The files its plan declared, for the area replay.</param>
/// <param name="Changed">The files its change touched, for the area replay.</param>
/// <param name="Cited">Every rule its findings cited, once each, so a rule's count across cycles survives.</param>
/// <param name="Areas">The areas its plan proposed.</param>
/// <param name="Rounds">How many rounds each document took.</param>
/// <param name="Reopens">How many times a person took its approval back.</param>
/// <param name="Verdicts">Every outcome its verify reached, oldest first.</param>
/// <param name="Releases">
/// The releases its rounds recorded, oldest first (D-204): what a later reading needs to tell a cost the framework has
/// since fixed from one it still has. Absent from ledgers written before, and from a cycle whose rounds recorded none.
/// </param>
/// <param name="Readers">What each reader raised on each document, so a reader's yield survives the folder (D-204).</param>
/// <param name="Spent">Weighted tokens per document, from its usage record, when its rounds recorded one (D-204).</param>
public sealed record HarvestedCycle(
    string Cycle,
    string? Merge,
    DateTimeOffset Closed,
    bool Abandoned,
    IReadOnlyList<string> Declared,
    IReadOnlyList<string> Changed,
    IReadOnlyList<string> Cited,
    IReadOnlyList<HarvestedArea> Areas,
    IReadOnlyDictionary<string, int> Rounds,
    int Reopens,
    IReadOnlyList<string> Verdicts,
    IReadOnlyList<string>? Releases = null,
    IReadOnlyList<ReaderYield>? Readers = null,
    IReadOnlyDictionary<string, double>? Spent = null);

/// <summary>What one reader raised on one document of a cycle, each finding counted once whatever rounds it stood.</summary>
/// <param name="Reader">The perspective that raised it, or <c>rules</c>.</param>
/// <param name="Document">The document: intent, spec, plan or verify.</param>
/// <param name="Raised">Every finding it raised.</param>
/// <param name="Blocking">Those that were ever blocking.</param>
/// <param name="Cited">Those that cited a rule.</param>
/// <param name="Withdrawn">Those a later reader took back.</param>
public sealed record ReaderYield(string Reader, string Document, int Raised, int Blocking, int Cited, int Withdrawn);

/// <summary>
/// A concern seen in fewer cycles than the bar, carried so its count survives the folders it came from.
/// </summary>
/// <param name="Gist">The concern, in one sentence, as the sitting wrote it.</param>
/// <param name="Cycles">The cycles it was seen in.</param>
/// <param name="Why">
/// What it prevents and what it promotes, in a line or two: the case a person weighs when the concern reaches the bar,
/// kept here because the reasoning it came from leaves with the folders (D-194). Absent from ledgers written before.
/// </param>
public sealed record Watched(string Gist, IReadOnlyList<string> Cycles, string? Why = null);

/// <summary>One sitting: the aspect it answered, the cycles it answered it for, and what the person accepted.</summary>
/// <param name="Recorded">When it was recorded.</param>
/// <param name="Aspect">The aspect it answered.</param>
/// <param name="Cycles">The cycles it answered that aspect for.</param>
/// <param name="Accepted">The headings the person marked, as the proposal file wrote them.</param>
/// <param name="Declined">The suggestions it declined, with why.</param>
/// <param name="Covered">
/// The suggestions it answered as already handled, each with what covers it. Absent from ledgers written before,
/// and from a sitting that covered nothing.
/// </param>
public sealed record HarvestSitting(
    DateTimeOffset Recorded,
    HarvestAspect Aspect,
    IReadOnlyList<string> Cycles,
    IReadOnlyList<string> Accepted,
    IReadOnlyList<HarvestDecline> Declined,
    IReadOnlyList<HarvestCover>? Covered = null);

/// <summary>
/// The harvest ledger, <c>.sdlc/harvested.json</c>: which aspects each completed cycle has had answered, and what
/// a closed cycle leaves once its folder goes (D-152, D-191, D-192).
/// </summary>
/// <remarks>
/// <para><strong>Written only by the binary</strong>, by <c>sdlc harvest record</c> and <c>sdlc harvest close</c>,
/// and denied to every agent write by the hook, as the suggestions ledger is. It binds nothing; it is what lets a
/// cycle's folder go without what later harvests need going with it: the merge commit to recover anything from, the
/// files the area replay reads, the rules a three-cycle count reads, and the concerns still short of the bar.</para>
/// <para><strong>A sitting answers one aspect.</strong> Each of the five is read and answered on its own, so the
/// ledger keeps which aspects every cycle has had answered, and a cycle closes only once all five have.</para>
/// </remarks>
/// <param name="Cycles">Every closed cycle, in the order closed.</param>
/// <param name="Watch">Concerns below the bar, each with the cycles it was seen in.</param>
/// <param name="Sittings">Every sitting recorded.</param>
/// <param name="Ignored">
/// Suggestions a sitting left alone, each concern with the cycles it was ignored in. Not a decision: a declined
/// suggestion keeps its reason in its sitting, and one taken further lives on as a successor or a cycle. An
/// ignored one is the only kind that leaves nothing behind once its folder goes, so this tally is what lets the
/// same concern ignored cycle after cycle reach the bar (D-153).
/// </param>
/// <param name="Recurred">
/// Each time a closed cycle's reader raised again a concern already answered here, folded in by
/// <c>sdlc harvest close</c> from the cycle's own suggestions ledger, each (concern, cycle, document, round) once
/// (D-221). Kept apart from <see cref="Watched.Cycles"/>, so what a person answered and what readers kept noticing
/// afterwards stay told apart. Absent from ledgers written before, and from one with nothing recurred.
/// </param>
public sealed record HarvestLedger(
    IReadOnlyList<HarvestedCycle> Cycles,
    IReadOnlyList<Watched> Watch,
    IReadOnlyList<HarvestSitting> Sittings,
    IReadOnlyList<Watched>? Ignored = null,
    IReadOnlyList<HarvestedRecurrence>? Recurred = null)
{
    /// <summary>The ledger's file name, in the framework's directory.</summary>
    public const string FileName = "harvested.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>A solution that has harvested nothing.</summary>
    public static HarvestLedger Empty { get; } = new([], [], [], []);

    /// <summary>Where a solution's ledger lives.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <returns>The path.</returns>
    public static string PathIn(string solutionRoot) => Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, FileName);

    /// <summary>Whether a cycle is closed.</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <returns>Its entry, or null.</returns>
    public HarvestedCycle? Find(string cycle) => Cycles.FirstOrDefault(entry => string.Equals(entry.Cycle, cycle, StringComparison.Ordinal));

    /// <summary>The aspects a sitting has answered for a cycle.</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <returns>The aspects, each once.</returns>
    public IReadOnlySet<HarvestAspect> Answered(string cycle) =>
        Sittings.Where(sitting => sitting.Cycles.Contains(cycle, StringComparer.Ordinal)).Select(sitting => sitting.Aspect).ToHashSet();

    /// <summary>The aspects still to answer for a cycle, in the order the playbook runs them.</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <returns>The aspects.</returns>
    public IReadOnlyList<HarvestAspect> Missing(string cycle)
    {
        IReadOnlySet<HarvestAspect> answered = Answered(cycle);

        return [.. HarvestAspects.All.Where(aspect => !answered.Contains(aspect))];
    }

    /// <summary>Reads a ledger's text.</summary>
    /// <param name="json">The text.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The ledger, or why it cannot be read.</returns>
    public static Result<HarvestLedger> Parse(string json, string source)
    {
        try
        {
            HarvestLedger? read = JsonSerializer.Deserialize<HarvestLedger>(json, _options);

            return read is null
                ? Result.Fail<HarvestLedger>(source + ": the harvest ledger is empty.")
                : Result.Ok(read with
                {
                    Cycles = read.Cycles ?? [],
                    Watch = read.Watch ?? [],
                    Sittings = read.Sittings ?? [],
                    Ignored = read.Ignored ?? [],

                    // Two close branches merged by hand can both carry one recurrence; it is one (D-221).
                    Recurred = read.Recurred is { Count: > 0 } recurred ? Distinct(recurred) : null,
                });
        }
        catch (JsonException error)
        {
            return Result.Fail<HarvestLedger>(source + ": the harvest ledger does not parse: " + error.Message);
        }
    }

    /// <summary>Reads a solution's ledger; an absent one is empty.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The ledger, or why it cannot be read.</returns>
    public static async Task<Result<HarvestLedger>> Read(string solutionRoot, CancellationToken cancellationToken)
    {
        string path = PathIn(solutionRoot);

        return File.Exists(path)
            ? Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path)
            : Result.Ok(Empty);
    }

    /// <summary>The ledger as the file holds it.</summary>
    /// <returns>Its JSON.</returns>
    public string Write() => JsonSerializer.Serialize(this, _options) + "\n";

    /// <summary>
    /// The ledger with a sitting and its watched and ignored concerns added: a concern already held gains the new
    /// cycles, and a new one is added. A concern the sitting covered leaves both tallies, so it stops counting
    /// toward the bar: something already handles it.
    /// </summary>
    /// <param name="sitting">The sitting.</param>
    /// <param name="watch">The concerns the sitting watched.</param>
    /// <param name="ignored">The suggestions the sitting ignored, by concern.</param>
    /// <param name="covered">The concerns the sitting covered, each as the tally words it.</param>
    /// <returns>The new ledger.</returns>
    public HarvestLedger With(
        HarvestSitting sitting,
        IReadOnlyList<Watched> watch,
        IReadOnlyList<Watched>? ignored = null,
        IReadOnlyCollection<string>? covered = null)
    {
        ArgumentNullException.ThrowIfNull(sitting);
        ArgumentNullException.ThrowIfNull(watch);

        HashSet<string> gone = new(covered ?? [], StringComparer.Ordinal);

        return this with
        {
            Watch = [.. Merged(Watch, watch).Where(concern => !gone.Contains(concern.Gist))],
            Sittings = [.. Sittings, sitting],
            Ignored = [.. Merged(Ignored ?? [], ignored ?? []).Where(concern => !gone.Contains(concern.Gist))],
        };
    }

    /// <summary>The ledger with newly closed cycles added.</summary>
    /// <param name="cycles">The cycles closed now.</param>
    /// <returns>The new ledger.</returns>
    public HarvestLedger Closing(IReadOnlyList<HarvestedCycle> cycles)
    {
        ArgumentNullException.ThrowIfNull(cycles);

        return this with { Cycles = [.. Cycles, .. cycles.Where(cycle => Find(cycle.Cycle) is null)] };
    }

    /// <summary>
    /// The ledger with what closing cycles' readers raised again folded in (D-221): each (concern, cycle, document,
    /// round) once, so folding the same cycle twice, or a merge carrying one recurrence twice, changes nothing.
    /// </summary>
    /// <param name="recurrences">The recurrences the closing cycles recorded.</param>
    /// <returns>The new ledger.</returns>
    public HarvestLedger Recurring(IEnumerable<HarvestedRecurrence> recurrences)
    {
        ArgumentNullException.ThrowIfNull(recurrences);

        List<HarvestedRecurrence> all = Distinct([.. Recurred ?? [], .. recurrences]);

        return this with { Recurred = all.Count == 0 ? null : all };
    }

    /// <summary>
    /// Every cycle a watched or ignored concern was seen in: those a person answered it for, and those whose readers
    /// raised it again after (D-221). What the bar of three counts.
    /// </summary>
    /// <param name="kind"><see cref="HarvestedConcerns.Ignored"/> or <see cref="HarvestedConcerns.Watch"/>.</param>
    /// <param name="concern">The concern.</param>
    /// <param name="pending">Recurrences not yet folded in: the open cycles' own, which a sitting counts before they close.</param>
    /// <returns>The cycles, each once, a person's first.</returns>
    public IReadOnlyList<string> SeenIn(string kind, Watched concern, IEnumerable<HarvestedRecurrence>? pending = null)
    {
        ArgumentNullException.ThrowIfNull(concern);

        string key = HarvestedConcerns.KeyOf(kind, concern);

        return
        [
            .. concern.Cycles
                .Concat((Recurred ?? []).Concat(pending ?? []).Where(recurrence => string.Equals(recurrence.Key, key, StringComparison.Ordinal)).Select(recurrence => recurrence.Cycle))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    private static List<HarvestedRecurrence> Distinct(IEnumerable<HarvestedRecurrence> recurrences) =>
    [
        .. recurrences
            .GroupBy(recurrence => (recurrence.Key, recurrence.Cycle, recurrence.Document, recurrence.Round))
            .Select(group => group.First()),
    ];

    private static List<Watched> Merged(IReadOnlyList<Watched> held, IReadOnlyList<Watched> added)
    {
        List<Watched> merged = [.. held];

        foreach (Watched item in added)
        {
            int at = merged.FindIndex(known => string.Equals(known.Gist, item.Gist, StringComparison.Ordinal));

            if (at >= 0)
            {
                // A later sitting may sharpen the reason; one that gives none keeps the one already held.
                merged[at] = merged[at] with
                {
                    Cycles = [.. merged[at].Cycles.Union(item.Cycles, StringComparer.Ordinal)],
                    Why = string.IsNullOrWhiteSpace(item.Why) ? merged[at].Why : item.Why,
                };
            }
            else
            {
                merged.Add(item);
            }
        }

        return merged;
    }
}
