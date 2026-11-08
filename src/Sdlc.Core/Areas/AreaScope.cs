using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Cycles;
using Sdlc.Core.Evidence;

namespace Sdlc.Core.Areas;

/// <summary>
/// Which areas one stage of one cycle touches, and so which entries its reader gets in full (D-151).
/// </summary>
/// <remarks>
/// <para><strong>Worked out from files, never chosen.</strong> The intent and the spec are the product owner's
/// and QA's, business-led, and touch no area: their readers get the entries that apply everywhere in full and
/// every other entry by its headline. The plan is the first document that declares files, so from the plan
/// on the areas are those its declared files fall in. A verification is read against the files the change
/// actually touched, so a build that wandered into another area is judged by that area's entries.</para>
/// <para><strong>It fails open.</strong> No area file, or an empty one, narrows nothing, which is how every
/// solution worked before areas existed. An entry that names no area applies everywhere. An entry whose areas
/// are all undefined, or all match no file in the tree, is treated as applying everywhere until somebody fixes
/// it, because the failure to avoid is a rule quietly reaching nobody.</para>
/// </remarks>
/// <param name="Narrowed">Whether anything is left to a headline at all.</param>
/// <param name="Touched">The areas this stage touches.</param>
/// <param name="Defined">Every area the solution defines.</param>
/// <param name="Stale">The defined areas whose globs match no file in the tree.</param>
public sealed record AreaScope(
    bool Narrowed,
    IReadOnlyList<string> Touched,
    IReadOnlyList<Area> Defined,
    IReadOnlyList<string> Stale)
{
    private static readonly Regex _patchFile = new(
        @"^diff --git a/(?<a>\S+) b/(?<b>\S+)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline,
        TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> _skipped = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules" };

    /// <summary>Nothing narrowed: every entry in full, as before areas existed.</summary>
    public static AreaScope Everything { get; } = new(false, [], [], []);

    /// <summary>Whether an entry carrying these areas reaches this stage's reader in full.</summary>
    /// <param name="areas">The areas the entry's <c>Areas:</c> line names; empty for a global entry.</param>
    /// <returns>True when its body is handed over, false when only its headline is.</returns>
    public bool InFull(IReadOnlyList<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        if (!Narrowed || areas.Count == 0 || areas.Any(Touched.Contains))
        {
            return true;
        }

        // Every area it names is undefined or covers nothing: shown as global until it is fixed.
        return areas.All(name => !Defined.Any(area => area.Name == name) || Stale.Contains(name));
    }

    /// <summary>The solution's areas, or none when it has no area file.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The areas, or why the file does not parse.</returns>
    public static async Task<Result<IReadOnlyList<Area>>> ReadDefined(string solutionRoot, CancellationToken cancellationToken)
    {
        string path = Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, AreaParser.FileName);

        return File.Exists(path)
            ? AreaParser.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), path)
            : Result.Ok<IReadOnlyList<Area>>([]);
    }

    /// <summary>
    /// The scope of one stage of one cycle.
    /// </summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="stage">The stage: whose document is being written or judged.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The scope, or why the area file does not parse.</returns>
    public static async Task<Result<AreaScope>> Of(Cycle cycle, ArtifactKind stage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Result<IReadOnlyList<Area>> defined = await ReadDefined(cycle.SolutionRoot, cancellationToken).ConfigureAwait(false);

        if (defined.IsFailed)
        {
            return defined.ToResult<AreaScope>();
        }

        if (defined.Value.Count == 0)
        {
            return Result.Ok(Everything);
        }

        IReadOnlyList<string> files = stage switch
        {
            ArtifactKind.Intent or ArtifactKind.Spec => [],
            ArtifactKind.Verify => await Changed(cycle, cancellationToken).ConfigureAwait(false),
            _ => await Declared(cycle, cancellationToken).ConfigureAwait(false),
        };

        return Result.Ok(For(defined.Value, files, CoveringNothing(defined.Value, cycle.SolutionRoot)));
    }

    /// <summary>The scope a set of files makes, given the areas and which of them are stale.</summary>
    /// <param name="defined">Every area the solution defines.</param>
    /// <param name="files">The files the stage touches.</param>
    /// <param name="stale">The areas whose globs match nothing.</param>
    /// <returns>The scope.</returns>
    public static AreaScope For(IReadOnlyList<Area> defined, IReadOnlyList<string> files, IReadOnlyList<string> stale)
    {
        ArgumentNullException.ThrowIfNull(defined);
        ArgumentNullException.ThrowIfNull(files);

        return defined.Count == 0
            ? Everything
            : new AreaScope(
                true,
                [.. defined.Where(area => files.Any(area.Covers)).Select(area => area.Name)],
                defined,
                stale);
    }

    /// <summary>The areas whose globs match no file under the solution's root.</summary>
    /// <param name="defined">The areas.</param>
    /// <param name="solutionRoot">The root.</param>
    /// <returns>The names of the areas that cover nothing.</returns>
    public static IReadOnlyList<string> CoveringNothing(IReadOnlyList<Area> defined, string solutionRoot)
    {
        ArgumentNullException.ThrowIfNull(defined);

        if (defined.Count == 0)
        {
            return [];
        }

        IReadOnlyList<string> files = Tree(solutionRoot);
        return [.. defined.Where(area => !files.Any(area.Covers)).Select(area => area.Name)];
    }

    /// <summary>
    /// Every file under a root, relative to it, skipping hidden folders and build output: what an area's
    /// globs are checked against.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns>The paths, with forward slashes.</returns>
    public static IReadOnlyList<string> Tree(string root)
    {
        List<string> files = [];
        Stack<string> pending = new([root]);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                files.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
            }

            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                string name = Path.GetFileName(child);

                if (!name.StartsWith('.') && !_skipped.Contains(name))
                {
                    pending.Push(child);
                }
            }
        }

        return files;
    }

    /// <summary>The files a patch touches, from its <c>diff --git</c> lines.</summary>
    /// <param name="patch">The patch.</param>
    /// <returns>The paths, both sides of a rename included.</returns>
    public static IReadOnlyList<string> FilesIn(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        return [.. _patchFile.Matches(patch)
            .SelectMany(match => new[] { match.Groups["a"].Value, match.Groups["b"].Value })
            .Distinct(StringComparer.Ordinal)];
    }

    private static async Task<IReadOnlyList<string>> Declared(Cycle cycle, CancellationToken cancellationToken)
    {
        string plan = cycle.PathOf(ArtifactKind.Plan);

        return File.Exists(plan)
            ? DeclaredFiles.Read(await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false))
            : [];
    }

    // The change as recorded; the plan's files where no patch was recorded yet.
    private static async Task<IReadOnlyList<string>> Changed(Cycle cycle, CancellationToken cancellationToken)
    {
        string patch = Path.Combine(cycle.EvidenceDirectory, "diff.patch");

        return File.Exists(patch)
            ? FilesIn(await File.ReadAllTextAsync(patch, cancellationToken).ConfigureAwait(false))
            : await Declared(cycle, cancellationToken).ConfigureAwait(false);
    }
}
