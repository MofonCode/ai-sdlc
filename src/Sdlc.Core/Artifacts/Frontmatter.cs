using System.Globalization;
using FluentResults;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Sdlc.Core.Artifacts;

/// <summary>A document split into its frontmatter and its prose.</summary>
/// <param name="Yaml">The YAML between the fences, without them.</param>
/// <param name="Body">Everything after the closing fence.</param>
public sealed record FrontmatterDocument(string Yaml, string Body);

/// <summary>
/// Splits an artifact into the facts a gate reads and the prose a person reads.
/// </summary>
/// <remarks>
/// Every artifact here carries both, and the division is deliberate: schemas and gates read the
/// frontmatter, humans read the body, and neither has to parse the other. This only finds the fences —
/// the YAML itself is handed to a real parser, never to a regular expression. Iteration 2 hand-rolled
/// one and it reported that a skill declared a setting while the real parser had dropped every field
/// in the file; the skill behaved one way at runtime and 333 tests agreed with the regex.
/// </remarks>
public static class Frontmatter
{
    private const string Fence = "---";

    /// <summary>
    /// Reads the frontmatter's keys through the parser.
    /// </summary>
    /// <remarks>
    /// <strong>One reader, so the schema and the gate cannot disagree about what a key says.</strong>
    /// They did: <c>ArtifactSchema</c> deserialised the YAML while <c>VerifyChain</c> and
    /// <c>ReadCycleStatus</c> compared the raw text after <c>outcome:</c>, so
    /// <c>outcome: "pass"</c> — valid YAML, accepted by the schema — read as <c>"pass"</c> with the
    /// quotes to the two layers that decide, and a legitimately passing cycle could not merge. A
    /// comment line, a block scalar and a stray <c>outcome:</c> inside a multi-line value were
    /// mishandled by the same code.
    /// <para>
    /// This file's own remarks had already lost that argument once, in iteration 2: the YAML is handed
    /// to a real parser, never to a line scan. The scan survived because it lived somewhere else.
    /// </para>
    /// </remarks>
    /// <param name="yaml">The frontmatter, as <see cref="Read"/> returned it.</param>
    /// <returns>The keys, or a failure saying what the parser could not read.</returns>
    public static Result<IReadOnlyDictionary<string, string?>> Keys(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        try
        {
            Dictionary<string, string?> keys = new DeserializerBuilder().Build()
                .Deserialize<Dictionary<string, string?>>(yaml) ?? [];

            return Result.Ok<IReadOnlyDictionary<string, string?>>(keys);
        }
        catch (YamlException error)
        {
            return Result.Fail<IReadOnlyDictionary<string, string?>>(
                "the frontmatter is not valid YAML. " + error.Message);
        }
    }

    /// <summary>
    /// Reads one frontmatter key through the parser.
    /// </summary>
    /// <param name="yaml">The frontmatter, as <see cref="Read"/> returned it.</param>
    /// <param name="key">Which key.</param>
    /// <returns>Its value trimmed, or null when the key is absent, empty or the YAML will not parse.</returns>
    public static string? Key(string yaml, string key)
    {
        Result<IReadOnlyDictionary<string, string?>> keys = Keys(yaml);

        return keys.IsSuccess
            && keys.Value.TryGetValue(key, out string? value)
            && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
    }

    /// <summary>
    /// Reads a document's frontmatter.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="source">What to call the document in an error message.</param>
    /// <returns>The split document, or a failure saying which fence was missing.</returns>
    public static Result<FrontmatterDocument> Read(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        int opening = Array.FindIndex(lines, line => line.Trim() == Fence);
        if (opening < 0 || lines.Take(opening).Any(line => line.Trim().Length > 0))
        {
            return Result.Fail<FrontmatterDocument>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: no frontmatter. The document must open with a '{Fence}' fence."));
        }

        int closing = Array.FindIndex(lines, opening + 1, line => line.Trim() == Fence);
        if (closing < 0)
        {
            return Result.Fail<FrontmatterDocument>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: the frontmatter opens and never closes. Add a '{Fence}' fence after it."));
        }

        string yaml = string.Join('\n', lines[(opening + 1)..closing]);
        string body = closing + 1 < lines.Length
            ? string.Join('\n', lines[(closing + 1)..])
            : string.Empty;

        return Result.Ok(new FrontmatterDocument(yaml, body));
    }

    /// <summary>
    /// Reads a file's frontmatter.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The split document, or a failure naming the file.</returns>
    public static async Task<Result<FrontmatterDocument>> ReadFile(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Fail<FrontmatterDocument>(string.Create(
                CultureInfo.InvariantCulture,
                $"{path}: no such file."));
        }

        string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Read(text, path);
    }
}
