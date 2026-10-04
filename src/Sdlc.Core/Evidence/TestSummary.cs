using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using FluentResults;

namespace Sdlc.Core.Evidence;

/// <summary>How one test ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestOutcome>))]
public enum TestOutcome
{
    /// <summary>It ran and asserted what it claims to.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>It ran and did not.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>
    /// It did not run.
    /// </summary>
    /// <remarks>
    /// Kept distinct from a pass, which is the whole reason this value exists. A skipped test is not
    /// evidence of anything, and a summary that rolled it into "not failed" would let a criterion be
    /// proven by a test nobody ran.
    /// </remarks>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}

/// <summary>
/// One test, as the framework records it.
/// </summary>
/// <remarks>
/// Not called TestResult, which is what it is: that name collides with the one MSTest publishes, and a
/// type whose name is ambiguous inside a test project is a type every test file has to disambiguate.
/// </remarks>
/// <param name="Name">The test's name, as the runner reported it.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Detail">The failure message, when it failed.</param>
public sealed record RecordedTest(string Name, TestOutcome Outcome, string? Detail = null);

/// <summary>
/// What a test run produced, in the one shape every stack normalises to.
/// </summary>
/// <remarks>
/// The gate must not know about test platforms. Reading a runner's native output directly would put a
/// <c>.trx</c> parser inside the gate, then a JUnit parser beside it, and the thing that owns process
/// constraints would start owning file formats. Normalising at the build step puts the stack-specific
/// part where the stack already is.
/// <para>
/// The runner's original output is kept alongside this rather than replaced, so a verify claim can cite
/// either and <c>CON_08</c> is satisfied by both.
/// </para>
/// </remarks>
/// <param name="FrameworkVersion">The version whose format this was written in.</param>
/// <param name="Cycle">The cycle the run belongs to.</param>
/// <param name="RanAt">When the run finished.</param>
/// <param name="Tests">Every test, in the order the runner reported them.</param>
public sealed record TestSummary(
    [property: JsonPropertyName("framework_version")] string? FrameworkVersion,
    string? Cycle,
    [property: JsonPropertyName("ran_at")] DateTimeOffset? RanAt,
    IReadOnlyList<RecordedTest> Tests)
{
    /// <summary>How many passed.</summary>
    [JsonIgnore]
    public int Passed => Tests.Count(test => test.Outcome == TestOutcome.Passed);

    /// <summary>How many failed.</summary>
    [JsonIgnore]
    public int Failed => Tests.Count(test => test.Outcome == TestOutcome.Failed);

    /// <summary>How many did not run.</summary>
    [JsonIgnore]
    public int Skipped => Tests.Count(test => test.Outcome == TestOutcome.Skipped);

    /// <summary>
    /// Whether a named test passed.
    /// </summary>
    /// <remarks>
    /// Matched on the suffix as well as the whole name, because a plan names a test as
    /// <c>Rolls_within_one_and_six</c> while a runner reports
    /// <c>Dice.Tests.RollTests.Rolls_within_one_and_six</c>. Requiring the fully qualified name would make
    /// every plan carry a namespace it has no reason to know at planning time.
    /// <para>
    /// <strong>A parameterised test is matched too, and every one of its cases has to have passed.</strong>
    /// A runner records one result per case and decorates the name with the arguments --
    /// <c>Page_shows_a_face ("chromium")</c> -- so a plan naming the test matched nothing and the chain
    /// check reported a passing proof as absent. The only remedy available to a cycle was to collapse the
    /// cases into a loop inside one test, which is what a trial run did: it cost the per-case result, so
    /// the evidence could no longer show which cases ran, and a failure in the second case stopped the
    /// first from running at all. A gate that makes a proof checkable must not be the reason the proof
    /// gets weaker.
    /// </para>
    /// <para>
    /// Every case rather than any case, because the cases of one test are one proof. <c>Any</c> let a
    /// passing case answer for a failing sibling, and the ambiguity check at recording time cannot see
    /// that pair -- it groups by the recorded name, and two cases of one test have different ones.
    /// </para>
    /// </remarks>
    /// <param name="name">The test's name, as a plan would write it.</param>
    /// <returns>True when that test ran and every case of it passed.</returns>
    public bool DidPass(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        List<RecordedTest> cases = [.. Tests.Where(test => Names(test.Name, name))];

        return cases.Count > 0 && cases.TrueForAll(test => test.Outcome == TestOutcome.Passed);
    }

    /// <summary>Whether a recorded result is a result for the test a plan named.</summary>
    /// <param name="recorded">The name as the runner reported it, arguments and all.</param>
    /// <param name="name">The name as a plan wrote it.</param>
    /// <returns>True when the two name the same test.</returns>
    private static bool Names(string recorded, string name)
    {
        string bare = WithoutCase(recorded);

        return string.Equals(bare, name, StringComparison.Ordinal)
            || bare.EndsWith("." + name, StringComparison.Ordinal);
    }

    /// <summary>
    /// A recorded name with the case arguments taken off.
    /// </summary>
    /// <remarks>
    /// Cut at the first bracket, which a method name cannot contain, so this holds for both spellings a
    /// runner uses: MSTest reports <c>Method ("arg")</c> and xUnit reports <c>Method(p: 1)</c>.
    /// </remarks>
    /// <param name="recorded">The name as the runner reported it.</param>
    /// <returns>The test's own name.</returns>
    private static string WithoutCase(string recorded)
    {
        int bracket = recorded.IndexOf('(', StringComparison.Ordinal);

        return bracket < 0 ? recorded : recorded[..bracket].TrimEnd();
    }
}

/// <summary>
/// Turns a runner's own output into the normalised summary.
/// </summary>
/// <remarks>
/// This is the boundary, and the only place in the framework that knows what a <c>.trx</c> looks like.
/// Adding a stack means adding a reader here, not changing anything the gate does.
/// </remarks>
public static class TestSummaryReader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Reads a Visual Studio test results file.
    /// </summary>
    /// <param name="xml">The file's contents.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The results, or a failure saying what is wrong with the file.</returns>
    public static Result<IReadOnlyList<RecordedTest>> ReadTrx(string xml, string source)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException error)
        {
            return Result.Fail<IReadOnlyList<RecordedTest>>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: not valid XML. {error.Message}"));
        }

        // The namespace is declared on the root and every element inherits it, so elements are found by
        // local name. A hard-coded namespace URI breaks the first time the schema version moves.
        List<XElement> results = [.. document
            .Descendants()
            .Where(element => element.Name.LocalName == "UnitTestResult")];

        if (results.Count == 0)
        {
            return Result.Fail<IReadOnlyList<RecordedTest>>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: no test results in it. A run that produced none is a run that did not happen, which is not the same as a run with nothing to report."));
        }

        List<RecordedTest> tests = [];

        foreach (XElement result in results)
        {
            string name = result.Attribute("testName")?.Value ?? "(unnamed)";
            string outcome = result.Attribute("outcome")?.Value ?? string.Empty;

            string? detail = result
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Message")
                ?.Value;

            tests.Add(new RecordedTest(name, Translate(outcome), Trim(detail)));
        }

        return Result.Ok<IReadOnlyList<RecordedTest>>(tests);
    }

    /// <summary>Writes a summary as the framework-defined JSON.</summary>
    /// <param name="summary">What to write.</param>
    /// <returns>The JSON, ending in a newline.</returns>
    public static string Write(TestSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return JsonSerializer.Serialize(summary, _options) + Environment.NewLine;
    }

    /// <summary>Reads a summary back.</summary>
    /// <param name="json">The summary's JSON.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The summary, or a failure naming the file.</returns>
    public static Result<TestSummary> Read(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        try
        {
            TestSummary? summary = JsonSerializer.Deserialize<TestSummary>(json, _options);

            if (summary?.Tests is null)
            {
                return Result.Fail<TestSummary>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{source}: no tests array. A run with none records an empty one rather than omitting it."));
            }

            return Result.Ok(summary);
        }
        catch (JsonException error)
        {
            return Result.Fail<TestSummary>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: not valid JSON. {error.Message}"));
        }
    }

    /// <summary>
    /// What a runner's outcome word means here.
    /// </summary>
    /// <remarks>
    /// Anything unrecognised becomes <see cref="TestOutcome.Skipped"/> rather than passed. A runner word
    /// nobody mapped is a test whose state is unknown, and treating unknown as passed is how a criterion
    /// gets proven by a test that errored.
    /// </remarks>
    private static TestOutcome Translate(string outcome) => outcome switch
    {
        string passed when passed.Equals("Passed", StringComparison.OrdinalIgnoreCase) => TestOutcome.Passed,
        string failed when failed.Equals("Failed", StringComparison.OrdinalIgnoreCase) => TestOutcome.Failed,
        string error when error.Equals("Error", StringComparison.OrdinalIgnoreCase) => TestOutcome.Failed,
        string timeout when timeout.Equals("Timeout", StringComparison.OrdinalIgnoreCase) => TestOutcome.Failed,
        _ => TestOutcome.Skipped,
    };

    private static string? Trim(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        // A stack trace in a summary a model reads is mostly noise, and the original .trx is kept beside
        // this for when it is not.
        string trimmed = detail.Trim();

        return trimmed.Length <= 400 ? trimmed : trimmed[..400] + "...";
    }
}
