using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Series;

/// <summary>What started one item of a series' wave.</summary>
public enum SeriesItemKind
{
    /// <summary>A successor that was ready against the series branch: its intent is suggested and accepted unasked.</summary>
    Successor,

    /// <summary>A parked cycle whose predecessors passed on the series branch, taken back.</summary>
    Parked,

    /// <summary>A cycle a person approved and nothing has built, under <c>approval: required</c>.</summary>
    Approved,
}

/// <summary>One thing a wave took, and the cycle it became once there was one.</summary>
/// <param name="Kind">What started it.</param>
/// <param name="Ref">A successor's slug, or the cycle id of a parked or approved cycle.</param>
/// <param name="Cycle">The cycle it is, once known: a successor's is resolved when its intent records it as the lead.</param>
public sealed record SeriesItem(SeriesItemKind Kind, string Ref, string? Cycle);

/// <summary>One wave of a series: what was ready against the series branch when it started.</summary>
/// <param name="Number">Its number, from one.</param>
/// <param name="Started">When it was taken.</param>
/// <param name="Items">What it took.</param>
/// <param name="Done">
/// When the series recorded it built, through <c>sdlc series wave --done</c>, or null while it is open: a series that
/// stopped partway through it finishes it before the next is taken (D-230).
/// </param>
/// <param name="Size">
/// How many items it could take, set from the series' wave size when it was taken: the wave owns its own limits (D-236).
/// Null for a wave recorded before the key existed.
/// </param>
/// <param name="Parallel">
/// How many of its cycles build at once, set from the series' setting when it was taken (D-236). Null for a wave recorded
/// before the key existed, which built one after another.
/// </param>
public sealed record SeriesWave(int Number, DateTimeOffset Started, IReadOnlyList<SeriesItem> Items, DateTimeOffset? Done = null, int? Size = null, int? Parallel = null);

/// <summary>One item on a series' list: what it may take, frozen when it starts (D-231).</summary>
/// <param name="Kind">What starts it.</param>
/// <param name="Ref">A successor's slug, or the cycle id of a parked or approved cycle.</param>
/// <param name="Wave">The wave it was forecast to be taken in when the series started: a forecast, never a promise.</param>
public sealed record SeriesScopeItem(SeriesItemKind Kind, string Ref, int Wave);

