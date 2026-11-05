using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using FluentResults;

namespace Sdlc.Core.Evidence;

/// <summary>
/// What a build's coverage says about the change and about the whole codebase (D-103).
/// </summary>
/// <param name="ChangedCoverable">Lines the change added that the report can cover.</param>
/// <param name="ChangedCovered">Of those, the ones a test ran.</param>
/// <param name="Valid">Every coverable line the report measured.</param>
/// <param name="Covered">Of those, the ones a test ran.</param>
/// <param name="Baseline">
/// The whole-codebase line rate recorded at the base commit, or null when this is the first measurement.
/// </param>
public sealed record CoverageSummary(
    [property: JsonPropertyName("changed_coverable")] int ChangedCoverable,
    [property: JsonPropertyName("changed_covered")] int ChangedCovered,
    int Valid,
    int Covered,
    double? Baseline)
{
    /// <summary>The share of changed coverable lines a test ran: what a cycle answers for.</summary>
    public const double ChangedFloor = 0.70;

    /// <summary>How far a whole-codebase rate may sit under the baseline before it counts as a fall.</summary>
    /// <remarks>Rounding, not leniency: a rate is stored to four places.</remarks>
    public const double Tolerance = 0.00005;

    /// <summary>The whole codebase's line rate.</summary>
    [JsonIgnore]
    public double Rate => Valid == 0 ? 1 : (double)Covered / Valid;

    /// <summary>The changed lines' rate, or null when the change added no coverable line.</summary>
    [JsonIgnore]
    public double? ChangedRate => ChangedCoverable == 0 ? null : (double)ChangedCovered / ChangedCoverable;

    /// <summary>Whether the changed lines meet the floor.</summary>
    [JsonIgnore]
    public bool ChangedMeetsFloor => ChangedRate is not { } rate || rate >= ChangedFloor;

    /// <summary>Whether the whole codebase is no less covered than at the base commit.</summary>
    [JsonIgnore]
    public bool HeldTheBaseline => Baseline is not { } baseline || Rate >= baseline - Tolerance;
}

/// <summary>
/// Reads coverage reports and the change, and says what is covered.
/// </summary>
/// <remarks>
/// <strong>Arithmetic, so the binary does it.</strong> Whether a changed line ran is a question a report and
/// a patch answer exactly, which by §2.3 puts it here rather than in a verifier's judgement -- and a number
/// computed here cannot be overclaimed, which is the defect every one of fourteen verify blockers in one
/// cycle was. Cobertura, because nearly every coverage tool on every stack can write it, so this stays a
/// reader of one format rather than a list of runners.
/// </remarks>
public static class Coverage
{
    /// <summary>The evidence file the summary is written to.</summary>
    public const string SummaryFileName = "coverage.json";

