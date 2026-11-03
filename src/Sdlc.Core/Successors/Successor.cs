using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Leads;

namespace Sdlc.Core.Successors;

/// <summary>
/// One thing a successor waits for: a cycle, by its id, or another successor, by its slug (D-125).
/// </summary>
/// <param name="Cycle">The cycle, when the reference names one.</param>
/// <param name="Slug">The successor, when the reference is <c>successor:&lt;slug&gt;</c>.</param>
public sealed record SuccessorReference(CycleId? Cycle, string? Slug)
{
    /// <summary>The prefix a successor is named by, as a reference and as a lead.</summary>
    public const string Prefix = "successor:";

    /// <summary>The reference as it is written.</summary>
    /// <returns>The cycle id, or the prefixed slug.</returns>
    public override string ToString() => Cycle?.ToString() ?? Prefix + Slug;

    /// <summary>Reads one entry of <c>succeeds:</c>.</summary>
    /// <param name="text">The entry.</param>
    /// <param name="reference">The reference, when this returns true.</param>
    /// <returns>True when it is a cycle id or a well-formed <c>successor:&lt;slug&gt;</c>.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SuccessorReference? reference)
    {
        reference = null;
        string trimmed = text?.Trim() ?? string.Empty;

        if (CycleId.TryParse(trimmed, out CycleId? id))
        {
            reference = new SuccessorReference(id, null);
            return true;
        }

        if (trimmed.StartsWith(Prefix, StringComparison.Ordinal) && Successor.IsSlug(trimmed[Prefix.Length..]))
        {
            reference = new SuccessorReference(null, trimmed[Prefix.Length..]);
            return true;
        }

        return false;
    }
}

