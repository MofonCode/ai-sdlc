using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>What one reader cost, as its host reported it (D-121).</summary>
/// <remarks>
/// <strong>A number the host did not report is null, never 0.</strong> Zero is a measurement, and a host
/// that does not say how many tokens it cached has not said none.
/// </remarks>
/// <param name="Role">Which reader: <c>rules</c>, a perspective's name, <c>reconcile</c> or <c>adjudicate</c>.</param>
/// <param name="Model">The model the host named.</param>
/// <param name="Effort">The effort it ran at, when the host says.</param>
/// <param name="PromptBytes">The size of the prompt it was handed.</param>
/// <param name="Input">Input tokens not read from or written to a cache.</param>
/// <param name="CacheWrite">Input tokens written to a cache.</param>
/// <param name="CacheRead">Input tokens read from a cache.</param>
/// <param name="Output">Output tokens, thinking included.</param>
/// <param name="Turns">Model turns the process took.</param>
/// <param name="Seconds">Wall-clock seconds.</param>
public sealed record ReaderUsage(
    string Role,
    string? Model,
    string? Effort,
    [property: JsonPropertyName("prompt_bytes")] long? PromptBytes,
    long? Input,
    [property: JsonPropertyName("cache_write")] long? CacheWrite,
    [property: JsonPropertyName("cache_read")] long? CacheRead,
    long? Output,
    int? Turns,
    double? Seconds);

/// <summary>One reader's answer: the text it returned, and what it cost.</summary>
/// <param name="Reader">Which reader.</param>
/// <param name="Text">What it returned.</param>
/// <param name="Usage">What it cost.</param>
public sealed record ReaderAnswer(string Reader, string Text, ReaderUsage Usage);

/// <summary>One finding as a reader wrote it, before anything is checked.</summary>
/// <remarks>
/// Every field optional, because a reader's answer is untrusted text: a <see cref="Finding"/> would default
/// a missing severity to blocking, and a reader that forgot to say would read as one that blocked.
/// </remarks>
internal sealed record ReaderFinding(
    string? Id,
    string? Severity,
    Citation? Cites,
    string? Statement,
    string? Locus,
    string? Remedy,
    string? Gist);

/// <summary>What one criterion or outcome came to, under one rule judged item by item (D-119).</summary>
/// <param name="Rule">The rule's id.</param>
/// <param name="Item">The criterion's or outcome's number.</param>
/// <param name="Met">Whether the document satisfies the rule for it.</param>
/// <param name="Why">The reason, in the reader's words.</param>
public sealed record ReaderRow(string? Rule, int? Item, bool? Met, string? Why);

/// <summary>A reader's whole answer body.</summary>
internal sealed record ReaderBody(
    IReadOnlyList<ReaderFinding>? Findings,
    IReadOnlyList<Conflict>? Conflicts,
    IReadOnlyList<ReaderRow>? Rows);

/// <summary>One reader's answer as findings.</summary>
/// <param name="Findings">What it raised, ids normalised.</param>
/// <param name="Conflicts">The conflicts it declared.</param>
/// <param name="Rows">Its row per criterion or outcome, under each rule judged item by item.</param>
/// <param name="Demoted">A perspective's blockers made suggestions, because they cite no rule it lists.</param>
internal sealed record ReadBody(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<Conflict> Conflicts,
    IReadOnlyList<ReaderRow> Rows,
    IReadOnlyList<string> Demoted);

/// <summary>A reconciler's answer: restatements and pairs (D-108).</summary>
internal sealed record ReconcilerBody(
    IReadOnlyList<Restatement>? Restatements,
    IReadOnlyList<Pair>? Pairs);

/// <summary>One finding of this round that restates one that stands.</summary>
/// <param name="Finding">This round's id.</param>
/// <param name="Standing">The id that stands.</param>
internal sealed record Restatement(string? Finding, string? Standing);

/// <summary>Two findings of this round that cannot both be answered.</summary>
/// <param name="Between">The two ids.</param>
/// <param name="Why">Why each remedy excludes the other.</param>
internal sealed record Pair(IReadOnlyList<string>? Between, string? Why);

