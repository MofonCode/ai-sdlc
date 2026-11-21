using System.Globalization;
using System.Text;
using Sdlc.Core.Harvest;

namespace Sdlc.FrameworkHarvest;

/// <summary>Several solutions' exports read together, as Markdown for a maintainer.</summary>
/// <remarks>
/// It counts and groups -- what a program can decide. Whether two statements about one surface are one problem is a
/// reader's question, so every statement is printed under its surface rather than merged by wording.
/// </remarks>
internal static class Reading
{
    /// <summary>Renders the reading.</summary>
    /// <param name="exports">Each export, with the file it came from.</param>
    /// <returns>The Markdown.</returns>
    public static string Render(IReadOnlyList<(string Source, FrameworkExport Export)> exports)
    {
        StringBuilder text = new();
        List<SentCycle> cycles = [.. exports.SelectMany(entry => entry.Export.Cycles)];
        int notes = exports.Sum(entry => entry.Export.Notes.Count);

        text.AppendLine("# What solutions handed the framework");
        text.AppendLine();
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{exports.Count} export{Plural(exports.Count)}, {notes} note{Plural(notes)}, {cycles.Count} closed cycle{Plural(cycles.Count)}. Exported by {string.Join(", ", exports.Select(entry => entry.Export.Release).Distinct(StringComparer.Ordinal))}."));

        Notes(text, exports);
        Releases(text, cycles);
        Readers(text, cycles);
        return text.ToString();
    }

    // By surface, the one most solutions raised first: a note several solutions wrote is the framework's, not a habit.
    private static void Notes(StringBuilder text, IReadOnlyList<(string Source, FrameworkExport Export)> exports)
    {
        IEnumerable<IGrouping<string, (string Source, SentNote Note)>> bySurface = exports
            .SelectMany(entry => entry.Export.Notes.Select(note => (entry.Source, Note: note)))
            .GroupBy(entry => entry.Note.Surface, StringComparer.Ordinal)
            .OrderByDescending(group => group.Select(entry => entry.Source).Distinct(StringComparer.Ordinal).Count())
            .ThenByDescending(group => group.Sum(entry => entry.Note.Cycles))
            .ThenBy(group => group.Key, StringComparer.Ordinal);

        text.AppendLine();
        text.AppendLine("## Notes, by surface");

        foreach (IGrouping<string, (string Source, SentNote Note)> surface in bySurface)
        {
            int solutions = surface.Select(entry => entry.Source).Distinct(StringComparer.Ordinal).Count();

            text.AppendLine();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"### {surface.Key} — {solutions} export{Plural(solutions)}, {surface.Sum(entry => entry.Note.Cycles)} cycles"));

            foreach ((string source, SentNote note) in surface)
            {
                string releases = note.Releases.Count == 0 ? "no release recorded" : string.Join(", ", note.Releases);
                text.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"- **{KindName(note.Kind)}** {note.Statement} _({source}; {note.Cycles} cycle{Plural(note.Cycles)} on {releases}{(note.Measure is null ? string.Empty : "; " + note.Measure)})_"));
            }
        }
    }

    // By the newest release each cycle ran on: a cost that falls away after a release is one that release fixed.
    private static void Releases(StringBuilder text, List<SentCycle> cycles)
    {
        if (cycles.Count == 0)
        {
            return;
        }

        string[] documents = ["intent", "spec", "plan", "verify"];

        text.AppendLine();
        text.AppendLine("## Cycles, by the release they ran on");
        text.AppendLine();
        text.AppendLine("| Release | Cycles | Abandoned | First verdict passed | " + string.Join(" | ", documents.Select(document => "Rounds: " + document)) + " | Weighted tokens a cycle |");
        text.AppendLine("|---|---|---|---|" + string.Concat(documents.Select(_ => "---|")) + "---|");

        foreach (IGrouping<string, SentCycle> release in cycles
            .GroupBy(cycle => cycle.Releases.Count == 0 ? "none recorded" : cycle.Releases[^1], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            List<SentCycle> ran = [.. release];
            List<SentCycle> judged = [.. ran.Where(cycle => cycle.Verdicts.Count > 0)];
            List<SentCycle> costed = [.. ran.Where(cycle => cycle.Spent is { Count: > 0 })];

            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {release.Key} | {ran.Count} | {ran.Count(cycle => cycle.Abandoned)} | {(judged.Count == 0 ? "—" : judged.Count(cycle => cycle.Verdicts[0] == "pass") + " of " + judged.Count)} | {string.Join(" | ", documents.Select(document => Mean(ran, cycle => cycle.Rounds.TryGetValue(document, out int rounds) ? rounds : null)))} | {(costed.Count == 0 ? "—" : Tokens(costed.Average(cycle => cycle.Spent!.Values.Sum())))} |"));
        }
    }

    // A reader whose findings are rarely cited, often withdrawn, or rarely blocking is the case for tuning it.
    private static void Readers(StringBuilder text, List<SentCycle> cycles)
    {
        List<ReaderYield> yields = [.. cycles.SelectMany(cycle => cycle.Readers)];

        if (yields.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("## Readers, every cycle");
        text.AppendLine();
        text.AppendLine("| Reader | Cycles | Raised | Raised a cycle | Ever blocking | Cited a rule | Withdrawn |");
        text.AppendLine("|---|---|---|---|---|---|---|");

        foreach (IGrouping<string, (SentCycle Cycle, ReaderYield Yield)> reader in cycles
            .SelectMany(cycle => cycle.Readers.Select(yield => (Cycle: cycle, Yield: yield)))
            .GroupBy(entry => entry.Yield.Reader, StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(entry => entry.Yield.Raised)))
        {
            int raised = reader.Sum(entry => entry.Yield.Raised);
            int seen = reader.Select(entry => entry.Cycle).Distinct().Count();

            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {reader.Key} | {seen} | {raised} | {(double)raised / seen:0.#} | {Share(reader.Sum(entry => entry.Yield.Blocking), raised)} | {Share(reader.Sum(entry => entry.Yield.Cited), raised)} | {Share(reader.Sum(entry => entry.Yield.Withdrawn), raised)} |"));
        }
    }

    private static string Mean(List<SentCycle> cycles, Func<SentCycle, int?> value)
    {
        List<int> values = [.. cycles.Select(value).OfType<int>()];
        return values.Count == 0 ? "—" : values.Average().ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string Share(int part, int whole) =>
        whole == 0 ? "—" : ((double)part / whole).ToString("0%", CultureInfo.InvariantCulture);

    private static string Tokens(double weighted) =>
        weighted >= 1_000_000
            ? (weighted / 1_000_000).ToString("0.0", CultureInfo.InvariantCulture) + "M"
            : (weighted / 1_000).ToString("0", CultureInfo.InvariantCulture) + "k";

    private static string KindName(FrameworkNoteKind kind) => kind switch
    {
        FrameworkNoteKind.Cost => "cost",
        FrameworkNoteKind.Missing => "missing",
        _ => "defect",
    };

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
