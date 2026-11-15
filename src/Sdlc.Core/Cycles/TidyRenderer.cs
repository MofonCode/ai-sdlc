using System.Globalization;
using System.Text;

namespace Sdlc.Core.Cycles;

/// <summary>Renders what <c>sdlc tidy</c> found, and the commands that remove it.</summary>
public static class TidyRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders the review.</summary>
    /// <param name="review">The review.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(TidyCycles.Response review)
    {
        ArgumentNullException.ThrowIfNull(review);

        StringBuilder output = new();

        if (review.Removable.Count == 0 && review.Kept.Count == 0)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Nothing the framework opened here has reached {review.Integration}, so there is nothing to tidy."));
            return output.ToString();
        }

        if (review.Removable.Count > 0)
        {
            output.AppendLine("Done, so these can go:");

            foreach (TidyCycles.Entry entry in review.Removable)
            {
                output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{entry.Branch}{(entry.Worktree is null ? string.Empty : ", and its worktree at " + entry.Worktree)}"));
                output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{Indent}{entry.Why}"));
            }
        }

        if (review.Kept.Count > 0)
        {
            if (review.Removable.Count > 0)
            {
                output.AppendLine();
            }

            output.AppendLine("Done, and still checked out:");

            foreach (TidyCycles.Entry entry in review.Kept)
            {
                output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{entry.Branch}"));
                output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{Indent}{entry.Why}"));
            }
        }

        if (review.Commands.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Once the person confirms, from any checkout of this repository:");

            foreach (string command in review.Commands)
            {
                output.AppendLine(Indent + command);
            }

            output.AppendLine();
            output.AppendLine("Git refuses to remove a worktree holding uncommitted work, which is the point. Remote branches are left alone.");
        }

        return output.ToString();
    }
}
