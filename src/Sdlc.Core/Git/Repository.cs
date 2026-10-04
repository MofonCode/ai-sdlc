using System.Diagnostics;
using System.Globalization;
using FluentResults;

namespace Sdlc.Core.Git;

/// <summary>
/// What the framework needs to ask git.
/// </summary>
/// <remarks>
/// An interface because adoption depends on the answers -- whether this is a repository at all, and
/// whether it already holds code -- and a test that had to build real repositories to cover the three
/// modes would be slow and would be testing git.
/// </remarks>
public interface IRepository
{
    /// <summary>Whether the directory is inside a git working tree.</summary>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>True when git recognises it.</returns>
    bool IsRepository(string workingDirectory);

    /// <summary>
    /// Every file git is tracking.
    /// </summary>
    /// <remarks>
    /// Tracked files rather than commits, because origin is about code rather than history: a repository
    /// holding one empty commit, a README and a licence has no solution in it yet.
    /// <para>
    /// <strong>Unfiltered. The caller decides what counts as code</strong> — <c>Initialise.IsCode</c> is
    /// where the framework's own files and a repository's furniture are excluded. This summary said
    /// "excluding anything the framework owns" while the implementation ran plain <c>git ls-files</c>,
    /// which matters because a second caller would otherwise assume filtering it does not get. F-37.
    /// </para>
    /// </remarks>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>The paths, relative to the root.</returns>
    IReadOnlyList<string> TrackedFiles(string workingDirectory);

    /// <summary>The configured user name, or null when git cannot say.</summary>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>The name, or null.</returns>
    string? UserName(string workingDirectory);

    /// <summary>
    /// The branch checked out, or null when there is not one.
    /// </summary>
    /// <remarks>
    /// Null rather than the literal <c>HEAD</c> for a detached head. A detached head is not a cycle
    /// branch, and the one caller asks this question only to find out whether it is on one -- so
    /// answering with a string that reads like a branch name would make it look like a cycle called
    /// HEAD.
    /// </remarks>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>The branch name, or null.</returns>
    string? CurrentBranch(string workingDirectory);

    /// <summary>
    /// Stages the given paths and commits them.
    /// </summary>
    /// <param name="workingDirectory">Where to run.</param>
    /// <param name="paths">What to stage, relative to the root.</param>
    /// <param name="message">The commit message.</param>
    /// <returns>Success, or a failure carrying what git said.</returns>
    Result Commit(string workingDirectory, IReadOnlyList<string> paths, string message);

    /// <summary>
    /// Where a branch diverged from the default branch.
    /// </summary>
    /// <remarks>
    /// A merge base rather than the default branch's current tip. Measuring from the tip would show every
    /// commit somebody else landed while this cycle was open as part of this change, and a diff that wide
    /// makes the declared-file comparison meaningless and the proof unreadable.
    /// </remarks>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <param name="branch">The cycle's branch.</param>
    /// <returns>The commit, or null when git cannot say.</returns>
    string? MergeBase(string workingDirectory, string branch);

    /// <summary>The diff from a commit to the working tree, including what is not yet committed.</summary>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <param name="from">The commit to measure from.</param>
    /// <returns>The patch, or null when git cannot say.</returns>
    string? Diff(string workingDirectory, string from);

    /// <summary>The paths that differ from a commit, including untracked files.</summary>
    /// <remarks>
    /// Untracked files are included deliberately. A new file an agent wrote and never staged is part of the
    /// change, and leaving it out would make the comparison miss exactly the case it is best at catching.
    /// </remarks>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <param name="from">The commit to measure from.</param>
    /// <returns>The paths, relative to the root.</returns>
    IReadOnlyList<string> ChangedFiles(string workingDirectory, string from);
}

