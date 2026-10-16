using Sdlc.Core.Cycles;
using Sdlc.Core.Git;

namespace Sdlc.Core.Findings;

/// <summary>Something a build saw, left alone, and wrote down.</summary>
/// <param name="Cycle">The cycle whose build wrote it.</param>
/// <param name="Statement">What it said, as one line of prose.</param>
/// <param name="Perspective">
/// The perspective that asked for it, when the line was tagged <c>Noticed: [security] ...</c>, or null.
/// Counted per perspective by <c>propose constraints</c>, the same as a finding's.
/// </param>
public sealed record Noticed(CycleId Cycle, string Statement, string? Perspective = null)
{
    /// <summary>The prefix a build writes before each one.</summary>
    public const string Prefix = "Noticed:";

    /// <summary>
    /// One line as read, split into its perspective tag and its statement.
    /// </summary>
    /// <remarks>
    /// The tag is the first thing after the prefix, in square brackets. Anything else in brackets is the
    /// statement's own, so a line that opens with a bracketed word nobody meant as a tag reads with the
    /// word as its perspective -- which <c>propose constraints</c> then shows under a name nobody
    /// recognises, rather than silently counting it somewhere.
    /// </remarks>
    /// <param name="cycle">The cycle whose build wrote it.</param>
    /// <param name="line">The line, without its prefix, as <see cref="ReadAll"/> returns it.</param>
    /// <returns>The noticed line.</returns>
    public static Noticed From(CycleId cycle, string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.StartsWith('[') && line.IndexOf(']', StringComparison.Ordinal) is var close and > 1)
        {
            string tag = line[1..close].Trim();
            string statement = line[(close + 1)..].Trim();

            if (tag.Length > 0 && statement.Length > 0 && !tag.Contains(' ', StringComparison.Ordinal))
            {
                return new Noticed(cycle, statement, tag);
            }
        }

        return new Noticed(cycle, line);
    }

    /// <summary>
    /// What one cycle's builds wrote down and left alone.
    /// </summary>
    /// <remarks>
    /// Read from the cycle's own build branches, measured from where each diverged, so a merged cycle
    /// whose branches are gone contributes nothing rather than failing. Silence here is not an error: a
    /// cycle that has not been built yet has no commits to read, which is the ordinary case for most of
    /// the cycles a caller walks. Every branch a build can run on is read -- by hand, unattended, or the
    /// bare branch of a cycle opened before those (D-85) -- and a line two of them carry is one.
    /// </remarks>
    /// <param name="repository">Git.</param>
    /// <param name="root">The solution root.</param>
    /// <param name="cycle">The cycle.</param>
    /// <param name="integration">The integration branch the build branches diverged from.</param>
    /// <returns>What it noticed, which may be none.</returns>
    public static IEnumerable<Noticed> OfCycle(IRepository repository, string root, CycleId cycle, string integration)
    {
        ArgumentNullException.ThrowIfNull(repository);

        string[] branches =
        [
            cycle.BranchName(CycleBranchKind.BuildUnattended),
            cycle.BranchName(CycleBranchKind.Build),
            cycle.BranchName(),
        ];

        return branches
            .SelectMany(branch => repository.MergeBase(root, branch, integration) is { } divergence
                ? repository.CommitMessages(root, branch, divergence)
                : [])
            .SelectMany(ReadAll)
            .Distinct(StringComparer.Ordinal)
            .Select(line => From(cycle, line));
    }

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
