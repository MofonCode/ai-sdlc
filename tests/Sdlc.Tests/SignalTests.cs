using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// The charter amendment signals, computed from the corpus on demand.
/// </summary>
/// <remarks>
/// Two properties carry the weight here.
/// <para>
/// <c>rejected</c> and <c>accepted-wont-fix</c> must never be merged. They feed OPPOSITE
/// signals: <c>rejected</c> means the finding was wrong and says nothing about the invariant,
/// while <c>accepted-wont-fix</c> means the finding was right and we knowingly did not act.
/// Merging them inflates the override count and makes a sound invariant look too broad —
/// corrupting the only evidence the charter improves from.
/// </para>
/// <para>
/// And nothing is stored. A stored signal would be a second list beside the corpus, and the
/// charter would grow with every cycle. The script is read-only by construction, which is why
/// it emits no timestamp: that would be the only part of its output capable of going stale.
/// </para>
/// </remarks>
[TestClass]
public sealed class SignalTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Get-SdlcSignals.ps1");

    private static string? _repo;

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-signals-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".sdlc", "cycles", "20260925-aa01", "findings"));

        // A real preventer for INV-04 to name. The fixture repo has no tools/ directory, so
        // an earlier draft pointed at one and the target correctly failed to resolve — which
        // is the test asserting the wrong thing rather than the script being wrong.
        Directory.CreateDirectory(Path.Combine(_repo, "templates"));
        File.WriteAllText(Path.Combine(_repo, "templates", "plan.md"), "# a template that exists");

        // INV-01 is cited and overridden three times -> too broad.
        // INV-02 is cited and rejected three times   -> says nothing about the rule.
        // INV-03 is never cited                      -> zero cites.
        // INV-04 names a Prevented-by that EXISTS.
        // INV-05 names a Prevented-by that does NOT resolve.
        // ~~INV-06~~ is struck through: a record of absence, not a declaration.
        File.WriteAllText(Path.Combine(_repo, ".sdlc", "CHARTER.md"), """
            ---
            version: 1
            solution: fixture
            ---

            # Charter — fixture

            ### INV-01 · Too broad

                Forbids:  something.

            ### INV-02 · Noisy role, not a broad rule

                Forbids:  something else.

            ### INV-03 · Never cited

                Forbids:  a third thing.

            ### INV-04 · Prevented upstream

                Forbids:  a fourth thing.
                Prevented-by: templates/plan.md

            ### INV-05 · Claims prevention that does not resolve

                Forbids:  a fifth thing.
                Prevented-by: tools/Does-Not-Exist.ps1

            ### ~~INV-06~~ · Removed

                This id is a record of absence.
            """);

        List<string> findings = [];

        void Add(string id, string invariant, string state, string? disposition, string category)
        {
            string disp = disposition is null ? string.Empty : $", \"disposition\": \"{disposition}\", \"disposition-reason\": \"because\"";
            findings.Add($$"""
                { "id": "{{id}}", "category": "{{category}}", "state": "{{state}}", "stage": "spec",
                  "detection": "reasoning", "summary": "s",
                  "cites": [{ "kind": "invariant", "id": "{{invariant}}" }]{{disp}} }
                """);
        }

        for (int i = 1; i <= 3; i++)
        {
            Add($"o{i}", "INV-01", "blocking", "accepted-wont-fix", "ambiguity");
            Add($"r{i}", "INV-02", "blocking", "rejected", "ambiguity");
        }

        // Four uncited suggestions in one category -> a cluster, which argues for ADDITION.
        for (int i = 1; i <= 4; i++)
        {
            findings.Add($$"""
                { "id": "s{{i}}", "category": "missing-case", "state": "suggested", "stage": "spec",
                  "detection": "reasoning", "summary": "s" }
                """);
        }

        // Two in another category -> below the threshold, deliberately not a cluster.
        for (int i = 1; i <= 2; i++)
        {
            findings.Add($$"""
                { "id": "t{{i}}", "category": "untestable", "state": "suggested", "stage": "spec",
                  "detection": "reasoning", "summary": "s" }
                """);
        }

        File.WriteAllText(
            Path.Combine(_repo, ".sdlc", "cycles", "20260925-aa01", "findings", "spec.r1.json"),
            "{\n  \"cycle-id\": \"20260925-aa01\",\n  \"target\": \"spec\",\n  \"charter-version\": 1,\n"
            + "  \"findings\": [\n" + string.Join(",\n", findings) + "\n  ]\n}");

        Git("init --initial-branch=master");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
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

    private static void Git(string args) => Run("git", args);

    private static string Run(string exe, string args)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = _repo!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Assert.AreEqual(0, p.ExitCode, $"{exe} {args} failed: {stderr}");
        return stdout;
    }

    private static JsonElement Signals() =>
        JsonDocument.Parse(Run("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\"")).RootElement.Clone();

    private static JsonElement Invariant(string id)
    {
        foreach (JsonElement row in Signals().GetProperty("invariants").EnumerateArray())
        {
            if (row.GetProperty("id").GetString() == id)
            {
                return row.Clone();
            }
        }

        Assert.Fail($"{id} is not in the survey");
        return default;
    }

    [TestMethod]
    public void RejectedAndAcceptedWontFixAreCountedApart()
    {
        // THE ONE THAT MATTERS. Dismissing a bogus finding must not inflate the override
        // count, or a sound invariant looks too broad and gets narrowed on false evidence.
        JsonElement broad = Invariant("INV-01");
        JsonElement noisy = Invariant("INV-02");

        Assert.AreEqual(3, broad.GetProperty("overridden").GetInt32());
        Assert.AreEqual(0, broad.GetProperty("rejected").GetInt32());

        Assert.AreEqual(3, noisy.GetProperty("rejected").GetInt32());
        Assert.AreEqual(0, noisy.GetProperty("overridden").GetInt32());
    }

    [TestMethod]
    public void OnlyTheOverriddenInvariantRaisesTheTooBroadSignal()
    {
        // Both were cited three times and both were dismissed. Only one of those dismissals
        // says anything about the rule.
        Assert.AreEqual("high-override", Invariant("INV-01").GetProperty("signal").GetString());
        Assert.IsNull(Invariant("INV-02").GetProperty("signal").GetString(),
            "three rejections say the ROLE is noisy, not that the invariant is broad");
    }

    [TestMethod]
    public void TheTooBroadReadingArguesForNarrowingNotDeletion()
    {
        Assert.IsTrue(Invariant("INV-01").GetProperty("reading").GetString()!
            .Contains("NARROWING, not deletion", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AnInvariantNothingCitesRaisesTheZeroCiteSignal()
    {
        // The one trigger that produces no event of its own: nothing will ever prompt you to
        // notice it, so the query has to go looking.
        JsonElement row = Invariant("INV-03");

        Assert.AreEqual(0, row.GetProperty("cites").GetInt32());
        Assert.AreEqual("zero-cites", row.GetProperty("signal").GetString());
        Assert.IsTrue(row.GetProperty("reading").GetString()!
            .Contains("Three readings, opposite actions", StringComparison.Ordinal),
            "the reading must not pick one of the three; that is a judgment");
    }

    [TestMethod]
    public void AVerifiedPreventedByTargetIsDistinguishedFromAnUnresolvableOne()
    {
        // `Prevented-by:` must name a RESOLVABLE target and be verified before zero cites may
        // read as working as designed. Otherwise the field converts an unfalsifiable claim
        // into a permanent exemption, which is the failure it exists to prevent.
        Assert.AreEqual("zero-cites-prevented", Invariant("INV-04").GetProperty("signal").GetString());
        Assert.AreEqual("zero-cites-prevention-unverified", Invariant("INV-05").GetProperty("signal").GetString());

        Assert.IsTrue(Invariant("INV-04").GetProperty("reading").GetString()!
            .Contains("still REJECTS", StringComparison.Ordinal),
            "existing is not enough: the mechanism must still reject a violation, and that is not checked here");
    }

    [TestMethod]
    public void AStruckThroughIdIsNotSurveyed()
    {
        // A struck id is a record of absence, not a declaration. Surveying one would report a
        // removed invariant as having zero cites, which is true and meaningless.
        foreach (JsonElement row in Signals().GetProperty("invariants").EnumerateArray())
        {
            Assert.AreNotEqual("INV-06", row.GetProperty("id").GetString(),
                "a struck-through id must not be surveyed");
        }
    }

    [TestMethod]
    public void ARecurringSuggestionClusterIsReportedAndASmallOneIsNot()
    {
        // A cluster is the only signal that argues for ADDING an invariant. Two of something
        // is not a theme, and reporting it would train someone to ignore the list.
        List<string> categories = [];
        foreach (JsonElement c in Signals().GetProperty("suggestion-clusters").EnumerateArray())
        {
            categories.Add(c.GetProperty("category").GetString()!);
        }

        CollectionAssert.Contains(categories, "missing-case", "four suggestions is a cluster");
        CollectionAssert.DoesNotContain(categories, "untestable", "two is not");
    }

    [TestMethod]
    public void TheOutputSaysASignalIsNotAVerdict()
    {
        // Stated in the output, not only in the help. Whoever reads this is the person who
        // might be tempted to act on it, and acting means changing the charter.
        string note = Signals().GetProperty("note").GetString()!;

        Assert.IsTrue(note.Contains("prompt to investigate, not a verdict", StringComparison.Ordinal));
        Assert.IsTrue(note.Contains("INV-08", StringComparison.Ordinal));
        Assert.IsTrue(note.Contains("Nothing here is stored", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TheQueryWritesNothing()
    {
        // Read-only by construction. A stored signal is a second list beside the corpus, and
        // the charter would grow with every cycle.
        string before = Run("git", "status --porcelain");
        Signals();
        string after = Run("git", "status --porcelain");

        Assert.AreEqual(before, after, "the survey must leave the tree untouched");
    }

    [TestMethod]
    public void TheUnclassifiedRateIsReportedAsATaxonomyHealthMetric()
    {
        // It measures the TAXONOMY, not any invariant. A high rate means the eleven categories
        // are not describing what interrogation actually finds.
        Assert.IsTrue(Signals().TryGetProperty("unclassified-rate", out JsonElement rate));
        Assert.AreEqual(0.0, rate.GetDouble(), "this fixture classifies everything");
    }
}
