using System.Diagnostics;
using FluentResults;
using Sdlc.Core;

namespace Sdlc.Cli;

/// <summary>
/// Hands a command to the release the solution pins, when that is not this one.
/// </summary>
/// <remarks>
/// <para><strong>Why the global binary hands on rather than being replaced.</strong> Every skill, hook, guide
/// and panel says <c>sdlc</c>, and a local tool is spelled <c>dotnet sdlc</c>; D-37 rejected the manifest on
/// exactly that. So the one installed globally stays the one every caller names, and inside a solution that
/// pins another release it starts that release with the same arguments, the same working directory and the
/// same standard streams, and returns its exit code (D-131). A hook reads its payload on standard input, and
/// that stream is inherited rather than copied.</para>
/// <para><strong>A pinned release not yet on this machine is restored first.</strong> The alternative is a
/// refusal on every hook call until somebody runs <c>dotnet tool restore</c>, and a hook that fails open
/// governs nothing in the meantime. Restore's own output goes to standard error, so a command whose standard
/// output is read -- a hook's decision, <c>sdlc admission</c> in a pipeline -- carries only the answer.</para>
/// </remarks>
internal static class Handoff
{
    /// <summary>Runs the command on the pinned release, or says this release should run it.</summary>
    /// <param name="args">The command line, unchanged.</param>
    /// <returns>The pinned release's exit code, or null when this release is the one to run it.</returns>
    public static async Task<int?> Run(string[] args)
    {
        if (Environment.GetEnvironmentVariable(PinnedRelease.HandedOnVariable) is { Length: > 0 })
        {
            return null;
        }

        // A dev build outranks the pin, everywhere, for as long as the variable is set (D-135). It never needs a
        // feed or a version bump, and the rounds it writes call it local, so nothing it judged reaches a pull request.
        if (PinnedRelease.DevBuild() is { } devBuild)
        {
            if (PinnedRelease.IsInside(AppContext.BaseDirectory, devBuild))
            {
                return null;
            }

            Result<string> dev = PinnedRelease.DevBuildEntry(devBuild);

            if (dev.IsFailed)
            {
                await Console.Error.WriteLineAsync(dev.Errors[0].Message).ConfigureAwait(false);
                return ExitCode.Usage;
            }

            return await Start(["exec", dev.Value, .. args], toStandardError: false).ConfigureAwait(false);
        }

        string? pinned = PinnedRelease.Find(Directory.GetCurrentDirectory());

        if (pinned is null || string.Equals(pinned, Release.Version, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? entry = PinnedRelease.Restored(pinned);

        if (entry is null)
        {
            await Console.Error.WriteLineAsync(
                $"This solution pins sdlc {pinned}, and this machine has {Release.Version}. Restoring {pinned}...").ConfigureAwait(false);

            int restored = await Start(["tool", "restore"], toStandardError: true).ConfigureAwait(false);
            entry = PinnedRelease.Restored(pinned);

            if (restored != 0 || entry is null)
            {
                await Console.Error.WriteLineAsync(
                    $"sdlc {pinned} could not be restored. Run 'dotnet tool restore' here and read what it says: usually no feed this machine can reach holds that version.").ConfigureAwait(false);
                return ExitCode.Usage;
            }
        }

        return await Start(["exec", entry, .. args], toStandardError: false).ConfigureAwait(false);
    }

    private static async Task<int> Start(IEnumerable<string> arguments, bool toStandardError)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = toStandardError,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment[PinnedRelease.HandedOnVariable] = Release.Version;

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("dotnet did not start.");

        if (toStandardError)
        {
            string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await Console.Error.WriteAsync(output).ConfigureAwait(false);
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }
}