/// <summary>
/// A series' own record, <c>.sdlc/series/&lt;id&gt;/series.json</c>: its wave limit, how many items a wave may take,
/// the approval setting it started under, its list, and what each wave took.
/// </summary>
/// <remarks>
/// <para><strong>Written only by the binary</strong>, committed on the series branch, so it reaches the integration
/// branch with the cycles it ran. It is a grouping, never a merged record: each cycle keeps its own folder, rounds,
/// verification and commits.</para>
/// <para><strong>Membership lives here, not in each intent.</strong> A <c>series:</c> key would change the shape of
/// a planning document the rounds hash, for a fact about the run rather than the intent. So a successor item names its
/// slug, and its cycle is resolved from the intent that records <c>lead: successor:&lt;slug&gt;</c> whenever the binary
/// reads the record through <c>sdlc series wave</c> or <c>sdlc series status</c>.</para>
/// <para><strong>One abandonment covers every cycle that exists only in the series.</strong> A person who rejects the
/// series writes <c>abandoned.md</c> beside this file, and each cycle the record lists that the series opened reads as
/// abandoned, so a harvest can still learn from what was rejected. A cycle the series took from the integration branch
/// -- an approved or parked cycle opened before it started -- is not the series' to abandon: it stays as it stands
/// there, to be built again (D-208). A working copy tells the two apart by <see cref="Covers"/>, a cycle opened before
/// the series started; where git is asked, a cycle whose folder is on the integration branch is left out too.</para>
/// </remarks>
/// <param name="Id">The series' id, shaped as a cycle id.</param>
/// <param name="Started">When it started.</param>
/// <param name="Limit">How many waves it may run.</param>
/// <param name="Approval">The approval setting <c>solution.md</c> declared when it started.</param>
/// <param name="Waves">Each wave taken, in order.</param>
/// <param name="WaveSize">
/// How many items one wave may take; what is ready past it waits for the next wave (D-228). A record written before the
/// key existed reads as <see cref="DefaultWaveSize"/>.
/// </param>
/// <param name="Scope">
/// Its list: everything it may take, in the order it takes them, frozen when it starts (D-231). Each wave takes from it
/// what is ready, so no item outside it is ever taken. Null for a record written before the key existed, or before the
/// list was frozen: such a series takes whatever is ready, as it did.
/// </param>
/// <param name="Fixes">
/// Each fix the series added to itself, in the order added: a fault that stopped a wave's evidence, fixed in a cycle of
/// its own at the head of that wave (D-232). Empty for a record written before the key existed.
/// </param>
/// <param name="Parallel">
/// How many cycles each wave builds at once, which each wave copies when it is taken (D-236). A record written before
/// the key existed reads as <see cref="Runs.WaveRecord.DefaultParallel"/>.
/// </param>
/// <param name="TakenOut">
/// Each cycle a wave took out rather than merge, in the order it did (D-235). Empty for a record written before the key
/// existed.
/// </param>
public sealed record SeriesRecord(string Id, DateTimeOffset Started, int Limit, ApprovalRequirement Approval, IReadOnlyList<SeriesWave> Waves, int WaveSize = SeriesRecord.DefaultWaveSize, IReadOnlyList<SeriesScopeItem>? Scope = null, IReadOnlyList<SeriesFix>? Fixes = null, int Parallel = Runs.WaveRecord.DefaultParallel, IReadOnlyList<Runs.TakenOut>? TakenOut = null)
{
    /// <summary>How many items a wave takes when the person starting the series names no number.</summary>
    public const int DefaultWaveSize = 3;

    /// <summary>The directory every series' folder is in, inside the framework's.</summary>
    public const string DirectoryName = "series";

    /// <summary>The record's file name, in the series' folder.</summary>
    public const string FileName = "series.json";

    /// <summary>The file a person places to reject the series, in its folder.</summary>
    public const string AbandonedFileName = "abandoned.md";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>Whether every wave it may run has been taken.</summary>
    [JsonIgnore]
    public bool Finished => Waves.Count >= Limit;

    /// <summary>A series' folder, relative to the root with forward slashes.</summary>
    /// <param name="id">The series.</param>
    /// <returns>The path.</returns>
    public static string RelativeDirectory(string id) => Cycle.SdlcDirectoryName + "/" + DirectoryName + "/" + id;

