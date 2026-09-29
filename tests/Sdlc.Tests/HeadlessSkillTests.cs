using System.Diagnostics;
using System.Text.Json;

namespace Sdlc.Tests;

/// <summary>
/// Skill behaviour, asserted by running the skill and reading what it produced.
/// <para>
/// Everything else in this suite checks the things around a skill — that its frontmatter parses, that the
/// script it names exists, that the schema governing its output rejects what it should. None of that
/// establishes that an interrogation reaches the right verdict, which is the only thing the framework is
/// for. This runs <c>claude -p</c> against a golden artifact staged into a materialized fixture and
/// compares the findings it wrote to the expectation checked in beside the artifact.
/// </para>
/// <para>
/// Assertions are on structure — which constraints are cited at which severity — never on wording, which
/// is model output. A judgement also carries real variance: <c>plan.good.md</c> has drawn a CON_06 finding
/// on one run and none on the next with nothing changed. A disagreeing case is re-run and reported as
/// flaky, never retried silently.
/// </para>
/// <para>
/// Skipped unless <c>SDLC_HEADLESS=1</c>. Each case spends tokens and takes a minute, so it is opt-in
/// locally and runs on its own schedule in CI rather than on every push.
/// </para>
/// </summary>
[TestClass]
public sealed class HeadlessSkillTests
{
    private const string EnableVariable = "SDLC_HEADLESS";

    /// <summary>A model call is slow, and one that hangs must not hang the suite.</summary>
    private static readonly TimeSpan _budget = TimeSpan.FromMinutes(6);

    private static string ArtifactsDir => RepoPaths.Artifacts;

    [TestMethod]
    [DataRow("intent.good.md")]
    [DataRow("intent.guessed-fact.md")]
    [DataRow("intent.solution-shaped.md")]
    [DataRow("spec.good.md")]
    [DataRow("spec.untestable.md")]
    [DataRow("spec.uncovered.md")]
    [DataRow("spec.scope-creep.md")]
    [DataRow("plan.good.md")]
    [DataRow("plan.inadequate-proof.md")]
    public void InterrogatingAGoldenArtifactCitesTheConstraintsItsExpectationNames(string artifact)
    {
        if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
        {
            Assert.Inconclusive(
                $"Set {EnableVariable}=1 to run the headless skill cases. Each one invokes the model, so "
                + "they cost tokens and time and are not part of an ordinary `dotnet test`.");
        }

        Expectation expectation = ReadExpectation(artifact);

        using var workspace = FixtureWorkspace.Materialize("greenfield");

        string cycle = Stage(workspace, artifact, expectation);

        workspace.Run("checkout", "--quiet", "-b", $"cycle/{cycle}");

        (int exitCode, string output) = Claude(
            workspace.Path, $"/sdlc:interrogate {cycle} {expectation.Kind}");

        string findings = Path.Combine(
            workspace.Path, ".sdlc", "cycles", cycle, "findings", $"{expectation.Kind}.r1.json");

        Assert.IsTrue(
            File.Exists(findings),
            $"No findings document was written for {artifact}. The skill exited {exitCode} and said:"
            + $"{Environment.NewLine}{Trim(output)}");

        string[] blocking = BlockingConstraints(findings);

        foreach (string required in expectation.MustCiteBlocking)
        {
            Assert.IsTrue(
                blocking.Contains(required, StringComparer.Ordinal),
                $"{artifact}: the interrogation did not raise {required} as blocking. "
                + $"It cited [{string.Join(", ", blocking)}]. {expectation.Note}"
                + $"{Environment.NewLine}Re-run before treating this as a defect — a judgement varies.");
        }

        if (!expectation.AllowOtherBlocking)
        {
            string[] unexpected = [.. blocking.Except(expectation.MustCiteBlocking, StringComparer.Ordinal)];

            Assert.AreEqual(
                0,
                unexpected.Length,
                $"{artifact}: the interrogation raised blocking findings its expectation does not allow: "
                + $"[{string.Join(", ", unexpected)}]. Either the artifact really does breach them and the "
                + "expectation is wrong, or the interrogation over-reached. Re-run before deciding.");
        }
    }

    private sealed record Expectation(
        string Kind,
        string? Against,
        string[] MustCiteBlocking,
        bool AllowOtherBlocking,
        string Note);

