using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Releasing: one moving ref, a version tag at each move, and the bump that happens there
/// and nowhere else.
/// </summary>
/// <remarks>
/// The refusals carry the weight. A release is the one moment the whole tree is claimed to be
/// shippable, and everyone subscribed to <c>release</c> receives whatever it points at — so a
/// release that skips the version bump, or ships a failing build, or is cut from the wrong
/// branch, is worse than no release.
/// <para>
/// The version is bumped HERE and never in-cycle, because CI requiring a bump on every plugin
/// change would make two concurrent cycles conflict every single time. That is a guaranteed
/// collision rather than a possible one, and it is the reason the CI rule reads "fail a
/// release move that does not bump" instead of gating every merge.
/// </para>
/// </remarks>
[TestClass]
public sealed class ReleaseTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Move-SdlcRelease.ps1");

    private static string? _repo;

    [ClassInitialize]
    public static void CreateFixtureRepo(TestContext context)
    {
        _repo = Path.Combine(Path.GetTempPath(), "sdlc-release-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_repo, ".claude-plugin"));

        File.WriteAllText(Path.Combine(_repo, ".claude-plugin", "plugin.json"), """
            {
              "name": "sdlc",
              "version": "0.1.0",
              "description": "fixture"
            }
            """);

        Git("init --initial-branch=master");

        // A LOCAL IDENTITY, because the scripts under test run `git commit` themselves and a
        // CI runner has no global one. Without this the commit fails, and it failed SILENTLY:
        // Submit-SdlcIntent discarded the result and reported a successful submission with an
        // empty branch. CI caught that; every local run passed because a global identity
        // happened to exist.
        Git("config user.email t@example.invalid");
        Git("config user.name Test");
        Git("add -A");
        Git("-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
    }

    [ClassCleanup]
    public static void RemoveFixtureRepo()
    {
        if (_repo is null || !Directory.Exists(_repo))
        {
            return;
        }

        try
        {
            foreach (string f in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(_repo, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Git(string args) => Run("git", args);

    private static string Run(string exe, string args, bool allowFailure = false)
    {
        using Process p = Process.Start(new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = _repo!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (!allowFailure)
        {
            Assert.AreEqual(0, p.ExitCode, $"{exe} {args} failed: {stderr}");
        }

        return stdout + stderr;
    }

    /// <summary>
    /// Runs the mover with <c>-WhatIf</c>, which exercises every refusal and the ordering of
    /// the checks without cutting a release or running a build.
    /// </summary>
    /// <summary>
    /// Runs the mover with <c>-WhatIf</c> and a gate command that trivially passes.
    /// </summary>
    /// <remarks>
    /// The gate is a real command, passed in rather than stubbed, so the production path always
    /// runs and there is no test-only bypass in the shipped script. The fixture repository has
    /// no .NET project, so the default <c>dotnet test</c> could only ever fail — which made
    /// every check after the gate unreachable by a test.
    /// </remarks>
    private static string Move(string version, string extra = "-WhatIf")
    {
        return Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -Version {version} " +
            $"-TestCommand \"cmd /c exit 0\" {extra}",
            allowFailure: true);
    }

    /// <summary>Runs the mover with a gate command that fails.</summary>
    private static string MoveWithFailingGate(string version)
    {
        return Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -Version {version} " +
            "-TestCommand \"cmd /c exit 1\" -WhatIf",
            allowFailure: true);
    }

    /// <summary>
    /// Read from the build output rather than by walking up from it. A relative path out of
    /// bin/ depends on the output layout, which is not this suite's to promise.
    /// </summary>
    private static string CiWorkflow(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "workflows-ci", name));

    /// <summary>
    /// Remove every version tag, so a version-rule test starts from a known state.
    /// </summary>
    /// <remarks>
    /// The fixture repository is shared across the class and the script checks for an existing
    /// tag BEFORE it checks the bump rule — so a tag left behind by one test makes an unrelated
    /// one report <c>refused-tag-exists</c>, which is what happened.
    /// </remarks>
    private static void ClearVersionTags()
    {
        // (char)10 rather than an escape: newline escapes have been mangled repeatedly while
        // generating this file, and a char code cannot be.
        foreach (string tag in Run("git", "tag --list v*").Split((char)10))
        {
            string t = tag.Trim();
            if (t.Length > 0)
            {
                Run("git", "tag -d " + t);
            }
        }
    }

    private static string? OutcomeOf(string stdout)
    {
        int start = stdout.IndexOf('{', StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(stdout[start..]).RootElement.GetProperty("outcome").GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [TestMethod]
    public void TheFirstReleaseMayMatchTheManifest()
    {
        // FOUND BY RUNNING IT. The script demanded a strictly greater version unconditionally,
        // so a first release at the version the manifest already declared was refused, and the
        // project could never reach v0.1.0 at all.
        //
        // It also contradicted the CI gate written for the same rule, which says "First
        // release. Nothing to compare against, and that is not a failure." Two halves of one
        // rule disagreeing with each other.
        ClearVersionTags();

        Assert.AreEqual("whatif", OutcomeOf(Move("0.1.0")),
            "with no v* tag, releasing the manifest's own version must be permitted");
    }

    [TestMethod]
    public void AFirstReleaseBelowTheManifestIsRefused()
    {
        // Permissive about equal, never about backwards.
        ClearVersionTags();

        Assert.AreEqual("refused-below-manifest", OutcomeOf(Move("0.0.9")));
    }

    [TestMethod]
    public void OnceATagExistsEveryReleaseMustBump()
    {
        // After the first, the rule is strict again: the ref moving while the manifest stands
        // still means everyone subscribed receives new code under an old version, so nothing
        // prompts them to update and nothing tells them what they have.
        // The tag is at a DIFFERENT version from the one being released, deliberately. Tagging
        // v0.1.0 and then releasing 0.1.0 trips the tag-exists check first, which is a different
        // refusal and would not test this rule at all.
        ClearVersionTags();
        Git("tag v0.0.5");
        try
        {
            Assert.AreEqual("refused-no-bump", OutcomeOf(Move("0.1.0")),
                "a v* tag exists, so this is no longer a first release and must bump");
            Assert.AreEqual("refused-no-bump", OutcomeOf(Move("0.0.9")));
        }
        finally
        {
            ClearVersionTags();
        }
    }

    [TestMethod]
    public void ANonVersionTagDoesNotCountAsAPreviousRelease()
    {
        // `raw-concept` is a real tag in this repository and is not a release. Counting it
        // would make a first release look like a second and refuse it.
        ClearVersionTags();
        Git("tag raw-concept-fixture");
        try
        {
            Assert.AreEqual("whatif", OutcomeOf(Move("0.1.0")), "only v* tags mark a release");
        }
        finally
        {
            Git("tag -d raw-concept-fixture");
        }
    }

    [TestMethod]
    public void ANonVersionIsRefused()
    {
        string output = Move("latest");

        Assert.IsTrue(output.Contains("is not a version", StringComparison.Ordinal),
            "a tag name is not a version:\n" + output);
    }

    [TestMethod]
    public void ReleasingFromAnythingButMasterIsRefused()
    {
        // `release` has no commits of its own: its tip is always a commit already on master.
        // Cutting one from a feature branch would publish work that never landed.
        Git("checkout -b some-work");
        try
        {
            Assert.AreEqual("refused-not-master", OutcomeOf(Move("0.2.0")));
        }
        finally
        {
            Git("checkout master");
        }
    }

    [TestMethod]
    public void ReleasingADirtyTreeIsRefused()
    {
        // A release points at a commit. If the tree is dirty, what was tested is not what
        // anyone would receive.
        File.WriteAllText(Path.Combine(_repo!, "scratch.txt"), "uncommitted");
        try
        {
            Assert.AreEqual("refused-dirty", OutcomeOf(Move("0.2.0")));
        }
        finally
        {
            File.Delete(Path.Combine(_repo!, "scratch.txt"));
        }
    }

    [TestMethod]
    public void ReusingAVersionTagIsRefused()
    {
        // A version tag is the immutable per-version record. Moving one would make an earlier
        // release unidentifiable after the fact.
        ClearVersionTags();
        Git("tag v0.2.0");
        try
        {
            Assert.AreEqual("refused-tag-exists", OutcomeOf(Move("0.2.0")));
        }
        finally
        {
            ClearVersionTags();
        }
    }

    [TestMethod]
    public void TheScriptRunsNonInteractivelyWithoutCrashing()
    {
        // FOUND BY CUTTING THE REAL RELEASE. With ConfirmImpact = 'High', ShouldProcess tries
        // to prompt; invoked as `powershell.exe -File`, there is no host that can ask, and it
        // threw "Object reference not set to an instance of an object" rather than saying so.
        //
        // Every other test here passes -WhatIf, which returns before ShouldProcess would
        // prompt — so the whole suite was green while the script could not actually run. This
        // one goes all the way to a local tag, which is why it catches it.
        ClearVersionTags();

        string output = Run(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -Version 0.2.0 " +
            "-NoPush -TestCommand \"cmd /c exit 0\"",
            allowFailure: true);

        Assert.IsFalse(output.Contains("Object reference not set", StringComparison.Ordinal),
            "the script must not crash when invoked the documented way: " + output);
        Assert.AreEqual("local-only", OutcomeOf(output),
            "it must reach the end of its work: " + output);

        ClearVersionTags();
        Run("git", "reset --hard HEAD~1", allowFailure: true);
    }

    [TestMethod]
    public void AFailingGateRefusesTheRelease()
    {
        // A release is the one moment the whole tree is claimed shippable, and everyone
        // subscribed to `release` receives whatever it points at. This assertion was
        // unreachable until the gate command became a parameter.
        ClearVersionTags();

        Assert.AreEqual("refused-tests-fail", OutcomeOf(MoveWithFailingGate("0.1.0")));
    }

    [TestMethod]
    public void TheVerifyGateRunsBeforeAnythingIsWritten()
    {
        // Ordering, asserted on the script. A release is the one moment the whole tree is
        // claimed shippable; verifying after the bump would leave a version commit behind for
        // a release that never happened.
        string text = File.ReadAllText(ScriptPath);

        int verify = text.IndexOf("Verifying before release", StringComparison.Ordinal);
        int bump = text.IndexOf("Add-Step 'bump'", StringComparison.Ordinal);
        int push = text.IndexOf("Add-Step 'push'", StringComparison.Ordinal);

        Assert.IsTrue(verify > 0 && bump > 0 && push > 0);
        Assert.IsTrue(verify < bump, "the gate must run before the version is written");
        Assert.IsTrue(bump < push, "and the bump must be pushed with the release, not after it");
    }

    [TestMethod]
    public void ReleaseIsMovedAsAPointerRatherThanMerged()
    {
        // `release` carries no commits of its own, so there is nothing to merge. Pushing
        // master:release moves the pointer, which is what lets a subscriber stay subscribed.
        string text = File.ReadAllText(ScriptPath);

        Assert.IsTrue(text.Contains("master:release", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("checkout release", StringComparison.Ordinal),
            "a checkout-and-merge would give release commits of its own");
    }

    // --- the CI half -------------------------------------------------------

    [TestMethod]
    public void TheReleaseGateWorkflowFiresOnTheReleaseRefAndNowhereElse()
    {
        // Gating every merge would make concurrent cycles collide on plugin.json every time.
        // This is the one place the rule can be checked accurately.
        string yml = CiWorkflow("release-gate.yml");

        Assert.IsTrue(yml.Contains("branches: [release]", StringComparison.Ordinal),
            "the gate must fire on the release ref");
        Assert.IsFalse(yml.Contains("pull_request", StringComparison.Ordinal),
            "gating pull requests would make every concurrent cycle conflict on the version");
    }

    [TestMethod]
    public void CiRunsOnWindowsBecauseTheScriptsAreCommittedToFiveOne()
    {
        // Not a preference. The suite parses every shipped script with the real Windows
        // PowerShell 5.1 parser, which is the only reason the BOM and ANSI-decoding defects
        // were catchable. On any other runner those checks silently do not run.
        string yml = CiWorkflow("ci.yml");

        Assert.IsTrue(yml.Contains("runs-on: windows-latest", StringComparison.Ordinal));
        Assert.IsTrue(yml.Contains("fetch-depth: 0", StringComparison.Ordinal),
            "a shallow clone would make the fact gatherer's commit pin and the lifecycle " +
            "derivation's branch reads both lie");
    }
}
