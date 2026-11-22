using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Series;

/// <summary>
/// Which cycles a rejected series abandons, as git refs hold it, read once per request (D-208).
/// </summary>
/// <remarks>
/// <para><strong>A series' abandonment covers only the cycles that exist only in the series.</strong> A cycle it took
/// from the integration branch -- an approved or parked cycle opened there -- is not the series' to stop: it stays on
/// the integration branch as it stands, to be built again. So a cycle is covered when the series' record lists it, it
/// was opened no earlier than the series started (<see cref="SeriesRecord.Covers"/>), and, read anywhere but the
/// integration branch itself, its folder is not on the integration branch.</para>
/// <para><strong>Read once.</strong> Every caller asks this of many cycles, and the answers rest on the same few refs:
/// the series branches this clone knows, each series' record and abandonment at a ref, and whether a cycle's folder is
/// on the integration branch. Each is asked of git once and kept for the request.</para>
/// </remarks>
/// <param name="repository">The repository.</param>
/// <param name="root">The solution's root.</param>
/// <param name="integration">The integration branch's name.</param>
internal sealed class SeriesAbandonment(IRepository repository, string root, string integration)
{
    private readonly Dictionary<(string Reference, string Id), SeriesRecord?> _records = [];
    private readonly Dictionary<CycleId, bool> _onIntegration = [];
    private readonly Dictionary<(string Reference, CycleId Cycle), string?> _leads = [];
    private IReadOnlyList<string>? _ids;
    private IReadOnlyList<string>? _integrations;
    private IReadOnlyList<(string Branch, string Id)>? _branches;

    /// <summary>The integration branch's refs this clone has, the remote's first.</summary>
    public IReadOnlyList<string> Integrations => _integrations ??=
        [.. new[] { "origin/" + integration, integration }.Where(name => repository.RefExists(root, name))];

    /// <summary>Every series branch this clone knows, its own before the remote's, with the series it is.</summary>
    public IReadOnlyList<(string Branch, string Id)> Branches => _branches ??=
    [
        .. repository.Branches(root, RunBranch.SeriesPrefix)
            .OrderBy(name => name.StartsWith("origin/", StringComparison.Ordinal))
            .ThenBy(name => name, StringComparer.Ordinal)
            .Select(name => (Name: name, Parsed: RunBranch.TryParse(Bare(name), out RunKind kind, out string? id) && kind == RunKind.Series ? id : null))
            .Where(entry => entry.Parsed is not null)
            .Select(entry => (entry.Name, entry.Parsed!)),
    ];

    private IReadOnlyList<string> Ids => _ids ??= SeriesRecord.KnownIds(repository, root);

    /// <summary>Whether a cycle's folder is on the integration branch, locally or on the remote.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>True when it is.</returns>
    public bool OnIntegration(CycleId cycle)
    {
        if (!_onIntegration.TryGetValue(cycle, out bool on))
        {
            string folder = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.ToString());
            on = Integrations.Any(name => repository.PathExistsAt(root, name, folder));
            _onIntegration[cycle] = on;
        }

        return on;
    }

    /// <summary>A series' record at a ref when its abandonment is there too; null when either is missing.</summary>
    /// <param name="reference">The ref.</param>
    /// <param name="id">The series.</param>
    /// <returns>The record, or null.</returns>
    public SeriesRecord? Rejected(string reference, string id)
    {
        if (_records.TryGetValue((reference, id), out SeriesRecord? known))
        {
            return known;
        }

        string folder = SeriesRecord.RelativeDirectory(id) + "/";

        SeriesRecord? record = repository.PathExistsAt(root, reference, folder + SeriesRecord.AbandonedFileName)
            && repository.ReadAt(root, reference, folder + SeriesRecord.FileName) is { } json
                ? SeriesRecord.Parse(json, folder + SeriesRecord.FileName).ValueOrDefault
                : null;

        _records[(reference, id)] = record;
        return record;
    }

    /// <summary>Whether a series' abandonment at a ref covers a cycle.</summary>
    /// <param name="reference">The ref.</param>
    /// <param name="id">The series.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>True when it does.</returns>
    public bool AbandonsAt(string reference, string id, CycleId cycle)
    {
        if (Rejected(reference, id) is not { } record
            || !record.Covers(cycle)
            || (!Integrations.Contains(reference, StringComparer.Ordinal) && OnIntegration(cycle)))
        {
            return false;
        }

        return record.Lists(cycle, null) || (record.HasUnresolved && record.Lists(cycle, LeadAt(reference, cycle)));
    }

    /// <summary>Whether any series' abandonment at a ref covers a cycle.</summary>
    /// <param name="reference">The ref.</param>
    /// <param name="cycle">The cycle.</param>
    /// <returns>True when one does.</returns>
    public bool AbandonedAt(string reference, CycleId cycle) => Ids.Any(id => AbandonsAt(reference, id, cycle));

    /// <summary>Whether a series' abandonment on the integration branch covers a cycle.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>True when one does.</returns>
    public bool AbandonedOnIntegration(CycleId cycle) => Integrations.Any(name => AbandonedAt(name, cycle));

    /// <summary>The series branch whose abandonment covers a cycle, its own before the remote's; null when none does.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The branch, or null.</returns>
    public string? BranchFor(CycleId cycle) =>
        Branches.FirstOrDefault(entry => AbandonsAt(entry.Branch, entry.Id, cycle)).Branch;

    private static string Bare(string name) => name.StartsWith("origin/", StringComparison.Ordinal) ? name["origin/".Length..] : name;

    private string? LeadAt(string reference, CycleId cycle)
    {
        if (!_leads.TryGetValue((reference, cycle), out string? lead))
        {
            string intent = string.Join('/', Cycle.SdlcDirectoryName, Cycle.CyclesDirectoryName, cycle.ToString(), Cycle.FileNameOf(ArtifactKind.Intent));
            lead = repository.ReadAt(root, reference, intent) is { } text ? SeriesRecord.LeadOf(text, intent) : null;
            _leads[(reference, cycle)] = lead;
        }

        return lead;
    }
}
