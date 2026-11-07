using System.ComponentModel;
using System.Diagnostics;
using Sdlc.Core;

namespace Sdlc.Cli;

/// <summary>
/// Starts Claude Code with the skills of this release, which in a pinned solution is the pinned one.
/// </summary>
/// <remarks>
/// <para><strong>Why the skills ride in the package.</strong> A marketplace is cloned once per machine under its
/// name, so two worktrees pinning two tags of one marketplace share a clone and the last fetch wins; a project's
/// marketplace is ignored by <c>claude -p</c> in an untrusted folder, which is where the factory runs. The tool
/// package is already restored per pin into a folder of its own, so the skills packed beside the binary are the
/// skills of exactly the release the solution pins, and <c>--plugin-dir</c> loads them over any installed copy
/// of the same name (D-131).</para>
/// <para><strong>The hand-on marker is not passed down.</strong> It tells a release started by the global
/// binary not to hand on again, and Claude Code hands its environment to its hooks. Inherited, every
/// <c>sdlc hook</c> the session ran would stop at the global binary and judge with the wrong release.</para>
/// </remarks>
internal static class ClaudeSession
{
    /// <summary>The verb that starts a session.</summary>
    public const string Verb = "claude";

    /// <summary>Where this release's plugin is: beside the binary, as the package lays it out.</summary>
    public static string PluginDirectory => Path.Combine(AppContext.BaseDirectory, "plugin");

    /// <summary>Starts <c>claude</c> with this release's plugin and the arguments given, and returns its exit code.</summary>
    /// <param name="arguments">Everything after the verb, passed to Claude Code unchanged.</param>
    public static async Task<int> Run(IReadOnlyList<string> arguments)
    {
        string plugin = PluginDirectory;

        if (!File.Exists(Path.Combine(plugin, ".claude-plugin", "plugin.json")))
        {
            await Console.Error.WriteLineAsync(
                $"sdlc {Release.Version} carries no plugin at {plugin}, so it cannot start a session with its own skills. Reinstall the tool from a package built by ci.yml.").ConfigureAwait(false);
            return ExitCode.Usage;
        }

        ProcessStartInfo start = new("claude") { UseShellExecute = false };
        start.ArgumentList.Add("--plugin-dir");
        start.ArgumentList.Add(plugin);

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove(PinnedRelease.HandedOnVariable);

        try
        {
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("claude did not start.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            await Console.Error.WriteLineAsync(
                "claude is not on the PATH. Install Claude Code, then run this again.").ConfigureAwait(false);
            return ExitCode.Usage;
        }
    }
}
