using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Harvest;

/// <summary>What a note for the framework says fell short.</summary>
public enum FrameworkNoteKind
{
    /// <summary>A rule, reader or step of the framework's cost more here than it caught.</summary>
    Cost,

    /// <summary>A rule or check any solution would need, which the framework does not have.</summary>
    Missing,

    /// <summary>The framework did something wrong: a command, a check or a document said or did what it should not.</summary>
    Defect,
}

/// <summary>
/// One note for the framework, as a harvest sitting answered it (D-204): what fell short, in words that need no
/// knowledge of this solution, and the evidence for it, which stays here.
/// </summary>
/// <param name="Recorded">When the sitting recorded it.</param>
/// <param name="Aspect">The aspect the sitting answered.</param>
/// <param name="Surface">
/// The part of the framework it is about: a rule's id (<c>CON_06</c>, <c>PRO_02</c>), or a kind and a name --
/// <c>perspective:security</c>, <c>stage:verify</c>, <c>command:evidence</c>, <c>skill:harvest-constraints</c>,
/// <c>guidance:&lt;name&gt;</c>, <c>document:spec</c>, <c>hook:pre-tool</c>, <c>refusal:&lt;name&gt;</c> -- or
/// <c>evidence</c>.
/// </param>
/// <param name="Kind">What fell short.</param>
/// <param name="Statement">What fell short, worded for somebody who has never seen this solution.</param>
/// <param name="Measure">The numbers behind it: cycles, findings, rounds, tokens.</param>
/// <param name="Cycles">The cycles it was seen in.</param>
/// <param name="Releases">The releases those cycles ran on, so a cost since fixed reads as history.</param>
/// <param name="Evidence">The cycles, finding ids and rounds behind it, in this solution's words. Never sent.</param>
public sealed record FrameworkNote(
    DateTimeOffset Recorded,
    HarvestAspect Aspect,
    string Surface,
    FrameworkNoteKind Kind,
    string Statement,
    string? Measure,
    IReadOnlyList<string> Cycles,
    IReadOnlyList<string> Releases,
    IReadOnlyList<string> Evidence);

/// <summary>
/// The framework's share of a solution's harvests, <c>.sdlc/framework-harvest.json</c> (D-204): every note a sitting
/// wrote under <c>## For the framework</c>, kept once the report it came from and the cycles behind it have gone.
/// </summary>
/// <remarks>
/// <para><strong>Two outputs from one sitting.</strong> What a harvest finds about the solution goes into its own
/// records; what it finds about the framework could not be changed here and was lost with the gitignored report. This
/// keeps it, and <c>sdlc harvest framework</c> prints what of it may leave the solution: every note without its
/// evidence, and the closed cycles' numbers without their files.</para>
/// <para><strong>Written only by the binary</strong>, by <c>sdlc harvest record</c>, and denied to every agent write by
/// the hook as the harvest ledger is: a note somebody typed in here would read as one a sitting answered.</para>
/// </remarks>
/// <param name="Notes">Every note recorded, oldest first.</param>
public sealed record FrameworkHarvest(IReadOnlyList<FrameworkNote> Notes)
{
    /// <summary>The file's name, in the framework's directory.</summary>
    public const string FileName = "framework-harvest.json";

    /// <summary>The section of a proposal file the notes are read from.</summary>
    public const string Section = "For the framework";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    // A rule's id, or a kind of surface and its name: what a reading across solutions groups by.
    private static readonly Regex _surface = new(
        @"^(?:(?:CON|PRO)_[0-9]+(?:\.[0-9]+)?|evidence|(?:perspective|stage|command|skill|guidance|document|hook|refusal|evidence):[A-Za-z0-9][A-Za-z0-9 ._/-]*)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>A solution that has recorded no note.</summary>
    public static FrameworkHarvest Empty { get; } = new([]);

    /// <summary>The surfaces a note may name, as a refusal lists them.</summary>
    public static string Surfaces =>
        "a rule's id (CON_06, PRO_02), evidence, or one of perspective:, stage:, command:, skill:, guidance:, document:, hook:, refusal:, evidence: followed by its name";

    /// <summary>Where a solution's file lives.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <returns>The path.</returns>
    public static string PathIn(string solutionRoot) => Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, FileName);

    /// <summary>Whether a surface is one a note may name.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>True when it is.</returns>
    public static bool IsSurface(string surface) => surface is not null && _surface.IsMatch(surface);

    /// <summary>Reads the file's text.</summary>
    /// <param name="json">The text.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The notes, or why they cannot be read.</returns>
    public static Result<FrameworkHarvest> Parse(string json, string source)
    {
        try
        {
            FrameworkHarvest? read = JsonSerializer.Deserialize<FrameworkHarvest>(json, _options);

            return read is null
                ? Result.Fail<FrameworkHarvest>(source + ": the framework's notes are empty.")
                : Result.Ok(read with { Notes = read.Notes ?? [] });
        }
        catch (JsonException error)
        {
            return Result.Fail<FrameworkHarvest>(source + ": the framework's notes do not parse: " + error.Message);
        }
    }

