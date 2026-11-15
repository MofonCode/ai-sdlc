using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Sdlc.Core.Hooks;

namespace Sdlc.Core;

/// <summary>One agent process the binary starts: where, with what, and what it reads and writes.</summary>
/// <param name="WorkingDirectory">Where it starts. A reader starts outside the checkout (D-163); a build in its worktree.</param>
/// <param name="Arguments">Everything after the word <c>claude</c>, one argument each, never re-split by a shell.</param>
/// <param name="Input">A file piped to its standard input, or null. A reader's prompt goes this way, never as an argument.</param>
/// <param name="Output">A file its standard output is written to, or null to pass it through to this process's own.</param>
/// <param name="Reader">
/// Whether it is a reader, marked so a red build's turn-end hook lets its turn end with its answer (D-109). A build's
/// own process is never one: the mark would let a red build stop.
/// </param>
/// <param name="Timeout">How long it may run before it is stopped, or null for as long as it takes.</param>
public sealed record AgentLaunch(
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    string? Input,
    string? Output,
    bool Reader,
    TimeSpan? Timeout);

/// <summary>How an agent process ended.</summary>
/// <param name="ExitCode">Its exit code; -1 when it never started.</param>
/// <param name="TimedOut">Whether it was stopped for running past its time.</param>
/// <param name="Problem">What went wrong, for a person, or null when it exited cleanly.</param>
/// <param name="NeverStarted">Whether there was no host to start: <c>claude</c> is not on the path.</param>
public sealed record AgentExit(int ExitCode, bool TimedOut, string? Problem, bool NeverStarted = false)
{
    /// <summary>Whether it ran to the end and said it succeeded.</summary>
    public bool Succeeded => ExitCode == 0 && !TimedOut && Problem is null;
}

/// <summary>
/// Starts an agent process: a reader, an adjudicator, or a build's own process.
/// </summary>
/// <remarks>
/// <para><strong>The binary starts every process the chain runs</strong> (D-183). A skill that started its own wrote a
/// multi-line shell command its host had to approve call by call -- which a headless process cannot do -- and that
/// differed between shells. Here the launch is one implementation, its tools named in its arguments, and never with
/// the host's permission checks switched off.</para>
/// <para>An interface because the suite must not start Claude Code, for the same reason <c>IRepository</c> is one.</para>
/// </remarks>
public interface IAgentProcess
{
    /// <summary>Runs one process to its end.</summary>
    /// <param name="launch">What to start.</param>
    /// <param name="cancellationToken">Token observed while it runs.</param>
    /// <returns>How it ended. Never throws for a process that would not start; that is a problem in the exit.</returns>
    Task<AgentExit> Run(AgentLaunch launch, CancellationToken cancellationToken);
}

/// <summary>
/// Starts <c>claude</c> as the host's own command line, the way the factory does and the readers always have.
/// </summary>
/// <remarks>
/// The hand-on marker is removed, as <c>sdlc claude</c> removes it: Claude Code hands its environment to its hooks,
/// and an inherited marker would stop every <c>sdlc hook</c> the process ran at the global binary.
/// </remarks>
public sealed class CliAgentProcess : IAgentProcess
{
    /// <inheritdoc />
    public async Task<AgentExit> Run(AgentLaunch launch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);

        ProcessStartInfo start = new(ToolPath.Resolve("claude"))
        {
            WorkingDirectory = launch.WorkingDirectory,
            UseShellExecute = false,
            // Always taken, and closed at once when there is nothing to pipe, so a headless process never waits on a
            // terminal it was not given -- what the factory's '< /dev/null' did.
            RedirectStandardInput = true,
            RedirectStandardOutput = launch.Output is not null,
            RedirectStandardError = launch.Output is not null,
            CreateNoWindow = launch.Output is not null,
        };

        foreach (string argument in launch.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove(PinnedRelease.HandedOnVariable);

        if (launch.Reader)
        {
            start.Environment[TurnEndGate.ReaderVariable] = "1";
        }
        else
        {
            start.Environment.Remove(TurnEndGate.ReaderVariable);
        }

        Process process;

        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("claude did not start.");
        }
        catch (Win32Exception)
        {
            return new AgentExit(-1, false, "claude is not on the PATH. Install Claude Code, then run this again.", NeverStarted: true);
        }

        // Disposed by hand, after every task reading or writing its pipes has been awaited on every path below.
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (launch.Timeout is { } timeout)
            {
                limit.CancelAfter(timeout);
            }

            if (launch.Input is null)
            {
                process.StandardInput.Close();
            }

            Task input = launch.Input is { } inputFile ? Pipe(inputFile, process) : Task.CompletedTask;
            Task output = launch.Output is { } outputFile ? Capture(process, outputFile) : Task.CompletedTask;
            Task<string> error = launch.Output is not null ? process.StandardError.ReadToEndAsync(CancellationToken.None) : Task.FromResult(string.Empty);
            bool stopped = false;

            try
            {
                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Stop(process);
                stopped = true;
            }

            // Awaited on every path, so nothing reads or writes the process's pipes after it is disposed. A stopped
            // process closes them mid-copy, which is not a second problem to report.
            string said;

            try
            {
                await Task.WhenAll(input, output).ConfigureAwait(false);
                said = (await error.ConfigureAwait(false)).Trim();
            }
            catch (IOException) when (stopped)
            {
                said = string.Empty;
            }

            if (stopped)
            {
                return cancellationToken.IsCancellationRequested
                    ? new AgentExit(-1, false, "It was stopped.")
                    : new AgentExit(-1, true, string.Create(CultureInfo.InvariantCulture, $"It ran past {launch.Timeout!.Value.TotalMinutes:0} minutes and was stopped."));
            }

            return process.ExitCode == 0
                ? new AgentExit(0, false, null)
                : new AgentExit(process.ExitCode, false, said.Length > 0
                    ? said
                    : string.Create(CultureInfo.InvariantCulture, $"claude exited {process.ExitCode} and said nothing."));
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task Pipe(string file, Process process)
    {
        FileStream source = File.OpenRead(file);

        await using (source.ConfigureAwait(false))
        {
            await source.CopyToAsync(process.StandardInput.BaseStream).ConfigureAwait(false);
        }

        process.StandardInput.Close();
    }

    private static async Task Capture(Process process, string file)
    {
        FileStream target = File.Create(file);

        await using (target.ConfigureAwait(false))
        {
            await process.StandardOutput.BaseStream.CopyToAsync(target).ConfigureAwait(false);
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exited) when (exited is InvalidOperationException or NotSupportedException)
        {
            // It ended between the wait giving up and the kill. Nothing to stop.
        }
    }
}
