using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// Comment strippers, in one place.
/// </summary>
/// <remarks>
/// A check that greps source text for a construct also matches the comment explaining
/// that construct. It happened four separate times while building this suite —
/// <c>Date.now()</c> in a workflow comment, <c>-AsHashtable</c> in a PowerShell one, an
/// ellipsis read as a spread, and a multi-line justification read as absent. Every one
/// reported a correct artifact as broken, which is the noise that gets a check switched
/// off, which is the INV-06 erosion failure one layer up.
/// <para>
/// So the rule: <b>a construct check reads stripped code.</b> Stripping can only cause
/// false negatives, and a missed violation is cheaper than a check nobody trusts.
/// </para>
/// <para>
/// This type exists because the rule was about to be written a third time. Two copies is
/// duplication; three is how a rule starts drifting, and a rule that holds in two of the
/// three places it is spelled out is worse than no rule, because it still reads as
/// enforced.
/// </para>
/// </remarks>
public static class SourceText
{
    /// <summary>
    /// Strips PowerShell comments: block comments first, then line comments.
    /// </summary>
    /// <remarks>
    /// Kept as a one-argument method so it still converts to a
    /// <see cref="Func{T, TResult}"/> where a caller passes it as a method group. Adding an
    /// optional parameter here broke exactly that, in a caller the change had nothing to do
    /// with.
    /// </remarks>
    public static string StripPowerShellComments(string text) => StripPowerShell(text, false);

    /// <summary>
    /// Strips PowerShell comments but keeps the line count, so a finding can name a line.
    /// </summary>
    /// <remarks>
    /// A check that reports "somewhere in this file" sends the reader hunting, and friction
    /// is what gets a check switched off - the same erosion this whole class exists to avoid.
    /// </remarks>
    public static string StripPowerShellCommentsPreservingLines(string text) =>
        StripPowerShell(text, true);

    private static string StripPowerShell(string text, bool preserveLines)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Block first. A line-comment pass run first would shred the `#>` terminator and
        // leave the rest of the block looking like code.
        text = Regex.Replace(
            text,
            @"<#.*?#>",
            m => preserveLines ? BlankButKeepNewlines(m.Value) : string.Empty,
            RegexOptions.Singleline);

        return Regex.Replace(text, @"(?m)#.*$", string.Empty);
    }

    /// <summary>Replaces every character of a block comment except its newlines.</summary>
    /// <remarks>
    /// The line feed and carriage return are character codes rather than escape sequences.
    /// Nine times in this project a backslash escape has been eaten by a layer between the
    /// intent and the file, twice producing source that would not compile and twice producing
    /// a workflow that parsed and was wrong. A character code cannot be eaten.
    /// </remarks>
    private static string BlankButKeepNewlines(string blockComment) =>
        new string(blockComment.Select(c => c is (char)10 or (char)13 ? c : ' ').ToArray());

    /// <summary>
    /// Strips JavaScript comments: block comments first, then line comments.
    /// </summary>
    public static string StripJavaScriptComments(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"(?m)//.*$", string.Empty);
    }
}
