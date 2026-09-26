using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The schemas, enforced at cycle time rather than only in this suite.
/// </summary>
/// <remarks>
/// <see cref="SchemaTests"/> proves each schema accepts what it should and rejects what it
/// should. That is a statement about the schema files. It says nothing about whether anything
/// validates a real artifact at the moment one is written — and until Test-SdlcArtifact.ps1
/// existed, nothing did.
/// <para>
/// The findings schema is where that mattered most. It carries the five anti-inflation rules,
/// and the interrogation workflow enforces three of them in its own code: a blocking finding
/// needs a citation, <c>unclassified</c> can never block, a location pair belongs only to a
/// contradiction. The other two — a mechanical finding never sits at <c>open</c>, a dismissal
/// states its reason — were enforced nowhere at all.
/// </para>
/// <para>
/// So these tests run the SHIPPED validator over the same fixtures the schema tests use, and
/// require the two to agree on every one. Two consumers, one authority, bound in both
/// directions: a fixture the suite rejects and the validator accepts is a rule that does not
/// hold where it counts.
/// </para>
/// </remarks>
[TestClass]
public sealed class ArtifactValidationTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Test-SdlcArtifact.ps1");

    private static string ExampleDir => Path.Combine(AppContext.BaseDirectory, "examples");

    /// <summary>The kind is the part of the filename before the first dot, as in SchemaTests.</summary>
    private static string SchemaFor(string example) =>
        example[..example.IndexOf('.', StringComparison.Ordinal)];

    public static IEnumerable<object[]> ValidExamples() =>
        Directory.EnumerateFiles(ExampleDir, "*.valid*.json")
            .Select(p => new object[] { Path.GetFileName(p) });

    public static IEnumerable<object[]> InvalidExamples() =>
        Directory.EnumerateFiles(ExampleDir, "*.invalid-*.json")
            .Select(p => new object[] { Path.GetFileName(p) });

    // A value tuple rather than a record: the analysers at `latest-all` object to a nested
    // type, to a public one, and to a property returning an array, and a suppression for each
    // would be three justifications for a test helper that needs none.
    private readonly record struct Verdict(bool Ok, bool Skipped, string[] Errors, int ExitCode);

    /// <summary>
    /// Runs the shipped script as an external process under Windows PowerShell 5.1, because
    /// that is what ships. A reimplementation here would prove the reimplementation.
    /// </summary>
    private static Verdict Validate(string relativePath, string? schema = null)
    {
        // TRIMMED, and this is not cosmetic. AppContext.BaseDirectory ends with a separator, and
        // a quoted Windows argument ending in a backslash escapes its own closing quote — so
        // `-RepoRoot "C:\dir\"` swallows every argument after it and the script dies before it
        // prints anything. The symptom was an empty result, which reads like a broken script.
        string baseDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

        string arguments =
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" " +
            $"-Path \"{Path.Combine(baseDir, relativePath)}\" " +
            $"-RepoRoot \"{baseDir}\"";

        if (schema is not null)
        {
            arguments += $" -Schema {schema}";
        }

        using Process p = Process.Start(new ProcessStartInfo("powershell.exe", arguments)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // Explicit UTF-8, or the harness decodes the child using the host codepage and a
            // schema complaint quoting a property name arrives mangled — measuring the harness.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout),
            $"the validator printed nothing for {relativePath}: {stderr}");

        using JsonDocument doc = JsonDocument.Parse(stdout);
        JsonElement root = doc.RootElement;

        return new Verdict(
            root.GetProperty("ok").GetBoolean(),
            root.GetProperty("skipped").GetBoolean(),
            [.. root.GetProperty("errors").EnumerateArray().Select(e => e.GetString() ?? string.Empty)],
            p.ExitCode);
    }

    [TestMethod]
    public void FixturesExistInBothDirections()
    {
        // Guards the shape where this class passes because it validated nothing — the same
        // guard SchemaTests carries, for the same reason.
        Assert.IsTrue(ValidExamples().Any(), "no valid examples, so acceptance is untested");
        Assert.IsTrue(InvalidExamples().Count() >= 5, "fewer invalid examples than enforced rules");
    }

    [TestMethod]
    [DynamicData(nameof(ValidExamples))]
    public void TheShippedValidatorAcceptsWhatTheSchemaAccepts(string example)
    {
        ArgumentNullException.ThrowIfNull(example);
        Verdict v = Validate(Path.Combine("examples", example), SchemaFor(example));

        Assert.IsTrue(v.Ok, $"{example}: {string.Join(" | ", v.Errors)}");
        Assert.AreEqual(0, v.ExitCode, "a valid artifact must exit zero, or every caller halts");
        Assert.IsFalse(v.Skipped, $"{example}: a kind with a schema must not report skipped");
    }

    [TestMethod]
    [DynamicData(nameof(InvalidExamples))]
    public void TheShippedValidatorRejectsWhatTheSchemaRejects(string example)
    {
        ArgumentNullException.ThrowIfNull(example);
        Verdict v = Validate(Path.Combine("examples", example), SchemaFor(example));

        Assert.IsFalse(v.Ok, $"{example} should have been rejected and was not");

        // NON-ZERO MATTERS AS MUCH AS THE VERDICT. A caller branching on the exit code is the
        // only reason this can gate anything; a check that merely prints gets skimmed.
        Assert.AreNotEqual(0, v.ExitCode, $"{example} was rejected but exited zero");
        Assert.IsTrue(v.Errors.Length > 0, $"{example} was rejected with no reason given");
    }

    [TestMethod]
    public void AKindWithNoSchemaIsReportedRatherThanPassedQuietly()
    {
        // The charter has no JSON Schema — its shape is checked by the invariant-form check
        // instead. Reporting that as an ordinary pass would let a caller read silence as
        // approval, so the answer says which of the two it is.
        Verdict v = Validate(Path.Combine("skills", "intent", "SKILL.md"));

        Assert.IsTrue(v.Skipped, "a file no schema governs must report skipped");
        Assert.IsTrue(v.Ok, "skipped is not a failure; nothing was checked");
        Assert.IsTrue(v.Errors.Any(e => e.Contains("no schema", StringComparison.Ordinal)),
            "the reason must say that nothing governed it");
    }

    [TestMethod]
    public void AMissingInputFailsClosed()
    {
        // A validator that cannot find its input has not approved anything. Exit 2 rather than
        // 1 so a caller can tell "this artifact is wrong" from "this did not run".
        Verdict v = Validate(Path.Combine("examples", "does-not-exist.json"), "findings");

        Assert.IsFalse(v.Ok);
        Assert.AreEqual(2, v.ExitCode, "could-not-run is a distinct answer from invalid");
    }
}
