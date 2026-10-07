using System.Globalization;
using FluentResults;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Proposals;
using Sdlc.Core.Reports;

namespace Sdlc.Tests;

/// <summary>
/// What a solution's whole history says about the rules it is missing.
/// </summary>
/// <remarks>
/// Two failures to guard against, and they are opposite. A report that proposes rules from thin evidence
/// grows a library nobody reads, which <c>CON_11</c> exists to refuse. A report that loses the evidence
/// it does have is the cross-cycle reading iteration 3 removed, returning as decoration. So most of these
/// are about the bar and about what counts as evidence — and the rest are about the one irreversible
/// thing in reach, which is a superseded rule or an id gap being printed away.
/// </remarks>
[TestClass]
public sealed class ProposeConstraintsTests
{
    [TestMethod]
    public async Task A_rule_cited_in_three_cycles_is_a_pattern()
    {
        using var workspace = CycleWorkspace.Create();
        Cite(workspace, "20260901_0900_01", "CON_06");
        Cite(workspace, "20260902_0900_02", "CON_06");
        Cite(workspace, "20260903_0900_03", "CON_06");

        ProposeConstraints.Response report = await Read(workspace);

        Recurrence only = report.Recurrences.Single();
        Assert.AreEqual("CON_06", only.Cited.ToString());
        Assert.AreEqual(3, only.Cycles.Count);
    }

    [TestMethod]
    public async Task A_rule_cited_in_two_cycles_is_coincidence_and_is_not_a_candidate()
    {
        // Two is coincidence, three is a pattern. Named in the report so it is honest about what it
        // looked at, and withheld as a candidate so the bar means something.
        using var workspace = CycleWorkspace.Create();
        Cite(workspace, "20260901_0900_01", "CON_06");
        Cite(workspace, "20260902_0900_02", "CON_06");

        ProposeConstraints.Response report = await Read(workspace);

        Assert.AreEqual(0, report.Recurrences.Count);
        Assert.AreEqual(1, report.BelowBar.Count);
    }

    [TestMethod]
    public async Task Three_findings_against_one_rule_in_one_cycle_are_not_three_cycles()
    {
        // One artifact going round the loop, which is the loop working. Counting findings rather than
        // cycles would read a stubborn spec as a solution-wide pattern.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteBlockingRound(ArtifactKind.Spec, round: 1, id: "F_01", cites: "CON_03");
        workspace.WriteBlockingRound(ArtifactKind.Plan, round: 1, id: "F_02", cites: "CON_03");
        workspace.WriteBlockingRound(ArtifactKind.Intent, round: 1, id: "F_03", cites: "CON_03");

        ProposeConstraints.Response report = await Read(workspace);

        Assert.AreEqual(0, report.Recurrences.Count);
        Assert.AreEqual(0, report.BelowBar.Count, "One cycle is not two, so it is not even a coincidence.");
    }

    [TestMethod]
    public async Task A_blocker_answered_in_a_later_round_is_still_evidence()
    {
        // The whole reason this is not a mode of 'sdlc suggestions'. That command folds a history down to
        // what is still open, and a blocker raised and answered in three separate cycles is the strongest
        // evidence here -- answering it is what makes it a problem the solution had rather than a task.
        using var workspace = CycleWorkspace.Create();

        foreach (string id in new[] { "20260901_0900_01", "20260902_0900_02", "20260903_0900_03" })
        {
            Cycle cycle = Elsewhere(workspace, id);
            Round(cycle, 1, Blocker("F_01", "CON_06", "open"));
            Round(cycle, 2, Blocker("F_01", "CON_06", "answered"));
        }

        ProposeConstraints.Response report = await Read(workspace);

        Assert.AreEqual(3, report.Recurrences.Single().Cycles.Count);
    }

