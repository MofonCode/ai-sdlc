using System.Globalization;
using FluentResults;
using Sdlc.Core.Artifacts;

namespace Sdlc.Core.Guidance;

/// <summary>
/// One guidance entry: how to do one kind of thing well, handed to the planner when a plan touches it (D-195).
/// </summary>
/// <remarks>
/// <para><strong>A push, never a gate.</strong> A perspective is used to plan and to verify, and its reader may
/// block. Guidance only guides planning: nobody judges a plan against it, and a solution overrides it at any point
/// -- with an entry of its own by the same name, or simply by already doing the thing another way.</para>
/// <para><strong>Switched on by the files a plan declares</strong>, the way a perspective's <c>files:</c> are, so
/// a file listing decides which entries apply and the binary decides it.</para>
/// </remarks>
/// <param name="Name">Its name, as a command spells it.</param>
/// <param name="Title">Its heading.</param>
/// <param name="Files">File patterns in a plan's declared files that switch it on, as <c>*.yml</c> or <c>.github/workflows/</c>.</param>
/// <param name="Text">The entry, as written, without its frontmatter.</param>
/// <param name="Solution">Whether the solution wrote it, under <c>.sdlc/guidance/</c>, rather than the framework.</param>
public sealed record GuidanceEntry(string Name, string Title, IReadOnlyList<string> Files, string Text, bool Solution)
{
    /// <summary>Whether a declared file switches this entry on.</summary>
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

    /// <summary>Reads a guidance document.</summary>
    /// <param name="text">The document.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <param name="solution">Whether the solution wrote it.</param>
    /// <returns>The entry, or what is wrong with the document.</returns>
    public static Result<GuidanceEntry> Read(string text, string source, bool solution)
    {
        ArgumentNullException.ThrowIfNull(text);

        Result<FrontmatterDocument> split = Frontmatter.Read(text, source);

        if (split.IsFailed)
        {
            return split.ToResult<GuidanceEntry>();
        }

        Result<IReadOnlyDictionary<string, string?>> keys = Frontmatter.Keys(split.Value.Yaml);

        if (keys.IsFailed)
        {
            return keys.ToResult<GuidanceEntry>();
        }

        string? name = keys.Value.GetValueOrDefault("name");
        IReadOnlyList<string> files = List(keys.Value.GetValueOrDefault("files"));
        string body = split.Value.Body.Trim();
        string title = body.ReplaceLineEndings("\n").Split('\n')
            .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || files.Count == 0 || title.Length == 0)
        {
            return Result.Fail<GuidanceEntry>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: a guidance entry needs a name, the files that switch it on, and a '# ' heading."));
        }

        return Result.Ok(new GuidanceEntry(name.Trim(), title, files, body, solution));
    }

    private static IReadOnlyList<string> List(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Trim().TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Trim('\'', '"'))
                .Where(item => item.Length > 0)];
}
