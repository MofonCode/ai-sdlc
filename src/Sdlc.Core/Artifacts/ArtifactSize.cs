using System.Globalization;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Artifacts;

/// <summary>
/// How big a document is, for the status panel.
/// </summary>
/// <remarks>
/// A signal and never a refusal (D-130). Slicing is where work is cut (D-125); this is what lets a person
/// see a document growing past what one pass of readers can judge. One plan reached 754 lines and 44 rounds
/// before anybody counted.
/// </remarks>
public static class ArtifactSize
{
    /// <summary>Describes a document's size in one line.</summary>
    /// <param name="kind">Which artifact it is.</param>
    /// <param name="markdown">Its contents.</param>
    /// <returns>Its lines, and what it lists that the readers judge one at a time.</returns>
    public static string Describe(ArtifactKind kind, string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        int lines = markdown.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Length;
        List<string> parts = [Count(lines, "line")];

        switch (kind)
        {
            case ArtifactKind.Spec:
                parts.Add(Count(ArtifactSections.NumberedItems(markdown, "Acceptance criteria").Count, "criterion", "criteria"));
                break;

            case ArtifactKind.Plan:
                IReadOnlyList<TableRow> rows = ArtifactSections.TableRows(markdown, "Proof");
                int entries = ArtifactSections.NumberedItems(markdown, "Proof").Count;
                parts.Add(Count(entries, "proof entry", "proof entries"));

                if (rows.Count > 0)
                {
                    parts.Add(Count(rows.Count, "proof row"));
                }

                break;

            case ArtifactKind.Verify:
                parts.Add(Count(ArtifactSections.NumberedItems(markdown, "Criteria").Count, "criterion", "criteria"));
                break;

            default:
                break;
        }

        return string.Join(" - ", parts);
    }

    private static string Count(int count, string one, string? many = null) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many ?? one + "s")}");
}