    /// <summary>Where a series' record lives.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="id">The series.</param>
    /// <returns>The path.</returns>
    public static string PathIn(string solutionRoot, string id) =>
        Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DirectoryName, id, FileName);

    /// <summary>Where a series' abandonment lives.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="id">The series.</param>
    /// <returns>The path.</returns>
    public static string AbandonedPathIn(string solutionRoot, string id) =>
        Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DirectoryName, id, AbandonedFileName);

    /// <summary>A series that has taken no wave yet.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="started">When it started.</param>
    /// <param name="limit">How many waves it may run.</param>
    /// <param name="approval">The approval setting it starts under.</param>
    /// <param name="waveSize">How many items one wave may take.</param>
    /// <param name="parallel">How many cycles each wave builds at once.</param>
    /// <returns>The record.</returns>
    public static SeriesRecord Start(string id, DateTimeOffset started, int limit, ApprovalRequirement approval, int waveSize = DefaultWaveSize, int parallel = Runs.WaveRecord.DefaultParallel) =>
        new(id, started, limit, approval, [], waveSize, Parallel: parallel);

    /// <summary>Reads a record's text.</summary>
    /// <param name="json">The text.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The record, or why it cannot be read.</returns>
    public static Result<SeriesRecord> Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            SeriesRecord? read = JsonSerializer.Deserialize<SeriesRecord>(json, _options);

            if (read is null || string.IsNullOrWhiteSpace(read.Id))
            {
                return Result.Fail<SeriesRecord>(source + ": the series record names no series.");
            }

            // An absent list is an empty one, and an absent wave size the default, so a record written before a key
            // existed still reads.
            return Result.Ok(read with
            {
                Waves = [.. (read.Waves ?? []).Select(wave => wave with { Items = wave.Items ?? [] })],
                WaveSize = read.WaveSize < 1 ? DefaultWaveSize : read.WaveSize,
                Parallel = read.Parallel < 1 ? Runs.WaveRecord.DefaultParallel : read.Parallel,
                Fixes = read.Fixes is null ? null : [.. read.Fixes.Select(fix => fix with { Holds = fix.Holds ?? [] })],
            });
        }
        catch (JsonException error)
        {
            return Result.Fail<SeriesRecord>(source + ": the series record does not parse: " + error.Message);
        }
    }

    /// <summary>Reads a series' record from a working copy.</summary>
    /// <param name="solutionRoot">The working copy's root.</param>
    /// <param name="id">The series.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The record, or why there is none.</returns>
    public static async Task<Result<SeriesRecord>> Read(string solutionRoot, string id, CancellationToken cancellationToken)
    {
        string path = PathIn(solutionRoot, id);

        return File.Exists(path)
            ? Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path)
            : Result.Fail<SeriesRecord>($"There is no record of series {id} at {path}. Run this in the series' worktree, on series/{id}.");
    }

    /// <summary>The record as the file holds it.</summary>
    /// <returns>Its JSON.</returns>
    public string Write() => JsonSerializer.Serialize(this, _options) + "\n";

    /// <summary>The record with a wave added.</summary>
    /// <param name="started">When the wave was taken.</param>
    /// <param name="items">What it took.</param>
    /// <returns>The new record.</returns>
    public SeriesRecord WithWave(DateTimeOffset started, IReadOnlyList<SeriesItem> items) =>
        this with { Waves = [.. Waves, new SeriesWave(Waves.Count + 1, started, items, Size: WaveSize, Parallel: Parallel)] };

    /// <summary>What names an item on a series' list or in a wave, the way the backlog names it.</summary>
    /// <param name="kind">What starts it.</param>
    /// <param name="reference">A successor's slug, or a cycle id.</param>
    /// <returns><c>successor:&lt;slug&gt;</c> for a successor, else the cycle id.</returns>
    public static string KeyOf(SeriesItemKind kind, string reference) =>
        kind == SeriesItemKind.Successor ? "successor:" + reference : reference;

    /// <summary>The record with its list frozen.</summary>
    /// <param name="scope">Everything it may take, in order.</param>
    /// <returns>The new record.</returns>
    public SeriesRecord WithScope(IReadOnlyList<SeriesScopeItem> scope) => this with { Scope = scope };

    /// <summary>
    /// The record with a fix added to itself (D-232): its successor first in the last wave, and first on the list, so
    /// the wave builds it before anything it holds.
    /// </summary>
    /// <param name="fix">The fix, whose <see cref="SeriesFix.Wave"/> is the last wave's number.</param>
    /// <returns>The new record; the same one when it has no wave.</returns>
    public SeriesRecord WithFix(SeriesFix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);

        if (Waves.Count == 0)
        {
            return this;
        }

        SeriesWave last = Waves[^1];

        return this with
        {
            Waves = [.. Waves.Take(Waves.Count - 1), last with { Items = [new SeriesItem(SeriesItemKind.Successor, fix.Successor, null), .. last.Items] }],
            Scope = Scope is null ? null : [new SeriesScopeItem(SeriesItemKind.Successor, fix.Successor, last.Number), .. Scope],
            Fixes = [.. Fixes ?? [], fix],
        };
    }

    /// <summary>The record with cycles its last wave took out (D-235).</summary>
    /// <param name="taken">What was taken out.</param>
    /// <returns>The new record.</returns>
    public SeriesRecord WithTakenOut(IReadOnlyList<Runs.TakenOut> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        return taken.Count == 0 ? this : this with { TakenOut = [.. TakenOut ?? [], .. taken] };
    }

    /// <summary>What the series took out of a cycle, when it took it out (D-235).</summary>
    /// <param name="cycle">The cycle's id.</param>
    /// <returns>The record of it, or null.</returns>
    public Runs.TakenOut? TakenOutOf(string? cycle) =>
        cycle is null ? null : TakenOut?.LastOrDefault(taken => string.Equals(taken.Cycle, cycle, StringComparison.Ordinal));

    /// <summary>The record with its last wave recorded built.</summary>
    /// <param name="done">When.</param>
    /// <returns>The new record, the same one when it has no wave or its last is already done.</returns>
    public SeriesRecord WithWaveDone(DateTimeOffset done) =>
        Waves.Count == 0 || Waves[^1].Done is not null
            ? this
            : this with { Waves = [.. Waves.Take(Waves.Count - 1), Waves[^1] with { Done = done }] };

    /// <summary>The record with each successor item's cycle filled in from the intents that record it as their lead.</summary>
    /// <remarks>
    /// <strong>Only a cycle opened once its wave was taken resolves an item.</strong> A slug can have been started
    /// before -- by a cycle since abandoned, or by one the series never took -- and taking that one would put a cycle
    /// in the series that it never ran.
    /// </remarks>
    /// <param name="started">Each started slug, with every live cycle whose intent records it as the lead, oldest first.</param>
    /// <returns>The record, the same one when nothing resolved.</returns>
    public SeriesRecord Resolved(IReadOnlyDictionary<string, IReadOnlyList<CycleId>> started)
    {
        ArgumentNullException.ThrowIfNull(started);

        return this with
        {
            Waves =
            [
                .. Waves.Select(wave => wave with
                {
                    Items =
                    [
                        .. wave.Items.Select(item => item.Cycle is null
                            && item.Kind == SeriesItemKind.Successor
                            && started.TryGetValue(item.Ref, out IReadOnlyList<CycleId>? cycles)
                            && cycles.Order().Where(cycle => NotBefore(cycle, wave.Started)).Select(cycle => (CycleId?)cycle).FirstOrDefault() is { } first
                                ? item with { Cycle = first.ToString() }
                                : item),
                    ],
                }),
            ],
        };
    }

    /// <summary>
    /// Whether this series' abandonment can cover a cycle: one it opened, never one opened before it started, which it
    /// can only have taken from the integration branch (D-208).
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <returns>True when the cycle was opened no earlier than the series started, to the second.</returns>
    public bool Covers(CycleId cycle) => NotBefore(cycle, Started);

    /// <summary>Whether a cycle was opened no earlier than a moment, to the second its id records.</summary>
    private static bool NotBefore(CycleId cycle, DateTimeOffset moment) =>
        cycle.Opened >= new DateTimeOffset(moment.UtcDateTime.Year, moment.UtcDateTime.Month, moment.UtcDateTime.Day, moment.UtcDateTime.Hour, moment.UtcDateTime.Minute, moment.UtcDateTime.Second, TimeSpan.Zero);

    /// <summary>Every cycle the record names, resolved or named by id, once each, in wave order.</summary>
    [JsonIgnore]
    public IReadOnlyList<CycleId> Cycles =>
    [
        .. Waves.SelectMany(wave => wave.Items)
            .Select(item => item.Cycle ?? (item.Kind == SeriesItemKind.Successor ? null : item.Ref))
            .Select(text => CycleId.TryParse(text, out CycleId? id) ? id : null)
            .OfType<CycleId>()
            .Distinct(),
    ];

    /// <summary>Whether the record lists a cycle: by its id, or as the successor its intent records as its lead.</summary>
    /// <remarks>
    /// A slug matches only an item whose cycle is not yet resolved, and only a cycle opened once that item's wave was
    /// taken: an item that names its cycle lists that one alone, and a cycle opened before the wave -- an older start
    /// of the same successor -- is not the one the series ran.
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="lead">Its intent's <c>lead:</c>, when it has one.</param>
    /// <returns>True when it does.</returns>
    public bool Lists(CycleId cycle, string? lead)
    {
        string id = cycle.ToString();
        string? slug = lead is not null && lead.StartsWith(SuccessorReference.Prefix, StringComparison.Ordinal)
            ? lead[SuccessorReference.Prefix.Length..].Trim()
            : null;

        return Waves.Any(wave => wave.Items.Any(item =>
            string.Equals(item.Cycle, id, StringComparison.Ordinal)
            || (item.Kind != SeriesItemKind.Successor && string.Equals(item.Ref, id, StringComparison.Ordinal))
            || (item.Kind == SeriesItemKind.Successor
                && item.Cycle is null
                && slug is not null
                && string.Equals(item.Ref, slug, StringComparison.Ordinal)
                && NotBefore(cycle, wave.Started))));
    }

    /// <summary>Whether a successor item is still unresolved, so membership may need a cycle's intent read.</summary>
    [JsonIgnore]
    internal bool HasUnresolved => Waves.Any(wave => wave.Items.Any(item => item.Kind == SeriesItemKind.Successor && item.Cycle is null));

    /// <summary>
    /// The abandonment of a series that lists a cycle and covers it, in the working copy the cycle is in, or null when
    /// none does.
    /// </summary>
    /// <remarks>
    /// File reads only, and none at all where no series has been rejected: this is asked of every cycle wherever
    /// abandonment is. A cycle opened before the series started is never covered (<see cref="Covers"/>): it came from
    /// the integration branch, and a working copy holding the integration branch's version of it must not read it as
    /// abandoned because a rejected series' folder was brought in beside it.
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <returns>The series' <c>abandoned.md</c>, or null.</returns>
    internal static string? AbandoningSeries(Cycle cycle) =>
        ListingSeries(cycle, abandonedOnly: true) is { } folder ? Path.Combine(folder, AbandonedFileName) : null;

    /// <summary>
    /// The folder of a series whose record, in the working copy the cycle is in, lists the cycle; null when none does.
    /// </summary>
    /// <remarks>
    /// File reads only, and none at all where no series has run. A cycle a series lists was opened unattended, so its
    /// intent may keep the lines nobody confirmed marked <c>(inferred)</c> for the run's report (see
    /// <see cref="Cycle.InSeries"/>).
    /// </remarks>
    /// <param name="cycle">The cycle.</param>
    /// <param name="abandonedOnly">Whether to look only at series a person has rejected.</param>
    /// <returns>The series' folder, or null.</returns>
    internal static string? ListingSeries(Cycle cycle, bool abandonedOnly)
    {
        string all = Path.Combine(cycle.SolutionRoot, Cycle.SdlcDirectoryName, DirectoryName);

        if (!Directory.Exists(all))
        {
            return null;
        }

        string? lead = null;
        bool leadRead = false;

        foreach (string folder in Directory.EnumerateDirectories(all).Order(StringComparer.Ordinal))
        {
            string abandoned = Path.Combine(folder, AbandonedFileName);
            string record = Path.Combine(folder, FileName);

            if ((abandonedOnly && !File.Exists(abandoned)) || !File.Exists(record))
            {
                continue;
            }

            if (Parse(File.ReadAllText(record), record).ValueOrDefault is not { } series)
            {
                continue;
            }

            if (!leadRead)
            {
                string intent = cycle.PathOf(ArtifactKind.Intent);
                lead = File.Exists(intent) ? LeadOf(File.ReadAllText(intent), intent) : null;
                leadRead = true;
            }

            if (series.Lists(cycle.Id, lead) && (!abandonedOnly || series.Covers(cycle.Id)))
            {
                return folder;
            }
        }

        return null;
    }

    /// <summary>The series ids this clone knows of: its own series branches and the remote's, and the folders here.</summary>
    /// <param name="repository">The repository.</param>
    /// <param name="root">The solution's root.</param>
    /// <returns>Each id once, in order.</returns>
    internal static IReadOnlyList<string> KnownIds(IRepository repository, string root)
    {
        string all = Path.Combine(root, Cycle.SdlcDirectoryName, DirectoryName);
        IEnumerable<string> here = Directory.Exists(all) ? Directory.EnumerateDirectories(all).Select(Path.GetFileName).OfType<string>() : [];

        return
        [
            .. repository.Branches(root, RunBranch.SeriesPrefix)
                .Select(branch => RunBranch.TryParse(branch.StartsWith("origin/", StringComparison.Ordinal) ? branch["origin/".Length..] : branch, out RunKind kind, out string? id) && kind == RunKind.Series ? id : null)
                .OfType<string>()
                .Concat(here)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Every series folder in a working copy whose record lists at least one cycle, with the cycles it lists there:
    /// those it names by id, and those whose intent records a successor it took as the lead.
    /// </summary>
    /// <remarks>
    /// Read before a close deletes any cycle's folder, because an unresolved successor item is matched through the
    /// intent, which goes with the folder. A folder whose record cannot be read is left out, and so stays. A rejected
    /// series lists only the cycles its abandonment covers: one it took from the integration branch stays there, to be
    /// built again, and does not hold the series' folder back.
    /// </remarks>
    /// <param name="root">The working copy's root.</param>
    /// <returns>Each series' id and its cycles, in id order.</returns>
    internal static IReadOnlyList<(string Id, IReadOnlyList<CycleId> Cycles)> Folders(string root)
    {
        string all = Path.Combine(root, Cycle.SdlcDirectoryName, DirectoryName);

        if (!Directory.Exists(all))
        {
            return [];
        }

        List<(CycleId Id, string? Lead)> cycles = [];

        foreach (Cycle cycle in CycleIndex.All(root))
        {
            string intent = cycle.PathOf(ArtifactKind.Intent);
            cycles.Add((cycle.Id, File.Exists(intent) ? LeadOf(File.ReadAllText(intent), intent) : null));
        }

        List<(string Id, IReadOnlyList<CycleId> Cycles)> folders = [];

        foreach (string folder in Directory.EnumerateDirectories(all).Order(StringComparer.Ordinal))
        {
            string record = Path.Combine(folder, FileName);

            if (!File.Exists(record) || Parse(File.ReadAllText(record), record).ValueOrDefault is not { } series)
            {
                continue;
            }

            bool rejected = File.Exists(Path.Combine(folder, AbandonedFileName));

            IReadOnlyList<CycleId> listed =
            [
                .. series.Cycles
                    .Concat(cycles.Where(cycle => series.Lists(cycle.Id, cycle.Lead)).Select(cycle => cycle.Id))
                    .Where(cycle => !rejected || series.Covers(cycle))
                    .Distinct()
                    .Order(),
            ];

            if (listed.Count > 0)
            {
                folders.Add((Path.GetFileName(folder), listed));
            }
        }

        return folders;
    }

    /// <summary>An intent's <c>lead:</c>, or null.</summary>
    internal static string? LeadOf(string intent, string source) =>
        Frontmatter.Read(intent, source) is { IsSuccess: true } split && Frontmatter.Key(split.Value.Yaml, "lead") is { } lead
            ? lead.Trim()
            : null;
}
