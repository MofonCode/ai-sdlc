using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Per-role yield, measured from the findings corpus rather than argued about.
/// </summary>
/// <remarks>
/// <b>Four roles may produce two and a half roles' worth of findings at four roles' cost.</b>
/// Roles are the fan-out unit of an interrogation, so a role that earns nothing is the single
/// most expensive thing in the framework — and nothing measured it.
/// <para>
/// <b>The trap this has to avoid.</b> When the artifact IS the charter, <c>charter</c> is
/// removed and <c>form</c> substituted, because a role cannot audit the document that defines
/// it. So <c>charter</c> shows zero findings on every charter interrogation, and the corpus
/// today holds exactly one interrogation — of the charter. A naive report would say
/// <c>charter</c> produces nothing and invite deleting the role that checks every OTHER
/// artifact against the invariants. <c>eligible-runs</c> is what a yield judgement divides by.
/// </para>
/// <para>
/// <b>Two dispositions that must never merge.</b> <c>rejected</c> means the finding was wrong,
/// which is the only disposition that argues against a role. <c>accepted-wont-fix</c> means it
/// was right and nobody acted, which says nothing bad about the role at all. Merging them makes
/// an accurate role look noisy — the same error <c>Get-SdlcSignals</c> exists to avoid one
/// layer up.
/// </para>
/// <para>
/// The harness lifts the aggregation out of the shipped script by AST, so these run the real
/// function against fixtures with no corpus on disk. The file-reading half makes no judgements.
/// </para>
/// </remarks>
[TestClass]
public sealed class RoleYieldTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcRoleYield.ps1");

    private static readonly string[] Roles = ["charter", "coherence", "facts", "gaps", "form"];

    /// <summary>
    /// Runs <c>Get-SdlcRoleYieldReport</c> against fixture documents.
    /// </summary>
    /// <param name="documentsLiteral">A PowerShell array literal of findings documents.</param>
    /// <param name="floor">Eligible runs required before a verdict is offered.</param>
    private static JsonDocument Report(string documentsLiteral, int floor)
    {
        string harness = string.Join(Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            // STRICT MODE ON PURPOSE. The script runs under it, and the reason the safe property
            // accessor exists is that strict mode throws on an absent property rather than
            // returning null. Testing without it would hide exactly the bug it guards.
            "Set-StrictMode -Version Latest",
            "$tokens = $null; $errors = $null",
            "$ast = [System.Management.Automation.Language.Parser]::ParseFile(",
            "    '" + ScriptPath.Replace("'", "''", StringComparison.Ordinal) + "', [ref] $tokens, [ref] $errors)",
            "if ($errors.Count -gt 0) { throw \"the script does not parse: $($errors[0].Message)\" }",
            "foreach ($name in @('Get-SdlcProperty', 'Get-SdlcRoleYieldReport')) {",
            "    $fn = $ast.FindAll({",
            "        $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] -and",
            "        $args[0].Name -eq $name }, $true)",
            "    if (-not $fn) { throw \"$name is not in the script\" }",
            "    . ([scriptblock]::Create($fn[0].Extent.Text))",
            "}",
            "$docs = @(" + documentsLiteral + ")",
            "$roles = @('charter', 'coherence', 'facts', 'gaps', 'form')",
            "$r = Get-SdlcRoleYieldReport -Documents $docs -DeclaredRoles $roles -Floor " + floor,
            "$r | ConvertTo-Json -Depth 6 -Compress");

        string temp = Path.Combine(Path.GetTempPath(), "yield-" + Guid.NewGuid().ToString("N")[..8] + ".ps1");
        File.WriteAllText(temp, harness, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        try
        {
            using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", temp },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            })!;

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            Assert.AreEqual(0, p.ExitCode, $"the harness failed:{Environment.NewLine}{stderr}");
            return JsonDocument.Parse(stdout.Trim());
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static JsonElement Row(JsonDocument doc, string role) =>
        doc.RootElement.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("role").GetString() == role);

    /// <summary>
    /// One finding, as a PSCustomObject. Omitting a field omits the PROPERTY, which is what
    /// ConvertFrom-Json does with an absent JSON key and what makes strict mode throw.
    /// </summary>
    /// <remarks>
    /// A plain hashtable would be the easier fixture and the wrong one: the real corpus arrives
    /// through ConvertFrom-Json as PSCustomObjects, and a hashtable does not expose its keys as
    /// PSObject properties — so every count silently read zero and the tests were measuring a
    /// shape that never occurs.
    /// </remarks>
    private static string Finding(string role, string state, string? disposition = null)
    {
        string d = disposition is null ? string.Empty : $"; disposition = '{disposition}'";
        return $"([pscustomobject] @{{ role = '{role}'; state = '{state}'{d} }})";
    }

    private static string Document(string target, params string[] findings) =>
        $"([pscustomobject] @{{ target = '{target}'; findings = @({string.Join(", ", findings)}) }})";

    [TestMethod]
    public void ASubstitutedRoleIsNotARoleThatFoundNothing()
    {
        // THE TRAP. The corpus holds one interrogation, of the charter, where `charter` is
        // replaced by `form`. Counting that as a run in which `charter` produced nothing would
        // build a case for deleting the role that checks every other artifact.
        using JsonDocument doc = Report(
            Document("charter", Finding("coherence", "blocking", "fixed")),
            floor: 1);

        Assert.AreEqual(0, Row(doc, "charter").GetProperty("eligible-runs").GetInt32(),
            "a charter interrogation is no evidence about the charter role, in either direction");
        Assert.AreEqual("insufficient-data", Row(doc, "charter").GetProperty("verdict").GetString(),
            "with no eligible run there is nothing to conclude, however low the floor");

        // And the substitution must not suppress the role that DID run.
        Assert.AreEqual(1, Row(doc, "coherence").GetProperty("eligible-runs").GetInt32());
        Assert.AreEqual(1, Row(doc, "coherence").GetProperty("findings").GetInt32());
    }

    [TestMethod]
    public void ANonCharterTargetMakesTheCharterRoleEligible()
    {
        // The other half: `charter` is a first-class role everywhere else, and if it were
        // permanently ineligible the report could never say anything about it at all.
        using JsonDocument doc = Report(
            Document("spec", Finding("charter", "blocking", "fixed")),
            floor: 1);

        Assert.AreEqual(1, Row(doc, "charter").GetProperty("eligible-runs").GetInt32());
        Assert.AreEqual("earning-its-place", Row(doc, "charter").GetProperty("verdict").GetString());
    }

    [TestMethod]
    public void BelowTheFloorEveryVerdictIsWithheldButTheCountsAreReported()
    {
        // One interrogation of one artifact tells you about that artifact, not about a role.
        // The charter already sets a ten-cycle floor for the zero-cite query for this reason.
        // The COUNTS are still true, so they are still reported — it is the inference that is
        // unavailable.
        using JsonDocument doc = Report(
            Document("spec", Finding("facts", "suggested"), Finding("facts", "suggested")),
            floor: 10);

        Assert.IsTrue(doc.RootElement.GetProperty("below-floor").GetBoolean());
        Assert.AreEqual("insufficient-data", Row(doc, "facts").GetProperty("verdict").GetString(),
            "two suggestions and no blocking finding is not yet evidence of anything");
        Assert.AreEqual(2, Row(doc, "facts").GetProperty("findings").GetInt32(),
            "the counts are facts and must be shown regardless");
    }

    [TestMethod]
    public void ARoleThatNeverBlockedIsFlaggedOnceThereIsEnoughData()
    {
        // `never-blocked` has three readings that call for OPPOSITE actions — decoration,
        // unenforceable, or preventing things upstream — which is exactly why it is a prompt to
        // investigate and not a recommendation to drop the role.
        using JsonDocument doc = Report(
            Document("spec", Finding("facts", "suggested"), Finding("coherence", "blocking", "fixed")),
            floor: 1);

        Assert.AreEqual("never-blocked", Row(doc, "facts").GetProperty("verdict").GetString());
        Assert.AreEqual("earning-its-place", Row(doc, "coherence").GetProperty("verdict").GetString());
    }

    [TestMethod]
    public void RejectedAndAcceptedWontFixAreNeverMerged()
    {
        // OPPOSITE SIGNALS. `rejected` means the finding was wrong — the only disposition that
        // argues against the role. `accepted-wont-fix` means it was right and nobody acted,
        // which says nothing bad about the role. Merged, an accurate role reads as noisy.
        using JsonDocument doc = Report(
            Document("spec",
                Finding("gaps", "blocking", "accepted-wont-fix"),
                Finding("gaps", "blocking", "accepted-wont-fix"),
                Finding("gaps", "blocking", "fixed")),
            floor: 1);

        JsonElement gaps = Row(doc, "gaps");
        Assert.AreEqual(2, gaps.GetProperty("accepted-wont-fix").GetInt32());
        Assert.AreEqual(0, gaps.GetProperty("rejected").GetInt32());
        Assert.AreEqual("earning-its-place", gaps.GetProperty("verdict").GetString(),
            "three correct findings nobody acted on twice is not a bad role");

        // The same shape with `rejected` instead reverses the verdict, which is the point.
        using JsonDocument wrong = Report(
            Document("spec",
                Finding("gaps", "blocking", "rejected"),
                Finding("gaps", "blocking", "rejected"),
                Finding("gaps", "blocking", "fixed")),
            floor: 1);

        Assert.AreEqual("more-wrong-than-right", Row(wrong, "gaps").GetProperty("verdict").GetString());
    }

    [TestMethod]
    public void AFindingWithNoDispositionPropertyDoesNotThrow()
    {
        // `Set-StrictMode -Version Latest` throws on a property the object does not have, and
        // ConvertFrom-Json omits absent keys rather than adding them as null — so a finding
        // written before it was dispositioned has no `disposition` property at all. The first
        // version of the script read it directly and terminated on the real corpus.
        using JsonDocument doc = Report(
            Document("spec", Finding("form", "blocking")),
            floor: 1);

        Assert.AreEqual(1, Row(doc, "form").GetProperty("undispositioned").GetInt32(),
            "an absent disposition counts as undispositioned, not as an error");
        Assert.AreEqual(0, Row(doc, "form").GetProperty("fixed").GetInt32());
    }

    [TestMethod]
    public void ARoleWithNoFindingsAcrossEnoughRunsIsDistinguishedFromOneThatNeverBlocked()
    {
        // Two different states of affairs, and they need different investigations: a role that
        // raises nothing at all may be mis-scoped, while one that raises findings that never
        // block may be pointed at the wrong evidence.
        using JsonDocument doc = Report(
            Document("spec", Finding("coherence", "suggested")),
            floor: 1);

        Assert.AreEqual("no-findings-at-all", Row(doc, "facts").GetProperty("verdict").GetString());
        Assert.AreEqual("never-blocked", Row(doc, "coherence").GetProperty("verdict").GetString());
    }

    [TestMethod]
    public void CostIsReportedAsUnavailableRatherThanEstimated()
    {
        // `US-22` is a live story about load-bearing numbers with no verifying source. Cost is
        // one agent per role plus one per candidate at triage, and the corpus records merged
        // findings rather than candidates — so any per-role cost here would be invented, and
        // inventing one to make the report look complete is that same defect.
        using JsonDocument doc = Report(Document("spec", Finding("facts", "suggested")), floor: 1);

        Assert.IsFalse(doc.RootElement.GetProperty("candidates-recorded").GetBoolean(),
            "the corpus cannot answer cost, and the report must say so rather than guess");
    }

    [TestMethod]
    public void TheShippedScriptRunsAgainstTheRealCorpus()
    {
        // The half the fixtures cannot cover: reading the charter's role table and the corpus
        // off disk. Asserts it runs and produces a row per declared role, not what the numbers
        // are — those change as the corpus grows, and a test pinned to them would be rewritten
        // every interrogation rather than telling anyone anything.
        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", ScriptPath, "-AsJson",
            },
            WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.AreEqual(0, p.ExitCode, "the script failed against the real corpus: " + stderr);

        using JsonDocument doc = JsonDocument.Parse(stdout.Trim());
        int rows = doc.RootElement.GetProperty("rows").GetArrayLength();
        Assert.AreEqual(Roles.Length, rows,
            "one row per role the charter declares, so a role added to the charter appears here");
    }
}
