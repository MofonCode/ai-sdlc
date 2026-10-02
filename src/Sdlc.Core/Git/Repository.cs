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
    /// The files git is tracking, excluding anything the framework owns.
    /// </summary>
    /// <remarks>
    /// Tracked files rather than commits, because origin is about code rather than history: a repository
    /// holding one empty commit, a README and a licence has no solution in it yet.
    /// </remarks>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>The paths, relative to the root.</returns>
    IReadOnlyList<string> TrackedFiles(string workingDirectory);

    /// <summary>The configured user name, or null when git cannot say.</summary>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>The name, or null.</returns>
    string? UserName(string workingDirectory);

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
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

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

        if (Run(workingDirectory, add) is null)
        {
            return Result.Fail("git could not stage what init created.");
        }

        // The message goes through a file rather than the command line: it is multi-line, and quoting a
        // multi-line argument differs between the shells this might run under.
        string file = Path.Combine(Path.GetTempPath(), "sdlc-commit-" + Guid.NewGuid().ToString("N"));

        try
        {
            File.WriteAllText(file, message);

            return Run(workingDirectory, string.Create(CultureInfo.InvariantCulture, $"commit -F \"{file}\"")) is null
                ? Result.Fail("git could not commit what init created.")
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
    public string? Diff(string workingDirectory, string from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);

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

    private static string? Run(string workingDirectory, string arguments)
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
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(Patience))
            {
                return null;
            }

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception error)
            when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No git on PATH, or it could not be started. Every caller treats null as "git cannot say",
            // which is what lets a missing git produce a clear refusal rather than a stack trace.
            return null;
        }
    }
}
