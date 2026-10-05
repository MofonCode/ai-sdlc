using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>Something a build saw, left alone, and wrote down.</summary>
/// <param name="Cycle">The cycle whose build wrote it.</param>
/// <param name="Statement">What it said, as one line of prose.</param>
public sealed record Noticed(CycleId Cycle, string Statement)
{
    /// <summary>The prefix a build writes before each one.</summary>
    public const string Prefix = "Noticed:";

    /// <summary>
    /// Reads the <c>Noticed:</c> lines out of one commit message.
    /// </summary>
    /// <remarks>
    /// <strong>These and the findings rounds are two different stores, and only one of them was being
    /// read.</strong> A build is forbidden from fixing what it notices in passing -- that is creep inside
    /// an approved file, which <c>CON_07.2</c> catches -- so the build skill has it write a
    /// <c>Noticed:</c> line in the commit instead. The commit is the right home: it reaches whoever
    /// reviews the pull request. But <c>sdlc suggestions</c> read only the interrogation rounds, so the
    /// command that answers "what have we noticed and not acted on" could not see the lines written by
    /// the stage that notices most. The dice-roller cycle wrote three and none of them was listed.
    /// <para>
    /// A line wraps, because a commit message wraps. Continuation is every following line until a blank
    /// one or the next prefix, joined with a single space -- so a statement reads back as the sentence it
    /// was written as rather than as the shape the terminal happened to give it.
    /// </para>
    /// </remarks>
    /// <param name="message">One commit message, whole.</param>
    /// <returns>What it noticed, in the order written, which may be none.</returns>
    public static IReadOnlyList<string> ReadAll(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        List<string> found = [];
        List<string>? current = null;

        foreach (string raw in message.ReplaceLineEndings().Split(Environment.NewLine))
        {
            string line = raw.Trim();

            if (line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                Close(found, current);
                current = [line[Prefix.Length..].Trim()];
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (line.Length == 0)
            {
                Close(found, current);
                current = null;
                continue;
            }

            current.Add(line);
        }

        Close(found, current);

        return found;
    }

    private static void Close(List<string> found, List<string>? current)
    {
        if (current is null)
        {
            return;
        }

        string statement = string.Join(' ', current.Where(part => part.Length > 0)).Trim();

        if (statement.Length > 0)
        {
            found.Add(statement);
        }
    }
}