    /// <summary>Reads a solution's file; an absent one holds no note.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The notes, or why they cannot be read.</returns>
    public static async Task<Result<FrameworkHarvest>> Read(string solutionRoot, CancellationToken cancellationToken)
    {
        string path = PathIn(solutionRoot);

        return File.Exists(path)
            ? Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path)
            : Result.Ok(Empty);
    }

    /// <summary>The notes as the file holds them.</summary>
    /// <returns>Its JSON.</returns>
    public string Write() => JsonSerializer.Serialize(this, _options) + "\n";

    /// <summary>The file with a sitting's notes added.</summary>
    /// <param name="notes">The notes.</param>
    /// <returns>The new file.</returns>
    public FrameworkHarvest Adding(IReadOnlyList<FrameworkNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return this with { Notes = [.. Notes, .. notes] };
    }

    /// <summary>
    /// Reads a proposal file's <c>## For the framework</c>: one <c>### &lt;surface&gt; — &lt;statement&gt;</c> heading
    /// each, then a <c>Kind:</c> line, optional <c>Cycles:</c> and <c>Measure:</c> lines, and the evidence quoted
    /// beneath with <c>&gt;</c>.
    /// </summary>
    /// <param name="markdown">The proposal file.</param>
    /// <param name="aspect">The aspect the sitting answered.</param>
    /// <param name="recorded">When it is recorded.</param>
    /// <param name="sitting">The cycles the sitting read: a note naming none was seen in these.</param>
    /// <param name="releases">The releases each cycle's rounds recorded.</param>
    /// <returns>The notes, or every heading that does not read as one and why.</returns>
    public static Result<IReadOnlyList<FrameworkNote>> ReadNotes(
        string markdown,
        HarvestAspect aspect,
        DateTimeOffset recorded,
        IReadOnlyList<string> sitting,
        IReadOnlyDictionary<string, IReadOnlyList<string>> releases)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(sitting);
        ArgumentNullException.ThrowIfNull(releases);

        if (!ArtifactSections.Read(markdown).TryGetValue(Section, out IReadOnlyList<string>? lines))
        {
            return Result.Ok<IReadOnlyList<FrameworkNote>>([]);
        }

        List<FrameworkNote> notes = [];
        List<string> refused = [];
        List<List<string>> entries = [];

        foreach (string raw in lines)
        {
            string line = raw.Trim();

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                entries.Add([line[4..].Trim()]);
            }
            else if (entries.Count > 0 && line.Length > 0)
            {
                entries[^1].Add(line);
            }
        }

        foreach (List<string> entry in entries)
        {
            string heading = entry[0];
            int dash = heading.IndexOf(" — ", StringComparison.Ordinal);
            dash = dash < 0 ? heading.IndexOf(" -- ", StringComparison.Ordinal) : dash;
            string surface = dash > 0 ? heading[..dash].Trim().Trim('`') : heading.Trim('`');
            string statement = dash > 0 ? heading[(dash + (heading[dash + 1] == '—' ? 3 : 4))..].Trim() : string.Empty;
            string? kind = Field(entry, "Kind");

            if (!IsSurface(surface))
            {
                refused.Add(string.Create(CultureInfo.InvariantCulture, $"'{heading}' names no surface of the framework: start it with {Surfaces}."));
                continue;
            }

            if (statement.Length == 0)
            {
                refused.Add(string.Create(CultureInfo.InvariantCulture, $"'{heading}' says nothing after its surface: write '### {surface} — <what fell short>'."));
                continue;
            }

            if (kind is null || !Enum.TryParse(kind, ignoreCase: true, out FrameworkNoteKind parsed) || !Enum.IsDefined(parsed))
            {
                refused.Add(string.Create(CultureInfo.InvariantCulture, $"'{heading}' needs a 'Kind:' line beneath it: cost, missing or defect."));
                continue;
            }

            List<string> cycles = Field(entry, "Cycles") is { } named
                ? [.. named.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(id => id.Trim('`'))]
                : [.. sitting];

            notes.Add(new FrameworkNote(
                recorded,
                aspect,
                surface,
                parsed,
                statement,
                Field(entry, "Measure"),
                cycles,
                [.. cycles.SelectMany(cycle => releases.TryGetValue(cycle, out IReadOnlyList<string>? ran) ? ran : []).Distinct(StringComparer.Ordinal)],
                [.. entry.Skip(1).Where(line => line.StartsWith('>')).Select(line => line.TrimStart('>').Trim()).Where(line => line.Length > 0)]));
        }

        return refused.Count > 0
            ? Result.Fail<IReadOnlyList<FrameworkNote>>("Under '## " + Section + "': " + string.Join(" ", refused))
            : Result.Ok<IReadOnlyList<FrameworkNote>>(notes);
    }

    private static string? Field(List<string> entry, string name) =>
        entry.Skip(1)
            .Where(line => line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(name.Length + 1)..].Trim())
            .FirstOrDefault(value => value.Length > 0);
}
