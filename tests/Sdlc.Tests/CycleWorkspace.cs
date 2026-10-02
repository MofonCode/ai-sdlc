using System.Globalization;
using Sdlc.Core.Cycles;

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

    /// <summary>Writes one of the planning documents with throwaway content.</summary>
    /// <param name="kind">Which document.</param>
    /// <param name="body">What to put in it, which only matters for its hash.</param>
    public void WriteArtifact(ArtifactKind kind, string body = "content")
    {
        string path = Cycle.PathOf(kind);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            string.Create(
                CultureInfo.InvariantCulture,
                $"---\nid: {Cycle.Id}\n---\n\n# {Cycle.NameOf(kind)}\n\n{body}\n"));
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

    /// <summary>Records that the cycle was abandoned.</summary>
    public void Abandon() => File.WriteAllText(
        Cycle.AbandonedPath,
        string.Create(
            CultureInfo.InvariantCulture,
            $"---\ncycle: {Cycle.Id}\nabandoned_by: Peter Almanzar\nat: 2026-10-01T12:00:00Z\nreason: superseded\n---\n"));

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
