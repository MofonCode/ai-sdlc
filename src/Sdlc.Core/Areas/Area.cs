using System.Text;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Areas;

/// <summary>
/// A part of this solution's code, named in the team's own words and defined by the files it covers (D-150).
/// </summary>
/// <remarks>
/// <para><strong>A name for a set of files, never a category somebody files an entry under.</strong> A rule
/// or decision carries the names of the areas it applies to, and a cycle's areas are worked out from the files
/// it declares or changed -- so which entries reach a reader in full is decided by a file listing, not by a
/// judgement that could leave one out.</para>
/// <para>An area is not a perspective. A perspective is a way of reading every change; an area is a part of
/// one solution, and the framework ships none.</para>
/// </remarks>
/// <param name="Name">The name, lower case with hyphens, as entries cite it: <c>market-data</c>.</param>
/// <param name="Files">The globs it covers, relative to the solution's root.</param>
/// <param name="Is">What the area is, in one line.</param>
public sealed record Area(string Name, IReadOnlyList<string> Files, string Is)
{
    /// <summary>Whether a path, relative to the solution's root, is one this area covers.</summary>
    /// <param name="path">The path, with either separator.</param>
    /// <returns>True when any of its globs matches.</returns>
    public bool Covers(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Files.Any(glob => AreaGlob.Matches(glob, path));
    }
}

/// <summary>
/// The glob language an area's <c>Files</c> line is written in.
/// </summary>
/// <remarks>
/// <para>Small on purpose, so a person can predict it without a reference: <c>*</c> is any run of characters
/// within one folder, <c>**</c> any number of folders, <c>?</c> one character. A glob with no slash matches a
/// file's name in any folder, so <c>*.sql</c> means every SQL file; one with a slash is matched from the
/// solution's root; one ending in a slash is everything under that folder. Case is ignored, because the
/// solutions this governs are built on Windows as often as not.</para>
/// <para>Written here rather than taken from a package: the core references two packages and stays a leaf,
/// and this is forty lines.</para>
/// </remarks>
internal static class AreaGlob
{
    /// <summary>Whether a glob matches a path.</summary>
    /// <param name="glob">The glob, as the area writes it.</param>
    /// <param name="path">The path, relative to the solution's root, with either separator.</param>
    /// <returns>True when it matches.</returns>
    public static bool Matches(string glob, string path)
    {
        ArgumentNullException.ThrowIfNull(glob);
        ArgumentNullException.ThrowIfNull(path);

        string pattern = glob.Trim().Replace('\\', '/');
        string normalised = path.Replace('\\', '/').TrimStart('/');

        if (normalised.StartsWith("./", StringComparison.Ordinal))
        {
            normalised = normalised[2..];
        }

        if (pattern.Length == 0)
        {
            return false;
        }

        if (pattern.EndsWith('/'))
        {
            pattern += "**";
        }

        if (!pattern.Contains('/', StringComparison.Ordinal))
        {
            pattern = "**/" + pattern;
        }

        return Regex.IsMatch(normalised, ToRegex(pattern.TrimStart('/')), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static string ToRegex(string pattern)
    {
        StringBuilder regex = new("^");

        for (int index = 0; index < pattern.Length; index++)
        {
            char current = pattern[index];

            if (current == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
            {
                // '**/' is zero or more folders; a trailing '**' is everything below.
                bool slash = index + 2 < pattern.Length && pattern[index + 2] == '/';
                regex.Append(slash ? "(?:.*/)?" : ".*");
                index += slash ? 2 : 1;
            }
            else if (current == '*')
            {
                regex.Append("[^/]*");
            }
            else if (current == '?')
            {
                regex.Append("[^/]");
            }
            else
            {
                regex.Append(Regex.Escape(current.ToString()));
            }
        }

        return regex.Append('$').ToString();
    }
}