    [TestMethod]
    public async Task A_finding_that_rested_on_no_rule_is_handed_over_with_its_statement()
    {
        // Where a missing rule actually shows. A blocker cites exactly one constraint, so one of these is
        // always a suggestion: somebody saw something real that nothing forbade.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_07", cites: null);

        ProposeConstraints.Response report = await Read(workspace);

        Uncited only = report.Uncited.Single();
        Assert.AreEqual("F_07", only.Id);
        Assert.AreEqual(ArtifactKind.Spec, only.Artifact);
        Assert.AreNotEqual(0, only.Statement.Length, "The statement is the material the skill reads.");
    }

    [TestMethod]
    public async Task A_withdrawn_finding_is_not_evidence_of_anything()
    {
        // A later reader retracted the objection, so there was nothing wrong. Counting it would propose a
        // rule against a problem the record says did not exist.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteSuggestionRound(ArtifactKind.Spec, round: 1, id: "F_07", cites: null);
        workspace.WriteSuggestionRound(
            ArtifactKind.Spec,
            round: 2,
            id: "F_07",
            cites: null,
            disposition: "withdrawn");

        Assert.AreEqual(0, (await Read(workspace)).Uncited.Count);
    }

    [TestMethod]
    public async Task A_stopped_cycle_contributes_the_reason_somebody_wrote()
    {
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(
            workspace.Cycle.AbandonedPath,
            """
            ---
            cycle: 20260929_1456_32
            by: Someone
            at: 2026-09-29T15:00:00Z
            ---

            # Why this stopped

            The criteria could not be written without a decision nobody has taken.
            """);

        Stopped only = (await Read(workspace)).Stopped.Single();

        Assert.IsNull(only.Unreadable);
        StringAssert.Contains(only.Reason, "could not be written");
    }

    [TestMethod]
    public async Task A_stopped_cycle_whose_record_will_not_parse_is_named_rather_than_fatal()
    {
        // Prose a person wrote under no schema's eye. Refusing to say anything about a solution's history
        // because one such file lacks a timestamp would lose the other nine reasons to protect a field
        // nothing here reads.
        using var workspace = CycleWorkspace.Create();
        CycleWorkspace.WriteRaw(workspace.Cycle.AbandonedPath, "no frontmatter, no reason, nothing.");

        Stopped only = (await Read(workspace)).Stopped.Single();

        Assert.IsNull(only.Reason);
        Assert.IsNotNull(only.Unreadable, "A record that cannot be read is named so somebody can look.");
    }

    [TestMethod]
    public async Task The_next_id_is_one_past_the_highest_ever_used_and_never_fills_a_gap()
    {
        // A gap is the record of a constraint that was removed. Filling it would make an old finding's
        // citation read as the new rule, which is the one mistake the monotonic rule exists to prevent.
        using var workspace = CycleWorkspace.Create();
        Rules(workspace, Rule("LOC_01"), Rule("LOC_04"));

        Assert.AreEqual("LOC_05", (await Read(workspace)).NextId.ToString());
    }

    [TestMethod]
    public async Task A_superseded_rule_is_reported_out_of_force_and_never_offered_for_deletion()
    {
        // The one irreversible thing in reach. A superseded rule stays on the page so a finding that cited
        // it remains readable, and whatever this prints is what gets pasted -- so a report offering an
        // entry to cut would get the entry cut, which is the regression nothing else would notice.
        using var workspace = CycleWorkspace.Create();
        Rules(workspace, Rule("LOC_01", supersededBy: "LOC_02"), Rule("LOC_02"));

        ProposeConstraints.Response report = await Read(workspace);

        Assert.AreEqual(1, report.InForce);
        Assert.AreEqual("LOC_01", report.Superseded.Single().ToString());

        string rendered = ProposalsRenderer.Render(report);

        foreach (string word in new[] { "delete", "remove", "cut ", "drop " })
        {
            Assert.IsFalse(
                rendered.Contains(word, StringComparison.OrdinalIgnoreCase),
                $"The report says '{word}', and whatever it prints is what somebody pastes.");
        }
    }

