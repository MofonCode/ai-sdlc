using System.Diagnostics;

namespace Sdlc.Tests;

/// <summary>
/// The scripts a cycle runs directly, rather than through a hook: build readiness, pinning the base a
/// change is measured from, and comparing the result to the plan.
/// <para>
/// Compare-DiffToPlan.ps1 had no test at all, which is how it shipped taking its base from
/// <c>git rev-parse HEAD</c> at the moment it ran. That is the artifacts commit only while nothing has
/// been committed since, so one ordinary <c>git commit</c> during a build moved the base on top of the
/// change, emptied the diff and reported success with an undeclared file in it. The check built so that
/// nothing could route around it was routed around by the commonest command in git.
/// </para>
/// </summary>
[TestClass]
public sealed class CycleScriptTests
{
    private const string CycleId = "20260101_aaaa";

    private sealed record Result(int ExitCode, string Output);

    private static Result Run(string script, params string[] arguments)
    {
        ProcessStartInfo info = new("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-ExecutionPolicy");
        info.ArgumentList.Add("Bypass");
        info.ArgumentList.Add("-File");
        info.ArgumentList.Add(RepoPaths.At("tools", script));

        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start powershell.exe.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new Result(process.ExitCode, stdout + stderr);
    }

    /// <summary>Commits everything staged, with an identity that is never written to disk.</summary>
    private static void Commit(FixtureWorkspace workspace, string message)
    {
        workspace.Run("add", "--all");
        workspace.Run(
            "-c", "user.name=ai-sdlc tests",
            "-c", "user.email=tests@localhost",
            "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--message", message);
    }

    /// <summary>Writes a plan declaring one file, approved unless told otherwise.</summary>
    private static void WritePlan(FixtureWorkspace workspace, bool approved = true)
    {
        string cycleDir = Path.Combine(workspace.Path, ".sdlc", "cycles", CycleId);
        Directory.CreateDirectory(cycleDir);

        List<string> lines =
        [
            "---",
            $"id: {CycleId}",
            $"spec: .sdlc/cycles/{CycleId}/spec.md",
            "tier: local",
        ];

        if (approved)
        {
            lines.Add("approved_by: a person");
            lines.Add("approved_at: 2026-01-01T12:00:00Z");
        }

        lines.AddRange(
        [
            "---",
            "",
            "# Plan",
            "",
            "## Files",
            "",
            "| File | Change |",
            "|---|---|",
            "| `src/Declared.cs` | the declared work |",
            "",
            "## Proof",
            "",
            "- it builds",
        ]);

        File.WriteAllLines(Path.Combine(cycleDir, "plan.md"), lines);
    }

    private static void OnCycleBranch(FixtureWorkspace workspace)
        => workspace.Run("checkout", "--quiet", "-b", $"cycle/{CycleId}");

