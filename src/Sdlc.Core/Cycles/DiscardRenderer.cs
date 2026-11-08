using System.Globalization;
using System.Text;

namespace Sdlc.Core.Cycles;

/// <summary>Renders what throwing a sandbox away would lose, and what to do first.</summary>
public static class DiscardRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders the review.</summary>
    /// <param name="review">The review.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(DiscardSandbox.Response review)
    {
        ArgumentNullException.ThrowIfNull(review);

        StringBuilder output = new();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{review.Sandbox}, at {review.Folder}:"));

        if (review.Cycles.Count == 0)
        {
            output.AppendLine(Indent + "carries no cycle of its own.");
        }

        foreach (DiscardSandbox.Entry entry in review.Cycles)
        {
            string verdict = entry.Verdict switch
            {
                DiscardVerdict.Empty => "can go",
                DiscardVerdict.Deletable => "can go",
                DiscardVerdict.WorthRecording => "worth recording",
                _ => "recorded",
            };

            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{entry.Id}  {entry.Headline ?? "(no headline)"}"));
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{Indent}  {verdict}: {entry.Why}"));
        }

        output.AppendLine();
        if (review.Remove is { } remove)
        {
            output.AppendLine("Nothing here is worth keeping. Once the person confirms, from their own checkout:");
            output.AppendLine(Indent + remove);
        }
        else
        {
            output.AppendLine("Not yet: record what is worth keeping, admit the records with 'sdlc admit', and ask again.");
        }

        return output.ToString();
    }
}
