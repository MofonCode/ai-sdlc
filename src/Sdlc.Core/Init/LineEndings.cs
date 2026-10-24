namespace Sdlc.Core.Init;

/// <summary>
/// How a solution's line endings are kept the same for everybody: one <c>.gitattributes</c> line.
/// </summary>
/// <remarks>
/// <strong>Not for the approval.</strong> The approval hashes canonicalised text, so a document checked out
/// with other endings still matches what was approved. This is for the people: once a solution is
/// shared, a commit from another platform without normalisation shows every line of a file as changed,
/// and a diff nobody can read is a review nobody did.
/// <para>
/// <c>text=auto</c> and no <c>eol=</c> pin, the same as this repository's own. A pin forces one
/// working-tree ending on every platform, and the tools on each expect their own.
/// </para>
/// </remarks>
public static class LineEndings
{
    /// <summary>The file, at the solution root.</summary>
    public const string FileName = ".gitattributes";

    /// <summary>The line that normalises every text file.</summary>
    public const string Rule = "* text=auto";

    /// <summary>What adoption writes where there is no file.</summary>
    /// <returns>The file's contents.</returns>
    public static string Contents() => string.Join(
        Environment.NewLine,
        [
            "# Written by 'sdlc init'. Yours to amend.",
            "#",
            "# Every text file is stored with LF endings and checked out with the platform's own -- CRLF on",
            "# Windows, LF elsewhere -- so a commit from another machine never shows a whole file changed.",
            "# Deliberately no eol= pin: one forces a working-tree ending on every platform, and the tools",
            "# on each platform expect their own.",
            string.Empty,
            Rule,
            string.Empty,
        ]);

    /// <summary>Whether a <c>.gitattributes</c> normalises every text file.</summary>
    /// <remarks>
    /// A line whose pattern is <c>*</c> and which sets <c>text</c> or <c>text=auto</c>. Narrower patterns
    /// leave the rest of the tree to whoever committed it, so they do not count; a later <c>-text</c> on
    /// <c>*</c> undoes it, the way git reads the file.
    /// </remarks>
    /// <param name="contents">The file.</param>
    /// <returns>True when every text file is normalised.</returns>
    public static bool Normalises(string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        bool normalises = false;

        foreach (string raw in contents.Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts[0] != "*")
            {
                continue;
            }

            foreach (string attribute in parts.Skip(1))
            {
                if (attribute is "text" or "text=auto")
                {
                    normalises = true;
                }
                else if (attribute is "-text" or "!text" or "binary")
                {
                    normalises = false;
                }
            }
        }

        return normalises;
    }
}