/// <summary>
/// Asks the real git.
/// </summary>
/// <remarks>
/// Spawns the git executable rather than linking a library. The framework needs four facts and a commit,
/// and a managed git implementation would be a large dependency carried for that -- while every machine
/// running this already has the tool the user's own workflow depends on.
/// </remarks>
public sealed class GitRepository : IRepository
{
    // Short, because every one of these runs on a path a person is waiting on. A git that has not
    // answered in five seconds is not going to, and failing to answer is handled rather than fatal.
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public bool IsRepository(string workingDirectory) =>
        Run(workingDirectory, "rev-parse --is-inside-work-tree") is { } answer
        && answer.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<string> TrackedFiles(string workingDirectory)
    {
        string? output = Run(workingDirectory, "ls-files");

        return Lines(output);
    }

    /// <inheritdoc />
    public string? UserName(string workingDirectory)
    {
        string? name = Run(workingDirectory, "config user.name")?.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <inheritdoc />
    public string? CurrentBranch(string workingDirectory)
    {
        string? branch = Run(workingDirectory, "rev-parse --abbrev-ref HEAD")?.Trim();

        return string.IsNullOrWhiteSpace(branch)
            || branch.Equals("HEAD", StringComparison.Ordinal)
            ? null
            : branch;
    }

    /// <inheritdoc />
    public Result Commit(string workingDirectory, IReadOnlyList<string> paths, string message)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (paths.Count == 0)
        {
            return Result.Ok();
        }

        // Each path quoted and staged explicitly rather than with `git add .`, so a commit made on a
        // person's behalf cannot pick up work they had in progress.
        string add = "add -- " + string.Join(' ', paths.Select(path => "\"" + path + "\""));

        (string? staged, string? staging) = Ask(workingDirectory, add);

        if (staged is null)
        {
            return Result.Fail(
                "git could not stage what init created. " + (staging ?? "It gave no reason."));
        }

        // The message goes through a file rather than the command line: it is multi-line, and quoting a
        // multi-line argument differs between the shells this might run under.
        string file = Path.Combine(Path.GetTempPath(), "sdlc-commit-" + Guid.NewGuid().ToString("N"));

        try
        {
            File.WriteAllText(file, message);

            (string? committed, string? complaint) = Ask(
                workingDirectory,
                string.Create(CultureInfo.InvariantCulture, $"commit -F \"{file}\""));

            return committed is null
                ? Result.Fail(
                    "git could not commit what init created. " + (complaint ?? "It gave no reason."))
                : Result.Ok();
        }
        finally
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    /// <inheritdoc />
    public string? MergeBase(string workingDirectory, string branch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        // The default branch is whatever the remote says it is, falling back to the local main or master.
        // Hard-coding one is how this breaks on the first repository that uses the other.
        string? head = Run(workingDirectory, "symbolic-ref --short refs/remotes/origin/HEAD")?.Trim();

        string[] candidates = head is { Length: > 0 }
            ? [head, "origin/main", "origin/master", "main", "master"]
            : ["origin/main", "origin/master", "main", "master"];

        foreach (string candidate in candidates)
        {
            if (Run(workingDirectory, $"merge-base {candidate} {branch}")?.Trim() is { Length: > 0 } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <strong>Untracked files are added with <c>--intent-to-add</c> first, and that is the whole
    /// point.</strong> <c>git diff &lt;base&gt;</c> cannot see a file git is not tracking, so the patch
    /// recorded as evidence excluded every new file -- which is every first cycle and most feature
    /// work. The reassuring "every declared file changed, and nothing else did" came from
    /// <see cref="ChangedFiles"/>, which does include them, so the panel agreed while
    /// <c>diff.patch</c> was empty and there was nothing for a verify to cite.
    /// <para>
    /// <c>--intent-to-add</c> stages the path and none of the content, so a person's index keeps
    /// whatever they had staged and the file still shows as new in the patch. The alternative was a
    /// <c>--no-index</c> diff per untracked file, which is one process per file and produces a patch
    /// in a slightly different shape.
    /// </para>
    /// </remarks>
    public string? Diff(string workingDirectory, string from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);

        IReadOnlyList<string> untracked = Lines(
            Run(workingDirectory, "ls-files --others --exclude-standard"));

        if (untracked.Count > 0)
        {
            // Quoted individually, and a failure here is deliberately not fatal: a patch missing one
            // new file is worse than it should be, and no patch at all is worse still.
            _ = Run(
                workingDirectory,
                "add --intent-to-add -- " + string.Join(' ', untracked.Select(path => "\"" + path + "\"")));
        }

        return Run(workingDirectory, $"diff {from}");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ChangedFiles(string workingDirectory, string from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string line in Lines(Run(workingDirectory, $"diff --name-only {from}")))
        {
            paths.Add(line);
        }

        // Untracked files too. A new file an agent wrote and never staged is part of the change, and
        // leaving it out would make the comparison miss the case it is best at catching.
        foreach (string line in Lines(Run(workingDirectory, "ls-files --others --exclude-standard")))
        {
            paths.Add(line);
        }

        return [.. paths.Order(StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string> Lines(string? output) => output is null
        ? []
        : [.. output
            .ReplaceLineEndings()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static string? Run(string workingDirectory, string arguments) =>
        Ask(workingDirectory, arguments).Output;

    /// <summary>
    /// Runs git and returns both of what it said.
    /// </summary>
    /// <remarks>
    /// <strong>Both streams are drained concurrently, and that is the fix rather than tidiness.</strong>
    /// This redirected standard error and never read it, while <c>ReadToEnd</c> on standard output
    /// blocked with no timeout -- so git writing enough to fill the stderr pipe buffer meant git
    /// blocked on stderr, the parent blocked on stdout, and <see cref="_patience"/> was never reached.
    /// A real deadlock, not a slow path: <c>git add</c> on Windows emits one "LF will be replaced by
    /// CRLF" warning per file, so staging enough files gets there. It is in <c>init</c> and
    /// <c>evidence</c>, which have no external timeout; the hook paths are capped by the host config
    /// and would have failed rather than hung.
    /// <para>
    /// <strong>And a failure carries git's message.</strong> Every failure used to return null, so
    /// git's own reason was thrown away and <c>Commit</c> reported "git could not stage what init
    /// created" with nothing to act on -- which contradicts the framework's own standard that a
    /// refusal says what has to change, and <c>CON_10.2</c> is that rule.
    /// </para>
    /// </remarks>
    /// <param name="workingDirectory">Where to run it.</param>
    /// <param name="arguments">The arguments, after the word git.</param>
    /// <returns>
    /// The output when git succeeded, and otherwise null with whatever it said about why. Both are
    /// null when git could not be started at all.
    /// </returns>
    private static (string? Output, string? Error) Ask(string workingDirectory, string arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return (null, null);
            }

            // Started before either is awaited, so neither pipe can fill while this waits on the other.
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(_patience))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception killed) when (killed is InvalidOperationException or NotSupportedException)
                {
                    // It exited between the wait timing out and the kill. Nothing to do.
                }

                // TotalSeconds, not `_patience / 1000`. Dividing a TimeSpan by 1000 gives five
                // milliseconds, so the message read "did not finish within 00:00:00.0050000 seconds"
                // -- about a timeout nobody could have hit.
                return (null, string.Create(
                    CultureInfo.InvariantCulture,
                    $"git {arguments} did not finish within {_patience.TotalSeconds} seconds and was stopped."));
            }

            // Safe to block now: the process has exited, so both pipes are closed and complete.
            string said = output.GetAwaiter().GetResult();
            string complained = error.GetAwaiter().GetResult();

            return process.ExitCode == 0
                ? (said, null)
                : (null, string.IsNullOrWhiteSpace(complained)
                    ? string.Create(CultureInfo.InvariantCulture, $"git {arguments} exited {process.ExitCode} and said nothing.")
                    : complained.Trim());
        }
        catch (Exception error)
            when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No git on PATH, or it could not be started. Every caller treats a null output as "git
            // cannot say", which is what lets a missing git produce a clear refusal rather than a
            // stack trace.
            return (null, "git could not be started. " + error.Message);
        }
    }
}
