using System.Globalization;

namespace Sdlc.Core.Artifacts;

/// <summary>What a document says about itself, read without understanding it.</summary>
/// <param name="Headline">Its first heading, which is what the artifact is about.</param>
/// <param name="Sections">Its second-level headings, in order.</param>
public sealed record ArtifactOutline(string? Headline, IReadOnlyList<string> Sections)
{
    /// <summary>An outline for a document that does not exist.</summary>
    public static ArtifactOutline Empty { get; } = new(null, []);
}

/// <summary>
/// Reads a document's shape for the status panel.
/// </summary>
/// <remarks>
/// The panel wants a line saying what the artifact is about, and a summary of content needs reading
/// comprehension that a command-line tool does not have. So this reads what is actually there: the
/// first heading, and the section headings under it. A skill could later write a one-line summary into
/// the frontmatter and the panel would show that instead — but a tool inventing a summary, or a panel
/// claiming one it does not have, would be worse than a tool showing the headings a person wrote.
/// </remarks>
public static class ArtifactOutlineReader
{
    /// <summary>Reads a document's outline from its text.</summary>
    /// <param name="markdown">The document.</param>
    /// <returns>The outline. A document with no headings yields an empty one rather than failing.</returns>
    public static ArtifactOutline Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        string? headline = null;
        List<string> sections = [];
        bool inFrontmatter = false;
        bool seenFence = false;

        foreach (string raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.Trim();

            // A heading inside frontmatter would be YAML, not a heading.
            if (line == "---")
            {
                if (!seenFence)
                {
                    seenFence = true;
                    inFrontmatter = true;
                    continue;
                }

                if (inFrontmatter)
                {
                    inFrontmatter = false;
                    continue;
                }
            }

            if (inFrontmatter)
            {
                continue;
            }

            if (headline is null && line.StartsWith("# ", StringComparison.Ordinal))
            {
                headline = line[2..].Trim();
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add(line[3..].Trim());
            }
        }

        return new ArtifactOutline(headline, sections);
    }

    /// <summary>Reads a file's outline, or an empty one when it does not exist.</summary>
    /// <param name="path">The document.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The outline.</returns>
    public static async Task<ArtifactOutline> ReadFile(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return ArtifactOutline.Empty;
        }

        string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Read(text);
    }

    /// <summary>
    /// Describes an outline in one short line.
    /// </summary>
    /// <param name="outline">The outline to describe.</param>
    /// <returns>The sections it holds, counted, or a note that it has none.</returns>
    public static string Describe(ArtifactOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        return outline.Sections.Count switch
        {
            0 => "no sections yet",
            1 => "1 section: " + outline.Sections[0],
            _ => string.Create(
                CultureInfo.InvariantCulture,
                $"{outline.Sections.Count} sections: {string.Join(", ", outline.Sections)}"),
        };
    }
}
