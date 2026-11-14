using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Sdlc.Core.Evidence;

/// <summary>One test and how long it took.</summary>
/// <param name="Name">The name the runner reported.</param>
/// <param name="Seconds">How long it ran.</param>
public sealed record TimedTest(string Name, double Seconds);

/// <summary>One results file's span.</summary>
/// <param name="File">The name it is kept under in the evidence directory.</param>
/// <param name="StartedAt">When its run started, or null when the file does not say.</param>
/// <param name="FinishedAt">When its run finished, or null when the file does not say.</param>
/// <param name="Tests">How many results it holds.</param>
public sealed record TimedFile(string File, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, int Tests);

/// <summary>
/// How long a recorded run took, and where the time went (D-169).
/// </summary>
/// <remarks>
/// <para><strong>For diagnosis, never for judgement.</strong> No reader is handed it and the evidence hash leaves
/// it out: a duration is a fact about the machine as much as the change, and D-167 keeps it out of every proof.
/// It exists because a team asking where a 2.5-hour run goes otherwise spends hours measuring what every
/// results file already carried.</para>
/// <para><strong>Committed, and small.</strong> The run's start and finish, each results file's span, and the
/// slowest tests only, so cycles can be compared after the results files themselves are gone.</para>
/// </remarks>
/// <param name="StartedAt">When the earliest results file's run started.</param>
/// <param name="FinishedAt">When the latest one finished.</param>
/// <param name="Files">Each results file's span, in the order recorded.</param>
/// <param name="Slowest">The slowest tests, slowest first.</param>
public sealed record RunTimings(
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<TimedFile> Files,
    IReadOnlyList<TimedTest> Slowest)
{
    /// <summary>The file's name in the evidence directory.</summary>
    public const string FileName = "timings.json";

    /// <summary>How many of the slowest tests are kept.</summary>
    public const int SlowestKept = 25;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>From the first start to the last finish, or null when either is unknown.</summary>
    [JsonIgnore]
    public TimeSpan? Wall => StartedAt is { } start && FinishedAt is { } finish ? finish - start : null;

    /// <summary>
    /// Reads the times every results file carries, or null when none carries any.
    /// </summary>
    /// <param name="files">Each results file's contents, and the name it is kept under.</param>
    /// <returns>The timings, or null.</returns>
    public static RunTimings? FromTrx(IReadOnlyList<(string Xml, string File)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        List<TimedFile> spans = [];
        List<TimedTest> tests = [];

        foreach ((string xml, string file) in files)
        {
            XDocument document;

            try
            {
                document = XDocument.Parse(xml);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            List<XElement> results = [.. document.Descendants().Where(element => element.Name.LocalName == "UnitTestResult")];
            XElement? times = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Times");

            // The run's own times when the file has them; otherwise the span of its results.
            DateTimeOffset? start = Moment(times?.Attribute("start")?.Value)
                ?? results.Select(result => Moment(result.Attribute("startTime")?.Value)).Where(moment => moment is not null).Min();
            DateTimeOffset? finish = Moment(times?.Attribute("finish")?.Value)
                ?? results.Select(result => Moment(result.Attribute("endTime")?.Value)).Where(moment => moment is not null).Max();

            spans.Add(new TimedFile(file, start, finish, results.Count));

            foreach (XElement result in results)
            {
                if (TimeSpan.TryParse(result.Attribute("duration")?.Value, CultureInfo.InvariantCulture, out TimeSpan duration))
                {
                    tests.Add(new TimedTest(result.Attribute("testName")?.Value ?? "(unnamed)", Math.Round(duration.TotalSeconds, 3)));
                }
            }
        }

        if (tests.Count == 0 && spans.TrueForAll(span => span.StartedAt is null && span.FinishedAt is null))
        {
            return null;
        }

        return new RunTimings(
            spans.Select(span => span.StartedAt).Where(moment => moment is not null).Min(),
            spans.Select(span => span.FinishedAt).Where(moment => moment is not null).Max(),
            spans,
            [.. tests.OrderByDescending(test => test.Seconds).ThenBy(test => test.Name, StringComparer.Ordinal).Take(SlowestKept)]);
    }

    /// <summary>Writes the timings as JSON.</summary>
    /// <param name="timings">What to write.</param>
    /// <returns>The JSON, ending in a newline.</returns>
    public static string Write(RunTimings timings) => JsonSerializer.Serialize(timings, _options) + "\n";

    /// <summary>Reads timings back, or null when the text is not timings.</summary>
    /// <param name="json">The file's contents.</param>
    /// <returns>The timings, or null.</returns>
    public static RunTimings? Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<RunTimings>(json, _options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A duration as a person reads it: <c>2h 31m</c>, <c>4m 12s</c>, <c>3.4s</c>.</summary>
    /// <param name="span">The duration.</param>
    /// <returns>The text.</returns>
    public static string Say(TimeSpan span) => span switch
    {
        { TotalHours: >= 1 } => string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}h {span.Minutes}m"),
        { TotalMinutes: >= 1 } => string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m {span.Seconds}s"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{span.TotalSeconds:0.#}s"),
    };

    private static DateTimeOffset? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset moment) ? moment : null;
}
