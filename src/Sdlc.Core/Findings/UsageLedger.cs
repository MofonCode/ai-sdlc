using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>What one recorded round's readers cost.</summary>
/// <param name="Artifact">The document judged.</param>
/// <param name="Round">The round, as the ledger numbered it.</param>
/// <param name="RecordedAt">When the round was recorded.</param>
/// <param name="Host">The host the session named, or null when it named none.</param>
/// <param name="Readers">Each reader the round ran, and what it cost.</param>
public sealed record UsageRound(
    string Artifact,
    int Round,
    [property: JsonPropertyName("recorded_at")] DateTimeOffset RecordedAt,
    string? Host,
    IReadOnlyList<ReaderUsage> Readers);

/// <summary>
/// A cycle's <c>findings/usage.json</c>: what every recorded round's readers cost (D-121).
/// </summary>
/// <remarks>
/// <para><strong>Outside the chain.</strong> D-107 chains what was judged, and cost is not judged, so a
/// host reporting one more field cannot move a hash an approval anchors. Committed, so the factory's cycles
/// and other people's are visible to whoever reads the record.</para>
/// <para><strong>Counts, never prices, and null for what a host does not report.</strong> The weights are
/// applied when shown, because a price stored is wrong the day it is read.</para>
/// </remarks>
/// <param name="Rounds">Every round recorded, oldest first.</param>
public sealed record UsageLedger(IReadOnlyList<UsageRound> Rounds)
{
    /// <summary>The file's name, beside the findings ledgers.</summary>
    public const string FileName = "usage.json";

    // Nulls written rather than omitted: a reader of the file sees "the host did not say" in so many words.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Where a cycle keeps it.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The path.</returns>
    public static string PathOf(Cycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        return Path.Combine(cycle.FindingsDirectory, FileName);
    }

    /// <summary>Reads a cycle's usage, empty when it has none yet.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The record, or a refusal naming a file that is not one.</returns>
    public static async Task<Result<UsageLedger>> Read(Cycle cycle, CancellationToken cancellationToken)
    {
        string path = PathOf(cycle);

        if (!File.Exists(path))
        {
            return Result.Ok(new UsageLedger([]));
        }

        try
        {
            UsageLedger? read = JsonSerializer.Deserialize<UsageLedger>(
                await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                _options);

            return Result.Ok(read is { Rounds: not null } ? read : new UsageLedger([]));
        }
        catch (JsonException error)
        {
            return Result.Fail<UsageLedger>(string.Create(
                CultureInfo.InvariantCulture,
                $"{path}: not a usage record. {error.Message}"));
        }
    }

    /// <summary>Appends one round's readers and writes the file.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="round">The round.</param>
    /// <param name="cancellationToken">Token observed while writing.</param>
    /// <returns>Success, or the refusal reading the file gave.</returns>
    public static async Task<Result> Append(Cycle cycle, UsageRound round, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(round);

        Result<UsageLedger> read = await Read(cycle, cancellationToken).ConfigureAwait(false);

        if (read.IsFailed)
        {
            return read.ToResult();
        }

        UsageLedger appended = new([.. read.Value.Rounds, round]);
        Directory.CreateDirectory(cycle.FindingsDirectory);
        await File.WriteAllTextAsync(PathOf(cycle), JsonSerializer.Serialize(appended, _options) + "\n", cancellationToken)
            .ConfigureAwait(false);

        return Result.Ok();
    }

    /// <summary>The usage a round file carries, as <c>sdlc round collect</c> drafted it.</summary>
    /// <param name="json">The round file's text.</param>
    /// <returns>Its host and readers, or null when it carries none.</returns>
    internal static (string? Host, IReadOnlyList<ReaderUsage> Readers)? InRoundFile(string json)
    {
        try
        {
            RoundFileUsage? carried = JsonSerializer.Deserialize<RoundFileUsage>(json, _options);

            return carried?.Usage is { Count: > 0 } readers ? (carried.Host, readers) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record RoundFileUsage(string? Host, IReadOnlyList<ReaderUsage>? Usage);
}
