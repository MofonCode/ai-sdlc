using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Filing an interrogation's findings, and recording what was done about each one.
/// </summary>
/// <remarks>
/// THE WIRE THAT WAS MISSING. Four consumers read a cycle's findings and nothing wrote them, so
/// verify counted undispositioned findings and always found none — a cycle carrying blocking
/// findings passed its gate, quietly, because the evidence was never on disk.
/// <para>
/// Both scripts refuse more than they accept, and the refusals are what these tests assert. A
/// document that does not satisfy the findings schema is not filed with a warning; a disposition
/// the schema would reject leaves the file byte-for-byte as it was. That last one is the
/// mechanism keeping <c>rejected</c> and <c>accepted-wont-fix</c> apart, which is the only
/// evidence the charter improves from.
/// </para>
/// </remarks>
[TestClass]
public sealed class FindingsFilingTests
{
    private const string Cycle = "20260925-bb01";

    private static string ToolDir => Path.Combine(AppContext.BaseDirectory, "tools");

    private static string? _repo;

    private static string FindingsDir =>
        Path.Combine(_repo!, ".sdlc", "cycles", Cycle, "findings");

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-filing-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", Cycle));

        // A git repository, because every script here resolves its root by walking up for .git
        // and a fixture that skips it would be testing a path no caller takes.
        Run("git", "init --initial-branch=master", _repo);
    }

    [ClassCleanup]
    public static void RemoveFixtureRepo()
    {
        if (_repo is null || !Directory.Exists(_repo))
        {
            return;
        }

        try
        {
            foreach (string f in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(_repo, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestInitialize]
    public void ClearFiledFindings()
    {
        // Each test starts from nothing filed. The duplicate-id and re-run cases depend on what
        // is already there, so leaking state between them would make them pass for the wrong
        // reason — or fail depending on execution order.
        if (Directory.Exists(FindingsDir))
        {
            Directory.Delete(FindingsDir, recursive: true);
        }
    }

    private static string Run(string exe, string args, string workingDirectory)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return stdout;
    }

    /// <summary>Writes a document to a scratch file and files it. Returns the script's report.</summary>
    private static JsonDocument File_(string json, string? cycle = null)
    {
        string scratch = Path.Combine(_repo!, "interrogation.json");
        File.WriteAllText(scratch, json);

        string script = Path.Combine(ToolDir, "Add-SdlcFindings.ps1");
        string stdout = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
            $"-CycleId {cycle ?? Cycle} -Path \"{scratch}\" -RepoRoot \"{_repo}\"",
            _repo!);

        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "the filing script printed nothing");
        return JsonDocument.Parse(stdout);
    }

    private static JsonDocument Dispose_(string finding, string disposition, string? reason = null)
    {
        string script = Path.Combine(ToolDir, "Set-SdlcDisposition.ps1");
        string args =
            $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" " +
            $"-CycleId {Cycle} -Finding {finding} -Disposition {disposition} -RepoRoot \"{_repo}\"";

        if (reason is not null)
        {
            args += $" -Reason \"{reason}\"";
        }

        string stdout = Run("powershell.exe", args, _repo!);

        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout), "the disposition script printed nothing");
        return JsonDocument.Parse(stdout);
    }

    private static string OutcomeOf(JsonDocument report) =>
        report.RootElement.GetProperty("outcome").GetString()!;

    /// <summary>One blocking finding with a citation that resolves, and one suggestion.</summary>
    private static string ValidDocument(string target = "spec", int round = 1, string idPrefix = "spec-r1") =>
        $$"""
        {
          "cycle-id": "{{Cycle}}",
          "target": "{{target}}",
          "charter-version": 3,
          "round": {{round}},
          "findings": [
            { "id": "{{idPrefix}}-check1", "category": "gate-bypass", "state": "blocking",
              "stage": "build", "detection": "check", "role": "facts",
              "summary": "Check `suppressions-justified` failed.",
              "cites": [{ "kind": "check", "id": "suppressions-justified" }] },
            { "id": "{{idPrefix}}-reason1", "category": "ambiguity", "state": "suggested",
              "stage": "spec", "detection": "reasoning", "role": "coherence",
              "summary": "Reads two ways." }
          ]
        }
        """;

    [TestMethod]
    public void AValidDocumentIsFiledUnderItsTargetAndRound()
    {
        using JsonDocument report = File_(ValidDocument());

        Assert.AreEqual("filed", OutcomeOf(report));
        Assert.AreEqual(1, report.RootElement.GetProperty("blocking").GetInt32());
        Assert.AreEqual(1, report.RootElement.GetProperty("suggested").GetInt32());

        // The path is the layout, and the layout is this script's to own: four consumers read it
        // and none of them should be spelling it out for themselves.
        Assert.AreEqual(
            $".sdlc/cycles/{Cycle}/findings/spec.r1.json",
            report.RootElement.GetProperty("filed").GetString());

        Assert.IsTrue(File.Exists(Path.Combine(FindingsDir, "spec.r1.json")));
    }

    [TestMethod]
    public void ADocumentTheSchemaRejectsIsNotFiledAtAll()
    {
        // A blocking finding with no citation. This is the anti-inflation guard, and the point of
        // refusing rather than warning is that a warning on the way in is a warning nobody sees
        // on the way out.
        using JsonDocument report = File_($$"""
            {
              "cycle-id": "{{Cycle}}",
              "target": "spec",
              "charter-version": 3,
              "round": 1,
              "findings": [
                { "id": "spec-r1-reason1", "category": "ambiguity", "state": "blocking",
                  "stage": "spec", "detection": "reasoning", "summary": "Uncited, and blocking." }
              ]
            }
            """);

        Assert.AreEqual("refused-invalid", OutcomeOf(report));
        Assert.IsTrue(report.RootElement.GetProperty("errors").GetArrayLength() > 0,
            "a refusal must say what was wrong");
        Assert.IsFalse(Directory.Exists(FindingsDir), "nothing may be written when validation fails");
    }

    [TestMethod]
    public void ADocumentFiledUnderTheWrongCycleIsRefused()
    {
        // The document travels into the retro and then into the corpus, where its own cycle-id is
        // the only thing saying where it came from. Filing it under a directory that disagrees
        // would attribute evidence to the wrong cycle permanently.
        using JsonDocument report = File_(ValidDocument(), cycle: "20260925-bb99");

        Assert.AreEqual("refused-cycle-mismatch", OutcomeOf(report));
    }

    [TestMethod]
    public void AnIdAlreadyUsedInTheCycleIsRefused()
    {
        using JsonDocument first = File_(ValidDocument());
        Assert.AreEqual("filed", OutcomeOf(first));

        // Same ids, different target, so it would land in a different file — and a disposition
        // recorded against that id would then be ambiguous across the cycle's own record.
        using JsonDocument second = File_(ValidDocument(target: "plan", round: 1));

        Assert.AreEqual("refused-duplicate-id", OutcomeOf(second));
        Assert.IsFalse(File.Exists(Path.Combine(FindingsDir, "plan.r1.json")));
    }

    [TestMethod]
    public void ReRunningTheSameInterrogationReplacesItsOwnDocument()
    {
        using JsonDocument first = File_(ValidDocument());
        Assert.AreEqual("filed", OutcomeOf(first));

        // The same target and round is the same interrogation run again, which is legitimate —
        // the first may have died partway. Its own ids are not a collision with itself, and the
        // outcome says plainly that something was replaced rather than added.
        using JsonDocument again = File_(ValidDocument());

        Assert.AreEqual("filed-replacing-a-rerun", OutcomeOf(again));
        Assert.AreEqual(1, Directory.GetFiles(FindingsDir, "*.json").Length);
    }

    [TestMethod]
    public void ADispositionIsRecordedOnTheFinding()
    {
        using JsonDocument filed = File_(ValidDocument());
        Assert.AreEqual("filed", OutcomeOf(filed));

        using JsonDocument report = Dispose_("spec-r1-check1", "fixed");
        Assert.AreEqual("recorded", OutcomeOf(report));

        using JsonDocument doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FindingsDir, "spec.r1.json")));

        JsonElement finding = doc.RootElement.GetProperty("findings").EnumerateArray()
            .First(f => f.GetProperty("id").GetString() == "spec-r1-check1");

        Assert.AreEqual("fixed", finding.GetProperty("disposition").GetString());
    }

    [TestMethod]
    public void StateIsNotTouchedWhenADispositionIsRecorded()
    {
        // State was assigned in code from the citation, which is the anti-inflation guard.
        // Disposition says what was done; letting it re-rank a finding would defeat the purpose
        // of assigning state by rule.
        using JsonDocument filed = File_(ValidDocument());
        using JsonDocument report = Dispose_("spec-r1-check1", "fixed");
        Assert.AreEqual("recorded", OutcomeOf(report));

        using JsonDocument doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FindingsDir, "spec.r1.json")));

        JsonElement finding = doc.RootElement.GetProperty("findings").EnumerateArray()
            .First(f => f.GetProperty("id").GetString() == "spec-r1-check1");

        Assert.AreEqual("blocking", finding.GetProperty("state").GetString(),
            "a disposition records what was done, not how serious it was");
    }

    [TestMethod]
    public void ADismissalWithNoReasonIsRefusedAndTheFileIsRestored()
    {
        // THE RULE THAT KEEPS THE OVERRIDE COUNT HONEST. `rejected` means the finding was wrong
        // and says nothing about the invariant; `accepted-wont-fix` means it was right and nobody
        // acted, which is the evidence an invariant may be too broad. Merging them corrupts the
        // only signal the charter improves from, so the schema demands a reason for both — and
        // the script lets the schema rule rather than restating it.
        using JsonDocument filed = File_(ValidDocument());
        string path = Path.Combine(FindingsDir, "spec.r1.json");
        string before = File.ReadAllText(path);

        using JsonDocument report = Dispose_("spec-r1-check1", "accepted-wont-fix");

        Assert.AreEqual("refused-schema", OutcomeOf(report));
        Assert.AreEqual(before, File.ReadAllText(path),
            "a refused disposition must leave the document byte-for-byte as it was");
    }

    [TestMethod]
    public void ADismissalWithAReasonIsAccepted()
    {
        using JsonDocument filed = File_(ValidDocument());

        using JsonDocument report = Dispose_(
            "spec-r1-reason1", "rejected", "The two readings lead to the same work.");

        Assert.AreEqual("recorded", OutcomeOf(report));
    }

    [TestMethod]
    public void AnUnknownFindingIdIsRefused()
    {
        using JsonDocument filed = File_(ValidDocument());

        using JsonDocument report = Dispose_("spec-r1-reason9", "fixed");

        Assert.AreEqual("refused-not-found", OutcomeOf(report));
    }

    [TestMethod]
    public void DispositioningBeforeAnythingIsFiledIsRefused()
    {
        using JsonDocument report = Dispose_("spec-r1-check1", "fixed");

        Assert.AreEqual("refused-no-findings", OutcomeOf(report));
    }
}
