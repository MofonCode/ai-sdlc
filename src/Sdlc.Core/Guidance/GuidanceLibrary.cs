using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Guidance;

/// <summary>
/// The guidance a solution plans with: the framework's entries, embedded in the binary, and the solution's own under
/// <c>.sdlc/guidance/</c>, which win by name (D-195).
/// </summary>
/// <remarks>
/// Giving the framework an entry is adding a file under <c>Guidance/</c>, as giving it a perspective is: the
/// resource names are the list. Giving a solution one is adding a file under <c>.sdlc/guidance/</c>.
/// </remarks>
public static class GuidanceLibrary
{
    /// <summary>The directory under <c>.sdlc/</c> a solution's own entries live in.</summary>
    public const string DirectoryName = "guidance";

    private const string Prefix = "Sdlc.Core.Guidance.";

    /// <summary>Every entry in force for a solution, by name: the solution's where it wrote one, the framework's otherwise.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The entries, or what is wrong with a document.</returns>
    public static async Task<Result<IReadOnlyList<GuidanceEntry>>> All(string solutionRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solutionRoot);

        Dictionary<string, GuidanceEntry> byName = new(StringComparer.OrdinalIgnoreCase);
        List<IError> errors = [];

        foreach (string resource in typeof(GuidanceLibrary).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal))
        {
            using Stream stream = typeof(GuidanceLibrary).Assembly.GetManifestResourceStream(resource)!;
            using StreamReader reader = new(stream);
            Add(GuidanceEntry.Read(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false), resource[Prefix.Length..], solution: false));
        }

        string own = Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DirectoryName);

        if (Directory.Exists(own))
        {
            foreach (string file in Directory.EnumerateFiles(own, "*.md").Order(StringComparer.Ordinal))
            {
                Add(GuidanceEntry.Read(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), file, solution: true));
            }
        }

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<GuidanceEntry>>(errors)
            : Result.Ok<IReadOnlyList<GuidanceEntry>>([.. byName.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal)]);

        // The framework's are read first, so a solution's entry of the same name replaces it.
        void Add(Result<GuidanceEntry> read)
        {
            if (read.IsFailed)
            {
                errors.AddRange(read.Errors);
                return;
            }

            byName[read.Value.Name] = read.Value;
        }
    }

    /// <summary>The entries a set of declared files switches on.</summary>
    /// <param name="entries">The entries in force.</param>
    /// <param name="declared">The plan's declared files.</param>
    /// <returns>Each entry with the first declared file that switched it on.</returns>
    public static IReadOnlyList<(GuidanceEntry Entry, string File)> For(IReadOnlyList<GuidanceEntry> entries, IReadOnlyList<string> declared)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(declared);

        return [.. entries
            .Select(entry => (Entry: entry, File: declared.FirstOrDefault(entry.Matches)))
            .Where(match => match.File is not null)
            .Select(match => (match.Entry, match.File!))];
    }

    /// <summary>Renders the list: one line per entry, with where it came from and what switches it on.</summary>
    /// <param name="entries">The entries in force.</param>
    /// <returns>The text.</returns>
    public static string RenderList(IReadOnlyList<GuidanceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        StringBuilder text = new();
        text.AppendLine("Guidance a plan is handed when it declares a matching file. It advises and never blocks; the solution's");
        text.AppendLine("own pipeline, decisions and constraints come first.");
        text.AppendLine();

        foreach (GuidanceEntry entry in entries)
        {
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {entry.Name,-20} {(entry.Solution ? "this solution's" : "the framework's"),-16} {entry.Title}  -- on {string.Join(", ", entry.Files)}"));
        }

        if (entries.Count == 0)
        {
            text.AppendLine("  none");
        }

        return text.ToString();
    }

    /// <summary>Renders the entries a cycle's plan switches on, in full, for the planner to read.</summary>
    /// <param name="matched">Each entry with the declared file that switched it on.</param>
    /// <returns>The text.</returns>
    public static string RenderFor(IReadOnlyList<(GuidanceEntry Entry, string File)> matched)
    {
        ArgumentNullException.ThrowIfNull(matched);

        if (matched.Count == 0)
        {
            return "No guidance applies: none of the plan's declared files switches an entry on." + Environment.NewLine;
        }

        StringBuilder text = new();
        text.AppendLine("Guidance for this plan. A push, not a rule: where the solution already does it another way, its way stands.");

        foreach ((GuidanceEntry entry, string file) in matched)
        {
            text.AppendLine();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"--- {entry.Name} ({(entry.Solution ? "this solution's" : "the framework's")}), because the plan declares {file} ---"));
            text.AppendLine();
            text.AppendLine(entry.Text);
        }

        return text.ToString();
    }
}