/// <summary>
/// Where a round's prompts and answers live, and how an answer is read back (D-117).
/// </summary>
/// <remarks>
/// <para>One directory per round, outside the repository: <c>&lt;reader&gt;.prompt.md</c> is what the
/// binary wrote and <c>&lt;reader&gt;.json</c> what the reader returned. A naming convention rather than a
/// list of paths on the command line, so the launch line is one loop over a glob on every shell.</para>
/// <para><strong>The answer is read in either of two shapes.</strong> Claude Code's
/// <c>--output-format json</c> wraps the reader's text in a result object carrying its usage; any other
/// host's plain text is the answer itself, with nothing known about its cost. The binary never names a
/// host to tell them apart (D-118): it looks at what the file holds.</para>
/// </remarks>
public static class ReaderAnswers
{
    /// <summary>The rules reader.</summary>
    public const string Rules = "rules";

    /// <summary>The reconciling reader.</summary>
    public const string Reconcile = "reconcile";

    /// <summary>The verify's adjudicator.</summary>
    public const string Adjudicate = "adjudicate";

    /// <summary>
    /// The verify round's diff reader: the change itself judged against every rule whose subject is the
    /// diff, the plan and the decisions (D-139).
    /// </summary>
    public const string Diff = "diff";

    /// <summary>
    /// Whether a finding is the diff reader's: about the change itself, so answered by changing the code or
    /// the plan rather than by a new adjudication (D-139).
    /// </summary>
    /// <param name="finding">The finding.</param>
    /// <returns>True when its id is under the diff reader's prefix.</returns>
    public static bool IsAboutTheChange(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return finding.Id.StartsWith(Diff + ".", StringComparison.Ordinal);
    }

    /// <summary>
    /// Every reader a round runs blind, in order: the rules reader, the diff reader on a verify, then each
    /// written perspective.
    /// </summary>
    /// <param name="artifact">The artifact being judged.</param>
    /// <param name="perspectives">The active perspectives that have a document.</param>
    /// <returns>The readers' names.</returns>
    public static IReadOnlyList<string> Blind(ArtifactKind artifact, IEnumerable<string> perspectives) =>
        [Rules, .. artifact == ArtifactKind.Verify ? (string[])[Diff] : [], .. perspectives];

    /// <summary>The round file collect drafts in the round's directory.</summary>
    public const string RoundFileName = "round.json";

    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Where one reader's prompt is written.</summary>
    /// <param name="directory">The round's directory.</param>
    /// <param name="reader">The reader.</param>
    /// <returns>The path.</returns>
    public static string PromptPath(string directory, string reader) => Path.Combine(directory, reader + ".prompt.md");

    /// <summary>Where one reader's answer is expected.</summary>
    /// <param name="directory">The round's directory.</param>
    /// <param name="reader">The reader.</param>
    /// <returns>The path.</returns>
    public static string AnswerPath(string directory, string reader) => Path.Combine(directory, reader + ".json");

    /// <summary>Reads one reader's answer and what it cost.</summary>
    /// <param name="directory">The round's directory.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The answer, or a refusal naming what is wrong with the file.</returns>
    public static async Task<Result<ReaderAnswer>> Read(string directory, string reader, CancellationToken cancellationToken)
    {
        string path = AnswerPath(directory, reader);

        if (!File.Exists(path))
        {
            return Result.Fail<ReaderAnswer>(string.Create(
                CultureInfo.InvariantCulture,
                $"There is no answer from the {reader} reader at {path}. Start it with {Path.GetFileName(PromptPath(directory, reader))} piped in and its output written there."));
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        string prompt = PromptPath(directory, reader);
        long? bytes = File.Exists(prompt) ? new FileInfo(prompt).Length : null;

        return Parse(reader, content, bytes, path);
    }

    /// <summary>Reads an answer from its text, in either shape.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="content">What the file holds.</param>
    /// <param name="promptBytes">The size of the prompt it was handed, when known.</param>
    /// <param name="source">What to call the file in a message.</param>
    /// <returns>The answer.</returns>
    public static Result<ReaderAnswer> Parse(string reader, string content, long? promptBytes, string source)
    {
        ArgumentNullException.ThrowIfNull(content);

        ReaderUsage unknown = new(reader, null, null, promptBytes, null, null, null, null, null, null);

        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { AllowTrailingCommas = true });
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out JsonElement type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != "result")
            {
                return Result.Ok(new ReaderAnswer(reader, content, unknown));
            }

