using System.Text.RegularExpressions;

namespace Sdlc.Tests;

/// <summary>
/// The shipped skills, and the load-bearing rule each one must still state.
/// </summary>
/// <remarks>
/// These checks are weaker than the ones they replace, and that is worth being honest
/// about. When a stage was a workflow, "the outcome is decided in code" was enforced by
/// actual code and a test could assert the branch existed. As a skill, the same rule is
/// prose that a model is asked to follow, so all a test can do is assert the instruction is
/// still there.
/// <para>
/// That is not nothing. Every rule below was learned from a measured failure, and a skill
/// quietly losing one is the regression most likely to happen during an edit and least
/// likely to be noticed — the instruction disappears and everything still "works". What a
/// test cannot do any more is prove the rule was obeyed.
/// </para>
/// <para>
/// Where a rule genuinely must be mechanical rather than instructed, it belongs in a script
/// under <c>tools/</c> and is tested there. That is why the blast-radius tier, the lifecycle
/// derivation and the landing gate are PowerShell and not prose.
/// </para>
/// </remarks>
[TestClass]
public sealed class SkillTests
{
    private static string SkillDir => Path.Combine(AppContext.BaseDirectory, "skills");

    /// <summary>Every skill that must ship, and which is which.</summary>
    private static readonly string[] Expected =
    [
        "intent", "spec", "plan", "repair", "build", "verify", "retro",
        "run", "init", "absorb-retro",
    ];

    public static IEnumerable<object[]> AllSkills() => Expected.Select(s => new object[] { s });

    private static string Read(string skill) =>
        File.ReadAllText(Path.Combine(SkillDir, skill, "SKILL.md"));

    private static string Frontmatter(string skill)
    {
        string text = Read(skill);
        Match m = Regex.Match(text, @"\A---\r?\n(?<body>.*?)\r?\n---", RegexOptions.Singleline);
        Assert.IsTrue(m.Success, $"{skill}: SKILL.md must open with YAML frontmatter");
        return m.Groups["body"].Value;
    }

    /// <summary>
    /// Collapses every run of whitespace to one space, because these files are hard-wrapped
    /// at about ninety columns and a phrase that happens to span a line break would never
    /// match. That is a property of the prose, not of the rule being checked, so matching on
    /// raw text makes the check fail for reasons the author cannot see — which is the noise
    /// that gets a check switched off.
    /// </summary>
    private static string Flat(string text) =>
        Regex.Replace(text, @"\s+", " ");

    private static void MustState(string skill, params string[] phrases)
    {
        string text = Flat(Read(skill));
        foreach (string phrase in phrases)
        {
            Assert.IsTrue(text.Contains(Flat(phrase), StringComparison.OrdinalIgnoreCase),
                $"skills/{skill}/SKILL.md no longer states: \"{phrase}\"");
        }
    }

