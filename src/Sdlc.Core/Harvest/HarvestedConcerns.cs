using System.Security.Cryptography;
using System.Text;
using FluentResults;

namespace Sdlc.Core.Harvest;

/// <summary>
/// One concern a solution has already answered, as the reconciling reader is shown it (D-221): a key that stays the
/// same while the answer does, what kind of answer it was, and the concern in a line.
/// </summary>
/// <param name="Key">
/// <c>&lt;kind&gt;:&lt;eight hex digits&gt;</c> of the line's text, or <c>successor:&lt;slug&gt;</c>: the same text
/// gives the same key on every machine and every branch, so a restatement recorded on one is folded on another.
/// </param>
/// <param name="Kind">How it was answered: <c>ignored</c>, <c>watch</c>, <c>declined</c>, <c>covered</c> or <c>successor</c>.</param>
/// <param name="Gist">The concern, in the words the answer kept.</param>
public sealed record HarvestedConcern(string Key, string Kind, string Gist);

/// <summary>
/// One time a reader raised again a concern the solution had already answered, folded into <c>harvested.json</c> when
/// its cycle closed (D-221).
/// </summary>
/// <param name="Key">The concern's key, as <see cref="HarvestedConcerns"/> made it.</param>
/// <param name="Gist">What the concern was when it recurred, so the file reads without working out a hash.</param>
/// <param name="Cycle">The cycle whose reader raised it.</param>
/// <param name="Document">The document that round judged.</param>
/// <param name="Round">That document's round.</param>
public sealed record HarvestedRecurrence(string Key, string Gist, string Cycle, string Document, int Round);

/// <summary>
/// The concerns a solution has already answered, for the reconciling reader to match a new suggestion against
/// (D-221): what a harvest ignored, watched, declined or covered, and every successor waiting.
/// </summary>
/// <remarks>
/// <para><strong>Grouped as a person answered them.</strong> One sitting may decline eighty suggestions for one reason
/// or cover a hundred with one successor; the reconciler is shown the reason once, not the eighty ids, because the
/// concern is what it matches and the ids are the cycles' own.</para>
/// <para><strong>Keyed by text, never by position.</strong> A sitting that covers a watched concern removes it from the
/// list, so an index would point at its neighbour afterwards; the text a key is made from is the text the ledger keeps.</para>
/// </remarks>
public static class HarvestedConcerns
{
    /// <summary>A concern a sitting ignored (D-153).</summary>
    public const string Ignored = "ignored";

    /// <summary>A concern a sitting watched, below the bar.</summary>
    public const string Watch = "watch";

    /// <summary>A suggestion a sitting declined, by its reason.</summary>
    public const string Declined = "declined";

    /// <summary>A suggestion a sitting covered, by what covers it and how.</summary>
    public const string Covered = "covered";

    /// <summary>A successor waiting to be started.</summary>
    public const string Successor = "successor";

    /// <summary>The key a concern of one kind takes from its text.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="text">The text the ledger keeps for it.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string kind, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(text);

        return kind + ":" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim())))[..8];
    }

    /// <summary>Every concern the ledger and the successors hold, each once.</summary>
    /// <param name="ledger">The harvest ledger.</param>
    /// <param name="successors">Each successor waiting, by slug and headline.</param>
    /// <returns>The concerns, in the ledger's order: ignored, watched, declined, covered, then successors.</returns>
    public static IReadOnlyList<HarvestedConcern> From(HarvestLedger ledger, IEnumerable<(string Slug, string? Headline)> successors)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(successors);

        List<HarvestedConcern> concerns = [];
        HashSet<string> keys = new(StringComparer.Ordinal);

        void Add(string kind, string keyText, string gist)
        {
            if (string.IsNullOrWhiteSpace(keyText))
            {
                return;
            }

            string key = kind == Successor ? Successor + ":" + keyText : KeyOf(kind, keyText);

            if (keys.Add(key))
            {
                concerns.Add(new HarvestedConcern(key, kind, gist.Trim()));
            }
        }

        foreach (Watched concern in ledger.Ignored ?? [])
        {
            Add(Ignored, concern.Gist, concern.Gist);
        }

        foreach (Watched concern in ledger.Watch)
        {
            Add(Watch, concern.Gist, concern.Gist);
        }

        foreach (HarvestDecline decline in ledger.Sittings.SelectMany(sitting => sitting.Declined ?? []))
        {
            Add(Declined, decline.Reason, decline.Reason);
        }

        foreach (HarvestCover cover in ledger.Sittings.SelectMany(sitting => sitting.Covered ?? []))
        {
            Add(Covered, CoverText(cover), "covered by " + cover.By + ": " + cover.Reason);
        }

        foreach ((string slug, string? headline) in successors)
        {
            Add(Successor, slug, headline ?? slug);
        }

        return concerns;
    }

    /// <summary>The key an ignored or watched concern takes, for counting its recurrences.</summary>
    /// <param name="kind"><see cref="Ignored"/> or <see cref="Watch"/>.</param>
    /// <param name="concern">The concern.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string kind, Watched concern)
    {
        ArgumentNullException.ThrowIfNull(concern);

        return KeyOf(kind, concern.Gist);
    }

    /// <summary>Reads a solution's harvested concerns: its ledger and its successors folder.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The concerns, or why the ledger cannot be read.</returns>
    public static async Task<Result<IReadOnlyList<HarvestedConcern>>> Read(string solutionRoot, CancellationToken cancellationToken)
    {
        Result<HarvestLedger> ledger = await HarvestLedger.Read(solutionRoot, cancellationToken).ConfigureAwait(false);

        if (ledger.IsFailed)
        {
            return ledger.ToResult<IReadOnlyList<HarvestedConcern>>();
        }

        List<(string, string?)> successors = [];
        string folder = Sdlc.Core.Successors.Successor.DirectoryOf(solutionRoot);

        if (Directory.Exists(folder))
        {
            foreach (string path in Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal))
            {
                string slug = Path.GetFileNameWithoutExtension(path);

                // A successor that does not parse is still a concern somebody wrote down; its slug says what it is.
                Result<Sdlc.Core.Successors.Successor> parsed = Sdlc.Core.Successors.Successor.Parse(slug, await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
                successors.Add((slug, parsed.IsSuccess ? parsed.Value.Headline : null));
            }
        }

        return Result.Ok(From(ledger.Value, successors));
    }

    private static string CoverText(HarvestCover cover) => cover.By + "\n" + cover.Reason;
}

/// <summary>What one cycle's readers raised again of concerns a harvest already answered (D-221).</summary>
internal static class Recurrences
{
    /// <summary>Reads a cycle's restatements from its own suggestions ledger, as the harvest ledger keeps them.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>Each restatement once, or why the cycle's ledger cannot be read.</returns>
    public static async Task<Result<IReadOnlyList<HarvestedRecurrence>>> Of(Cycles.Cycle cycle, CancellationToken cancellationToken)
    {
        Result<Findings.SuggestionLedger> ledger = await Findings.FindingsReader.ReadSuggestions(cycle, cancellationToken).ConfigureAwait(false);

        return ledger.IsFailed
            ? ledger.ToResult<IReadOnlyList<HarvestedRecurrence>>()
            : Result.Ok<IReadOnlyList<HarvestedRecurrence>>(
            [
                .. ledger.Value.RecurredOnce().Select(item => new HarvestedRecurrence(
                    item.Restates,
                    (item.Gist ?? item.Statement).ReplaceLineEndings(" ").Trim(),
                    cycle.Id.ToString(),
                    item.Artifact,
                    item.Round)),
            ]);
    }
}
