namespace Sdlc.Core.Runs;

/// <summary>What one run of a working copy's whole suite produced.</summary>
/// <param name="Built">Whether the build succeeded; when it did not, no test ran.</param>
/// <param name="Results">Every results file the runner wrote, in path order.</param>
/// <param name="Log">What the build and the runner printed, for a refusal to quote.</param>
public sealed record SuiteRun(bool Built, IReadOnlyList<string> Results, string Log);

/// <summary>Builds a working copy and runs its whole suite once, keeping the results files.</summary>
/// <remarks>
/// What a merge into a run's branch is checked by (D-234). A seam, so the suite can stand in a runner that answers from
/// what it was told rather than building a solution.
/// </remarks>
public interface ISuiteRunner
{
    /// <summary>Builds the working copy, then runs its suite with its results written to a directory.</summary>
    /// <param name="workingDirectory">The working copy.</param>
    /// <param name="resultsDirectory">Where the runner writes its results files.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>What the run produced.</returns>
    Task<SuiteRun> Run(string workingDirectory, string resultsDirectory, CancellationToken cancellationToken);
}

/// <summary>
/// <c>dotnet build</c>, then <c>dotnet test --report-trx</c>: the same build and runner the pipeline the framework writes
/// uses, so a merge is held to what its pull request will be.
/// </summary>
public sealed class DotnetSuiteRunner : ISuiteRunner
{
    /// <inheritdoc />
    public async Task<SuiteRun> Run(string workingDirectory, string resultsDirectory, CancellationToken cancellationToken)
    {
        (int built, string buildLog) = await LongProcess.Run("dotnet", ["build"], workingDirectory, cancellationToken).ConfigureAwait(false);

        if (built != 0)
        {
            return new SuiteRun(false, [], buildLog);
        }

        Directory.CreateDirectory(resultsDirectory);

        // Its exit code says only that something failed; which tests did is read from the results files.
        (_, string testLog) = await LongProcess
            .Run("dotnet", ["test", "--report-trx", "--results-directory", resultsDirectory], workingDirectory, cancellationToken)
            .ConfigureAwait(false);

        return new SuiteRun(
            true,
            [.. Directory.EnumerateFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories).Order(StringComparer.Ordinal)],
            buildLog + testLog);
    }
}
