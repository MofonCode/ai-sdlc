using System.Diagnostics;

namespace Sdlc.Cli;

/// <summary>Reads what git already knows, so the gate does not ask for it again.</summary>
internal static class Git
{
    /// <summary>
    /// The configured user name, for pre-filling who is approving.
    /// </summary>
    /// <remarks>
    /// Pre-filling only. The entry is pasted by a person, so what matters is that the name is theirs
    /// and not that a tool chose it — and a person who pastes a name that is not theirs has done
    /// something a tool cannot prevent and a reviewer can see.
    /// </remarks>
    /// <param name="workingDirectory">Where to ask from.</param>
    /// <returns>The name, or null when git cannot say.</returns>
    public static string? UserName(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "config user.name")
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

            string output = process.StandardOutput.ReadToEnd().Trim();

            // A short wait: this runs on a path a person is waiting on, and a git that has not answered
            // in two seconds is not going to.
            if (!process.WaitForExit(TimeSpan.FromSeconds(2)))
            {
                return null;
            }

            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No git on PATH, or it could not be started. Not a reason to refuse to print an entry;
            // the name is then left for the person to fill in.
            return null;
        }
    }
}