            if (root.TryGetProperty("is_error", out JsonElement failed) && failed.ValueKind == JsonValueKind.True)
            {
                return Result.Fail<ReaderAnswer>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: the {reader} reader's process ended in an error ({Text(root, "subtype") ?? "no reason given"}). Start it again."));
            }

            string? text = Text(root, "result");

            if (text is null)
            {
                return Result.Fail<ReaderAnswer>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: the {reader} reader's result holds no text. Start it again."));
            }

            return Result.Ok(new ReaderAnswer(reader, text, UsageOf(root, unknown)));
        }
        catch (JsonException)
        {
            // Not JSON at all: a host writing plain text, which is the answer itself.
            return Result.Ok(new ReaderAnswer(reader, content, unknown));
        }
    }

    /// <summary>The JSON object a reader's text carries, fenced or bare.</summary>
    /// <remarks>
    /// Readers are told to answer with JSON only and mostly do, but a fence around it or a sentence before
    /// it is common enough that refusing it would only cost a rerun. Anything else is refused: a reader
    /// that answered in prose has not answered the question it was asked.
    /// </remarks>
    /// <typeparam name="T">The shape expected.</typeparam>
    /// <param name="answer">The answer.</param>
    /// <returns>The body, or a refusal saying the reader should be started again.</returns>
    internal static Result<T> Body<T>(ReaderAnswer answer)
        where T : class
    {
        string text = answer.Text;
        int fence = text.IndexOf("```json", StringComparison.Ordinal);

        if (fence >= 0)
        {
            int close = text.IndexOf("```", fence + 7, StringComparison.Ordinal);
            text = close > fence ? text[(fence + 7)..close] : text[(fence + 7)..];
        }

        int start = text.IndexOf('{', StringComparison.Ordinal);
        int end = text.LastIndexOf('}');

        if (start < 0 || end < start)
        {
            return Refuse<T>(answer, "holds no JSON object");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(text[start..(end + 1)], Options) is { } body
                ? Result.Ok(body)
                : Refuse<T>(answer, "holds an empty answer");
        }
        catch (JsonException error)
        {
            return Refuse<T>(answer, "is not the JSON asked for: " + error.Message);
        }
    }

    /// <summary>
    /// A reader's findings as findings: checked for shape, and a perspective's ids put under its name.
    /// </summary>
    /// <remarks>
    /// <para>A perspective's ids are its own, so they are put under its name whatever prefix it wrote, and
    /// <c>perspective</c> is set on each: the field is what <c>propose constraints</c> counts by (D-90).</para>
    /// <para><strong>A perspective's blocker citing a rule it does not list is a suggestion.</strong> Only a
    /// rule the perspective lists may block on its account, and this applies that, rather than refusing an
    /// answer whose finding is otherwise sound and paying for a rerun to hear it again.</para>
    /// </remarks>
    /// <param name="answer">The answer.</param>
    /// <param name="perspective">The perspective that read, or null for the rules reader.</param>
    /// <returns>The findings, conflicts and rows, and the ids made suggestions; or a refusal.</returns>
    internal static Result<ReadBody> FindingsOf(ReaderAnswer answer, Perspectives.Perspective? perspective) =>
        FindingsOf(answer, perspective, prefix: null);

    /// <summary>One reader's findings, by the reader's name: its perspective, or its own prefix.</summary>
    /// <remarks>
    /// The diff reader's ids go under <c>diff.</c>, as a perspective's go under its name, so they cannot
    /// collide with the rules reader's in the same round. It names no perspective: <c>propose
    /// constraints</c> counts by perspective, and the diff reader is not one.
    /// </remarks>
    /// <param name="answer">The answer.</param>
    /// <param name="reader">The reader's name.</param>
    /// <returns>The findings, or a refusal.</returns>
    internal static Result<ReadBody> FindingsOf(ReaderAnswer answer, string reader) => reader switch
    {
        Rules => FindingsOf(answer, perspective: null, prefix: null),
        Diff => FindingsOf(answer, perspective: null, prefix: Diff),
        _ => FindingsOf(answer, Perspectives.PerspectiveLibrary.Find(reader).ValueOrDefault, prefix: null),
    };

    private static Result<ReadBody> FindingsOf(ReaderAnswer answer, Perspectives.Perspective? perspective, string? prefix)
    {
        ArgumentNullException.ThrowIfNull(answer);

        Result<ReaderBody> body = Body<ReaderBody>(answer);

        if (body.IsFailed)
        {
            return body.ToResult<ReadBody>();
        }

        if (body.Value.Findings is null)
        {
            return Refuse<ReadBody>(answer, "has no findings array, and a clean reading is an empty one");
        }

        List<Finding> findings = [];
        List<string> demoted = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        int unnamed = 0;

        foreach (ReaderFinding given in body.Value.Findings)
        {
            Severity severity;

            if (string.Equals(given.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
            {
                severity = Severity.Blocking;
            }
            else if (string.Equals(given.Severity, "suggestion", StringComparison.OrdinalIgnoreCase))
            {
                severity = Severity.Suggestion;
            }
            else
            {
                return Refuse<ReadBody>(answer, $"gives finding {given.Id ?? "(no id)"} the severity '{given.Severity}', where it is blocking or suggestion");
            }

            if (string.IsNullOrWhiteSpace(given.Statement))
            {
                return Refuse<ReadBody>(answer, $"gives finding {given.Id ?? "(no id)"} no statement");
            }

            string local = string.IsNullOrWhiteSpace(given.Id)
                ? string.Create(CultureInfo.InvariantCulture, $"{(severity == Severity.Blocking ? "F" : "S")}_x{++unnamed:D2}")
                : given.Id.Trim();

            string? under = perspective?.Name ?? prefix;
            string id = under is null ? local : under + "." + local[(local.LastIndexOf('.') + 1)..];

            if (!ids.Add(id))
            {
                return Refuse<ReadBody>(answer, $"uses the id {id} twice");
            }

            if (perspective is not null
                && severity == Severity.Blocking
                && (given.Cites?.Id is not { Length: > 0 } rule || !perspective.Rules.Contains(rule, StringComparison.Ordinal)))
            {
                severity = Severity.Suggestion;
                demoted.Add(id);
            }

            findings.Add(new Finding(
                id,
                severity,
                Disposition.Open,
                given.Cites,
                given.Statement.Trim(),
                given.Locus,
                given.Remedy,
                Perspective: perspective?.Name,
                Gist: severity == Severity.Blocking ? given.Gist?.Trim() : given.Gist));
        }

        return Result.Ok(new ReadBody(findings, body.Value.Conflicts ?? [], body.Value.Rows ?? [], demoted));
    }

    private static Result<T> Refuse<T>(ReaderAnswer answer, string what) => Result.Fail<T>(string.Create(
        CultureInfo.InvariantCulture,
        $"The {answer.Reader} reader's answer {what}. Start it again; do not repair it, because a repaired answer is this session writing the finding."));

    private static ReaderUsage UsageOf(JsonElement root, ReaderUsage unknown)
    {
        JsonElement usage = root.TryGetProperty("usage", out JsonElement found) && found.ValueKind == JsonValueKind.Object
            ? found
            : default;

        // The model is the key of the host's per-model breakdown. A process that used more than one names
        // the one that wrote the most, which is the one that answered.
        string? model = null;

        if (root.TryGetProperty("modelUsage", out JsonElement models) && models.ValueKind == JsonValueKind.Object)
        {
            model = models.EnumerateObject()
                .OrderByDescending(entry => Number(entry.Value, "outputTokens") ?? 0)
                .Select(entry => entry.Name)
                .FirstOrDefault();
        }

        double? milliseconds = Number(root, "duration_ms");

        return unknown with
        {
            Model = model,
            Input = Whole(usage, "input_tokens"),
            CacheWrite = Whole(usage, "cache_creation_input_tokens"),
            CacheRead = Whole(usage, "cache_read_input_tokens"),
            Output = Whole(usage, "output_tokens"),
            Turns = Whole(root, "num_turns") is { } turns ? (int)turns : null,
            Seconds = milliseconds is { } ms ? Math.Round(ms / 1000, 1) : null,
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? Whole(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out long number)
            ? number
            : null;

    private static double? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
}
