using FluentResults;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// A git that answers from what a test handed it.
/// </summary>
/// <remarks>
/// Building real repositories to cover adoption's three modes, or to make a diff with an untracked file in
/// it, would be slow and would mostly be testing git. What is worth testing is what the framework does with
/// the answers.
/// <para>
/// Every answer defaults to the shape a working repository gives, so a test says only what it is about.
/// </para>
/// </remarks>
internal sealed class FakeRepository : IRepository
{
    /// <summary>Whether this pretends to be a repository at all.</summary>
    public bool Repository { get; init; } = true;

    /// <summary>What git is tracking.</summary>
    public IReadOnlyList<string> Tracked { get; init; } = [];

    /// <summary>What differs from the base commit, including anything untracked.</summary>
    public IReadOnlyList<string> Changed { get; init; } = [];

    /// <summary>The commit a cycle's branch diverged at, or null when git cannot say.</summary>
    public string? Base { get; init; } = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The patch.</summary>
    public string? Patch { get; init; } = "diff --git a/src/Thing.cs b/src/Thing.cs\n";

    /// <summary>Who git thinks is working.</summary>
    public string? Name { get; init; } = "Peter Almanzar";

    /// <summary>
    /// The branch checked out.
    /// </summary>
    /// <remarks>
    /// Defaults to a branch that is not a cycle branch, so a test that does not mention it is a test
    /// about something else -- and the turn-end gate's first rule is to say nothing off a cycle.
    /// </remarks>
    public string? Branch { get; init; } = "master";

    /// <summary>What was committed, so a test can assert what was staged.</summary>
    public List<IReadOnlyList<string>> Commits { get; } = [];

    public bool IsRepository(string workingDirectory) => Repository;

    public IReadOnlyList<string> TrackedFiles(string workingDirectory) => Tracked;

    public string? UserName(string workingDirectory) => Name;

    public string? CurrentBranch(string workingDirectory) => Branch;

    public Result Commit(string workingDirectory, IReadOnlyList<string> paths, string message)
    {
        Commits.Add(paths);
        return Result.Ok();
    }

    public string? MergeBase(string workingDirectory, string branch) => Base;

    public string? Diff(string workingDirectory, string from) => Patch;

    public IReadOnlyList<string> ChangedFiles(string workingDirectory, string from) => Changed;
}
