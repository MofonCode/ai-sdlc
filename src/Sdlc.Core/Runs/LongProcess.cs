using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Sdlc.Core.Runs;

/// <summary>
/// Runs a program that may take as long as it takes -- a merge, a worktree opened, a push, a build, a suite -- and
/// returns its exit code with everything it printed.
/// </summary>
/// <remarks>
/// The repository's own git calls give up after five seconds, which is right for a question and wrong for a merge of a
/// large branch or a push over a slow network. Both streams are drained together, so neither pipe fills while this
/// waits on the other.
/// </remarks>
internal static class LongProcess
{
    /// <summary>Runs a program to its end, or until the token stops it.</summary>
    /// <param name="tool">The program, found on the path.</param>
    /// <param name="arguments">Its arguments, one each.</param>
    /// <param name="workingDirectory">Where it runs.</param>
    /// <param name="cancellationToken">Stops it, and everything it started.</param>
    /// <returns>The exit code, -1 when it could not start, and what it printed on both streams.</returns>
    public static async Task<(int Code, string Output)> Run(
        string tool,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(ToolPath.Resolve(tool))
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove(PinnedRelease.HandedOnVariable);

        Process process;

        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException(tool + " did not start.");
        }
        catch (Win32Exception)
        {
            return (-1, string.Create(CultureInfo.InvariantCulture, $"{tool} is not on the PATH."));
        }

        using (process)
        {
            process.StandardInput.Close();
            Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception gone) when (gone is InvalidOperationException or NotSupportedException)
                {
                    // It ended between the token and the kill.
                }

                throw;
            }

            string said = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
            return (process.ExitCode, said);
        }
    }

    /// <summary>Runs git to its end.</summary>
    /// <param name="workingDirectory">Where it runs.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <param name="arguments">Its arguments, one each.</param>
    /// <returns>The exit code and what it printed.</returns>
    public static Task<(int Code, string Output)> Git(string workingDirectory, CancellationToken cancellationToken, params string[] arguments) =>
        Run("git", arguments, workingDirectory, cancellationToken);
}
