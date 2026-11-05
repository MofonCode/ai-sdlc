using System.Text;

namespace Sdlc.Core.Drafts;

/// <summary>
/// Renders what a person reads before placing a drafted file.
/// </summary>
/// <remarks>
/// One shape for every drafter: the text to place, then what to do with it. A skill shows this output
/// verbatim, so it is a contract rather than presentation -- and three drafters printing three layouts
/// would make the one habit this pattern depends on, read then place, feel like three habits.
/// </remarks>
public static class DraftRenderer
{
    /// <summary>
    /// Renders a drafted block and its instructions.
    /// </summary>
    /// <param name="draft">What the drafter produced.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(DraftResult draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        StringBuilder output = new();

        output.AppendLine("Place this:");
        output.AppendLine();
        output.Append(draft.Entry);
        output.AppendLine();
        output.AppendLine(draft.Instructions);

        return output.ToString();
    }
}
