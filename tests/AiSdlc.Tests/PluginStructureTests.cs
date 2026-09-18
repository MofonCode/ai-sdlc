namespace AiSdlc.Tests;

/// <summary>
/// Structural integrity of the marketplace, and the charter invariants that claim to be
/// checkable by grep.
/// </summary>
/// <remarks>
/// An invariant nobody runs is an aspiration. These run on every build.
/// </remarks>
[TestClass]
public sealed class PluginStructureTests
{
    private const string ParseCheck =
        "$errs = $null; "
        + "$null = [System.Management.Automation.Language.Parser]::ParseFile("
        + "$env:SCRIPT_UNDER_TEST, [ref]$null, [ref]$errs); "
        + "if ($errs -and $errs.Count -gt 0) { $errs | ForEach-Object { $_.ToString() }; exit 1 }";

    private static IEnumerable<string> EngineInstructionFiles()
    {
        string engine = Path.Combine(Repo.Root, "plugins", "sdlc-core");
        foreach (string dir in new[] { "skills", "agents" })
        {
            string full = Path.Combine(engine, dir);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    [TestMethod]
    public void CharterInvariantI1_EngineImposesNoStack()
    {
        string[] stackTerms =
        [
            "dotnet", ".net", "c#", "csharp", "blazor", "aspire", "asp.net", "ef core",
            "entity framework", "azure", "aws", "terraform", "msbuild", "nuget",
            "sql server", "java", "typescript", "pyyaml", "redis",
        ];

        List<string> violations = [];
        foreach (string file in EngineInstructionFiles())
        {
            string[] lines = File.ReadAllLines(file);
            bool inFence = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;
                    continue;
                }

                // Illustrative paths inside fenced examples impose nothing. The invariant is
                // about normative prose telling a consuming repo what to build with.
                if (inFence)
                {
                    continue;
                }

                string lower = lines[i].ToLowerInvariant();
                foreach (string term in stackTerms)
                {
                    if (lower.Contains(term, StringComparison.Ordinal))
                    {
                        violations.Add(
                            Path.GetRelativePath(Repo.Root, file) + ":" + (i + 1) + " -> " + term);
                    }
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            "the engine must impose no stack on the repo being built:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void EveryShippedPowerShellScript_Parses()
    {
        string[] scripts = Directory.GetFiles(
            Path.Combine(Repo.Root, "plugins"), "*.ps1", SearchOption.AllDirectories);

        Assert.IsTrue(scripts.Length > 0, "expected at least one shipped script");

        foreach (string script in scripts)
        {
            // Path goes through the environment rather than into the command string: quoting
            // a Windows path into a PowerShell -Command is a trap, and this repo has already
            // lost two files to escaping layers.
            Repo.RunResult result = Repo.Run(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ParseCheck],
                environment: new Dictionary<string, string?> { ["SCRIPT_UNDER_TEST"] = script });

            Assert.AreEqual(0, result.ExitCode, script + Environment.NewLine + result.Combined);
        }
    }

    [TestMethod]
    public void NoPythonShipsInThisRepository()
    {
        // The charter commits to this, and a session shim once sat here untracked long
        // enough for a fresh reviewer to catch it in the act.
        string git = Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar;
        string[] python = Directory
            .GetFiles(Repo.Root, "*.py", SearchOption.AllDirectories)
            .Where(p => !p.Contains(git, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            python.Length,
            "python found: "
                + string.Join(", ", python.Select(p => Path.GetRelativePath(Repo.Root, p))));
    }
}
