using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The parity check between the backlog file and the work-item board.
/// </summary>
/// <remarks>
/// <b>Why this exists at all.</b> On 2026-09-25, asked "are you updating the stories as you
/// complete them?", the answer was no. Ten items existed only in <c>FINDINGS.md</c>, and four
/// work items sat in <c>New</c> after the work was finished and released. Two representations
/// of one list, drifting, with nothing that failed when they disagreed — which is exactly what
/// INV-03 forbids, in the repository that declares INV-03.
/// <para>
/// <b>Why nothing caught it.</b> Closing a work item is not part of any ceremony.
/// <c>verify</c> checks the build, <c>land</c> checks the PR, <c>retro</c> reads the findings.
/// Nothing reads the board, so staleness was free.
/// </para>
/// <para>
/// <b>What is tested, and what is not.</b> The comparison needs <c>az</c> and a credentialled
/// board connection, which CI has neither of — so the gathering is not covered here and the
/// script reports plus returns a non-zero exit code instead of gating a merge. What IS covered
/// is every judgement about what counts as a mismatch, driven from fixtures. That split is
/// deliberate: the untestable half collects facts and makes no decisions.
/// </para>
/// <para>
/// The harness lifts <c>Compare-SdlcBacklogState</c> out of the shipped file by AST rather than
/// by regex or by copying it, so the test runs the real function and incidentally proves the
/// script parses. A copy of the logic in the test would be a third representation of it.
/// </para>
/// </remarks>
[TestClass]
public sealed class BacklogParityTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Compare-SdlcBacklog.ps1");

    /// <summary>
    /// Runs <c>Compare-SdlcBacklogState</c> against fixture state and returns its result.
    /// </summary>
    /// <param name="fileEntries">PowerShell hashtable body: story id to $true when open.</param>
    /// <param name="boardEntries">PowerShell hashtable body: story id to @{Id=..;State='..'}.</param>
    private static JsonDocument Compare(string fileEntries, string boardEntries)
    {
        // Pull the one function out of the shipped script by AST. Dot-sourcing would execute
        // the gather section and reach for `az`; a regex would go stale the first time the
        // file is reformatted.
        string harness = string.Join(Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            "Set-StrictMode -Version Latest",
            "$tokens = $null; $errors = $null",
            "$ast = [System.Management.Automation.Language.Parser]::ParseFile(",
            "    '" + ScriptPath.Replace("'", "''", StringComparison.Ordinal) + "', [ref] $tokens, [ref] $errors)",
            "if ($errors.Count -gt 0) { throw \"the script does not parse: $($errors[0].Message)\" }",
            "$fn = $ast.FindAll({",
            "    $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] -and",
            "    $args[0].Name -eq 'Compare-SdlcBacklogState' }, $true)",
            "if (-not $fn) { throw 'Compare-SdlcBacklogState is not in the script' }",
            ". ([scriptblock]::Create($fn[0].Extent.Text))",
            "$file = @{" + fileEntries + "}",
            "$board = @{" + boardEntries + "}",
            "$r = Compare-SdlcBacklogState -FileState $file -BoardState $board",
            // Project to exactly what the assertions need, so a shape change surfaces here.
            "[pscustomobject] @{",
            "    mismatches = @($r.mismatches)",
            "    order = @($r.rows | ForEach-Object { $_.story })",
            "    openInFile = $r.'open-in-file'",
            "    openOnBoard = $r.'open-on-board'",
            "} | ConvertTo-Json -Depth 6 -Compress");

        string temp = Path.Combine(Path.GetTempPath(), "parity-" + Guid.NewGuid().ToString("N")[..8] + ".ps1");

        // A BOM, because Windows PowerShell 5.1 reads a BOM-less file as ANSI. Everything here
        // is ASCII so the failure would be silent, which is how it went unnoticed once already.
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

    /// <summary><c>US-9</c> before <c>US-10</c>, which string ordering gets backwards.</summary>
    private static readonly string[] NumericOrder = ["US-2", "US-9", "US-10", "US-24"];

    private static string[] Mismatches(JsonDocument doc) =>
        doc.RootElement.GetProperty("mismatches").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

    [TestMethod]
    public void AgreementIsSilent()
    {
        // The state the board and the file were left in: twenty open, five closed, no argument.
        using JsonDocument doc = Compare(
            "'US-01' = $true; 'US-03' = $false; 'US-16' = $false; 'US-17' = $true",
            "'US-01' = @{Id=67;State='New'}; 'US-03' = @{Id=69;State='Closed'}; " +
            "'US-16' = @{Id=82;State='Closed'}; 'US-17' = @{Id=83;State='New'}");

        Assert.AreEqual(0, Mismatches(doc).Length,
            "agreement must produce nothing: a check that cries wolf gets switched off");
        Assert.AreEqual(2, doc.RootElement.GetProperty("openInFile").GetInt32());
        Assert.AreEqual(2, doc.RootElement.GetProperty("openOnBoard").GetInt32());
    }

    [TestMethod]
    public void WorkFinishedOnTheBoardButStillOpenInTheFileIsAMismatch()
    {
        using JsonDocument doc = Compare(
            "'US-03' = $true",
            "'US-03' = @{Id=69;State='Closed'}");

        string[] m = Mismatches(doc);
        Assert.AreEqual(1, m.Length, "expected exactly one mismatch, got: " + string.Join(" | ", m));
        StringAssert.Contains(m[0], "US-03", StringComparison.Ordinal);
        StringAssert.Contains(m[0], "open in the file", StringComparison.Ordinal);
    }

    [TestMethod]
    public void WorkFinishedInTheFileButStillNewOnTheBoardIsAMismatch()
    {
        // THE ACTUAL DRIFT. `US-03` was released and `US-04`/`US-05` dissolved, and all three
        // sat in `New` for days. This is the direction that a person looking at the board gets
        // wrong: it makes finished work look outstanding.
        using JsonDocument doc = Compare(
            "'US-04' = $false; 'US-05' = $false",
            "'US-04' = @{Id=70;State='New'}; 'US-05' = @{Id=71;State='Active'}");

        string[] m = Mismatches(doc);
        Assert.AreEqual(2, m.Length, "expected both, got: " + string.Join(" | ", m));
        Assert.IsTrue(m.All(x => x.Contains("closed in the file", StringComparison.Ordinal)),
            "each should name the direction of the disagreement: " + string.Join(" | ", m));
    }

    [TestMethod]
    public void AStoryMissingFromTheBoardIsAMismatchEvenWhenClosedInTheFile()
    {
        // `US-16` was done before it ever reached the board. Recording it there anyway is the
        // point: the board is what a teammate sees, including what was already finished and
        // why. So "closed in the file" does not excuse "absent from the board".
        using JsonDocument doc = Compare(
            "'US-16' = $false; 'US-24' = $true",
            "");

        string[] m = Mismatches(doc);
        Assert.AreEqual(2, m.Length, "both should be reported, got: " + string.Join(" | ", m));
        Assert.IsTrue(m.All(x => x.Contains("missing from the board", StringComparison.Ordinal)),
            string.Join(" | ", m));
    }

    [TestMethod]
    public void AStoryOnTheBoardButNotInTheFileIsAMismatch()
    {
        // The more alarming direction: work is being tracked that the authority on what is
        // true does not know about.
        using JsonDocument doc = Compare(
            "'US-01' = $true",
            "'US-01' = @{Id=67;State='New'}; 'US-99' = @{Id=120;State='New'}");

        string[] m = Mismatches(doc);
        Assert.AreEqual(1, m.Length, string.Join(" | ", m));
        StringAssert.Contains(m[0], "US-99", StringComparison.Ordinal);
        StringAssert.Contains(m[0], "not in the backlog file", StringComparison.Ordinal);
    }

    [TestMethod]
    public void RemovedCountsAsClosed()
    {
        // A story deleted from the board is finished as far as the board is concerned. Treating
        // `Removed` as open would report a permanent mismatch nobody can clear, and a check
        // with an unclearable failure is a check people learn to ignore.
        using JsonDocument doc = Compare(
            "'US-12' = $false",
            "'US-12' = @{Id=78;State='Removed'}");

        Assert.AreEqual(0, Mismatches(doc).Length,
            "Removed must count as closed, or the mismatch can never be cleared");
    }

    [TestMethod]
    public void RowsAreOrderedNumericallyNotLexically()
    {
        // `US-9` before `US-10`, which string ordering gets backwards. The report is read by a
        // person deciding what to pick up, so a list that jumps around costs attention.
        using JsonDocument doc = Compare(
            "'US-2' = $true; 'US-10' = $true; 'US-9' = $true; 'US-24' = $true",
            "'US-2' = @{Id=68;State='New'}; 'US-10' = @{Id=76;State='New'}; " +
            "'US-9' = @{Id=75;State='New'}; 'US-24' = @{Id=90;State='New'}");

        string[] order = doc.RootElement.GetProperty("order").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty).ToArray();

        CollectionAssert.AreEqual(NumericOrder, order,
            "ordering is numeric: " + string.Join(", ", order));
    }
}
