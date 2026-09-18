using System.Diagnostics;
using System.Text;

namespace AiSdlc.Tests;

/// <summary>
/// Locates the repository and runs the shipped artefacts as real external processes.
/// </summary>
/// <remarks>
/// Every helper here drives the actual file a user's session invokes. Porting the hook's
/// logic into C# to make it easier to assert on would test the port, not the product, and
/// the two would drift silently - which is the failure mode this repo is most exposed to.
/// </remarks>
internal static class Repo
{
    /// <summary>Repository root, found by walking up from the test binary.</summary>
    public static string Root { get; } = FindRoot();

    public static string GateHook => Path.Combine(
        Root, "plugins", "sdlc-core", "hooks", "sdlc_gate.ps1");

    public static string ManifestCheck => Path.Combine(
        Root, "plugins", "sdlc-core", "scripts", "Assert-SliceManifest.ps1");

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".claude-plugin")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "repository root not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Result of running an external process: what the caller can actually assert on.</summary>
    public sealed record RunResult(int ExitCode, string StdOut, string StdErr)
    {
        public string Combined => StdOut + Environment.NewLine + StdErr;
    }

    public static RunResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        string? standardInput = null,
        IDictionary<string, string?>? environment = null)
    {
        ProcessStartInfo info = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? Root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // The gate reads SDLC_GATE. Clear it by default so an inherited value from the
        // developer's own shell cannot silently change what these tests are asserting.
        info.Environment.Remove("SDLC_GATE");
        if (environment is not null)
        {
            foreach (KeyValuePair<string, string?> pair in environment)
            {
                if (pair.Value is null)
                {
                    info.Environment.Remove(pair.Key);
                }
                else
                {
                    info.Environment[pair.Key] = pair.Value;
                }
            }
        }

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("failed to start " + fileName);

        StringBuilder stdout = new();
        StringBuilder stderr = new();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.StandardInput.Write(standardInput ?? string.Empty);
        process.StandardInput.Close();

        if (!process.WaitForExit(milliseconds: 60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(fileName + " did not exit within 60s");
        }

        // WaitForExit() with no argument flushes the async output handlers; the overload
        // above does not, so without this the last lines are occasionally missing.
        process.WaitForExit();

        return new RunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Runs a shipped PowerShell script the way hooks.json does.</summary>
    public static RunResult PowerShell(
        string scriptPath,
        IEnumerable<string>? scriptArgs = null,
        string? workingDirectory = null,
        string? standardInput = null,
        IDictionary<string, string?>? environment = null)
    {
        List<string> args = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath];
        if (scriptArgs is not null)
        {
            args.AddRange(scriptArgs);
        }

        return Run("powershell.exe", args, workingDirectory, standardInput, environment);
    }
}
