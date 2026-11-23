using System.Diagnostics;
using Sdlc.Core;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// The upgrade pipeline's script, run rather than read: the steps that move the pin, run adoption and commit, under
/// bash, in a clone of a throwaway remote, on an agent with no git identity of its own.
/// </summary>
/// <remarks>
/// The other upgrade tests assert strings in the template, and every one of them passed while the script could not
/// open a pull request on any agent: adoption committed before the script set an identity, and had it not, its commit
/// would have left nothing staged for the script's own. <c>dotnet</c> is a stand-in that moves the pin and does what
/// adoption does to git -- writes, then commits -- so what is tested is the script around it, not the binary.
/// </remarks>
[TestClass]
public sealed class UpgradePipelineScriptTests
{
    private const string Integration = "development";
    private const string From = "0.1.0";
    private const string To = "0.2.0";

    // Moves the pin, lists it, and stands in for adoption: it changes a file an upgrade carries and writes one it may
    // not, and commits both as 'sdlc init' does, failing as it fails when git has no identity to commit with.
    private const string FakeDotnet = """
        #!/usr/bin/env bash
        manifest=.config/dotnet-tools.json
        case "$*" in
          "tool list --local")
            printf 'Package Id  Version  Commands  Manifest\n---\nmofoncode.sdlc.cli  %s  sdlc  %s\n' "$(sed -n 's/.*"version": "\(.*\)".*/\1/p' $manifest)" "$manifest" ;;
          "tool update --local MofonCode.Sdlc.Cli --version "*)
            sed -i "s/\"version\": \"[^\"]*\"/\"version\": \"$6\"/" $manifest ;;
          "sdlc init")
            echo "written by the new release" >> .sdlc/README.md
            echo "an instruction file nobody had" > AGENTS.md
            git add -- .sdlc/README.md AGENTS.md $manifest && git commit -q -m "Update the ai-sdlc scaffolding" || exit 3 ;;
          "sdlc factory --keep")
            echo "Kept" ;;
          *)
            echo "unexpected: dotnet $*" >&2; exit 9 ;;
        esac

        """;

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void It_pushes_an_upgrade_branch_carrying_what_adoption_wrote_and_only_what_an_upgrade_may(Platform platform)
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
                ["RELEASE"] = To,
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
            string branch = $"upgrade/sdlc-{To}";
            string changed = Git(work, environment, $"--git-dir=\"{remote}\" diff --name-only {Integration} {branch}");

            CollectionAssert.AreEquivalent(
                new[] { PinnedRelease.ManifestPath, ".sdlc/README.md" },
                changed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                "The pin and what adoption wrote reach the pull request's branch, and AGENTS.md, which an upgrade may not carry, does not.\n" + output);
            StringAssert.Contains(output, "AGENTS.md", "What was left out is said.");
            StringAssert.Contains(
                Git(work, environment, $"--git-dir=\"{remote}\" log -1 --format=%s {branch}"),
                $"Move to sdlc {To}",
                "One commit, the script's own, rather than adoption's beside it.");
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

    /// <summary>The step's script, from the first line after its block opens up to the pull request's platform call.</summary>
    private static string Script(Platform platform)
    {
        string[] lines = UpgradePipeline.Text(platform, Integration).ReplaceLineEndings("\n").Split('\n');
        int identity = Array.FindIndex(lines, line => line.Contains("git config user.name 'sdlc upgrade'", StringComparison.Ordinal));
        int start = Array.FindLastIndex(lines, identity, line => line.TrimEnd().EndsWith('|')) + 1;
        int end = Array.FindIndex(lines, start, line => line.Contains("gh pr view", StringComparison.Ordinal) || line.Contains("az extension add", StringComparison.Ordinal));
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
