using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core;

/// <summary>
/// The commands an earlier release had that this one answers only with their new name (D-148, D-191, D-224).
/// </summary>
/// <remarks>
/// <para><strong>One list, read both ways.</strong> The binary answers a retired command from it, so somebody typing
/// one from memory still gets where they meant; doctor reads it to find a retired command a file only a person writes
/// still names, which no upgrade can rewrite. A release that renames or removes a command adds it here, which is how
/// typing it keeps working; a test holds that the binary dispatches each one's first word, so it is answered.</para>
/// </remarks>
public static partial class CommandNames
{
    /// <summary>A command a release retired, and what replaced it.</summary>
    /// <param name="Was">The command as it was typed, after the word <c>sdlc</c>.</param>
    /// <param name="Now">What replaced it, after the word <c>sdlc</c>.</param>
    /// <param name="More">What else a person needs to know to use the new one, or null.</param>
    public sealed record Retirement(string Was, string Now, string? More = null)
    {
        /// <summary>What the binary says to somebody who typed it.</summary>
        /// <returns>One line.</returns>
        public string Answer() => string.Create(
            CultureInfo.InvariantCulture,
            $"'sdlc {Was}' is now 'sdlc {Now}'.{(More is null ? string.Empty : " " + More)}");
    }

    /// <summary>Every command an earlier release had and this one answers only with its new name, longest first.</summary>
    public static IReadOnlyList<Retirement> Retired { get; } =
    [
        new("propose constraints", "harvest constraints", "For a rule you have in mind, 'sdlc constraints --new' prints an empty entry at the next free id."),
        new("propose decisions", "harvest decisions"),
        new("propose theme", "theme"),
        new("draft mining", "draft harvest <file>"),
        new("draft convergence", "draft harvest <file>"),
        new("propose", "harvest"),
        new("mine", "harvest", "A harvest reads the completed cycles one aspect at a time; 'sdlc harvest' shows what each still needs."),
    ];

    /// <summary>The retirement a command falls under, or null when it is not retired.</summary>
    /// <param name="command">The command after the word <c>sdlc</c>, any number of words.</param>
    /// <returns>The longest retired command it starts with, word for word.</returns>
    public static Retirement? RetirementOf(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        string[] words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return Retired.FirstOrDefault(retired =>
        {
            string[] was = retired.Was.Split(' ');
            return words.Length >= was.Length && was.Select((word, at) => string.Equals(word, words[at], StringComparison.OrdinalIgnoreCase)).All(same => same);
        });
    }

    /// <summary>A retired command a text names.</summary>
    /// <param name="Line">The line it is on, counting from 1.</param>
    /// <param name="Mention">The command as named, after the word <c>sdlc</c>.</param>
    /// <param name="Instead">What to write in its place.</param>
    public sealed record Stale(int Line, string Mention, string Instead);

    /// <summary>Every retired command a text names in code -- <c>`sdlc &lt;verb&gt; ...`</c> -- with what replaced it.</summary>
    /// <remarks>
    /// Only what is in backticks is read, because that is how every scaffolded file names a command, and prose that
    /// says <em>sdlc</em> is not one. A verb this release never had is left alone: it is a typo or a stage typed at the
    /// binary, which the binary answers by naming its skill, not a command a release took away.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <returns>Each one, in the order they appear.</returns>
    public static IReadOnlyList<Stale> StaleIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<Stale> stale = [];
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');

        for (int at = 0; at < lines.Length; at++)
        {
            foreach (Match mention in Mention().Matches(lines[at]))
            {
                string command = mention.Groups["command"].Value.Trim();

                if (RetirementOf(command) is { } retired)
                {
                    stale.Add(new Stale(at + 1, command, "sdlc " + retired.Now + (retired.More is null ? string.Empty : " -- " + retired.More)));
                }
            }
        }

        return stale;
    }

    [GeneratedRegex("`sdlc (?<command>[a-z][a-z-]*(?: [a-z][a-z-]*)?)", RegexOptions.CultureInvariant)]
    private static partial Regex Mention();
}
