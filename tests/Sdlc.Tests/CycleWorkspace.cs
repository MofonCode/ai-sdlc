using System.Globalization;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;
using Sdlc.Core.Solutions;

namespace Sdlc.Tests;

/// <summary>
/// A scratch solution on disk, for exercising the parts of the gate that read files.
/// </summary>
/// <remarks>
/// The gate's whole job is to read what is actually on disk and recompute from it, so testing it
/// against an in-memory abstraction would test something else. This builds a real directory under the
/// temp path and removes it afterwards.
/// </remarks>
internal sealed class CycleWorkspace : IDisposable
{
    private CycleWorkspace(string root, CycleId id)
    {
        Root = root;
        Cycle = new Cycle(id, root);
    }

    /// <summary>The scratch solution's root.</summary>
    public string Root { get; }

    /// <summary>The cycle under test.</summary>
    public Cycle Cycle { get; }

    /// <summary>Creates a workspace holding one cycle's directory.</summary>
    /// <param name="id">The cycle id to use, defaulting to a fixed one so output is comparable.</param>
    /// <returns>The workspace.</returns>
    public static CycleWorkspace Create(string id = "20260929_1456_32")
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed), $"'{id}' is not a cycle id.");

        string root = Path.Combine(Path.GetTempPath(), "sdlc-tests", Guid.NewGuid().ToString("N"));
        CycleWorkspace workspace = new(root, parsed.Value);

        Directory.CreateDirectory(workspace.Cycle.Directory);
        return workspace;
    }

    /// <summary>
    /// Writes one artifact, shaped the way the schema asks for.
    /// </summary>
    /// <remarks>
    /// Schema-valid rather than minimal, because a fixture that would be rejected by the schema makes
    /// every test built on it prove less than it looks like it proves -- and the chain check reads shape.
    /// </remarks>
    /// <param name="kind">Which document.</param>
    /// <param name="body">What to put in each section, which is what moves its hash.</param>
    public void WriteArtifact(ArtifactKind kind, string body = "content")
    {
        List<string> lines =
        [
            "---",
            "framework_version: " + Cycle.FrameworkVersion,
            "cycle: " + Cycle.Id,
        ];

        foreach (string key in ArtifactSchema.BodyKeysOf(kind))
        {
            lines.Add(key + ": " + key switch
            {
                "type" => "feature addition",
                "tier" => "local",
                "outcome" => "pass",
                _ => throw new InvalidOperationException("unknown body key " + key),
            });
        }

        lines.AddRange("---", string.Empty, "# " + Cycle.NameOf(kind), string.Empty);

        foreach (string section in ArtifactSchema.BodySectionsOf(kind))
        {
            lines.AddRange("## " + section, string.Empty, body, string.Empty);
        }

        lines.Add("## " + ArtifactSchema.OpenSection);

        WriteRaw(Cycle.PathOf(kind), string.Join('\n', lines) + "\n");
    }

    /// <summary>
    /// Writes a spec carrying a given number of numbered acceptance criteria.
    /// </summary>
    /// <param name="criteria">How many criteria, which may be none.</param>
    public void WriteSpec(int criteria)
    {
        List<string> lines =
        [
            "---",
            "framework_version: " + Cycle.FrameworkVersion,
            "cycle: " + Cycle.Id,
            "---",
            string.Empty,
            "# a spec",
            string.Empty,
            "## Acceptance criteria",
            string.Empty,
        ];

        for (int number = 1; number <= criteria; number++)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{number}. Something observable happens, checked by asking for it."));
        }

        lines.AddRange(
            string.Empty,
            "## Out of scope",
            string.Empty,
            "Nothing else.",
            string.Empty,
            "## " + ArtifactSchema.OpenSection);

        WriteRaw(Cycle.PathOf(ArtifactKind.Spec), string.Join('\n', lines) + "\n");
    }

    /// <summary>
    /// Writes a plan whose proof section cites a given number of criteria.
    /// </summary>
    /// <param name="proofs">How many proof entries, numbered from one.</param>
    public void WritePlan(int proofs)
    {
        List<string> lines =
        [
            "---",
            "framework_version: " + Cycle.FrameworkVersion,
            "cycle: " + Cycle.Id,
            "tier: local",
            "---",
            string.Empty,
            "# a plan",
            string.Empty,
            "## Shape and departures",
            string.Empty,
            "No departure from the declared defaults.",
            string.Empty,
            "## Files",
            string.Empty,
            "| File | Change |",
            "|---|---|",
            "| `src/Thing.cs` | new |",
            string.Empty,
            "## Proof",
            string.Empty,
        ];

        for (int number = 1; number <= proofs; number++)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{number}. `Thing_does_the_thing_{number}` asserts it."));
        }

        lines.AddRange(
            string.Empty,
            "## Risk",
            string.Empty,
            "One file, reversible.",
            string.Empty,
            "## " + ArtifactSchema.OpenSection);

        WriteRaw(Cycle.PathOf(ArtifactKind.Plan), string.Join('\n', lines) + "\n");
    }

    /// <summary>
    /// Writes a normalised test summary in which the plan's named tests passed.
    /// </summary>
    /// <param name="proofs">How many of them, matching WritePlan.</param>
    /// <param name="failing">Which proof numbers should record a failure instead.</param>
    public void WriteTestSummary(int proofs, params int[] failing)
    {
        List<RecordedTest> tests = [];

        for (int number = 1; number <= proofs; number++)
        {
            tests.Add(new RecordedTest(
                string.Create(CultureInfo.InvariantCulture, $"Thing.Tests.Thing_does_the_thing_{number}"),
                failing.Contains(number) ? TestOutcome.Failed : TestOutcome.Passed,
                failing.Contains(number) ? "it did not" : null));
        }

        Directory.CreateDirectory(Cycle.EvidenceDirectory);

        WriteRaw(
            Path.Combine(Cycle.EvidenceDirectory, "tests.json"),
            TestSummaryReader.Write(new TestSummary(
                Cycle.FrameworkVersion,
                Cycle.Id.ToString(),
                DateTimeOffset.UnixEpoch,
                tests)));
    }

    /// <summary>Writes a verify document with a given outcome.</summary>
    /// <param name="outcome">pass, blocked or escalated.</param>
    /// <param name="outcome">The outcome value, written raw so a test can quote it.</param>
    /// <param name="alsoInFrontmatter">
    /// Extra frontmatter lines, for the shapes a line scan used to mishandle -- a comment, or a block
    /// scalar whose text contains <c>outcome:</c>.
    /// </param>
    public void WriteVerify(string outcome, params string[] alsoInFrontmatter)
    {
        List<string> lines =
        [
            "---",
            "framework_version: " + Cycle.FrameworkVersion,
            "cycle: " + Cycle.Id,
            .. alsoInFrontmatter,
            "outcome: " + outcome,
            "---",
            string.Empty,
            "# a verify",
            string.Empty,
            "## Criteria",
            string.Empty,
            "1. Met. Thing_does_the_thing_1 passed.",
            string.Empty,
            "## " + ArtifactSchema.OpenSection,
        ];

        WriteRaw(Cycle.PathOf(ArtifactKind.Verify), string.Join('\n', lines) + "\n");
    }

    /// <summary>Writes all three planning documents.</summary>
    public void WriteAllArtifacts()
    {
        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            WriteArtifact(kind);
        }
    }

    /// <summary>Writes an interrogation round that found nothing.</summary>
    /// <param name="kind">The artifact interrogated.</param>
    /// <param name="round">The round number.</param>
    public void WriteCleanRound(ArtifactKind kind, int round = 1, string? upstream = null) =>
        WriteRound(kind, round, findings: string.Empty, upstream);

    /// <summary>Writes an interrogation round holding one open blocking finding.</summary>
    /// <param name="kind">The artifact interrogated.</param>
    /// <param name="round">The round number.</param>
    /// <param name="id">The finding's id.</param>
    /// <param name="cites">The constraint it cites.</param>
    public void WriteBlockingRound(
        ArtifactKind kind,
        int round = 1,
        string id = "F_01",
        string cites = "CON_06")
    {
        WriteRound(kind, round, string.Create(
            CultureInfo.InvariantCulture,
            $$"""
              {
                "id": "{{id}}",
                "severity": "blocking",
                "disposition": "open",
                "cites": "{{cites}}",
                "statement": "something that must be answered.",
                "locus": "somewhere",
                "remedy": "answer it."
              }
              """));
    }

    /// <summary>Writes an interrogation round holding one non-blocking finding.</summary>
    /// <param name="kind">The artifact interrogated.</param>
    /// <param name="round">The round number.</param>
    /// <param name="id">The finding's id, which keeps its value across rounds.</param>
    /// <param name="cites">The constraint it cites, or null for one that cites nothing.</param>
    /// <param name="disposition">Where it has got to.</param>
    public void WriteSuggestionRound(
        ArtifactKind kind,
        int round = 1,
        string id = "F_02",
        string? cites = "CON_12",
        string disposition = "open")
    {
        string citation = cites is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"\"cites\": \"{cites}\",");

        WriteRound(kind, round, string.Create(
            CultureInfo.InvariantCulture,
            $$"""
              {
                "id": "{{id}}",
                "severity": "suggestion",
                "disposition": "{{disposition}}",
                {{citation}}
                "statement": "something worth doing that nothing forbids."
              }
              """));
    }

    /// <summary>Writes a round in which a finding has been answered.</summary>
    /// <param name="kind">The artifact interrogated.</param>
    /// <param name="round">The round number.</param>
    /// <param name="id">The finding's id, which keeps its value across rounds.</param>
    public void WriteAnsweredRound(ArtifactKind kind, int round, string id = "F_01")
    {
        WriteRound(kind, round, string.Create(
            CultureInfo.InvariantCulture,
            $$"""
              {
                "id": "{{id}}",
                "severity": "blocking",
                "disposition": "answered",
                "cites": "CON_06",
                "statement": "something that was answered."
              }
              """));
    }

    /// <summary>Writes a raw round, for cases the helpers do not cover.</summary>
    /// <param name="kind">The artifact interrogated.</param>
    /// <param name="round">The round number.</param>
    /// <param name="findings">The findings array's contents, without its brackets.</param>
    /// <param name="upstream">
    /// The upstream hash to record. Null records the real hash of the upstream on disk, which is what
    /// an interrogation does; passing a value is how a test makes a round look judged against an
    /// upstream that has since changed.
    /// </param>
    public void WriteRound(ArtifactKind kind, int round, string findings, string? upstream = null)
    {
        Directory.CreateDirectory(Cycle.FindingsDirectory);

        // An intent has no upstream, so its rounds record none. Everything below it records the hash of
        // the document above as it stands when the round runs.
        string recorded = Cycle.UpstreamOf(kind) is { } above
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"\"upstream_sha256\": \"{upstream ?? HashOf(above)}\",")
            : string.Empty;

        File.WriteAllText(
            Cycle.FindingsPath(kind, round),
            string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                  {
                    "framework_version": "1.0.0",
                    "cycle": "{{Cycle.Id}}",
                    "artifact": "{{Cycle.NameOf(kind)}}.md",
                    {{recorded}}
                    "interrogated_at": "2026-10-01T10:00:00Z",
                    "round": {{round}},
                    "findings": [{{findings}}]
                  }
                  """));
    }

    /// <summary>
    /// Writes a file verbatim, for a shape the typed helpers deliberately cannot produce.
    /// </summary>
    /// <param name="path">Where to write it.</param>
    /// <param name="content">What to write.</param>
    public static void WriteRaw(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>The canonical hash of one artifact as it stands on disk.</summary>
    /// <param name="kind">The artifact.</param>
    /// <returns>Its hash, or a placeholder when it has not been written.</returns>
    public string HashOf(ArtifactKind kind)
    {
        string path = Cycle.PathOf(kind);
        return File.Exists(path)
            ? Sdlc.Core.Artifacts.ContentHash.Of(File.ReadAllText(path))
            : "0000000000000000000000000000000000000000000000000000000000000000";
    }

    /// <summary>Writes an approval ledger holding one decision.</summary>
    /// <param name="decision">The decision, as it is written in the file.</param>
    /// <param name="hashes">
    /// Each document's recorded hash. Null records the real hash of what is on disk, which is the
    /// normal case; passing a value is how a test makes a document look edited after approval.
    /// </param>
    /// <param name="reason">The reason, required on anything but an approval.</param>
    public void WriteApproval(
        string decision = "approved",
        IReadOnlyDictionary<ArtifactKind, string>? hashes = null,
        string? reason = null)
    {
        List<string> lines =
        [
            "---",
            string.Create(CultureInfo.InvariantCulture, $"cycle: {Cycle.Id}"),
            "framework_version: 1.0.0",
            "decisions:",
            string.Create(CultureInfo.InvariantCulture, $"  - decision: {decision}"),
            "    by: Peter Almanzar",
            "    at: 2026-10-01T12:00:00Z",
            "    hashes:",
        ];

        foreach (ArtifactKind kind in Cycle.PlanningDocuments)
        {
            string hash = hashes is not null && hashes.TryGetValue(kind, out string? given)
                ? given
                : Sdlc.Core.Artifacts.ContentHash.Of(File.ReadAllText(Cycle.PathOf(kind)));

            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"      {Cycle.FileNameOf(kind)}: {hash}"));
        }

        if (reason is not null)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    reason: {reason}"));
        }

        lines.Add("---");

        File.WriteAllText(Cycle.ApprovalPath, string.Join('\n', lines) + "\n");
    }

    /// <summary>Records that the cycle was abandoned, with a reason, as a person would.</summary>
    public void Abandon() => File.WriteAllText(
        Cycle.AbandonedPath,
        string.Create(
            CultureInfo.InvariantCulture,
            $"---\ncycle: {Cycle.Id}\nby: Peter Almanzar\nat: 2026-10-01T12:00:00Z\n---\n\n# Why this cycle stopped\n\nSuperseded by a narrower intent.\n"));

    /// <summary>Writes the solution document.</summary>
    /// <param name="profile">The profile that governs the solution.</param>
    /// <param name="origin">Where the code came from.</param>
    public void WriteSolution(string profile, SolutionOrigin origin) => WriteRaw(
        SolutionDocument.PathIn(Root),
        string.Create(
            CultureInfo.InvariantCulture,
            $"---\nprofile: {profile}\norigin: {SolutionDocument.NameOf(origin)}\n---\n\n# a solution\n"));

    /// <summary>
    /// Writes a solution constraints file holding the numbered constraints given.
    /// </summary>
    /// <remarks>
    /// Takes the numbers rather than a count, so a test can leave a gap -- which is the case that
    /// matters, because a gap is the record of a removed constraint rather than a free id.
    /// </remarks>
    /// <param name="numbers">The LOC_ numbers to write.</param>
    public void WriteSolutionConstraints(params int[] numbers)
    {
        List<string> lines = ["# This solution's own constraints", string.Empty];

        foreach (int number in numbers)
        {
            lines.AddRange(
                string.Create(CultureInfo.InvariantCulture, $"## LOC_{number:00} — rule {number}"),
                string.Empty,
                "**Applies to:** a plan, at approval.",
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"**Forbids:** something {number} rules out."),
                string.Empty,
                string.Create(CultureInfo.InvariantCulture, $"**Why:** the cost rule {number} avoids."),
                string.Empty);
        }

        WriteRaw(
            Path.Combine(Root, Cycle.SdlcDirectoryName, "constraints.md"),
            string.Join('\n', lines));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A scratch directory left behind under the temp path is not worth failing a test over.
        }
    }
}
