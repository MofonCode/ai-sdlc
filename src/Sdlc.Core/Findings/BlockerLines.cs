using System.Globalization;

namespace Sdlc.Core.Findings;

/// <summary>
/// One open blocker as the approval surfaces print it: which rule, what set it off, where, and the fix.
/// </summary>
/// <remarks>
/// <para>One shape for the approval draft, the approval refusal and the gate's refusal, because those are
/// read by the person deciding what to do about a blocker and they had drifted: one printed the rule and
/// the fix, one the rule and no fix, one bare ids. Each of them told somebody a rule was broken and none
/// told them where.</para>
/// <para>The status panel groups blockers by rule and lays them out on its own grid, so it does not use
/// this; what it prints for each one is the same four things.</para>
/// </remarks>
internal static class BlockerLines
{
    /// <summary>The lines for one blocker.</summary>
    /// <param name="id">The id as the reader should see it, qualified by artifact where there are several.</param>
    /// <param name="finding">The blocker.</param>
    /// <param name="indent">What every line starts with.</param>
    /// <returns>Two to four lines, without line endings.</returns>
    public static IReadOnlyList<string> Of(string id, Finding finding, string indent)
    {
        ArgumentNullException.ThrowIfNull(finding);

        string rule = finding.Cites is { } cites
            ? string.Create(CultureInfo.InvariantCulture, $"  {cites.Id}  {cites.Heading}").TrimEnd()
            : string.Empty;

        List<string> lines =
        [
            indent + id + rule,
            indent + "    " + finding.Headline,
        ];

        if (finding.Place is { } place)
        {
            lines.Add(indent + "    Where: " + place);
        }

        if (finding.Remedy is { } remedy)
        {
            lines.Add(indent + "    Fix: " + remedy.ReplaceLineEndings(" "));
        }

        return lines;
    }
}
