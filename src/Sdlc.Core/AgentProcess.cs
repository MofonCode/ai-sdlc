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

    /// <summary>What a build's or a series' process handed back as it ended, or null when it said nothing readable.</summary>
    public string? Said { get; init; }

    /// <summary>
    /// Each tool use the host refused a build's or a series' process, as <c>tool: command</c>, oldest first (D-224).
    /// </summary>
    /// <remarks>
    /// A refusal stops a headless build at the step that needed the command, and its hand-back then reads like a
    /// failing test. Named here, from the host's own record, so the person reads which command and why.
    /// </remarks>
    public IReadOnlyList<string> Refused { get; init; } = [];
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
    private readonly string? _executable;

    /// <summary>Starts <c>claude</c>, found on the path.</summary>
    public CliAgentProcess()
    {
    }

    /// <summary>Starts another program in its place, handed the same arguments, input and output.</summary>
    /// <param name="executable">The program. The suite uses a shell, so a process can end without reading its input.</param>
    public CliAgentProcess(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        _executable = executable;
    }

    /// <inheritdoc />
    public async Task<AgentExit> Run(AgentLaunch launch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launch);

        ProcessStartInfo start = new(_executable ?? ToolPath.Resolve("claude"))
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

            Task<bool> input = launch.Input is { } inputFile ? Pipe(inputFile, process) : Task.FromResult(true);
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
            bool whole = true;

            try
            {
                await Task.WhenAll(input, output).ConfigureAwait(false);
                whole = await input.ConfigureAwait(false);
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

            // A process that ended before taking its whole prompt answered something other than what it was asked, so
            // even a clean exit is a problem. What it said on its way out is the reason, when it said anything.
            string unread = whole ? string.Empty : " It ended before reading all of its prompt.";

            if (process.ExitCode == 0)
            {
                return whole ? new AgentExit(0, false, null) : new AgentExit(0, false, (said + unread).Trim());
            }

            return new AgentExit(process.ExitCode, false, (said.Length > 0
                ? said
                : string.Create(CultureInfo.InvariantCulture, $"claude exited {process.ExitCode} and said nothing.")) + unread);
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>Pipes a file to the process's input.</summary>
    /// <returns>Whether it took all of it: false when it closed its end first, by exiting.</returns>
    /// <remarks>
    /// A process that exits early closes the pipe under the copy, and the write throws. Thrown on, one reader ending
    /// early took every other reader with it, and what it said on the way out was never read (D-214).
    /// </remarks>
    private static async Task<bool> Pipe(string file, Process process)
    {
        FileStream source = File.OpenRead(file);

        await using (source.ConfigureAwait(false))
        {
            try
            {
                await source.CopyToAsync(process.StandardInput.BaseStream).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                return false;
            }
        }

        return true;
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