    [TestMethod]
    public void EverySkillTheLibraryPromisesShips()
    {
        // The library IS the surface. A stage with no skill is a stage nobody can invoke,
        // which is how ten items came to be built with nothing callable.
        foreach (string skill in Expected)
        {
            Assert.IsTrue(File.Exists(Path.Combine(SkillDir, skill, "SKILL.md")),
                $"skills/{skill}/SKILL.md is missing, so /sdlc:{skill} resolves to nothing");
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllSkills))]
    public void FrontmatterNamesTheSkillAndSaysWhenToUseIt(string skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        string fm = Frontmatter(skill);

        Match name = Regex.Match(fm, @"(?m)^name:\s*(?<v>\S+)\s*$");
        Assert.IsTrue(name.Success, $"{skill}: frontmatter needs a name");
        Assert.AreEqual(skill, name.Groups["v"].Value,
            "the name must match the directory, because the directory is what forms /sdlc:<name>");

        // Without a description the model cannot tell when the skill applies, so it is
        // reachable by typing but invisible to delegation.
        Assert.IsTrue(Regex.IsMatch(fm, @"(?m)^description:\s*\S"),
            $"{skill}: frontmatter needs a description");
    }

    [TestMethod]
    public void SideEffectingSkillsAreNotModelInvocable()
    {
        // `run` launches N unattended processes, `init` writes a charter, `absorb-retro`
        // proposes charter amendments. None should start because a model inferred it was a
        // good idea; each is a deliberate act by a person.
        foreach (string skill in new[] { "run", "init", "absorb-retro" })
        {
            Assert.IsTrue(Read(skill).Contains("disable-model-invocation: true", StringComparison.Ordinal),
                $"{skill} has real side effects and must be user-invoked only");
        }
    }

    [TestMethod]
    public void BuildAndVerifyStayModelInvocableBecauseTheBatchCallsThem()
    {
        // The dispatcher hands a -p session a prompt telling it to run build then verify.
        // Marking either user-only would make every unattended batch fail at step one.
        foreach (string skill in new[] { "build", "verify" })
        {
            Assert.IsFalse(Read(skill).Contains("disable-model-invocation: true", StringComparison.Ordinal),
                $"{skill} is invoked by the batch dispatcher and must stay model-invocable");
        }
    }

    // --- the rules each skill carries --------------------------------------

    [TestMethod]
    public void IntentElicitsRatherThanDrafts()
    {
        MustState("intent",
            "elicit",
            "INV-01",              // failure modes, the most-skipped question
            "INV-02",              // two readings
            "caused-by");          // the retro loop's raw material
        Assert.IsTrue(Read("intent").Contains("Do not run the spec yourself", StringComparison.Ordinal),
            "the gate between intent and spec is a person");
    }

    [TestMethod]
    public void SpecMarksEveryCriterionVerifiableOrNot()
    {
        // Without the distinction you either annotate the unannotatable, or quietly pass
        // untested criteria as tested.
        MustState("spec", "AC-", "Verifiable", "unique", "out of scope");
    }

    [TestMethod]
    public void SpecAsksRatherThanPickingAReading()
    {
        // This is the reason spec is a skill and not a workflow. An ambiguity about what
        // the user wanted cannot be resolved by reasoning, and a workflow cannot ask.
        MustState("spec", "ASK THE USER", "Do not pick a reading");
    }

    [TestMethod]
    public void PlanTakesTheTierFromTheGathererAndNeverJudgesIt()
    {
        MustState("plan",
            "Get-SdlcFacts.ps1",
            "Do not compute or adjust the tier yourself",
            "INV-05");
    }

    [TestMethod]
    public void PlanNeverWritesItsOwnApproval()
    {
        // `approved:` is what makes a cycle batchable. A stage that writes its own approval
        // has bypassed the only gate standing between a draft and an unattended build.
        MustState("plan", "approved:", "You do not approve it");
    }

    [TestMethod]
    public void RepairHaltsOnNoProgressAndLooksForWhatItIntroduced()
    {
        // Measured, not assumed: three of five blocking findings in a v01 repair round were
        // introduced by the previous round's repairs; three of four on a second codebase.
        // And the finding rate does not decay, so "until clean" never fires.
        MustState("repair", "INTRODUCED", "halted-no-progress", "Halt on no progress");
    }

    [TestMethod]
    public void RepairRefusesToEditTheCharterOrErasePlainly()
    {
        MustState("repair",
            "Do not touch the charter",     // INV-08: no cycle amends the charter governing it
            "Do not repair by deleting the claim",
            "Do not change a finding's `state`");
    }

    [TestMethod]
    public void BuildRefusesAnUnapprovedPlanAndHaltsOnTierEscalation()
    {
        MustState("build",
            "No `approved:`",
            "halt",
            "Touch the declared files and nothing else",
            "more dangerous than it was approved to be");
    }

    [TestMethod]
    public void VerifyDerivesEveryNumberAndDecidesByRule()
    {
        MustState("verify",
            "do not compute any of these numbers yourself",
            "undispositioned",
            "corrected, never signed off");
    }

    [TestMethod]
    public void RetroRefusesToCertifyACycleThatDidNotLand()
    {
        // A retro's PRESENCE is what makes a cycle read as done. Write one for a dead cycle
        // and a dispatcher stops relaunching it, so the work is silently lost.
        MustState("retro", "refuse", "completion certificate", "landed-at", "charter-version");
    }

    [TestMethod]
    public void RetroRecordsRatherThanConcludes()
    {
        MustState("retro", "Do not compute the signals here");
    }

    [TestMethod]
    public void RunDispatchesOnlyApprovedPlansAndNamesWhatItSkips()
    {
        // A batch that silently drops work reads as having covered everything.
        MustState("run",
            "planned",
            "name each skipped cycle",
            "no concurrency cap",
            "worktree count");
    }

    [TestMethod]
    public void RunDoesNotLand()
    {
        MustState("run", "Landing is not part of this", "semantic");
    }

    [TestMethod]
    public void InitRefusesToOverwriteACharterAndNeverRenumbers()
    {
        MustState("init",
            "INV-08",
            "Never renumber",
            "gap");
    }

    [TestMethod]
    public void AbsorbRetroRefusesToOverwriteAndOnlyProposes()
    {
        // The refusal is what earns INV-03's exemption for an immutable copy. Without it the
        // corpus is just a second list and the exemption does not hold.
        MustState("absorb-retro",
            "never overwrite",
            "Never apply",
            "one at a time",
            "must never be merged");
    }

    [TestMethod]
    public void NoSkillCollidesWithAWorkflowName()
    {
        // Both contribute /sdlc:<name>. Two components claiming one name means one shadows
        // the other and which one wins is undefined.
        string workflowDir = Path.Combine(AppContext.BaseDirectory, "workflows");
        HashSet<string> workflowNames = new(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(workflowDir, "*.js"))
        {
            Match m = Regex.Match(File.ReadAllText(file), @"\bname:\s*'(?<n>[^']+)'");
            if (m.Success)
            {
                workflowNames.Add(m.Groups["n"].Value);
            }
        }

        foreach (string skill in Expected)
        {
            Assert.IsFalse(workflowNames.Contains(skill),
                $"/sdlc:{skill} is claimed by both a skill and a workflow");
        }
    }
}
