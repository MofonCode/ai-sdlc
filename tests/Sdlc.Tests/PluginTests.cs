using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Constraints;

namespace Sdlc.Tests;

/// <summary>
/// The plugin as it ships: its manifest, its hook configuration, and its skills.
/// </summary>
/// <remarks>
/// The skills are the advisory layer, which is bypassable by the agent constantly -- so what can be
/// asserted is not that they are obeyed but that they do not <em>contradict</em> the layers below. A skill
/// that told an agent to write an approval, or that restated a constraint from memory, would be teaching
/// the opposite of what the gate enforces, and that is the failure worth catching mechanically.
/// </remarks>
[TestClass]
public sealed class PluginTests
{
    private static readonly string Root = RepoPaths.Root;

    /// <summary>
    /// The pipeline still runs the two things that are only checked there.
    /// </summary>
    /// <remarks>
    /// <para>By the same argument as the ban on the framework's response factories: a step in a YAML
    /// file is deletable, and deleting it is silent. The scaffold verification exists because every
    /// other test about the templates asserts strings, so if the step goes, the gap it closes reopens
    /// with nothing failing — and the templates are exactly the area where that already shipped two
    /// defects in one change.</para>
    /// <para>This asserts the commands rather than the step names, because a step can be renamed for
    /// good reasons and what matters is that the work still happens. It deliberately does not assert
    /// the whole file: a rule that pinned the YAML would fail on every ordinary edit and be deleted
    /// for being in the way.</para>
    /// </remarks>
    [TestMethod]
    public void The_pipeline_builds_and_tests_a_generated_solution()
    {
        string pipeline = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));

        StringAssert.Contains(
            pipeline,
            "dotnet run --project tools/Sdlc.ScaffoldHarness",
            "Nothing else generates a solution from the templates, so without this step no build ever "
            + "sees what they produce.");

        StringAssert.Contains(
            pipeline,
            "dotnet build --configuration Release \"${{ runner.temp }}/scaffold/Demo.slnx\"",
            "Generating a solution and not building it proves only that the templates can be written "
            + "to disk, which Scaffold.Files already proves in-process.");

        StringAssert.Contains(
            pipeline,
            "dotnet test --configuration Release \"${{ runner.temp }}/scaffold/Demo.slnx\"",
            "The generated solution's own tests are the only place the architecture rules the skeleton "
            + "ships ever run. Build alone would compile them and never execute one.");

        // Comment lines are dropped before looking, and that is not a convenience. The first version
        // of this assertion searched the whole file and failed on the comment in the YAML explaining
        // why the gate is deliberately absent -- a rule that cannot tell an explanation from an
        // invocation, which is the same mistake as reading source with a regular expression when the
        // question is about what the code does.
        string[] commands = [.. pipeline
            .ReplaceLineEndings()
            .Split(Environment.NewLine)
            .Where(line => !line.TrimStart().StartsWith('#'))];

        Assert.IsFalse(
            commands.Any(line => line.Contains("verify-chain", StringComparison.Ordinal)),
            "This repository is the framework and does not run cycles on itself -- there is no .sdlc/ "
            + "here and adding one is wrong, because a framework judged by the constraints it is "
            + "changing is a deadlock. AGENTS.md states it; this is what holds it.");
    }

    [TestMethod]
    public void The_manifest_is_valid_json_and_names_the_plugin()
    {
        JsonNode manifest = Read(Path.Combine(".claude-plugin", "plugin.json"));

        Assert.AreEqual("sdlc", manifest["name"]!.GetValue<string>());
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(manifest["description"]!.GetValue<string>()),
            "The description is what somebody reads when deciding whether to install this.");
    }

    [TestMethod]
    public void The_marketplace_entry_points_at_this_plugin()
    {
        JsonNode marketplace = Read(Path.Combine(".claude-plugin", "marketplace.json"));

        JsonArray plugins = marketplace["plugins"]!.AsArray();

        Assert.AreEqual(1, plugins.Count);
        Assert.AreEqual("sdlc", plugins[0]!["name"]!.GetValue<string>());
    }

    [TestMethod]
    public void The_hook_runs_the_one_binary_and_not_a_script_per_rule()
    {
        // Iteration 3 ran five PowerShell scripts across two matchers. One binary with one policy is what
        // keeps the local layer and the pipeline check from drifting apart, and it is why the hook config
        // is the same shape on every host.
        JsonNode hooks = Read(Path.Combine("hooks", "hooks.json"));

        JsonArray entries = hooks["hooks"]!["PreToolUse"]!.AsArray();

        Assert.AreEqual(1, entries.Count, "One rule set, not one per check.");

        JsonArray commands = entries[0]!["hooks"]!.AsArray();

        Assert.AreEqual(1, commands.Count);
        StringAssert.Contains(commands[0]!["command"]!.GetValue<string>(), "sdlc hook --host claude");
    }

    [TestMethod]
    public void The_turn_end_is_checked_by_the_same_binary()
    {
        // The turn end is what makes the compiler an enforcement layer rather than one a reviewer
        // consults afterwards: an agent cannot report done over a failing suite. Without this entry
        // every analyzer severity and every architecture test is something discovered later.
        JsonNode hooks = Read(Path.Combine("hooks", "hooks.json"));

        JsonArray entries = hooks["hooks"]!["Stop"]!.AsArray();

        Assert.AreEqual(1, entries.Count, "One rule set, not one per check.");

        JsonObject entry = entries[0]!.AsObject();

        Assert.IsFalse(
            entry.ContainsKey("matcher"),
            "A turn end has no tool to match on, and a matcher here would be a filter on nothing.");

        JsonArray commands = entry["hooks"]!.AsArray();

        Assert.AreEqual(1, commands.Count);
        StringAssert.Contains(
            commands[0]!["command"]!.GetValue<string>(),
            "sdlc hook --host claude --event turn-end");
    }

    [TestMethod]
    public void The_hook_matcher_covers_the_shell_as_well_as_file_writes()
    {
        // A shell can write a file without any tool naming its path, so matching only the edit tools would
        // be a front door with the back door open.
        JsonNode hooks = Read(Path.Combine("hooks", "hooks.json"));

        string matcher = hooks["hooks"]!["PreToolUse"]!.AsArray()[0]!["matcher"]!.GetValue<string>();

        foreach (string tool in (string[])["Edit", "Write", "Bash"])
        {
            StringAssert.Contains(matcher, tool);
        }
    }

    [TestMethod]
    public void Every_skill_has_a_name_and_a_description_in_its_frontmatter()
    {
        foreach (string path in Skills())
        {
            string text = File.ReadAllText(path);
            string name = Path.GetFileName(Path.GetDirectoryName(path))!;

            StringAssert.Contains(text, "name: " + name, path + " does not declare its own directory name.");
            StringAssert.Contains(
                text,
                "description:",
                path + " has no description. Without one a host cannot decide when to offer it.");
        }
    }

    [TestMethod]
    public void No_skill_restates_a_constraint_instead_of_running_the_command()
    {
        // A copy of a rule in a skill is a second place it lives, and the copy is always the one that goes
        // stale. Citing a rule by id is fine -- quoting its text is what this catches.
        foreach (string path in Skills())
        {
            string text = File.ReadAllText(path);

            foreach (string clause in (string[])["**Applies to:**", "**Forbids:**", "**Why:**"])
            {
                Assert.IsFalse(
                    text.Contains(clause, StringComparison.Ordinal),
                    $"{path} reproduces a constraint's clauses. It should run 'sdlc constraints' instead.");
            }
        }
    }

    [TestMethod]
    public void Every_skill_that_writes_an_artifact_runs_the_constraints_command_first()
    {
        // This is how the rules reach a session: they live in the framework and are never copied into a
        // solution, so a skill that did not run this is working from recollection.
        foreach (string name in (string[])["intent", "spec", "plan", "interrogate"])
        {
            string text = File.ReadAllText(Path.Combine(Root, "skills", name, "SKILL.md"));

            StringAssert.Contains(text, "sdlc constraints", name + " never asks what the rules are.");
        }
    }

    [TestMethod]
    public void No_skill_offers_to_write_a_file_only_a_person_writes()
    {
        // The hook denies it, so a skill that offered would be promising something it cannot do -- and the
        // person would read the refusal as a broken tool rather than as the mechanism.
        foreach (string path in Skills())
        {
            string text = File.ReadAllText(path);

            foreach (string phrase in (string[])
            [
                "I will write the approval",
                "write approval.md",
                "create approval.md for you",
                "paste it for you",
            ])
            {
                Assert.IsFalse(
                    text.Contains(phrase, StringComparison.OrdinalIgnoreCase),
                    $"{path} offers to place a file only a person writes.");
            }
        }
    }

    [TestMethod]
    public void The_stage_skills_hand_back_rather_than_chaining_into_the_next_stage()
    {
        // The separate commands are where a person decides to proceed. A skill that chained straight on
        // would preserve every constraint and still remove the moment somebody chooses to continue.
        foreach (string name in (string[])["intent", "spec", "plan"])
        {
            string text = File.ReadAllText(Path.Combine(Root, "skills", name, "SKILL.md"));

            StringAssert.Contains(
                text,
                "stop",
                name + " does not say to stop. Chaining removes the human-in-the-loop point.");
        }
    }

    [TestMethod]
    public void The_interrogate_skill_judges_in_a_fresh_process_and_says_why()
    {
        string text = File.ReadAllText(Path.Combine(Root, "skills", "interrogate", "SKILL.md"));

        StringAssert.Contains(text, "fresh process");
        StringAssert.Contains(
            text,
            "investment bias",
            "The skill has to say what the fresh process buys, or somebody will replace it with a subagent.");

        StringAssert.Contains(
            text,
            "model bias",
            "And what it does not buy, or the round gets reported as independent verification.");
    }

    [TestMethod]
    public void Every_skill_names_a_command_the_binary_actually_has()
    {
        // A skill naming a verb that does not exist sends a person to a usage error, and the usage text is
        // the one place they would not think to look for the answer.
        string usage = File.ReadAllText(Path.Combine(Root, "src", "Sdlc.Cli", "Usage.cs"));

        string[] verbs =
        [
            "sdlc constraints", "sdlc status", "sdlc gate", "sdlc draft approval",
            "sdlc draft abandon", "sdlc draft constraint", "sdlc draft profile",
            "sdlc suggestions", "sdlc verify-chain", "sdlc init", "sdlc new",
        ];

        foreach (string path in Skills())
        {
            string text = File.ReadAllText(path);

            foreach (string verb in verbs.Where(verb => text.Contains(verb, StringComparison.Ordinal)))
            {
                StringAssert.Contains(
                    usage,
                    verb["sdlc ".Length..],
                    $"{path} names '{verb}', which the usage text does not list.");
            }
        }
    }

    [TestMethod]
    public void The_concepts_command_table_and_the_binarys_verbs_agree_in_both_directions()
    {
        // The same check Every_skill_names_a_command_the_binary_actually_has makes, pointed at the
        // document instead of the skills. §15.2 omitted `scaffold` -- which D-35 added -- and
        // `version`, which appeared nowhere in CONCEPT.md at all. F-36.
        //
        // Both directions, because each omission has its own failure. A verb the table does not list
        // is a command nobody discovers from the design document; a row for a verb the binary does not
        // answer sends somebody to a usage error.
        string program = File.ReadAllText(Path.Combine(Root, "src", "Sdlc.Cli", "Program.cs"));
        string concept = File.ReadAllText(Path.Combine(Root, "CONCEPT.md"));

        int start = concept.IndexOf("### 15.2", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "§15.2 has moved or been renamed.");

        int end = concept.IndexOf("\n## ", start, StringComparison.Ordinal);
        string section = end > start ? concept[start..end] : concept[start..];

        // The verbs Program dispatches, read from the one switch over line.Verb rather than from a
        // second list. Bounded to that switch: Draft has a switch of its own over its sub-verbs, and
        // `approval` is not a command.
        int dispatch = program.IndexOf("return line.Verb switch", StringComparison.Ordinal);
        Assert.IsTrue(dispatch >= 0, "The dispatch switch has moved.");

        int closes = program.IndexOf("_ => await Unknown", dispatch, StringComparison.Ordinal);
        Assert.IsTrue(closes > dispatch, "The dispatch switch does not close where expected.");

        List<string> dispatched = [];

        foreach (string line in program[dispatch..closes].ReplaceLineEndings().Split(Environment.NewLine))
        {
            string trimmed = line.Trim();

            if (!trimmed.StartsWith('"') || !trimmed.Contains("=> await", StringComparison.Ordinal))
            {
                continue;
            }

            dispatched.Add(trimmed[1..trimmed.IndexOf('"', 1)]);
        }

        // Answered before the switch, because it needs no workspace.
        dispatched.Add("version");

        Assert.IsTrue(dispatched.Count >= 13, "Only found " + dispatched.Count + " verbs: " + string.Join(", ", dispatched));

        foreach (string verb in dispatched)
        {
            StringAssert.Contains(
                section,
                "`" + verb,
                $"The binary answers '{verb}' and §15.2's table does not list it. A command missing "
                + "from the design document is one nobody discovers by reading it.");
        }

        // And nothing in the table that the binary does not answer.
        foreach (string line in section.ReplaceLineEndings().Split(Environment.NewLine))
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            string cell = line[3..];
            string verb = cell[..cell.IndexOfAny([' ', '`', '\\'])];

            Assert.IsTrue(
                dispatched.Contains(verb, StringComparer.Ordinal),
                $"§15.2's table lists '{verb}' and the binary does not answer it.");
        }
    }

    [TestMethod]
    public void Every_decision_says_what_it_governs_and_the_section_exists()
    {
        // Governs is the appendix's way in. §1-18 carry a trailing (D-nn) marker, which answers "why
        // does this section say that"; Governs answers the reverse, which is the direction a
        // challenged reader travels -- they arrive at an id from a commit message or a code remark.
        //
        // It was part of the agreed format from the day the appendix was designed, and it went missing
        // from all forty-three entries without anything noticing, which is what this test is for. A
        // pointer at a section that does not exist is worse than none, so both halves are checked.
        // D-44.
        string concept = File.ReadAllText(Path.Combine(Root, "CONCEPT.md"));
        string[] lines = concept.ReplaceLineEndings().Split(Environment.NewLine);

        HashSet<string> headings = [];

        foreach (string line in lines)
        {
            Match heading = Regex.Match(line, @"^#{2,4} (\d+(?:\.\d+)*)\.? ");

            if (heading.Success)
            {
                headings.Add("\u00a7" + heading.Groups[1].Value);
            }
        }

        Assert.IsTrue(headings.Count > 40, "Only found " + headings.Count + " numbered sections.");

        List<string> ungoverned = [];
        List<string> dangling = [];
        string? current = null;
        bool said = true;

        foreach (string line in lines)
        {
            Match entry = Regex.Match(line, @"^### (D-\d+)");

            if (entry.Success)
            {
                if (current is not null && !said)
                {
                    ungoverned.Add(current);
                }

                current = entry.Groups[1].Value;
                said = false;
                continue;
            }

            if (current is null || !line.StartsWith("**Governs:**", StringComparison.Ordinal))
            {
                continue;
            }

            said = true;

            foreach (Match section in Regex.Matches(line, @"\u00a7[\d.]+"))
            {
                if (!headings.Contains(section.Value))
                {
                    dangling.Add(current + " -> " + section.Value);
                }
            }
        }

        if (current is not null && !said)
        {
            ungoverned.Add(current);
        }

        Assert.AreEqual(
            0,
            ungoverned.Count,
            "These decisions do not say what they govern:\n  " + string.Join("\n  ", ungoverned)
            + "\n\nAdd a **Governs:** line naming the sections that state the rule, or \"nothing\" with "
            + "the reason - a superseded decision governs nothing and should say so.");

        Assert.AreEqual(
            0,
            dangling.Count,
            "These decisions point at a section that does not exist:\n  " + string.Join("\n  ", dangling));
    }

    [TestMethod]
    public void The_concepts_constraint_table_matches_the_shipped_library()
    {
        // §7.5's table is where a reader copies a citation from, and §7.4 makes a bare id revision 1 --
        // so the table read `CON_10` while the library shipped `CON_10.2` with different wording, which
        // means it named an id that is not on the page. F-30.
        //
        // Ids and statements both, because the wording is the half that drifts quietly: a revision that
        // bumped the suffix and left the old sentence in the table would pass an id-only check.
        string concept = File.ReadAllText(Path.Combine(Root, "CONCEPT.md"));
        Result<IReadOnlyList<Constraint>> library = ConstraintParser.Parse(
            File.ReadAllText(RepoPaths.Library),
            RepoPaths.Library);

        Assert.IsTrue(library.IsSuccess, string.Join("; ", library.Errors.Select(e => e.Message)));

        int start = concept.IndexOf("### 7.5", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "§7.5 has moved or been renamed.");

        int end = concept.IndexOf("\n## ", start, StringComparison.Ordinal);
        string section = end > start ? concept[start..end] : concept[start..];

        foreach (Constraint constraint in library.Value.Where(rule => rule.IsInForce))
        {
            StringAssert.Contains(
                section,
                "`" + constraint.Id + "`",
                $"{constraint.Id} is in force in the library and §7.5's table does not name it. A "
                + "reader copies a citation from that table, so an id that is not on the page is a "
                + "finding nobody can write.");

            // The statement, loosely: the table abbreviates, so this asserts the distinctive words
            // rather than the whole sentence. CON_10's table row said "says why" while the library
            // said "says what has to change", and that is the shape of drift worth catching.
            string[] words = [.. constraint.Statement
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(word => word.Length > 4)];

            int present = words.Count(word =>
                section.Contains(word.Trim('.', ',', '`'), StringComparison.OrdinalIgnoreCase));

            Assert.IsTrue(
                present >= Math.Max(1, words.Length / 2),
                $"{constraint.Id}'s row does not read like its statement.\n  library: {constraint.Statement}\n"
                + "  Matched " + present + " of " + words.Length + " significant words.");
        }
    }

    [TestMethod]
    public void Every_stage_the_panel_can_name_is_a_verb_the_binary_answers()
    {
        // The panel ends with the literal next command, so a stage it names and the binary rejects would
        // make the most-read output in the plugin wrong -- and the person would conclude the install is
        // broken rather than that the stage is a skill.
        string renderer = File.ReadAllText(
            Path.Combine(Root, "src", "Sdlc.Core", "Cycles", "StatusRenderer.cs"));

        foreach (string stage in (string[])["intent", "spec", "plan", "interrogate", "refine", "build"])
        {
            if (!renderer.Contains("sdlc {name}", StringComparison.Ordinal)
                && !renderer.Contains("sdlc " + stage, StringComparison.Ordinal))
            {
                continue;
            }

            Assert.IsTrue(
                Sdlc.Cli.Stages.IsSkill(stage),
                $"The panel can print 'sdlc {stage}' and the binary would call it unknown.");
        }
    }

    [TestMethod]
    public void Every_skill_in_the_plugin_is_one_the_binary_knows_about()
    {
        // Two lists that could disagree: the skills on disk, and what the binary says when somebody types
        // one. The one that goes stale is the binary's, because adding a skill is a file and nothing fails.
        foreach (string path in Skills())
        {
            string name = Path.GetFileName(Path.GetDirectoryName(path))!;

            // The thin wrappers over commands are not stages, and the binary answers them properly.
            if (name is "constraints" or "status" or "suggestions" or "draft" or "init")
            {
                continue;
            }

            Assert.IsTrue(
                Sdlc.Cli.Stages.IsSkill(name),
                $"The plugin ships a '{name}' skill that the binary does not know is a skill.");
        }
    }

    [TestMethod]
    public void The_skills_on_disk_are_exactly_the_ones_the_concept_inventories()
    {
        // Two lists that drift silently: a skill added to the plugin and never written down reads as
        // undocumented, and one written down and never built sends a person to a slash command that does
        // not answer. The inventory is a table in the concept, so this compares against the table.
        string concept = File.ReadAllText(Path.Combine(Root, "CONCEPT.md"));

        int start = concept.IndexOf("## 15. Skills", StringComparison.Ordinal);
        int end = concept.IndexOf("### 15.1", StringComparison.Ordinal);

        Assert.IsTrue(start >= 0 && end > start, "The concept no longer has a skills inventory to check.");

        HashSet<string> inventoried = new(
            System.Text.RegularExpressions.Regex
                .Matches(concept[start..end], @"^\| `(?<name>[a-z-]+)`", System.Text.RegularExpressions.RegexOptions.Multiline)
                .Select(match => match.Groups["name"].Value),
            StringComparer.Ordinal);

        HashSet<string> shipped = new(
            Skills().Select(path => Path.GetFileName(Path.GetDirectoryName(path))!),
            StringComparer.Ordinal);

        Assert.AreEqual(
            0,
            shipped.Except(inventoried, StringComparer.Ordinal).Count(),
            "Shipped and not inventoried: " + string.Join(", ", shipped.Except(inventoried, StringComparer.Ordinal)));

        Assert.AreEqual(
            0,
            inventoried.Except(shipped, StringComparer.Ordinal).Count(),
            "Inventoried and not shipped: " + string.Join(", ", inventoried.Except(shipped, StringComparer.Ordinal)));
    }

    [TestMethod]
    public void The_readme_counts_what_the_repository_actually_ships()
    {
        // A number in a readme is a claim somebody will check, and the first one that is wrong makes the
        // rest not worth reading. These two are countable, so they are asserted rather than trusted; the
        // decision count is deliberately not stated, because it drifts every time one is added.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        int skills = Skills().Count();
        int constraints = System.Text.RegularExpressions.Regex
            .Count(File.ReadAllText(RepoPaths.Library), @"(?m)^## CON_");

        Assert.AreEqual(17, skills, "The readme says seventeen skills.");
        Assert.AreEqual(14, constraints, "The readme says fourteen global constraints.");

        StringAssert.Contains(readme, "seventeen skills");
        StringAssert.Contains(readme, "fourteen global constraints");
    }

    [TestMethod]
    public void The_binary_and_the_plugin_carry_the_same_release_version()
    {
        // Two halves of one install, fetched separately. They disagreed silently until the props set a
        // VersionPrefix: the plugin said 0.9.0 and the package took the SDK default of 1.0.0, so neither
        // number told anybody what they actually had. A skill calling a verb an older binary does not
        // have is what that shape eventually produces, and the version report is the only thing that
        // would have shown it -- so the report has to be true.
        JsonNode manifest = Read(Path.Combine(".claude-plugin", "plugin.json"));

        Assert.AreEqual(
            manifest["version"]!.GetValue<string>(),
            Sdlc.Cli.Release.Version,
            "plugin.json and the packed binary disagree. VersionPrefix in Directory.Build.props is the "
            + "one place the release version is set.");
    }

    [TestMethod]
    public void The_artifact_format_version_is_not_bound_to_the_release()
    {
        // These two are different facts and the temptation is to make them one number. The format version
        // is stamped into every artifact's frontmatter to say which envelope it was written in, so moving
        // it on a release that changed no format would restamp every file as a new format and make a
        // reader distrust files it can parse perfectly well. Asserting they are independent is awkward,
        // so this asserts what the independence is for: the report says which is which.
        StringAssert.Contains(
            Sdlc.Cli.Release.Text,
            "artifact format",
            "The version report has to name the format version, or the two get read as one number.");

        StringAssert.Contains(
            Sdlc.Cli.Release.Text,
            Sdlc.Core.Cycles.Cycle.FrameworkVersion,
            "And report the value actually being stamped into artifacts.");
    }

    [TestMethod]
    public void The_readme_states_the_release_version_it_actually_ships()
    {
        // A version in a readme is the first thing a reader believes and the last thing anybody updates.
        // It is countable against the manifest, so it is asserted rather than trusted -- the same reason
        // the skill and constraint counts are.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));
        string version = Read(Path.Combine(".claude-plugin", "plugin.json"))["version"]!.GetValue<string>();

        StringAssert.Contains(
            readme,
            version,
            "The readme does not state the version in plugin.json. One of the two is stale.");
    }

    [TestMethod]
    public void Every_guide_the_readme_sends_somebody_to_exists()
    {
        // The design's own rule applied to its documentation: a pointer that goes nowhere is the
        // directory that appears enforced and is not. A reader who clicks one dead link stops trusting
        // the rest of the table, which is the whole value of having one.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex
            .Matches(readme, @"`(?<path>docs/[A-Za-z0-9/._-]+\.md)`"))
        {
            string relative = match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar);

            Assert.IsTrue(
                File.Exists(Path.Combine(Root, relative)),
                $"The readme points at {match.Groups["path"].Value}, which does not exist.");
        }
    }

    [TestMethod]
    public void Every_markdown_path_a_document_names_exists()
    {
        // The readme's rule applied to every document in the repository rather than only the guides.
        // They cross-reference each other heavily, and three of the four guides were referenced by name
        // before any of them was written.
        //
        // This was scoped to docs/ and the scope was the hole: src/README.md was written as a guide,
        // moved to src/, and carried four paths that still resolved from the directory it used to be in.
        // Nothing failed, because nothing outside docs/ was being read. A document's links rot wherever
        // it lives, so every .md in the tree is read now -- the two root authorities, AGENTS.md, the
        // readmes beside the code and the reference material included.
        //
        // A path reference is one that carries a directory or a hyphenated name. That is not a
        // convention invented for this test: the guides are named for what they walk through, so they
        // hyphenate, while every file the framework writes is a single word -- intent.md, approval.md,
        // solution.md, techstack.md. Keying on the shape means no list of artifact names to exclude, and
        // no list that goes stale the first time the envelope gains a file.
        string guides = Path.Combine(Root, "docs", "guides");
        Assert.IsTrue(Directory.Exists(guides), "The guides directory is gone.");

        string[] documents = [.. Directory
            .EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
            .Where(path => !Ignored(Path.GetRelativePath(Root, path)))];

        Assert.IsTrue(
            documents.Length > 20,
            "Almost no documents were read, so this is passing by finding nothing. The tree moved or "
            + "the exclusions are swallowing it.");

        foreach (string path in documents)
        {
            string directory = Path.GetDirectoryName(path)!;

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(path), @"`(?<path>[A-Za-z0-9/.-]*[/-][A-Za-z0-9/.-]*\.md)`"))
            {
                string named = match.Groups["path"].Value;

                // A path inside a governed solution is being described, not linked. `.sdlc/README.md` is
                // written by adoption into somebody else's repository, so there is nothing here to
                // resolve it against.
                if (named.StartsWith('.') && !named.StartsWith("..", StringComparison.Ordinal))
                {
                    continue;
                }

                // Resolved from the file that names it, the way a reader following it would.
                string resolved = Path.GetFullPath(
                    Path.Combine(directory, named.Replace('/', Path.DirectorySeparatorChar)));

                Assert.IsTrue(
                    File.Exists(resolved),
                    $"{Path.GetRelativePath(Root, path)} points at '{named}', which resolves to "
                    + $"{Path.GetRelativePath(Root, resolved)} and does not exist.");
            }
        }
    }

    [TestMethod]
    public void The_readme_says_what_is_not_done_rather_than_only_what_is()
    {
        // The design's own rule, pointed at its readme: a thing that appears finished and is not is worse
        // than one that never pretended. Somebody deciding whether to adopt this needs the second list.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        StringAssert.Contains(readme, "Where it stands");
        StringAssert.Contains(
            readme,
            "Copilot gets the hook, not yet the skills",
            "The known gap has to be in the readme, not only in a commit message.");
    }

    [TestMethod]
    public void The_land_skill_carries_the_same_caveat_the_pipeline_does()
    {
        // D-32's own reasoning, applied consistently: the caveat goes where somebody meets the path. The
        // generated pipeline warns and the skill did not, which would have left an agent confidently
        // running an untried command and reading the failure as its own mistake.
        string land = File.ReadAllText(Path.Combine(Root, "skills", "land", "SKILL.md"));

        StringAssert.Contains(land, "az repos pr create");
        StringAssert.Contains(
            land,
            "never been run",
            "The skill names the Azure DevOps command, so it has to say the path is untried.");
    }

    [TestMethod]
    public void Every_path_agents_md_links_to_exists_with_the_case_it_states()
    {
        // The readme's rule pointed at the file a session reads before it knows anything else. A dead
        // link here is worse than one in a guide: the reader is deciding where to look, and it will not
        // report that the map it was handed is wrong -- it will look somewhere else and work from
        // recollection, which is the state this whole repository is written against.
        //
        // The capitalisation is asserted as well as the existence, and not because of the filesystem --
        // .gitattributes records that this repository is Windows-only by design, so a clone resolves
        // 'Concept.md' happily. It is because the web does not: a relative link is resolved
        // case-sensitively when the file is rendered on the host, so a link that works in every editor
        // here is dead for the reader who met this repository through a browser. That reader is the one
        // with the least context to recover from it.
        string agents = File.ReadAllText(Path.Combine(Root, "AGENTS.md"));

        string[] targets = [.. System.Text.RegularExpressions.Regex
            .Matches(agents, @"\]\((?<path>[^)\s#]+)\)")
            .Select(match => match.Groups["path"].Value)
            .Where(path => !path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)];

        Assert.IsTrue(
            targets.Length > 0,
            "AGENTS.md links to nothing. It is the repository's map, so an empty one is a regression "
            + "rather than a matter of taste.");

        foreach (string target in targets)
        {
            string full = Path.Combine(Root, target.Replace('/', Path.DirectorySeparatorChar));

            Assert.IsTrue(
                File.Exists(full) || Directory.Exists(full),
                $"AGENTS.md links to {target}, which does not exist.");

            string name = Path.GetFileName(full);

            Assert.IsTrue(
                Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(full)!, name)
                    .Any(entry => string.Equals(Path.GetFileName(entry), name, StringComparison.Ordinal)),
                $"AGENTS.md links to {target}, which exists under a different capitalisation. That "
                + "resolves in a clone and is a dead link when the file is rendered on the host.");
        }
    }

    [TestMethod]
    public void Agents_md_sends_a_session_to_the_skill_reference_rather_than_restating_it()
    {
        // What AGENTS.md must not do is describe the skills itself. A second description is the one that
        // goes stale, and a session working from the stale copy has no way to tell -- the same reason no
        // skill restates a constraint. So it points at docs/skills.md, and says that the surface and the
        // documents describing it move together, which is the part of the rule a test can actually hold.
        string agents = File.ReadAllText(Path.Combine(Root, "AGENTS.md"));

        StringAssert.Contains(
            agents,
            "docs/skills.md",
            "AGENTS.md has to send a session to the skill reference rather than summarise it.");

        StringAssert.Contains(
            agents,
            "same change",
            "And say that a change to the surface updates the documents describing it, in that change.");
    }

    /// <summary>
    /// Whether a path is build output rather than a document somebody wrote.
    /// </summary>
    /// <remarks>
    /// Build output carries package readmes by the hundred, and none of them is this repository's claim
    /// about itself. Keyed on a path segment rather than a prefix, so a new project's output is excluded
    /// the day it appears instead of the day somebody remembers to add it.
    /// </remarks>
    /// <param name="relative">The path, relative to the repository root.</param>
    /// <returns>True when the file is not a document this repository is responsible for.</returns>
    private static bool Ignored(string relative)
    {
        string[] segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("TestResults", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || segment.StartsWith('.'));
    }

    private static IEnumerable<string> Skills() =>
        Directory.EnumerateFiles(Path.Combine(Root, "skills"), "SKILL.md", SearchOption.AllDirectories);

    private static JsonNode Read(string relative)
    {
        string path = Path.Combine(Root, relative);

        Assert.IsTrue(File.Exists(path), path + " is missing.");

        var node = JsonNode.Parse(
            File.ReadAllText(path),
            documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

        Assert.IsNotNull(node, path + " is not valid JSON.");
        return node;
    }
}