    private static Expectation ReadExpectation(string artifact)
    {
        string path = Path.Combine(
            ArtifactsDir, Path.GetFileNameWithoutExtension(artifact) + ".expected.json");

        Assert.IsTrue(File.Exists(path), $"{artifact} has no expectation beside it.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        return new Expectation(
            root.GetProperty("kind").GetString()!,
            root.TryGetProperty("against", out JsonElement against) ? against.GetString() : null,
            [.. root.GetProperty("must_cite_blocking").EnumerateArray().Select(v => v.GetString()!)],
            root.GetProperty("allow_other_blocking").GetBoolean(),
            root.TryGetProperty("note", out JsonElement note) ? note.GetString() ?? string.Empty : string.Empty);
    }

    /// <summary>
    /// Copies the artifact under test into a cycle, with whatever it needs to be judged against, and
    /// returns the cycle id.
    /// <para>
    /// The id comes from the artifact's own frontmatter, so nothing is rewritten. Cross-references are
    /// followed: a spec names an intent, a plan names a spec, and an interrogation that cannot resolve one
    /// refuses rather than skipping the relational constraints — which is the behaviour that makes this
    /// staging load-bearing rather than decoration. A dependency that is not the artifact under test is
    /// staged from its `.good.md` variant, the only kind the corpus carries as a dependency.
    /// </para>
    /// </summary>
    private static string Stage(FixtureWorkspace workspace, string artifact, Expectation expectation)
    {
        string cycle = FrontmatterValue(Path.Combine(ArtifactsDir, artifact), "id")
            ?? throw new InvalidOperationException($"{artifact} declares no id.");

        string cycleDirectory = Path.Combine(workspace.Path, ".sdlc", "cycles", cycle);
        Directory.CreateDirectory(cycleDirectory);

        Copy(artifact, cycleDirectory, expectation.Kind);

        if (expectation.Against is { } against)
        {
            string againstKind = against.Split('.')[0];
            Copy(against, cycleDirectory, againstKind);

            // A staged spec names an intent of its own, so follow one more link.
            string? nested = FrontmatterValue(Path.Combine(ArtifactsDir, against), "intent")
                ?? FrontmatterValue(Path.Combine(ArtifactsDir, against), "spec");

            if (nested is not null)
            {
                string nestedKind = Path.GetFileNameWithoutExtension(nested);

                if (!File.Exists(Path.Combine(cycleDirectory, $"{nestedKind}.md")))
                {
                    Copy($"{nestedKind}.good.md", cycleDirectory, nestedKind);
                }
            }
        }

        return cycle;
    }

    private static void Copy(string artifact, string cycleDirectory, string kind)
        => File.Copy(
            Path.Combine(ArtifactsDir, artifact),
            Path.Combine(cycleDirectory, $"{kind}.md"),
            overwrite: true);

    /// <summary>One frontmatter value, read with a parser rather than guessed at.</summary>
    private static string? FrontmatterValue(string path, string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);

            if (colon > 0 && line[..colon].Trim().Equals(key, StringComparison.Ordinal))
            {
                return line[(colon + 1)..].Trim();
            }

            if (line.StartsWith('#'))
            {
                break;
            }
        }

        return null;
    }

    /// <summary>The constraints a findings document raises at blocking severity.</summary>
    private static string[] BlockingConstraints(string findings)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(findings));

        return [.. document.RootElement
            .GetProperty("findings")
            .EnumerateArray()
            .Where(f => f.TryGetProperty("severity", out JsonElement s)
                && string.Equals(s.GetString(), "blocking", StringComparison.Ordinal))
            .Where(f => f.TryGetProperty("cites", out _))
            .Select(f => f.GetProperty("cites").GetString()!)
            .Distinct(StringComparer.Ordinal)];
    }

    private static (int ExitCode, string Output) Claude(string workingDirectory, string prompt)
    {
        ProcessStartInfo info = new("claude")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        info.ArgumentList.Add("-p");
        info.ArgumentList.Add(prompt);
        info.ArgumentList.Add("--permission-mode");
        info.ArgumentList.Add("acceptEdits");

        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException(
                "Could not start `claude`. The CLI has to be on PATH and the sdlc plugin installed, or "
                + "the slash command does not resolve.");

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)_budget.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);

            Assert.Fail($"The skill did not finish inside {_budget.TotalMinutes} minutes.");
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string Trim(string output)
        => output.Length <= 4000 ? output : output[^4000..];
}