    [TestMethod]
    public async Task A_candidate_a_person_already_refused_is_read_back_and_echoed()
    {
        // Without somewhere for a refusal to live the same candidates return on every run forever, which
        // teaches people to skim the report. Echoed rather than silently applied: a suppressed candidate
        // would make the report's silence ambiguous.
        using var workspace = CycleWorkspace.Create();
        Rules(
            workspace,
            Rule("LOC_01"),
            string.Join(
                Environment.NewLine,
                string.Empty,
                "## " + ProposeConstraints.RejectedHeading,
                string.Empty,
                "- One handler per file: an analyzer decides it, so it fails test two.",
                string.Empty));

        ProposeConstraints.Response report = await Read(workspace);

        StringAssert.Contains(report.Rejected.Single(), "One handler per file");
        StringAssert.Contains(ProposalsRenderer.Render(report), "One handler per file");
    }

    [TestMethod]
    public async Task A_rejected_section_does_not_stop_the_rules_being_read()
    {
        // The parser recognises only '## LOC_nn -- statement' headings, so a prose section beside them
        // costs nothing. If that stopped being true, the refusal list would break id allocation.
        using var workspace = CycleWorkspace.Create();
        Rules(
            workspace,
            Rule("LOC_01"),
            string.Join(
                Environment.NewLine,
                string.Empty,
                "## " + ProposeConstraints.RejectedHeading,
                string.Empty,
                "- Something refused.",
                string.Empty));

        Assert.AreEqual("LOC_02", (await Read(workspace)).NextId.ToString());
    }

    [TestMethod]
    public async Task An_empty_history_has_no_material_so_the_form_is_what_prints()
    {
        // The ordinary state of a young solution, of a well-behaved one, and of a person adding a rule by
        // hand before any cycle exists. It is a mode rather than a failure.
        using var workspace = CycleWorkspace.Create();

        ProposeConstraints.Response report = await Read(workspace);

        Assert.IsFalse(report.HasMaterial);
        StringAssert.Contains(ProposalsRenderer.Render(report), "no pattern");
    }

    [TestMethod]
    public async Task A_coincidence_on_its_own_is_not_material()
    {
        // Two cycles is not evidence, so the command falls to its empty-handed mode rather than printing
        // a candidate from a coincidence.
        using var workspace = CycleWorkspace.Create();
        Cite(workspace, "20260901_0900_01", "CON_06");
        Cite(workspace, "20260902_0900_02", "CON_06");

        Assert.IsFalse((await Read(workspace)).HasMaterial);
    }

    [TestMethod]
    public async Task The_admission_tests_are_printed_whenever_there_is_evidence()
    {
        // The quality bar. A report listing evidence without them would be at its most persuasive exactly
        // where it is least authoritative.
        using var workspace = CycleWorkspace.Create();
        Cite(workspace, "20260901_0900_01", "CON_06");
        Cite(workspace, "20260902_0900_02", "CON_06");
        Cite(workspace, "20260903_0900_03", "CON_06");

        string rendered = ProposalsRenderer.Render(await Read(workspace));

        StringAssert.Contains(rendered, "no analyzer, schema or hook could decide it");
        StringAssert.Contains(rendered, "incompatible with at least one plausible implementation");
    }

    [TestMethod]
    public async Task The_next_free_id_is_stated_once_and_no_candidate_wears_it()
    {
        // Candidates are labelled by the skill, never numbered by this. Pre-assigning ids to three
        // proposals and having a person take only the middle one would leave a gap, and a gap is the
        // record of a rule that was removed.
        using var workspace = CycleWorkspace.Create();
        Cite(workspace, "20260901_0900_01", "CON_06");
        Cite(workspace, "20260902_0900_02", "CON_06");
        Cite(workspace, "20260903_0900_03", "CON_06");

        string rendered = ProposalsRenderer.Render(await Read(workspace));

        Assert.AreEqual(
            1,
            rendered.Split("LOC_01").Length - 1,
            "The next free id appears once, in the header, and never against a candidate.");
    }

