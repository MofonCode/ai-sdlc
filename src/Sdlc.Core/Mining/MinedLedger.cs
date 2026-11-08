using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Mining;

/// <summary>One area a mined cycle's plan proposed, kept so the three-cycle count survives its folder.</summary>
/// <param name="Name">The name the plan proposed.</param>
/// <param name="Files">The files or globs it listed.</param>
public sealed record MinedArea(string Name, IReadOnlyList<string> Files);

/// <summary>A suggestion mining declined, with why, so what became of it can still be said.</summary>
/// <param name="Id">The suggestion's id, as its cycle's ledger held it.</param>
/// <param name="Reason">Why it was declined.</param>
public sealed record MinedDecline(string Id, string Reason);

/// <summary>
/// What one mined cycle leaves in the tree once its folder is deleted (D-152).
/// </summary>
/// <param name="Cycle">The cycle's id.</param>
/// <param name="Merge">
/// The last commit on the integration branch that touched its folder: <c>git show &lt;merge&gt;:.sdlc/cycles/&lt;id&gt;/…</c>
/// recovers any of it, and a decision's <c>From</c> naming the cycle still resolves.
/// </param>
/// <param name="Mined">When it was recorded.</param>
/// <param name="Abandoned">Whether it stopped rather than passed.</param>
/// <param name="Declared">The files its plan declared, for the area replay.</param>
/// <param name="Changed">The files its change touched, for the area replay.</param>
/// <param name="Cited">Every rule its findings cited, once each, so a rule's count across cycles survives.</param>
/// <param name="Areas">The areas its plan proposed.</param>
/// <param name="Rounds">How many rounds each document took.</param>
/// <param name="Reopens">How many times a person took its approval back.</param>
/// <param name="Verdicts">Every outcome its verify reached, oldest first.</param>
public sealed record MinedCycle(
    string Cycle,
    string? Merge,
    DateTimeOffset Mined,
    bool Abandoned,
    IReadOnlyList<string> Declared,
    IReadOnlyList<string> Changed,
    IReadOnlyList<string> Cited,
    IReadOnlyList<MinedArea> Areas,
    IReadOnlyDictionary<string, int> Rounds,
    int Reopens,
    IReadOnlyList<string> Verdicts);

/// <summary>
/// A concern seen in fewer cycles than the bar, carried so its count survives the folders it came from.
/// </summary>
/// <param name="Gist">The concern, in one sentence, as mining wrote it.</param>
/// <param name="Cycles">The cycles it was seen in.</param>
public sealed record Watched(string Gist, IReadOnlyList<string> Cycles);

/// <summary>One mining sitting: the cycles it covered and what the person accepted.</summary>
/// <param name="Mined">When it was recorded.</param>
/// <param name="Cycles">The cycles it recorded.</param>
/// <param name="Accepted">The headings the person marked, as the proposal file wrote them.</param>
/// <param name="Declined">The suggestions it declined, with why.</param>
public sealed record MiningRun(
    DateTimeOffset Mined,
    IReadOnlyList<string> Cycles,
    IReadOnlyList<string> Accepted,
    IReadOnlyList<MinedDecline> Declined);

/// <summary>
/// The mining ledger, <c>.sdlc/mined.json</c>: the only thing a husk leaves in the tree (D-152).
/// </summary>
/// <remarks>
/// <para><strong>Written only by the binary</strong>, by <c>sdlc mine record</c>, and denied to every agent
/// write by the hook, as the suggestions ledger is. It binds nothing; it is what lets a cycle's folder go
/// without what later mining needs going with it: the merge commit to recover anything from, the files the
/// area replay reads, the rules a three-cycle count reads, and the concerns still short of the bar.</para>
/// </remarks>
/// <param name="Cycles">Every mined cycle, in the order recorded.</param>
/// <param name="Watch">Concerns below the bar, each with the cycles it was seen in.</param>
/// <param name="Runs">Every mining sitting recorded.</param>
public sealed record MinedLedger(IReadOnlyList<MinedCycle> Cycles, IReadOnlyList<Watched> Watch, IReadOnlyList<MiningRun> Runs)
{
    /// <summary>The ledger's file name, in the framework's directory.</summary>
    public const string FileName = "mined.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A solution that has mined nothing.</summary>
    public static MinedLedger Empty { get; } = new([], [], []);

    /// <summary>Where a solution's ledger lives.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <returns>The path.</returns>
    public static string PathIn(string solutionRoot) => Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, FileName);

    /// <summary>Whether a cycle is recorded as mined.</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <returns>Its entry, or null.</returns>
    public MinedCycle? Find(string cycle) => Cycles.FirstOrDefault(entry => string.Equals(entry.Cycle, cycle, StringComparison.Ordinal));

    /// <summary>Reads a ledger's text.</summary>
    /// <param name="json">The text.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The ledger, or why it cannot be read.</returns>
    public static Result<MinedLedger> Parse(string json, string source)
    {
        try
        {
            MinedLedger? read = JsonSerializer.Deserialize<MinedLedger>(json, _options);

            return read is null
                ? Result.Fail<MinedLedger>(source + ": the mining ledger is empty.")
                : Result.Ok(read with { Cycles = read.Cycles ?? [], Watch = read.Watch ?? [], Runs = read.Runs ?? [] });
        }
        catch (JsonException error)
        {
            return Result.Fail<MinedLedger>(source + ": the mining ledger does not parse: " + error.Message);
        }
    }

    /// <summary>Reads a solution's ledger; an absent one is empty.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The ledger, or why it cannot be read.</returns>
    public static async Task<Result<MinedLedger>> Read(string solutionRoot, CancellationToken cancellationToken)
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
    /// The ledger with newly mined cycles, a sitting, and watch items added: a concern already watched gains the
    /// new cycles, and a new one is added.
    /// </summary>
    /// <param name="cycles">The cycles mined now.</param>
    /// <param name="run">The sitting.</param>
    /// <param name="watch">The concerns the sitting watched.</param>
    /// <returns>The new ledger.</returns>
    public MinedLedger With(IReadOnlyList<MinedCycle> cycles, MiningRun run, IReadOnlyList<Watched> watch)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(watch);

        List<Watched> merged = [.. Watch];

        foreach (Watched item in watch)
        {
            int at = merged.FindIndex(known => string.Equals(known.Gist, item.Gist, StringComparison.Ordinal));
            merged.Add(item);

            if (at >= 0)
            {
                merged[at] = merged[at] with { Cycles = [.. merged[at].Cycles.Union(item.Cycles, StringComparer.Ordinal)] };
                merged.RemoveAt(merged.Count - 1);
            }
        }

        return this with
        {
            Cycles = [.. Cycles, .. cycles.Where(cycle => Find(cycle.Cycle) is null)],
            Watch = merged,
            Runs = [.. Runs, run],
        };
    }
}
