using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sdlc.Core.Artifacts;

/// <summary>
/// Hashes an artifact's bytes, after normalising what a platform is allowed to change.
/// </summary>
/// <remarks>
/// An approval records a hash of each planning document, and the gate recomputes it. Without
/// canonicalisation that check would fail for reasons nobody caused: <c>.gitattributes</c> normalises
/// to LF in the repository and checks out native, so the same committed file is CRLF on Windows and
/// LF elsewhere, and an editor that strips a trailing space changes bytes without changing a word.
/// Both would read as an artifact edited after approval, which is the one thing the hash exists to
/// detect — so a false positive there costs more than the narrower check it buys.
/// <para>
/// What is normalised is only what a platform or an editor does on its own: line endings, trailing
/// whitespace on a line, and a trailing blank line at the end. Everything else is content, and a
/// change to it is a change.
/// </para>
/// </remarks>
public static class ContentHash
{
    /// <summary>
    /// Canonicalises text for hashing.
    /// </summary>
    /// <param name="text">The text to canonicalise.</param>
    /// <returns>The text with CRLF normalised to LF, trailing whitespace stripped per line, and a single closing newline.</returns>
    public static string Canonicalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder canonical = new(text.Length);
        List<string> lines = [];

        foreach (string line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            lines.Add(line.TrimEnd());
        }

        // A file that ends in a newline and one that ends in several are the same document. Trailing
        // blank lines are dropped and exactly one closing newline is written, so an editor's
        // insert-final-newline setting cannot change a hash.
        int last = lines.FindLastIndex(line => line.Length > 0);

        for (int index = 0; index <= last; index++)
        {
            canonical.Append(lines[index]).Append('\n');
        }

        return canonical.ToString();
    }

    /// <summary>
    /// Hashes text.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>The SHA-256 of the canonical form, lower-case hex.</returns>
    public static string Of(string text)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalise(text)));
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    /// Hashes a file's contents.
    /// </summary>
    /// <param name="path">The file to hash.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The SHA-256 of the canonical form, lower-case hex.</returns>
    public static async Task<string> OfFile(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Of(text);
    }

    /// <summary>
    /// The leading characters of a hash, for showing a person.
    /// </summary>
    /// <remarks>
    /// A pasted hash cannot be checked by eye, so the status panel shows this much of what was
    /// recorded beside this much of what is on disk. Twelve characters is enough that two artifacts
    /// will not collide in a cycle and short enough to compare at a glance.
    /// </remarks>
    /// <param name="hash">The full hash.</param>
    /// <returns>The first twelve characters, or the whole hash when it is shorter.</returns>
    public static string Abbreviate(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return hash.Length <= 12
            ? hash
            : string.Create(CultureInfo.InvariantCulture, $"{hash[..12]}");
    }
}
