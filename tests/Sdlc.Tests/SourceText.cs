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
    public static string StripPowerShellComments(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Block first. A line-comment pass run first would shred the `#>` terminator and
        // leave the rest of the block looking like code.
        text = Regex.Replace(text, @"<#.*?#>", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"(?m)#.*$", string.Empty);
    }

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
