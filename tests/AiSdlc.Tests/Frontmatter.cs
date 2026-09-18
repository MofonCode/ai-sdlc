using System.Text.RegularExpressions;

namespace AiSdlc.Tests;

/// <summary>
/// Reads the YAML frontmatter block that decides whether a file is loaded at all.
/// </summary>
/// <remarks>
/// A deliberately minimal reader rather than a YAML dependency: it only needs the flat
/// scalar fields the harness itself reads, and being strict about the block's position is
/// the point. Frontmatter that is not the very first thing in the file is silently ignored
/// by the harness, so a leading blank line disables an entire skill without any error.
/// </remarks>
internal static partial class Frontmatter
{
    [GeneratedRegex(@"\A---\r?\n(.*?)\r?\n---(\r?\n|\z)", RegexOptions.Singleline)]
    private static partial Regex Block();

    [GeneratedRegex(@"^([A-Za-z_][\w-]*):\s*(.*)$", RegexOptions.Multiline)]
    private static partial Regex Field();

    /// <summary>
    /// The frontmatter fields, or null when the file has no frontmatter as its first line.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Read(string path)
    {
        Match block = Block().Match(File.ReadAllText(path));
        if (!block.Success)
        {
            return null;
        }

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match field in Field().Matches(block.Groups[1].Value))
        {
            fields[field.Groups[1].Value] = field.Groups[2].Value.Trim();
        }

        return fields;
    }
}
