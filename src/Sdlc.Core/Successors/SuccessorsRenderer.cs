using System.Globalization;
using System.Text;

namespace Sdlc.Core.Successors;

/// <summary>
/// Renders where a solution's successors stand, for <c>sdlc successors</c> and <c>sdlc successor check</c>.
/// </summary>
/// <remarks>
/// The ready ones first, because they are the ones a person can act on: each names the one command that starts it.
/// Then what is wrong with the set, which the check refuses on, then what is still waiting.
/// </remarks>
public static class SuccessorsRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders every successor and where each stands.</summary>
    /// <param name="read">Where they stand.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(ReadSuccessors.Response read)
    {
        ArgumentNullException.ThrowIfNull(read);

        StringBuilder output = new();
        int count = read.Successors.Count + read.Malformed.Count;

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"successors - {count} waiting as files in {Successor.RelativeDirectory}/, merged read at {read.ReadAt ?? "the working copy"}"));
        output.AppendLine();

        if (count == 0)
        {
            output.AppendLine(Indent + "None. A slicing writes one for each piece that needs another's code.");
            return output.ToString();
        }

        foreach (SuccessorState state in new[] { SuccessorState.Ready, SuccessorState.Stuck, SuccessorState.Unresolved, SuccessorState.Waiting })
        {
            foreach (ReadSuccessors.Standing standing in read.Successors.Where(s => s.State == state))
            {
                output.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Indent}{Word(state),-10}  successor:{standing.Successor.Slug}{(standing.Successor.Headline is { } headline ? "  " + headline : string.Empty)}"));

                foreach (ReadSuccessors.Wait wait in standing.Waits)
                {
                    output.AppendLine(Indent + new string(' ', 12) + "succeeds " + ReadSuccessors.Describe(wait));
                }

                if (state == SuccessorState.Ready)
                {
                    output.AppendLine(Indent + new string(' ', 12) + "start it: /sdlc:intent --from successor:" + standing.Successor.Slug);
                }
            }
        }

        foreach (ReadSuccessors.Malformed malformed in read.Malformed)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Indent}{"malformed",-10}  {Successor.RelativePath(malformed.Slug)}"));

            foreach (string problem in malformed.Problems)
            {
                output.AppendLine(Indent + new string(' ', 12) + problem);
            }
        }

        foreach (IReadOnlyList<string> loop in read.Loops)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{"loop",-10}  {string.Join(" -> ", loop.Select(slug => "successor:" + slug))}: none of them can come ready"));
        }

        return output.ToString();
    }

    /// <summary>Renders the check: sound, or every problem it refuses on.</summary>
    /// <param name="read">Where they stand.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string RenderCheck(ReadSuccessors.Response read)
    {
        ArgumentNullException.ThrowIfNull(read);

        if (read.Sound)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"successors - sound: {read.Successors.Count} well formed, every reference resolves, and nothing loops.{Environment.NewLine}");
        }

        StringBuilder output = new();
        output.AppendLine("successors - refused:");

        foreach (ReadSuccessors.Malformed malformed in read.Malformed)
        {
            foreach (string problem in malformed.Problems)
            {
                output.AppendLine(Indent + Successor.RelativePath(malformed.Slug) + ": " + problem);
            }
        }

        foreach (ReadSuccessors.Standing standing in read.Successors.Where(s => s.State == SuccessorState.Unresolved))
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}successor:{standing.Successor.Slug} names what does not exist: {string.Join(", ", standing.Waits.Where(w => w.State == WaitState.Missing).Select(w => w.Reference))}. Correct its succeeds:."));
        }

        foreach (IReadOnlyList<string> loop in read.Loops)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}a loop: {string.Join(" -> ", loop.Select(slug => "successor:" + slug))}. None can come ready; correct one succeeds: so it opens."));
        }

        return output.ToString();
    }

    private static string Word(SuccessorState state) => state switch
    {
        SuccessorState.Ready => "ready",
        SuccessorState.Stuck => "stuck",
        SuccessorState.Unresolved => "unresolved",
        _ => "waiting",
    };
}
