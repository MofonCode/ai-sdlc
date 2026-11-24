using System.Diagnostics;
using Sdlc.Core;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The upgrade pipeline's script, run rather than read: it installs the release asked for and hands the move to it,
/// under bash, in a clone of a throwaway remote.
/// </summary>
/// <remarks>
/// The script is what an older release leaves in a solution, so it is held to doing only that (D-213). <c>dotnet</c>
/// is a stand-in that moves the pin and records what it was asked to run, so what is tested is the script, not the
/// binary: <c>ApplyUpgradeTests</c> holds the move.
/// </remarks>
[TestClass]
public sealed class UpgradePipelineScriptTests
{
    private const string Integration = "development";
    private const string From = "0.1.0";
    private const string To = "0.2.0";

    // Moves the pin, lists it, and records the hand-over with the release the manifest pinned when it was asked.
    private const string FakeDotnet = """
        #!/usr/bin/env bash
        manifest=.config/dotnet-tools.json
        case "$*" in
          "tool list --local")
            printf 'Package Id  Version  Commands  Manifest\n---\nmofoncode.sdlc.cli  %s  sdlc  %s\n' "$(sed -n 's/.*"version": "\(.*\)".*/\1/p' $manifest)" "$manifest" ;;
          "tool update --local MofonCode.Sdlc.Cli --version "*)
            sed -i "s/\"version\": \"[^\"]*\"/\"version\": \"$6\"/" $manifest ;;
          "sdlc upgrade apply --push")
            echo "handed over as $(sed -n 's/.*"version": "\(.*\)".*/\1/p' $manifest)" ;;
          *)
            echo "unexpected: dotnet $*" >&2; exit 9 ;;
        esac

        """;

    [TestMethod]
    [DataRow(Platform.GitHub, To, true)]
    [DataRow(Platform.AzureDevOps, To, true)]
    [DataRow(Platform.GitHub, From, false)]
    [DataRow(Platform.AzureDevOps, From, false)]
    public void It_installs_the_release_asked_for_and_hands_the_move_to_it(Platform platform, string release, bool moves)
    {
        string root = Path.Combine(Path.GetTempPath(), "sdlc-upgrade-script", Guid.NewGuid().ToString("N"));
        string remote = Path.Combine(root, "remote.git");
        string work = Path.Combine(root, "work");
        string bin = Path.Combine(root, "bin");
        Directory.CreateDirectory(work);
        Directory.CreateDirectory(bin);

        try
        {
            // No identity anywhere: not global, not system, and none git may invent from the host's name.
            string noIdentity = Path.Combine(root, "gitconfig");
            File.WriteAllText(noIdentity, "[user]\n\tuseConfigOnly = true\n");
            Dictionary<string, string> environment = new()
            {
                ["GIT_CONFIG_GLOBAL"] = noIdentity,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
                ["RELEASE"] = release,
                ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            };

            Git(root, environment, $"init -q --bare -b {Integration} \"{remote}\"");
            Git(work, environment, $"clone -q \"{remote}\" .");
            Git(work, environment, $"switch -q -c {Integration}");
            Directory.CreateDirectory(Path.Combine(work, ".config"));
            Directory.CreateDirectory(Path.Combine(work, ".sdlc"));
            File.WriteAllText(Path.Combine(work, ".config", "dotnet-tools.json"), $$"""{ "tools": { "mofoncode.sdlc.cli": { "version": "{{From}}" } } }""" + "\n");
            File.WriteAllText(Path.Combine(work, ".sdlc", "README.md"), "the old release's\n");
            Git(work, environment, "add -A");
            Git(work, environment, "-c user.name=seed -c user.email=seed@localhost commit -q -m seed");
            Git(work, environment, $"push -q origin {Integration}");

            string dotnet = Path.Combine(bin, "dotnet");
            File.WriteAllText(dotnet, FakeDotnet.ReplaceLineEndings("\n"));

            // Not executable, bash passes it by and finds the real one further down the path.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(dotnet, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            string script = Path.Combine(root, "upgrade.sh");
            File.WriteAllText(script, Script(platform));

            (int exit, string output) = Run(Bash(), $"\"{script.Replace('\\', '/')}\"", work, environment);

            Assert.AreEqual(0, exit, output);

            if (moves)
            {
                StringAssert.Contains(output, "handed over as " + To, "The move runs as the release just installed.\n" + output);
            }
            else
            {
                StringAssert.Contains(output, "Nothing to upgrade", output);
                Assert.IsFalse(output.Contains("handed over", StringComparison.Ordinal), "A solution on the release already stops cleanly.");
            }

            Assert.AreEqual("seed", Git(work, environment, "log -1 --format=%s").Trim(), "The script commits nothing itself: the release it hands to does.");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A scratch directory a virus scanner still has open. Nothing here depends on it going.
            }
            catch (UnauthorizedAccessException)
            {
                // git's object files are read-only on Windows. Nothing here depends on it going.
            }
        }
    }

    /// <summary>The step's script, from the first line after its block opens to the hand-over, which is its last.</summary>
    private static string Script(Platform platform)
    {
        string[] lines = UpgradePipeline.Text(platform, Integration).ReplaceLineEndings("\n").Split('\n');
        int identity = Array.FindIndex(lines, line => line.Contains("git config user.name 'sdlc upgrade'", StringComparison.Ordinal));
        int start = Array.FindLastIndex(lines, identity, line => line.TrimEnd().EndsWith('|')) + 1;
        int end = Array.FindIndex(lines, start, line => line.Contains("dotnet sdlc upgrade apply", StringComparison.Ordinal)) + 1;
        Assert.IsTrue(end > start);
        Assert.IsFalse(lines[end].StartsWith(new string(' ', lines[start].Length - lines[start].TrimStart().Length), StringComparison.Ordinal) && lines[end].Trim().Length > 0, "The hand-over is the script's last line.");
        int indent = lines[start].Length - lines[start].TrimStart().Length;

        return string.Join('\n', lines[start..end].Select(line => line.Length >= indent ? line[indent..] : line.TrimStart())) + "\n";
    }

    /// <summary>Git's own bash on Windows, so a WSL bash earlier on the path is never the one started.</summary>
    private static string Bash()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        (_, string execPath) = Run("git", "--exec-path", Path.GetTempPath(), []);
        string bash = Path.GetFullPath(Path.Combine(execPath.Trim(), "..", "..", "..", "bin", "bash.exe"));
        Assert.IsTrue(File.Exists(bash), "Git for Windows carries bash beside it: " + bash);
        return bash;
    }

    private static string Git(string directory, Dictionary<string, string> environment, string arguments)
    {
        (int exit, string output) = Run("git", arguments, directory, environment);
        Assert.AreEqual(0, exit, $"git {arguments}: {output}");
        return output;
    }

    private static (int Exit, string Output) Run(string file, string arguments, string directory, Dictionary<string, string> environment)
    {
        ProcessStartInfo start = new(file, arguments)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach ((string name, string value) in environment)
        {
            start.Environment[name] = value;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException(file + " did not start.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.Result);
    }
}
