using System.Globalization;
using System.Text;
using FluentResults;
using Sdlc.Core.Artifacts;

namespace Sdlc.Core.Perspectives;

/// <summary>
/// One perspective: a method that steers authoring and judges in a fresh reader of its own (D-90).
/// </summary>
/// <remarks>
/// <para><strong>A document with behaviour in it, not a label on rules.</strong> A filter over the rule set
/// makes nobody look harder; what a perspective brings is how to look. So it carries a method, what an author
/// should cover at each stage, what a reader asks at each stage, the rules it can block on, and the evidence it
/// needs.</para>
/// <para><strong>Embedded in the binary, one file per perspective</strong>, the way a stack profile is, and read
/// through <c>sdlc perspective show</c> — a skill pastes what it prints, and never restates it.</para>
/// </remarks>
/// <param name="Name">Its name, as a cycle and a command spell it.</param>
/// <param name="Title">Its heading.</param>
/// <param name="Purpose">What it asks of a change, in a paragraph.</param>
/// <param name="Always">Whether every cycle gets it.</param>
/// <param name="Areas">The areas an intent names that switch it on: <c>ui</c>, <c>api</c>, <c>data</c>, <c>infrastructure</c>.</param>
/// <param name="Files">File patterns in a plan's declared files that switch it on, as <c>*.razor</c> or <c>infra/</c>.</param>
/// <param name="Method">How to examine a change.</param>
/// <param name="Cover">What an author covers, per stage.</param>
/// <param name="Questions">What a reader asks, per stage.</param>
/// <param name="Rules">The rules it may block on, or the sentence saying it has none yet.</param>
/// <param name="Evidence">The evidence it needs a build to record.</param>
public sealed record Perspective(
    string Name,
    string Title,
    string Purpose,
    bool Always,
    IReadOnlyList<string> Areas,
    IReadOnlyList<string> Files,
    string Method,
    IReadOnlyDictionary<string, string> Cover,
    IReadOnlyDictionary<string, string> Questions,
    string Rules,
    string Evidence)
{
    /// <summary>The sections every perspective document carries.</summary>
    public static IReadOnlyList<string> Required { get; } = ["Method", "Cover this", "Questions", "Rules", "Evidence"];

    /// <summary>The stages a perspective speaks to, in order.</summary>
    public static IReadOnlyList<string> Stages { get; } = ["intent", "spec", "plan", "build", "verify"];

    /// <summary>The perspectives every cycle gets, whether or not a document for each has been written yet.</summary>
    public static IReadOnlyList<string> AlwaysOn { get; } = ["security", "operability", "maintainability"];

    /// <summary>Whether a declared file switches this perspective on.</summary>
    /// <param name="path">A path from a plan's declared files.</param>
    /// <returns>True when one of its patterns matches.</returns>
    public bool Matches(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string normalised = path.Replace('\\', '/');

        return Files.Any(pattern => pattern.StartsWith("*.", StringComparison.Ordinal)
            ? normalised.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
            : normalised.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads a perspective document.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The perspective, or what is wrong with the document.</returns>
    public static Result<Perspective> Read(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);

        Result<FrontmatterDocument> split = Frontmatter.Read(text, source);

        if (split.IsFailed)
        {
            return split.ToResult<Perspective>();
        }

        Result<IReadOnlyDictionary<string, string?>> keys = Frontmatter.Keys(split.Value.Yaml);

        if (keys.IsFailed)
        {
            return keys.ToResult<Perspective>();
        }

        string? name = keys.Value.GetValueOrDefault("name");
        Dictionary<string, string> sections = Sections(split.Value.Body, "## ", out string title, out string purpose);
        List<string> missing = [.. Required.Where(s => !sections.ContainsKey(s))];

        if (string.IsNullOrWhiteSpace(name) || missing.Count > 0)
        {
            return Result.Fail<Perspective>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: {(string.IsNullOrWhiteSpace(name) ? "it has no name. " : string.Empty)}{(missing.Count > 0 ? "it has no " + string.Join(", ", missing) + " section." : string.Empty)}"));
        }

        Dictionary<string, string> cover = Sections(sections["Cover this"], "### ", out _, out _);
        Dictionary<string, string> questions = Sections(sections["Questions"], "### ", out _, out _);
        List<string> uncovered = [.. Stages.Where(stage => !cover.ContainsKey(stage) || !questions.ContainsKey(stage))];

        if (uncovered.Count > 0)
        {
            return Result.Fail<Perspective>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: every stage needs what to cover and what to ask, and these have neither or one: {string.Join(", ", uncovered)}."));
        }

        return Result.Ok(new Perspective(
            name.Trim(),
            title,
            purpose,
            string.Equals(keys.Value.GetValueOrDefault("always")?.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            List(keys.Value.GetValueOrDefault("areas")),
            List(keys.Value.GetValueOrDefault("files")),
            sections["Method"],
            cover,
            questions,
            sections["Rules"],
            sections["Evidence"]));
    }

    private static IReadOnlyList<string> List(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Trim().TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Trim('\'', '"'))
                .Where(item => item.Length > 0)];

    private static Dictionary<string, string> Sections(string body, string marker, out string title, out string purpose)
    {
        Dictionary<string, string> sections = new(StringComparer.Ordinal);
        string? current = null;
        StringBuilder text = new();
        StringBuilder lead = new();
        title = string.Empty;

        foreach (string line in body.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal) && marker == "## " && title.Length == 0)
            {
                title = line[2..].Trim();
                continue;
            }

            if (line.StartsWith(marker, StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    sections[current] = text.ToString().Trim();
                }

                current = line[marker.Length..].Trim();
                text.Clear();
                continue;
            }

            (current is null ? lead : text).AppendLine(line);
        }

        if (current is not null)
        {
            sections[current] = text.ToString().Trim();
        }

        purpose = lead.ToString().Trim();
        return sections;
    }
}
