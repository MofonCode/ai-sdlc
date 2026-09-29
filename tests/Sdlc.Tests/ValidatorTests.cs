using System.Diagnostics;

namespace Sdlc.Tests;

/// <summary>
/// The complete schema check, pointed at files on disk.
/// <para>
/// Two layers check artifacts, and until this tool existed only one of them ran against live files.
/// <c>Assert-ArtifactShape.ps1</c> compares top-level key names on every write, because Windows
/// PowerShell 5.1 has no JSON Schema evaluator. The evaluator that checks the rules that matter — that
/// <c>pass</c> requires no open blocking finding, that <c>escalated</c> requires a spent budget — only
/// ever saw fixtures. So a real cycle could commit a verify document claiming <c>pass</c> alongside an
/// open blocking finding, and nothing in the framework would object.
/// </para>
/// </summary>
[TestClass]
public sealed class ValidatorTests
{
    private sealed record Result(int ExitCode, string Output);

    /// <summary>
    /// Runs the validator. Both projects are in the solution, so <c>dotnet test</c> has already built
    /// this and the assembly is on disk next to its dependencies.
    /// </summary>
    private static Result Run(params string[] arguments)
    {
        string assembly = Directory
            .EnumerateFiles(
                RepoPaths.At("tools", "Sdlc.Validate", "bin"),
                "Sdlc.Validate.dll",
                SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Sdlc.Validate.dll was not found. It is in the solution, so a solution build produces it.");

        ProcessStartInfo info = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoPaths.Root,
        };

        info.ArgumentList.Add(assembly);

        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start dotnet.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new Result(process.ExitCode, stdout + stderr);
    }

    /// <summary>Writes an artifact into a throwaway cycle directory and returns its path.</summary>
    private static string Artifact(string name, params string[] lines)
    {
        string directory = Path.Combine(
            Path.GetTempPath(), "sdlc-validate", Guid.NewGuid().ToString("N"), ".sdlc", "cycles", "20260101_aaaa");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, name);
        File.WriteAllLines(path, lines);

        return path;
    }

    [TestMethod]
    public void TheCheckedInArtifactFixturesAreValid()
    {
        // The golden artifacts are what every interrogation test judges. If one does not satisfy its own
        // schema, every expectation built on it is measuring something malformed.
        Result result = Run(RepoPaths.At("tests", "fixtures", "artifacts"));

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "intent.schema.json", StringComparison.Ordinal);
        StringAssert.Contains(result.Output, "plan.schema.json", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AVerifyDocumentClaimingPassWithAnOpenBlockingFindingIsRejected()
    {
        // The rule the shallow gate cannot see, and the reason this tool exists. Every key here is a
        // legal name, so Assert-ArtifactShape.ps1 permits the document; the contradiction is in the
        // values, and only a real evaluator finds it.
        string path = Artifact(
            "verify.md",
            "---",
            "framework_version: 0.7.0",
            "cycle: 20260101_aaaa",
            "spec: .sdlc/cycles/20260101_aaaa/spec.md",
            "plan: .sdlc/cycles/20260101_aaaa/plan.md",
            "verified_at: 2026-01-01T12:00:00Z",
            "outcome: pass",
            "remediation:",
            "  passes_spent: 0",
            "evidence:",
            "  build:",
            "    path: .sdlc/cycles/20260101_aaaa/evidence/build.log",
            "  tests:",
            "    path: .sdlc/cycles/20260101_aaaa/evidence/tests.trx",
            "  diff:",
            "    path: .sdlc/cycles/20260101_aaaa/evidence/diff.patch",
            "findings:",
            "  - id: F_01",
            "    severity: blocking",
            "    disposition: open",
            "    cites: CON_07",
            "    statement: A file the plan never declared was changed.",
            "    locus: diff.patch:12",
            "    remedy: Amend the plan and have it re-approved.",
            "---",
            "",
            "# Verify");

        Result result = Run(path);

        Assert.AreEqual(1, result.ExitCode, $"pass with an open blocking finding must be rejected: {result.Output}");
        StringAssert.Contains(result.Output, "FAIL", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AnIntegerInFrontmatterStaysAnInteger()
    {
        // The converter's own defect, kept as a test. Deserializing YAML to object makes every scalar a
        // string, so passes_spent: 0 arrived as "0" and a valid document was rejected for a reason that
        // had nothing to do with it. YAML 1.2 decides by quoting, and this document is the smallest case
        // that tells the two apart.
        string path = Artifact(
            "verify.md",
            "---",
            "framework_version: 0.7.0",
            "cycle: 20260101_aaaa",
            "spec: .sdlc/cycles/20260101_aaaa/spec.md",
            "plan: .sdlc/cycles/20260101_aaaa/plan.md",
            "verified_at: 2026-01-01T12:00:00Z",
            "outcome: pass",
            "remediation:",
            "  passes_spent: 0",
            "evidence:",
            "  build:",
            "    path: .sdlc/cycles/20260101_aaaa/evidence/build.log",
            "  tests:",
            "    path: .sdlc/cycles/20260101_aaaa/evidence/tests.trx",
            "  diff:",
            "    path: .sdlc/cycles/20260101_aaaa/evidence/diff.patch",
            "findings: []",
            "---",
            "",
            "# Verify");

        Result result = Run(path);

        Assert.AreEqual(0, result.ExitCode, $"passes_spent: 0 is an integer, not \"0\": {result.Output}");
    }

    [TestMethod]
    public void AMissingArtifactIsAUsageErrorRatherThanAPass()
    {
        // Exit 2, not 0. A validator that reports success when it validated nothing is the failure mode
        // every check in this repository is written to avoid.
        Result result = Run(Path.Combine(Path.GetTempPath(), "sdlc-validate-not-here", "verify.md"));

        Assert.AreEqual(2, result.ExitCode, result.Output);
    }
}
