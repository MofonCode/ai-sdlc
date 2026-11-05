namespace Sdlc.Core;

/// <summary>
/// Finds a platform CLI the way a shell would, so a process can start it.
/// </summary>
/// <remarks>
/// <para><strong>The Azure CLI on Windows is <c>az.cmd</c>.</strong> Starting a process without the shell
/// resolves only <c>.exe</c> files from a bare name, so <c>az</c> reads as not on the path on every Windows
/// machine that has it -- and every caller turns that into "install the Azure CLI", which is false. This
/// walks the path with the extensions Windows itself would try, and hands back the full path, which a
/// process starts whatever the extension.</para>
///
/// <para>A name it cannot find comes back unchanged, so the caller's own "not on the path" still says
/// so.</para>
/// </remarks>
internal static class ToolPath
{
    /// <summary>The full path to a tool, or the name itself when it is not found.</summary>
    /// <param name="tool">A bare name: <c>az</c>, <c>gh</c>.</param>
    /// <returns>What to start.</returns>
    public static string Resolve(string tool)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(tool) || Path.IsPathRooted(tool))
        {
            return tool;
        }

        string[] extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string[] directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string directory in directories)
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory.Trim('"'), tool + extension);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return tool;
    }
}
