using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Drives <c>tools/Complete-SdlcCycle.ps1</c>, the landing step.
/// </summary>
/// <remarks>
/// <b>This script had never executed once.</b> Every reference to it in this suite read it as text.
/// It was unreachable: it refuses anything but <c>awaiting-landing</c>, and it also refuses a cycle
/// with no <c>retro.md</c> on the branch — while the lifecycle derivation called a cycle
/// <c>done</c> the moment a retro existed. Without a retro it failed its own step 4; with one it
/// failed its own precondition. No input satisfied both.
/// <para>
/// <b>Its first execution belongs in a throwaway repository, not this one.</b> It checks out,
/// rebases, commits, pushes and opens a pull request. Each fixture here is a real git repository in
/// the temp directory with a real minimal test project, because the script's re-verification runs
/// <c>dotnet test</c> directly and a fixture that cannot satisfy it stops at step 2 — which would
/// leave steps 3 through 7 exactly as untested as they were.
/// </para>
/// <para>
/// No fixture has a remote. The script's terminal outcome is then <c>verified-no-remote</c>, which
/// is as far as a test can drive it without inventing a GitHub.
/// </para>
/// </remarks>
[TestClass]
public sealed class CycleLandingTests
{
    private static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "tools", "Complete-SdlcCycle.ps1");

    private const string Csproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <OutputType>Exe</OutputType>
            <NoWarn>CA1515</NoWarn>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="MSTest" Version="4.4.1" />
          </ItemGroup>
        </Project>
        """;

    private const string PassingTest = """
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace Fixture;

        [TestClass]
        public sealed class Fixed
        {
            [TestMethod]
            public void Passes() => Assert.IsTrue(true);
        }
        """;

    /// <summary>
    /// A repository whose cycle is verified on its branch, optionally with a retro and findings.
    /// Master carries everything except the cycle branch's own commits.
    /// </summary>
    private static string MakeLandableRepo(string cycleId, bool withRetro, bool withFindings)
    {
        string root = Path.Combine(Path.GetTempPath(), "sdlc-land-" + Guid.NewGuid().ToString("N")[..8]);
        string cycleDir = Path.Combine(root, ".sdlc", "cycles", cycleId);
        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "intents"));
        Directory.CreateDirectory(cycleDir);

        File.WriteAllText(Path.Combine(root, ".sdlc", "CHARTER.md"),
            "---\nversion: 1\nsolution: fixture\n---\n\n# Charter\n");

        File.WriteAllText(Path.Combine(root, ".sdlc", "intents", cycleId + ".md"),
            $"---\nid: {cycleId}\nsource: authored\ncharter-version: 1\n---\n\n# Intent\n");

        File.WriteAllText(Path.Combine(cycleDir, "spec.md"),
            $"---\ncycle-id: {cycleId}\nintent: {cycleId}\ncharter-version: 1\n---\n\n# Spec\n");

        // The re-verification runs `dotnet test` in the repository root, so the fixture needs
        // something for it to run. Without this the script stops at step 2 and steps 3 to 7 stay
        // unexercised.
        File.WriteAllText(Path.Combine(root, "Fixture.csproj"), Csproj);
        File.WriteAllText(Path.Combine(root, "Fixed.cs"), PassingTest);

        // A .gitignore so build output does not become part of the cycle's diff.
        File.WriteAllText(Path.Combine(root, ".gitignore"), "bin/\nobj/\n");

        Git(root, "init --initial-branch=master");
        Git(root, "add -A");
        Git(root, "-c user.email=t@example.invalid -c user.name=Test commit -m fixture");

        // Everything below lands on the cycle branch only, which is what makes the retro present
        // on the branch and absent from master.
        Git(root, "checkout -b cycle-" + cycleId);

        File.WriteAllText(Path.Combine(cycleDir, "verify.md"),
            $"---\ncycle-id: {cycleId}\nplan: {cycleId}\ncharter-version: 1\noutcome: passed\n---\n\n# Verify\n");

        if (withFindings)
        {
            Directory.CreateDirectory(Path.Combine(cycleDir, "findings"));
            File.WriteAllText(Path.Combine(cycleDir, "findings", "spec.r1.json"), $$"""
                {
                  "cycle-id": "{{cycleId}}",
                  "target": "spec",
                  "round": 1,
                  "charter-version": 1,
                  "findings": [
                    {
                      "id": "spec-r1-reason1",
                      "state": "suggested",
                      "category": "ambiguity",
                      "stage": "spec",
                      "detection": "reasoning",
                      "role": "coherence",
                      "summary": "a suggestion that should reach the intake file",
                      "cites": []
                    }
                  ]
                }
                """);
        }

        if (withRetro)
        {
            File.WriteAllText(Path.Combine(cycleDir, "retro.md"),
                $"---\ncycle-id: {cycleId}\nsolution: fixture\nlanded-at: 2026-09-26\ncharter-version: 1\n---\n\n# Retro\n");
        }

        Git(root, "add -A");
        Git(root, "-c user.email=t@example.invalid -c user.name=Test commit -m cycle");

        return root;
    }

    private static void Git(string root, string args)
    {
        using Process p = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
    }

    /// <summary>Runs the landing script and returns everything it printed, JSON included.</summary>
    private static string LandRaw(string root, string cycleId)
    {
        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", ScriptPath, "-CycleId", cycleId, "-RepoRoot", root,
            },
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return stdout;
    }

    /// <summary>Runs the landing script and returns its JSON result, or the error it threw.</summary>
    private static (JsonElement? Result, string Error) Land(string root, string cycleId)
    {
        using Process p = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", ScriptPath, "-CycleId", cycleId, "-RepoRoot", root,
            },
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        })!;

        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        // The script prints human lines before its JSON, so the document starts at the first brace
        // of a line that opens one.
        int start = stdout.IndexOf("{\r\n", StringComparison.Ordinal);
        if (start < 0)
        {
            start = stdout.IndexOf("{\n", StringComparison.Ordinal);
        }

        if (start < 0)
        {
            return (null, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }

        try
        {
            return (JsonDocument.Parse(stdout[start..]).RootElement.Clone(), stderr);
        }
        catch (JsonException)
        {
            return (null, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }
    }

    private static void Discard(string dir)
    {
        try
        {
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-5")]
    [TestProperty("AC", "AC-20260926-26a1-7")]
    [TestProperty("AC", "AC-20260926-26a1-8")]
    public void AVerifiedCycleWithARetroOnItsBranchLandsAsFarAsTheRemote()
    {
        // ONE RUN, THREE CRITERIA, because each `dotnet test` the script runs costs real seconds and
        // the three questions are about the same single execution: did it get past the precondition
        // that used to reject it, did it write the intake, and where did it stop.
        string root = MakeLandableRepo("20260926-b001", withRetro: true, withFindings: true);

        try
        {
            (JsonElement? result, string error) = Land(root, "20260926-b001");
            Assert.IsNotNull(result, "the landing script produced no JSON result: " + error);

            JsonElement d = result!.Value;
            string outcome = d.GetProperty("outcome").GetString()!;

            // AC-5: the precondition no longer rejects it. Anything but these two proves it ran.
            Assert.AreNotEqual("refused-no-retro", outcome,
                "the retro is on the branch, so step 4 has no grounds to refuse");
            Assert.AreNotEqual("not-started", outcome,
                "the script threw before doing anything: " + error);

            // AC-8: it reaches a terminal outcome naming the remote, rather than a refusal.
            Assert.AreEqual("verified-no-remote", outcome,
                "expected it to run out of road at the remote, not before: "
                + JsonSerializer.Serialize(d));

            // AC-7: the suggestions intake rode in with the branch.
            string intake = Path.Combine(root, ".sdlc", "suggestions", "20260926-b001.md");
            Assert.IsTrue(File.Exists(intake),
                "no suggestions intake was written, so the cycle would land without it");
            StringAssert.Contains(File.ReadAllText(intake), "a suggestion that should reach the intake file",
                StringComparison.Ordinal);
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-6")]
    public void AVerifiedCycleWithNoRetroIsStillRefused()
    {
        // The refusal that must survive the fix. The retro is the completion certificate and it
        // rides in with the squash, so a cycle without one has nothing to land.
        string root = MakeLandableRepo("20260926-b002", withRetro: false, withFindings: false);

        try
        {
            (JsonElement? result, string error) = Land(root, "20260926-b002");
            Assert.IsNotNull(result, "the landing script produced no JSON result: " + error);

            Assert.AreEqual("refused-no-retro", result!.Value.GetProperty("outcome").GetString(),
                "a cycle with no retro must still be refused: " + JsonSerializer.Serialize(result));
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-9")]
    public void ACycleThatIsNotAwaitingLandingIsRefusedBeforeAnythingHappens()
    {
        // Cheap by construction: this throws at the precondition, so no re-verification runs. The
        // cycle here has no verify.md, which makes it `dead` rather than landable.
        string root = Path.Combine(Path.GetTempPath(), "sdlc-land-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "intents"));
        Directory.CreateDirectory(Path.Combine(root, ".sdlc", "cycles", "20260926-b003"));

        try
        {
            File.WriteAllText(Path.Combine(root, ".sdlc", "intents", "20260926-b003.md"),
                "---\nid: 20260926-b003\nsource: authored\ncharter-version: 1\n---\n\n# Intent\n");

            Git(root, "init --initial-branch=master");
            Git(root, "add -A");
            Git(root, "-c user.email=t@example.invalid -c user.name=Test commit -m fixture");
            Git(root, "checkout -b cycle-20260926-b003");

            (JsonElement? result, string error) = Land(root, "20260926-b003");

            Assert.IsNull(result,
                "a cycle that is not awaiting-landing must throw rather than report an outcome");
            StringAssert.Contains(error, "awaiting-landing", StringComparison.Ordinal,
                "the refusal has to name the state it required: " + error);
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-16")]
    public void ACycleWithNoInterrogationDoesNotClaimASuggestionsIntake()
    {
        // FOUND BY RUNNING IT. The first real execution of this script printed
        // "landed as .sdlc/suggestions/ + 20260926-26a1.md + , for triage" for a cycle that had filed
        // no interrogation at all. Two defects in one line: `'a' + "$b" + 'c'` passes three positional
        // arguments to Write-Host, which joins them with spaces, and the line was unconditional.
        //
        // A landing step whose success message misreports what it did is a landing step that
        // misreports, which is the thing this cycle exists to stop.
        string root = MakeLandableRepo("20260926-b004", withRetro: true, withFindings: false);

        try
        {
            string output = LandRaw(root, "20260926-b004");

            Assert.IsFalse(output.Contains("Unresolved suggestions landed", StringComparison.Ordinal),
                "no interrogation was filed, so there is no intake to announce: " + output);

            Assert.IsFalse(output.Contains("suggestions/ +", StringComparison.Ordinal),
                "the path must be a path, not a concatenation expression: " + output);

            Assert.IsFalse(File.Exists(Path.Combine(root, ".sdlc", "suggestions", "20260926-b004.md")),
                "nothing was suggested, so nothing should have been written");
        }
        finally
        {
            Discard(root);
        }
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-17")]
    public void ThePullRequestTitleNamesTheCycleOnce()
    {
        // ALSO FOUND BY RUNNING IT. The first pull request this script ever opened was titled
        // `20260926-26a1: 20260926-26a1: done means landed, not written`, because the script prefixes
        // the cycle id to a commit subject that house convention already prefixes. The doubled form
        // was the normal case, not an edge one.
        //
        // Asserted on the source, because the title is only ever built on the path that calls GitHub
        // and no test can call GitHub. Both directions: the prefix is stripped when present and the
        // prefix is still added.
        string code = File.ReadAllText(ScriptPath);

        StringAssert.Contains(code, "$CycleId`: $title", StringComparison.Ordinal,
            "a subject with no cycle id still needs one, so the prefix must stay");

        StringAssert.Contains(code, "[regex]::Escape($CycleId)", StringComparison.Ordinal,
            "a subject that already names the cycle must not be prefixed twice");
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-12")]
    public void NoTestInTheSuiteIsSkippedOrInconclusive()
    {
        // A SKIPPED TEST IS A CRITERION NOBODY CHECKED, and it reads as coverage: the traceability
        // check sees the annotation and the verification sees a green suite. This is the mechanically
        // checkable half of "every test passes, none skipped" — a test cannot assert its own suite's
        // result, which is what the verification outcome's own tests-passed gate is for.
        //
        // It lives in this file because this cycle introduced it, and it reads the CHECKOUT rather
        // than the build output because the suite's own sources are not copied there. A missing
        // checkout fails rather than skips, which would be this very defect.
        string? root = FindCheckout(AppContext.BaseDirectory);
        Assert.IsNotNull(root, "no git checkout found above the test output, so the claim is unverified");

        string suite = Path.Combine(root!, "tests", "Sdlc.Tests");
        Assert.IsTrue(Directory.Exists(suite), "the suite's sources are not where they were expected");

        // ASSEMBLED RATHER THAN WRITTEN OUT, because a check that greps source for a construct also
        // matches the source of the check. Written literally, this reported its own file — the same
        // defect SourceText's remarks describe, and the third time this shape has appeared in this
        // repository. Concatenation keeps the needles out of the haystack.
        string ignoreAttribute = "[" + "Ignore";
        string inconclusive = "Assert." + "Inconclusive";

        List<string> offenders = [];
        foreach (string file in Directory.EnumerateFiles(suite, "*.cs", SearchOption.TopDirectoryOnly))
        {
            string code = File.ReadAllText(file);
            if (code.Contains(ignoreAttribute, StringComparison.Ordinal)
                || code.Contains(inconclusive, StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "these files skip or abandon a test rather than asserting it: " + string.Join(", ", offenders));
    }

    private static string? FindCheckout(string start)
    {
        string? dir = start;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) && Directory.Exists(Path.Combine(dir, ".sdlc")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    [TestMethod]
    [TestProperty("AC", "AC-20260926-26a1-10")]
    public void ThePreconditionStillRequiresExactlyAwaitingLanding()
    {
        // THE LINE THAT MUST NOT MOVE. Widening this to accept `done` was the cheaper repair and the
        // wrong one: it would leave `done` meaning "a retro was written" and put a second place in
        // the tree that knows better. One predicate, corrected once.
        string code = File.ReadAllText(ScriptPath);

        StringAssert.Contains(code, "'awaiting-landing'", StringComparison.Ordinal,
            "the landing script must still name the one state it accepts");

        Assert.IsFalse(code.Contains("-eq 'done'", StringComparison.Ordinal),
            "the landing script must not learn about `done`; the derivation is what was wrong");
    }
}