    [TestMethod]
    public async Task A_report_is_kept_beside_the_solution_and_ignores_itself()
    {
        // A report is not a record: nothing reads one back. Tracking them would be worse than untidy --
        // an untracked one reaching a build's diff is a file no plan declared, which is CON_07.2.
        using var workspace = CycleWorkspace.Create();

        string path = await ReportFile.Write(
            workspace.Root,
            "constraints",
            "  the body, as it was printed." + Environment.NewLine,
            new DateTimeOffset(2026, 10, 7, 9, 15, 30, TimeSpan.Zero),
            CancellationToken.None);

        Assert.AreEqual("20261007_0915_30-constraints.md", Path.GetFileName(path));

        string kept = await File.ReadAllTextAsync(path, CancellationToken.None);
        StringAssert.Contains(kept, "A report, not a record.");
        StringAssert.Contains(kept, "the body, as it was printed.");

        string ignore = Path.Combine(Path.GetDirectoryName(path)!, ".gitignore");
        Assert.IsTrue(File.Exists(ignore), "The directory carries its own ignore rather than merging one.");
        StringAssert.Contains(await File.ReadAllTextAsync(ignore, CancellationToken.None), "!.gitignore");
    }

    /// <summary>Dispatches the request the way the application composes it.</summary>
    private static async Task<ProposeConstraints.Response> Read(CycleWorkspace workspace)
    {
        using var gate = Gate.For(workspace.Root);

        Result<ProposeConstraints.Response> result = await gate
            .Send<ProposeConstraints.Query, ProposeConstraints.Response>(new ProposeConstraints.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    /// <summary>Writes this solution's own rule file, with whatever blocks are given.</summary>
    private static void Rules(CycleWorkspace workspace, params string[] blocks) =>
        CycleWorkspace.WriteRaw(
            Path.Combine(
                workspace.Root,
                Cycle.SdlcDirectoryName,
                FileConstraintDocuments.SolutionConstraintsFileName),
            "# This solution's own constraints"
                + Environment.NewLine
                + string.Join(Environment.NewLine, blocks));

    /// <summary>One well-formed rule, in force unless it names a replacement.</summary>
    private static string Rule(string id, string? supersededBy = null)
    {
        List<string> lines =
        [
            string.Empty,
            string.Create(CultureInfo.InvariantCulture, $"## {id} — a rule this solution holds itself to"),
            string.Empty,
            "**Applies to:** a plan, at the moment it is written",
            string.Empty,
            "**Forbids:** reaching for something the solution has decided against",
            string.Empty,
            "**Why:** the decision cost an argument once and should not cost it again",
            string.Empty,
        ];

        if (supersededBy is not null)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"**Superseded-by:** {supersededBy}"));
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>A second cycle in the same solution, citing one rule as a blocker.</summary>
    private static void Cite(CycleWorkspace workspace, string id, string cites) =>
        Round(Elsewhere(workspace, id), 1, Blocker("F_01", cites, "open"));

    /// <summary>Another cycle's directory in the workspace's solution.</summary>
    private static Cycle Elsewhere(CycleWorkspace workspace, string id)
    {
        Assert.IsTrue(CycleId.TryParse(id, out CycleId? parsed), $"'{id}' is not a cycle id.");

        Cycle cycle = new(parsed.Value, workspace.Root);
        Directory.CreateDirectory(cycle.Directory);
        return cycle;
    }

    /// <summary>Writes one round of plan findings into a cycle.</summary>
    private static void Round(Cycle cycle, int round, string finding) => CycleWorkspace.WriteRaw(
        cycle.FindingsPath(ArtifactKind.Plan, round),
        string.Create(
            CultureInfo.InvariantCulture,
            $$"""
              {
                "cycle": "{{cycle.Id}}",
                "artifact": "plan.md",
                "round": {{round}},
                "findings": [{{finding}}]
              }
              """));

    private static string Blocker(string id, string cites, string disposition) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""
          {
            "id": "{{id}}",
            "severity": "blocking",
            "disposition": "{{disposition}}",
            "cites": { "id": "{{cites}}", "heading": "a plan's proof is adequate to the criterion it claims to prove" },
            "statement": "the proof does not reach the criterion.",
            "locus": "the proof section",
            "remedy": "name a check that could fail."
          }
          """);
}
