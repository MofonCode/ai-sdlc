using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Harvest;

/// <summary>One note for the framework as it may leave the solution: what fell short, with its evidence left behind.</summary>
/// <param name="Surface">The part of the framework it is about.</param>
/// <param name="Kind">What fell short.</param>
/// <param name="Statement">What fell short, in words that need no knowledge of the solution.</param>
/// <param name="Measure">The numbers behind it.</param>
/// <param name="Cycles">How many cycles it was seen in.</param>
/// <param name="Releases">The releases those cycles ran on.</param>
public sealed record SentNote(string Surface, FrameworkNoteKind Kind, string Statement, string? Measure, int Cycles, IReadOnlyList<string> Releases);

/// <summary>One closed cycle as it may leave the solution: its numbers, without its id, files, commits or areas.</summary>
/// <param name="Abandoned">Whether it stopped rather than passed.</param>
/// <param name="Releases">The releases its rounds recorded.</param>
/// <param name="Rounds">How many rounds each document took.</param>
/// <param name="Reopens">How many times a person took its approval back.</param>
/// <param name="Verdicts">Every outcome its verify reached, oldest first.</param>
/// <param name="Cited">The framework's own rules its findings cited: the solution's are its own business.</param>
/// <param name="Readers">What each reader raised on each document.</param>
/// <param name="Spent">Weighted tokens per document.</param>
public sealed record SentCycle(
    bool Abandoned,
    IReadOnlyList<string> Releases,
    IReadOnlyDictionary<string, int> Rounds,
    int Reopens,
    IReadOnlyList<string> Verdicts,
    IReadOnlyList<string> Cited,
    IReadOnlyList<ReaderYield> Readers,
    IReadOnlyDictionary<string, double>? Spent);

/// <summary>
/// What of a solution's harvests may be handed to the framework (D-204): every note without its evidence, and every
/// closed cycle's numbers. Nothing in it names the solution, a file, a commit, or a rule of the solution's own.
/// </summary>
/// <param name="Release">The release that printed it, which is also what says how to read it.</param>
/// <param name="Notes">Every note the solution's sittings recorded.</param>
/// <param name="Cycles">Every closed cycle's numbers.</param>
public sealed record FrameworkExport(string Release, IReadOnlyList<SentNote> Notes, IReadOnlyList<SentCycle> Cycles)
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>The export as a file holds it: what a person hands the framework.</summary>
    /// <returns>Its JSON.</returns>
    public string Write() => JsonSerializer.Serialize(this, _options) + "\n";

    /// <summary>Reads an export a solution handed over.</summary>
    /// <param name="json">The text.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The export, or why it cannot be read.</returns>
    public static Result<FrameworkExport> Parse(string json, string source)
    {
        try
        {
            FrameworkExport? read = JsonSerializer.Deserialize<FrameworkExport>(json, _options);

            return read is null
                ? Result.Fail<FrameworkExport>(source + ": the export is empty.")
                : Result.Ok(read with { Notes = read.Notes ?? [], Cycles = read.Cycles ?? [] });
        }
        catch (JsonException error)
        {
            return Result.Fail<FrameworkExport>(source + ": the export does not parse: " + error.Message);
        }
    }
}

/// <summary>
/// Prints what of a solution's harvests may be handed to the framework, and writes it to a file when asked (D-204).
/// Sends nothing: a person reads it and decides.
/// </summary>
public static class ExportFramework
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddExportFrameworkRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, FrameworkExport>, Handler>();
    }

    /// <summary>Asks for the export.</summary>
    public sealed record Query : IRequest<FrameworkExport>;

    /// <summary>Builds the export from the two files a harvest keeps.</summary>
    /// <param name="notes">The framework's notes.</param>
    /// <param name="ledger">The harvest ledger.</param>
    /// <param name="release">The release printing it.</param>
    /// <returns>The export.</returns>
    public static FrameworkExport Of(FrameworkHarvest notes, HarvestLedger ledger, string release)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(ledger);

        return new FrameworkExport(
            release,
            [.. notes.Notes.Select(note => new SentNote(note.Surface, note.Kind, note.Statement, note.Measure, note.Cycles.Count, note.Releases))],
            [.. ledger.Cycles.Select(cycle => new SentCycle(
                cycle.Abandoned,
                cycle.Releases ?? [],
                cycle.Rounds,
                cycle.Reopens,
                cycle.Verdicts,
                [.. cycle.Cited.Where(id => id.StartsWith("CON_", StringComparison.Ordinal) || id.StartsWith("PRO_", StringComparison.Ordinal))],
                cycle.Readers ?? [],
                cycle.Spent))]);
    }

    /// <summary>The export, for the person deciding whether to hand it over.</summary>
    /// <param name="export">The export.</param>
    /// <param name="written">Where it was written, or null when it was only printed.</param>
    /// <returns>The text.</returns>
    public static string Render(FrameworkExport export, string? written)
    {
        ArgumentNullException.ThrowIfNull(export);

        StringBuilder text = new();

        if (export.Notes.Count == 0 && export.Cycles.Count == 0)
        {
            text.AppendLine("Nothing for the framework yet: no sitting wrote a note under '## For the framework', and no cycle is closed.");
            return text.ToString();
        }

        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"For the framework: {export.Notes.Count} note{(export.Notes.Count == 1 ? string.Empty : "s")}, {export.Cycles.Count} closed cycle{(export.Cycles.Count == 1 ? string.Empty : "s")}. This is all that would leave the solution."));

        foreach (SentNote note in export.Notes)
        {
            text.AppendLine();
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {note.Surface} [{KindName(note.Kind)}] {note.Statement}"));
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    {note.Cycles} cycle{(note.Cycles == 1 ? string.Empty : "s")}{(note.Releases.Count == 0 ? string.Empty : " on " + string.Join(", ", note.Releases))}{(note.Measure is null ? string.Empty : "; " + note.Measure)}"));
        }

        if (export.Cycles.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("  Closed cycles, numbers only:");

            foreach (SentCycle cycle in export.Cycles)
            {
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"    {(cycle.Abandoned ? "abandoned" : "passed")} on {(cycle.Releases.Count == 0 ? "no recorded release" : string.Join(", ", cycle.Releases))}: rounds {string.Join(", ", cycle.Rounds.Select(round => round.Key + " " + round.Value))}; verdicts {(cycle.Verdicts.Count == 0 ? "none" : string.Join(" -> ", cycle.Verdicts))}; {cycle.Readers.Sum(reader => reader.Raised)} findings raised"));
            }
        }

        text.AppendLine();
        text.AppendLine(written is null
            ? "Sends nothing. 'sdlc harvest framework --out <file>' writes it as a file to hand to the framework's maintainers."
            : "Written to " + written + ". Sends nothing: hand it over if you choose to.");
        return text.ToString();
    }

    private static string KindName(FrameworkNoteKind kind) => kind switch
    {
        FrameworkNoteKind.Cost => "cost",
        FrameworkNoteKind.Missing => "missing",
        _ => "defect",
    };

    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, FrameworkExport>
    {
        public async Task<Result<FrameworkExport>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Result<FrameworkHarvest> notes = await FrameworkHarvest.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            if (notes.IsFailed)
            {
                return notes.ToResult<FrameworkExport>();
            }

            Result<HarvestLedger> ledger = await HarvestLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            return ledger.IsFailed
                ? ledger.ToResult<FrameworkExport>()
                : Result.Ok(Of(notes.Value, ledger.Value, global::Sdlc.Core.Release.Version));
        }
    }
}
