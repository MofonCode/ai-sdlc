using System.Diagnostics;

namespace Sdlc.Tests;

/// <summary>
/// A disposable copy of a fixture solution, initialized as a git repository.
/// <para>
/// Skills mutate the solution they run in, so they must never run in the checked-in fixture: a
/// test that dirties its own input passes once and is meaningless afterwards. Git is present
/// because build and verify read a diff, which does not exist without a commit to diff against.
/// </para>
/// </summary>
internal sealed class FixtureWorkspace : IDisposable
{
    private FixtureWorkspace(string path, string baseCommit)
    {
        Path = path;
        BaseCommit = baseCommit;
    }

    /// <summary>Absolute path of the materialized copy.</summary>
    public string Path { get; }

    /// <summary>The commit holding the fixture as checked in. Diffs are taken against this.</summary>
    public string BaseCommit { get; }

    /// <summary>
    /// Copies the named fixture solution into a fresh temporary directory and commits it.
    /// </summary>
    public static FixtureWorkspace Materialize(string solutionName)
    {
        string source = System.IO.Path.Combine(RepoPaths.Solutions, solutionName);

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"No sample solution named '{solutionName}' under {RepoPaths.Solutions}.");
        }

        string target = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ai-sdlc-fixture",
            $"{solutionName}-{Guid.NewGuid():N}");

        CopyTree(source, target);

        // -c rather than config so the identity is never written anywhere persistent, and so this
        // works on a runner with no global git identity.
        Git(target, "init", "--quiet", "--initial-branch=main");
        Git(target, "add", "--all");
        Git(target,
            "-c", "user.name=ai-sdlc fixtures",
            "-c", "user.email=fixtures@localhost",
            "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--message", $"fixture: {solutionName} as checked in");

        string head = Git(target, "rev-parse", "HEAD").Trim();

        return new FixtureWorkspace(target, head);
    }

    /// <summary>Runs git in this workspace and returns its standard output.</summary>
    public string Run(params string[] arguments) => Git(Path, arguments);

    public void Dispose()
    {
        try
        {
            // git marks objects read-only, which blocks a plain recursive delete on Windows.
            foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temporary directory is untidy, not a test failure. Swallowing this keeps a
            // file lock from masking the result of the test that actually ran.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, target, StringComparison.Ordinal));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, target, StringComparison.Ordinal), overwrite: true);
        }
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo info = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start git.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed with {process.ExitCode}: {stderr.Trim()}");
        }

        return stdout;
    }
}