/// <summary>
/// A slice that needs another's code, waiting as a file and not as a cycle (D-125).
/// </summary>
/// <remarks>
/// <para><strong>It holds an intent's substance and nothing more</strong>: what it waits for, the lead the
/// slicing began from, and the outcome and its why. A plan written now would describe code that does not exist
/// yet, so there is none, and nothing interrogates or approves the file while it waits.</para>
/// <para><strong>Its readiness is not here.</strong> Whether it is ready is worked out from the record on every
/// read, by <see cref="ReadSuccessors"/>; a stored state would be a second copy of what the cycles already say.</para>
/// </remarks>
/// <param name="Slug">Its name: the file's, without <c>.md</c>.</param>
/// <param name="Headline">Its first heading, when it has one.</param>
/// <param name="Succeeds">What it waits for, in the order written.</param>
/// <param name="SlicedFrom">The lead the slicing began from, when there was one.</param>
/// <param name="Mirror">A work item standing for it on a board, when somebody filed one.</param>
/// <param name="Outcome">What it should make possible, as the interview left it.</param>
/// <param name="Why">Why that matters.</param>
public sealed record Successor(
    string Slug,
    string? Headline,
    IReadOnlyList<SuccessorReference> Succeeds,
    string? SlicedFrom,
    string? Mirror,
    string Outcome,
    string Why)
{
    /// <summary>The directory under <c>.sdlc/</c> that holds the successors.</summary>
    public const string DirectoryName = "successors";

    private static readonly Regex _slug = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly string[] _keys = ["succeeds", "sliced_from", "mirror"];

    /// <summary>
    /// Where a solution's successors live, relative to its root and forward-slashed.
    /// </summary>
    public static string RelativeDirectory => Cycle.SdlcDirectoryName + "/" + DirectoryName;

    /// <summary>Where one successor's file lives, relative to the root and forward-slashed.</summary>
    /// <param name="slug">The successor.</param>
    /// <returns>The path.</returns>
    public static string RelativePath(string slug) => RelativeDirectory + "/" + slug + ".md";

    /// <summary>Where a solution's successors live on disk.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <returns>The directory, which may not exist.</returns>
    public static string DirectoryOf(string solutionRoot) =>
        Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DirectoryName);

    /// <summary>
    /// Whether a name is a slug: lower-case letters and digits, joined by single hyphens.
    /// </summary>
    /// <remarks>
    /// One spelling, because it is a file name, a lead and a branch segment at once, and a name that is fine as
    /// one and not another would fail in the place furthest from where it was written.
    /// </remarks>
    /// <param name="name">The name.</param>
    /// <returns>True when it is one.</returns>
    public static bool IsSlug(string? name) => name is { Length: > 0 and <= 64 } && _slug.IsMatch(name);

    /// <summary>
    /// Whether a path, relative to the root, is a successor's file.
    /// </summary>
    /// <param name="path">The path, with either separator.</param>
    /// <returns>True when it is <c>.sdlc/successors/&lt;slug&gt;.md</c>.</returns>
    public static bool IsSuccessorPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string normalised = path.Replace('\\', '/').TrimStart('/');
        string prefix = RelativeDirectory + "/";

        return normalised.StartsWith(prefix, StringComparison.Ordinal)
            && normalised.EndsWith(".md", StringComparison.Ordinal)
            && IsSlug(normalised[prefix.Length..^3]);
    }

    /// <summary>
    /// Reads a successor's file, saying everything wrong with it at once.
    /// </summary>
    /// <param name="slug">The slug its file is named for.</param>
    /// <param name="text">The file.</param>
    /// <returns>The successor, or a failure carrying one reason per problem.</returns>
    public static Result<Successor> Parse(string slug, string text)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(text);

        List<string> problems = [];

        if (!IsSlug(slug))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'{slug}' is not a slug: lower-case letters and digits, joined by single hyphens, as in 'export-totals'."));
        }

        Result<FrontmatterDocument> split = Frontmatter.Read(text, slug + ".md");

        if (split.IsFailed)
        {
            return Result.Fail<Successor>([.. problems, .. split.Errors.Select(error => error.Message)]);
        }

        Result<IReadOnlyDictionary<string, string?>> keys = Frontmatter.Keys(split.Value.Yaml);

        if (keys.IsFailed)
        {
            return Result.Fail<Successor>([.. problems, .. keys.Errors.Select(error => error.Message)]);
        }

        foreach (string key in keys.Value.Keys.Where(key => !_keys.Contains(key, StringComparer.Ordinal)))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'{key}' is not a key a successor carries. It carries succeeds:, sliced_from: and mirror:."));
        }

        List<SuccessorReference> succeeds = [];
        string? waits = Value(keys.Value, "succeeds");

        if (waits is null)
        {
            problems.Add("succeeds: is missing or empty. A successor names what it waits for -- cycle ids, or successor:<slug> -- or it is a cycle of its own.");
        }
        else
        {
            foreach (string entry in waits.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (SuccessorReference.TryParse(entry, out SuccessorReference? reference))
                {
                    succeeds.Add(reference);
                    continue;
                }

                problems.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{entry}' in succeeds: is neither a cycle id nor successor:<slug>."));
            }
        }

        string? slicedFrom = Value(keys.Value, "sliced_from");

        if (slicedFrom is not null && !LeadReference.TryParse(slicedFrom, out _, out string? leadProblem))
        {
            problems.Add("sliced_from: " + leadProblem);
        }

        string? mirror = Value(keys.Value, "mirror");

        if (mirror is not null
            && (!LeadReference.TryParse(mirror, out LeadReference? item, out _)
                || item.Source is not (LeadSource.AzureDevOps or LeadSource.GitHub or LeadSource.Jira)))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"mirror: '{mirror}' is not a work item. Name one as azuredevops:1234, github:56 or jira:PROJ-123."));
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(text);
        string outcome = Section(sections, "Outcome", problems);
        string why = Section(sections, "Why", problems);

        return problems.Count > 0
            ? Result.Fail<Successor>(problems)
            : Result.Ok(new Successor(
                slug,
                ArtifactOutlineReader.Read(text).Headline,
                succeeds,
                slicedFrom,
                mirror,
                outcome,
                why));
    }

    private static string? Value(IReadOnlyDictionary<string, string?> keys, string key) =>
        keys.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string Section(
        IReadOnlyDictionary<string, IReadOnlyList<string>> sections,
        string heading,
        List<string> problems)
    {
        string text = sections.TryGetValue(heading, out IReadOnlyList<string>? lines)
            ? string.Join('\n', lines).Trim()
            : string.Empty;

        if (text.Length == 0)
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'## {heading}' is missing or empty. A successor keeps the interview so far: the outcome and its why."));
        }

        return text;
    }
}