    private static void WriteSource(FixtureWorkspace workspace, string relative, string content)
    {
        string full = Path.Combine(workspace.Path, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    // ------------------------------------------------------------------ the base is pinned, not guessed

    [TestMethod]
    public void TheComparisonIsUnchangedByHowOftenTheBuildCommits()
    {
        // The regression test for the defect above. The same work, compared three times: uncommitted,
        // after one commit, and after two. An undeclared file must be reported every time.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        OnCycleBranch(workspace);
        WritePlan(workspace);
        Commit(workspace, "record artifacts");

        Result pinned = Run("Set-CycleBase.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);
        Assert.AreEqual(0, pinned.ExitCode, pinned.Output);

        WriteSource(workspace, "src/Declared.cs", "class Declared {}");
        WriteSource(workspace, "src/Undeclared.cs", "class Sneaky {}");

        List<string> bases = [];

        for (int commits = 0; commits <= 2; commits++)
        {
            Result result = Run(
                "Compare-DiffToPlan.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

            Assert.AreEqual(
                1,
                result.ExitCode,
                $"After {commits} commit(s) the undeclared file must still be refused: {result.Output}");

            StringAssert.Contains(
                result.Output,
                "src/Undeclared.cs",
                StringComparison.Ordinal,
                $"After {commits} commit(s) the undeclared file vanished from the comparison. " +
                "A commit must not be able to hide a change from it.");

            bases.Add(result.Output
                .Split('\n')
                .First(line => line.StartsWith("base:", StringComparison.Ordinal))
                .Trim());

            // Real content each time, so every commit has something in it and the build genuinely grows
            // across them rather than the loop committing nothing twice.
            WriteSource(workspace, "src/Declared.cs", $"class Declared {{ int v{commits}; }}");
            Commit(workspace, $"build, commit {commits + 1}");
        }

        Assert.AreEqual(
            1,
            bases.Distinct(StringComparer.Ordinal).Count(),
            "The base moved between comparisons: " + string.Join(" | ", bases));
    }

    [TestMethod]
    public void TheComparisonRefusesWhenNoBaseWasPinned()
    {
        // Guessing a base is what the old behaviour did. Refusing is the only safe answer, because a
        // wrong base reports success rather than failing.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        OnCycleBranch(workspace);
        WritePlan(workspace);
        Commit(workspace, "record artifacts");

        WriteSource(workspace, "src/Undeclared.cs", "class Sneaky {}");

        Result result = Run(
            "Compare-DiffToPlan.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

        Assert.AreNotEqual(0, result.ExitCode, "A comparison with no base must not succeed.");
        StringAssert.Contains(result.Output, "base.sha", StringComparison.Ordinal);
    }

    [TestMethod]
    public void APinnedBaseIsNotMoved()
    {
        // Re-pinning would put work already done behind the new base and hide it, which is the same
        // failure by another route.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        OnCycleBranch(workspace);
        WritePlan(workspace);
        Commit(workspace, "record artifacts");

        Result first = Run("Set-CycleBase.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);
        Assert.AreEqual(0, first.ExitCode, first.Output);

        WriteSource(workspace, "src/Declared.cs", "class Declared {}");
        Commit(workspace, "build");

        Result second = Run("Set-CycleBase.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

        StringAssert.Contains(second.Output, "ALREADY PINNED", StringComparison.Ordinal);

        string basePath = Path.Combine(
            workspace.Path, ".sdlc", "cycles", CycleId, "evidence", "base.sha");

        StringAssert.Contains(
            first.Output,
            File.ReadAllText(basePath).Trim(),
            StringComparison.Ordinal,
            "The recorded base changed after a second pin.");
    }

    // ------------------------------------------------------------------ build readiness

    [TestMethod]
    public void BuildIsNotReadyOffTheCycleBranch()
    {
        // Off cycle/<id> the write gates stay silent, so a build there is unenforced while looking
        // governed. That is worse than no build at all.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        WritePlan(workspace);

        Result result = Run(
            "Assert-BuildReady.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

        Assert.AreEqual(1, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "cycle branch", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildIsNotReadyWithUncommittedSource()
    {
        // Anything already dirty is attributed to this cycle and surfaces as scope the plan never
        // declared, which sends a reader to investigate the wrong thing.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        OnCycleBranch(workspace);
        WritePlan(workspace);
        WriteSource(workspace, "src/Preexisting.cs", "class Old {}");

        Result result = Run(
            "Assert-BuildReady.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

        Assert.AreEqual(1, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "uncommitted", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildIsReadyWhenOnlyTheCycleArtifactsAreDirty()
    {
        // The carve-out that makes the check usable: the plan has just been written and is necessarily
        // uncommitted at this point. A rule that refused here would refuse every real build.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        OnCycleBranch(workspace);
        WritePlan(workspace);

        Result result = Run(
            "Assert-BuildReady.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

        Assert.AreEqual(0, result.ExitCode, $"Only .sdlc/ is dirty, which is normal: {result.Output}");
        StringAssert.Contains(result.Output, "READY", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildIsNotReadyWithoutAnApprovedPlan()
    {
        // The gate the whole process rests on. A person decides what a change may touch, before it does.
        using FixtureWorkspace workspace = FixtureWorkspace.Materialize("greenfield");
        OnCycleBranch(workspace);
        WritePlan(workspace, approved: false);

        Result result = Run(
            "Assert-BuildReady.ps1", "-CycleId", CycleId, "-RepositoryRoot", workspace.Path);

        Assert.AreEqual(1, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "not approved", StringComparison.Ordinal);
    }
}