    /// <summary>The solution's whole-codebase baseline, carried forward by each cycle that records coverage.</summary>
    public const string BaselineFileName = "coverage.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Reads one or more cobertura reports into hits per line, per file relative to the solution root.
    /// </summary>
    /// <remarks>
    /// A run with several test projects writes a report each, and the same source line can appear in more
    /// than one; a line is covered when any report ran it.
    /// </remarks>
    /// <param name="reports">Each report's text, with what to call it in an error.</param>
    /// <param name="solutionRoot">The root that file names are made relative to.</param>
    /// <returns>Hits per line, per file, or a failure naming the report that could not be read.</returns>
    public static Result<IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>>> Read(
        IReadOnlyList<(string Text, string Source)> reports,
        string solutionRoot)
    {
        ArgumentNullException.ThrowIfNull(reports);

        Dictionary<string, Dictionary<int, int>> lines = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string text, string source) in reports)
        {
            XDocument document;

            try
            {
                document = XDocument.Parse(text);
            }
            catch (XmlException error)
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: not a readable cobertura report. {error.Message}"));
            }

            if (document.Root?.Name.LocalName != "coverage")
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: not a cobertura report -- its root is not <coverage>."));
            }

            List<string> sources = [.. document.Root
                .Elements("sources").Elements("source")
                .Select(element => element.Value.Trim())
                .Where(value => value.Length > 0)];

            foreach (XElement type in document.Root.Descendants("class"))
            {
                string? name = type.Attribute("filename")?.Value;

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string file = Relative(name, sources, solutionRoot);

                // Generated code is not anybody's to cover. A scaffolded api's OpenAPI source generator
                // writes 384 lines under obj/ that no test reaches, which is three quarters of the
                // skeleton's measured lines -- a rate that says nothing about the code a person wrote.
                if (IsBuildOutput(file))
                {
                    continue;
                }

                if (!lines.TryGetValue(file, out Dictionary<int, int>? hits))
                {
                    hits = [];
                    lines[file] = hits;
                }

                foreach (XElement line in type.Elements("lines").Elements("line"))
                {
                    if (int.TryParse(line.Attribute("number")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                        && long.TryParse(line.Attribute("hits")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long count))
                    {
                        int clipped = (int)Math.Min(count, int.MaxValue);
                        hits[number] = hits.TryGetValue(number, out int existing) ? Math.Max(existing, clipped) : clipped;
                    }
                }
            }
        }

        return Result.Ok<IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>>>(
            lines.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<int, int>)pair.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The lines a unified diff adds, per file, by their number in the new file.
    /// </summary>
    /// <param name="patch">The patch.</param>
    /// <returns>Added line numbers, per file path as the patch names it.</returns>
    public static IReadOnlyDictionary<string, IReadOnlySet<int>> AddedLines(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        Dictionary<string, HashSet<int>> added = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int>? current = null;
        int next = 0;

        foreach (string raw in patch.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.StartsWith("+++ ", StringComparison.Ordinal))
            {
                string path = raw[4..].Trim();
                current = null;

                if (path != "/dev/null")
                {
                    path = path.StartsWith("b/", StringComparison.Ordinal) ? path[2..] : path;
                    current = added.TryGetValue(path, out HashSet<int>? existing) ? existing : added[path] = [];
                }

                continue;
            }

            if (raw.StartsWith("@@", StringComparison.Ordinal))
            {
                int plus = raw.IndexOf('+', 2);
                int end = plus < 0 ? -1 : raw.IndexOfAny([',', ' '], plus);
                next = plus >= 0 && end > plus
                    && int.TryParse(raw[(plus + 1)..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)
                        ? start
                        : 0;
                continue;
            }

            if (current is null || raw.StartsWith("--- ", StringComparison.Ordinal))
            {
                continue;
            }

            if (raw.StartsWith('+'))
            {
                current.Add(next++);
            }
            else if (raw.StartsWith(' '))
            {
                next++;
            }
        }

        return added.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<int>)pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Summarises a run's coverage against the change it made.</summary>
    /// <param name="lines">Hits per line, per file.</param>
    /// <param name="patch">The change.</param>
    /// <param name="baseline">The whole-codebase rate at the base commit, or null.</param>
    /// <returns>The summary.</returns>
    public static CoverageSummary Summarise(
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> lines,
        string patch,
        double? baseline)
    {
        ArgumentNullException.ThrowIfNull(lines);

        int coverable = 0;
        int covered = 0;

        foreach ((string file, IReadOnlySet<int> numbers) in AddedLines(patch))
        {
            if (!lines.TryGetValue(file, out IReadOnlyDictionary<int, int>? hits))
            {
                continue;
            }

            foreach (int number in numbers)
            {
                if (hits.TryGetValue(number, out int count))
                {
                    coverable++;
                    covered += count > 0 ? 1 : 0;
                }
            }
        }

        int valid = lines.Values.Sum(file => file.Count);
        int run = lines.Values.Sum(file => file.Values.Count(count => count > 0));

        return new CoverageSummary(coverable, covered, valid, run, baseline);
    }

    /// <summary>Writes a summary as the evidence file.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>Its JSON.</returns>
    public static string Write(CoverageSummary summary) => JsonSerializer.Serialize(summary, _options) + "\n";

    /// <summary>Reads the evidence file back.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The summary, or null when it cannot be read.</returns>
    public static CoverageSummary? ReadSummary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CoverageSummary>(json, _options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The baseline file's text, for a whole-codebase rate.</summary>
    /// <param name="rate">The rate.</param>
    /// <returns>The file's JSON.</returns>
    public static string WriteBaseline(double rate) => string.Create(
        CultureInfo.InvariantCulture,
        $"{{\n  \"line_rate\": {Math.Round(rate, 4):0.0000}\n}}\n");

    /// <summary>Reads the baseline file's rate.</summary>
    /// <param name="json">The file's text, or null when there is none.</param>
    /// <returns>The rate, or null.</returns>
    public static double? ReadBaseline(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("line_rate", out JsonElement rate) && rate.TryGetDouble(out double value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsBuildOutput(string path) => path
        .Split('/')
        .Any(segment => segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("bin", StringComparison.OrdinalIgnoreCase));

    private static string Relative(string name, List<string> sources, string solutionRoot)
    {
        string absolute = Path.IsPathRooted(name)
            ? name
            : sources.Select(source => Path.Combine(source, name)).FirstOrDefault(File.Exists)
                ?? (sources.Count > 0 ? Path.Combine(sources[0], name) : Path.Combine(solutionRoot, name));

        string relative = Path.GetRelativePath(solutionRoot, absolute);

        return (relative.StartsWith("..", StringComparison.Ordinal) ? absolute : relative).Replace('\\', '/');
    }
}
