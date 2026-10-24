using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Constraints;
using Sdlc.Core.Hooks;

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
    public void The_plugins_hook_config_agrees_with_what_the_host_table_says_about_claude_code()
    {
        // Two files have to say the same thing and only one is generated: this one ships inside the
        // plugin and cannot be written at install time, while `.claude/settings.json` is assembled
        // from the host's row by adoption. So they can disagree, and nothing said so -- which is the
        // defect class D-46 exists to close, sitting one level up from the code it closed it in.
        //
        // Not byte-identical, deliberately. This file carries a `statusMessage` per hook, which is
        // what a person sees while the gate is being asked and has no equivalent in a settings file.
        // What has to agree is the part that decides anything: the event keys, the command, and the
        // timeout.
        SupportedHost claude = SupportedHosts.Of(HookHost.Claude);
        JsonObject shipped = Read(Path.Combine("hooks", "hooks.json"))["hooks"]!.AsObject();

        string[] expected = claude.TurnEndEvent is null
            ? [claude.PreToolEvent]
            : [claude.PreToolEvent, claude.TurnEndEvent];

        CollectionAssert.AreEquivalent(
            expected,
            shipped.Select(entry => entry.Key).ToArray(),
            "The plugin configures different moments than the host table says Claude Code has.");

        foreach (string moment in expected)
        {
            JsonObject hook = shipped[moment]!.AsArray()[0]!["hooks"]!.AsArray()[0]!.AsObject();

            StringAssert.Contains(
                hook["command"]!.GetValue<string>(),
                "sdlc hook --host " + claude.Spelling,
                $"The plugin's '{moment}' hook names a spelling the table does not.");

            Assert.AreEqual(
                SupportedHosts.TimeoutSeconds,
                hook[claude.TimeoutKey]!.GetValue<int>(),
                $"The plugin's '{moment}' hook and the generated one disagree about how long the "
                + "gate may take, so the same hook behaves differently depending on how it arrived.");
        }
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
    public void The_root_manifest_conforms_to_the_agent_plugins_schema()
    {
        // The portable half of the package. One manifest at the root makes the skills
        // discoverable by every client that implements Agent Plugins 1.0, out of the same `skills/`
        // directory Claude Code already reads -- so there is no second copy to go stale. The schema
        // is closed and `additionalProperties` is false, which means a field nobody checked is a
        // plugin the client rejects outright rather than loads with a warning. D-47.
        JsonObject manifest = Read("plugin.json").AsObject();

        Assert.AreEqual(
            "https://agent-plugins.org/schemas/1.0.0/plugin.schema.json",
            manifest["$schema"]!.GetValue<string>(),
            "The schema identifier is canonical and pinned to a version. A client matches on it.");

        string name = manifest["name"]!.GetValue<string>();

        Assert.IsTrue(
            Regex.IsMatch(name, "^(?!.*(?:--|\\.\\.))[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$")
            && name.Length is > 0 and <= 64,
            $"'{name}' does not satisfy the specification's name pattern.");

        // The closed set, from the published schema. Anything else is fatal to the whole plugin, so
        // a field added here in good faith is worth catching in the suite rather than on somebody's
        // machine.
        string[] permitted =
        [
            "$schema", "name", "version", "description", "author",
            "homepage", "repository", "license", "keywords", "extensions",
        ];

        string[] unknown = [.. manifest.Select(field => field.Key).Except(permitted, StringComparer.Ordinal)];

        Assert.AreEqual(
            0,
            unknown.Length,
            "plugin.json carries fields the Agent Plugins schema does not define: "
            + string.Join(", ", unknown));
    }

    [TestMethod]
    public void The_two_manifests_describe_the_same_plugin()
    {
        // Two manifests for two plugin systems, one package. Claude Code reads
        // `.claude-plugin/plugin.json` and every Agent Plugins client reads the root one, and they
        // can disagree -- which is the whole lesson of the host table one commit earlier. The
        // version has a third home in Directory.Build.props and a test of its own above; this holds
        // the pair that a person reads in a client's plugin listing.
        JsonNode portable = Read("plugin.json");
        JsonNode claude = Read(Path.Combine(".claude-plugin", "plugin.json"));

        foreach (string field in (string[])["name", "version", "description"])
        {
            Assert.AreEqual(
                claude[field]!.GetValue<string>(),
                portable[field]!.GetValue<string>(),
                $"The two manifests disagree about '{field}', so the same package describes itself "
                + "differently depending on which client installed it.");
        }
    }

    [TestMethod]
    public void Every_skill_conforms_to_the_agent_skills_specification()
    {
        // The skills were written for one host and happen to satisfy the open specification, which
        // is worth holding rather than rediscovering: these constraints are what decides whether a
        // non-Claude client loads a skill, skips it, or warns. `description` is the field that
        // matters most off Claude Code -- there is no `/sdlc:spec` on Copilot, so the
        // description is the whole of how a skill gets chosen.
        foreach (string path in Skills())
        {
            string directory = Path.GetFileName(Path.GetDirectoryName(path))!;
            string[] frontmatter = Frontmatter(path);

            string name = Value(frontmatter, "name", path);

            Assert.AreEqual(
                directory,
                name,
                $"{path}: the specification requires the name to match the parent directory.");

            Assert.IsTrue(
                Regex.IsMatch(name, "^(?!.*--)[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$") && name.Length <= 64,
                $"{path}: '{name}' is not 1-64 lowercase alphanumeric characters and single hyphens.");

            string description = Value(frontmatter, "description", path);

            Assert.IsTrue(
                description.Length is > 0 and <= 1024,
                $"{path}: a description must be 1-1024 characters, and this one is {description.Length}.");

            // Optional in the specification, required here: every one of these skills shells out to
            // a binary the package does not carry. On Claude Code the install guide says so; a
            // client that discovered the skill through Agent Plugins has nothing else that would.
            string compatibility = Value(frontmatter, "compatibility", path);

            Assert.IsTrue(
                compatibility.Length <= 500,
                $"{path}: compatibility must be at most 500 characters.");

            StringAssert.Contains(
                compatibility,
                "sdlc binary on PATH",
                $"{path}: the one thing a reader needs before the first command is the binary.");
        }
    }

    [TestMethod]
    public void No_skill_directory_hides_another_skill_inside_it()
    {
        // The specification is explicit that a client reads each immediate child of `skills/` and
        // MUST NOT recurse deeper. A skill nested below one would load on Claude Code and be
        // invisible everywhere else, which is the quiet half of a divergence rather than a failure.
        foreach (string path in Skills())
        {
            string relative = Path.GetRelativePath(Path.Combine(Root, "skills"), path).Replace('\\', '/');

            Assert.AreEqual(
                2,
                relative.Split('/').Length,
                $"{relative} is not an immediate child of skills/, so only Claude Code would find it.");
        }
    }

    [TestMethod]
    public void A_skill_that_keeps_a_task_list_ends_it_at_the_hand_back_and_never_calls_it_the_record()
    {
        // Six long skills open a task list where the host has one. Two sentences are what keep that
        // from undoing the design: a stage's list holds nothing past its hand-back, so it never shows a
        // stage a person has not decided to run, and every list is a view rather than the record, so a
        // tick is never mistaken for a placed file or recorded evidence. Held per skill, because each
        // skill is loaded alone and a rule stated only in the index is one an agent never reads. D-80.
        //
        // The batch has no hand-back between cycles to protect, and a different way to mislead: four
        // outcomes shown in a tick box with two states. So its two sentences are its own.
        string[] keeping = ["batch-build", "build", "init", "intent", "plan", "spec"];

        List<string> found = [];

        foreach (string path in Skills())
        {
            string text = File.ReadAllText(path);
            string name = Path.GetFileName(Path.GetDirectoryName(path)!);

            if (!text.Contains("## Keep a task list", StringComparison.Ordinal))
            {
                continue;
            }

            found.Add(name);

            StringAssert.Contains(text, "## Keep a task list, if your host has one", name);
            StringAssert.Contains(text, "carry on without one", name + " must still work on a host with no task list.");
            StringAssert.Contains(text, "**The list is a view, never the record.**", name);

            if (name == "batch-build")
            {
                StringAssert.Contains(text, "**One line per cycle.**", name);
                StringAssert.Contains(text, "**A tick means `passed`, and nothing else does.**", name);
            }
            else
            {
                StringAssert.Contains(text, "**This stage only.**", name);
            }

            if (name == "build")
            {
                StringAssert.Contains(
                    text,
                    "**Inside a batch, keep none.**",
                    "Seven lines per cycle would bury the batch's one line per cycle.");
            }
        }

        found.Sort(StringComparer.Ordinal);

        CollectionAssert.AreEqual(
            keeping,
            found,
            "The skills index and CONCEPT.md D-80 name these six. A seventh is a decision, not an edit.");

        string index = File.ReadAllText(Path.Combine(Root, "skills", "README.md"));

        StringAssert.Contains(index, "**Six of them keep a task list where the host has one.**");
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
    [DataRow("intent", "Then stop. The person decides whether to move to the spec.")]
    [DataRow("spec", "then stop. The person decides whether to move to the plan.")]
    [DataRow("plan", "Approving them is yours to do")]
    [DataRow("build", "Then stop. The person decides whether to verify.")]
    [DataRow("verify", "The person decides whether to open a pull request.")]
    public void The_stage_skills_hand_back_rather_than_chaining_into_the_next_stage(string name, string handBack)
    {
        // The separate commands are where a person decides to proceed. A skill that chained straight on
        // would preserve every constraint and still remove the moment somebody chooses to continue.
        // Held to the sentence that hands back rather than to the word "stop", which every one of these
        // skills uses a dozen times for other reasons and so proved nothing.
        string text = File.ReadAllText(Path.Combine(Root, "skills", name, "SKILL.md")).ReplaceLineEndings(" ");

        StringAssert.Contains(
            text,
            handBack,
            name + " no longer hands the next step back to a person. Chaining removes the human-in-the-loop point.");
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
        //
        // Read from the skills rather than from a list of the verbs worth checking: that list held eleven
        // of the commands the skills name, so a skill naming any other one -- or a new one -- was never
        // checked. A command is one written as code, in a span or at the start of a line in a block, so
        // prose such as "the sdlc binary on PATH" is not mistaken for one.
        string usage = File.ReadAllText(Path.Combine(Root, "src", "Sdlc.Cli", "Usage.cs"));

        int checkedCount = 0;

        foreach (string path in Skills())
        {
            IEnumerable<string> named = System.Text.RegularExpressions.Regex
                .Matches(
                    File.ReadAllText(path),
                    @"(?:`|^[ \t]*)sdlc (?<verb>[a-z-]+(?: [a-z]+)?)",
                    System.Text.RegularExpressions.RegexOptions.Multiline)
                .Select(match => match.Groups["verb"].Value)
                .Distinct(StringComparer.Ordinal);

            foreach (string verb in named)
            {
                // The stage skills are named deliberately, and the binary answers those by explaining itself.
                if (Sdlc.Cli.Stages.IsSkill(verb.Split(' ')[0]))
                {
                    continue;
                }

                checkedCount++;
                StringAssert.Contains(
                    usage,
                    verb,
                    $"{path} names 'sdlc {verb}', which the usage text does not list.");
            }
        }

        Assert.IsTrue(checkedCount >= 20, $"Only {checkedCount} commands were found in the skills, so the pattern has stopped matching them.");
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
    public void Every_decision_carries_the_record_shape_and_its_sections_exist()
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

        // Three required fields, because an absent one cannot be told apart from an unwritten one.
        // The heading is the decision, so **Decision.** is optional - see §20's preamble.
        // Prefixes, not exact lines. The longer entries qualify the heading -- "**Why, measured rather
        // than reasoned.**", "**Why one project rather than two.**" -- which reads better than a bare
        // **Why.** and is the same fact. A test stricter than the prose would be a test that makes the
        // prose worse.
        string[] required = ["**Governs:**", "**Why", "**Rejected"];

        List<string> incomplete = [];
        List<string> dangling = [];
        string? current = null;
        HashSet<string> seen = [];

        foreach (string line in lines)
        {
            Match entry = Regex.Match(line, @"^### (D-\d+)");

            if (entry.Success)
            {
                if (current is not null)
                {
                    incomplete.AddRange(required
                        .Where(field => !seen.Contains(field))
                        .Select(field => current + " has no " + field.Trim('*', ':')));
                }

                current = entry.Groups[1].Value;
                seen = [];
                continue;
            }

            if (current is null)
            {
                continue;
            }

            foreach (string field in required)
            {
                if (line.StartsWith(field, StringComparison.Ordinal))
                {
                    seen.Add(field);
                }
            }

            if (!line.StartsWith("**Governs:**", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match section in Regex.Matches(line, @"\u00a7[\d.]+"))
            {
                if (!headings.Contains(section.Value))
                {
                    dangling.Add(current + " -> " + section.Value);
                }
            }
        }

        if (current is not null)
        {
            incomplete.AddRange(required
                .Where(field => !seen.Contains(field))
                .Select(field => current + " has no " + field.Trim('*', ':')));
        }

        List<string> ungoverned = [.. incomplete];

        Assert.AreEqual(
            0,
            ungoverned.Count,
            "These decisions are missing a required field:\n  " + string.Join("\n  ", ungoverned)
            + "\n\nGoverns names the sections that state the rule, or \"nothing\" with the reason - a "
            + "superseded decision governs nothing and should say so. Rejected names what was "
            + "foreclosed, or \"nothing\" and why the entry is here anyway: an absent field cannot be "
            + "told apart from an unwritten one, and an entry that forecloses nothing is a section of "
            + "§1-18 in the wrong place. The heading is the decision, so **Decision.** is optional.");

        Assert.AreEqual(
            0,
            dangling.Count,
            "These decisions point at a section that does not exist:\n  " + string.Join("\n  ", dangling));
    }

    [TestMethod]
    public void Every_decision_id_is_used_once()
    {
        // A commit, a code remark or a finding arrives at a decision by its id, so two entries under one
        // id send that reader to whichever they happen to find first. A branch numbers its entries when it
        // is cut, and another branch can spend the same numbers before it merges: the AppHost branch and
        // development both wrote a D-96 and a D-97, and the merge would have kept all four without a word.
        string concept = File.ReadAllText(Path.Combine(Root, "CONCEPT.md"));

        List<string> reused = [.. Regex.Matches(concept, @"^### (D-\d+)", RegexOptions.Multiline)
            .GroupBy(entry => entry.Groups[1].Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key + " heads " + group.Count() + " entries")];

        Assert.AreEqual(
            0,
            reused.Count,
            "These decision ids are used more than once:\n  " + string.Join("\n  ", reused)
            + "\n\nAn entry takes its number when it merges, not when it is drafted: renumber the "
            + "later one, and every place that cites it.");
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
    public void Every_stage_the_binary_names_as_a_skill_is_one_the_plugin_ships()
    {
        // The panel ends with the literal next command, so a stage the binary explains as a skill and the
        // plugin does not ship sends a person to a skill their host has never heard of -- "a tool must
        // never send somebody somewhere it does not go", which is what Stages' own remarks are about.
        //
        // This replaced two tests that both ran the other way, disk -> binary, and both needed a list to
        // do it. The direction was the defect rather than the lists:
        //
        //   Every_stage_the_panel_can_name_is_a_verb_the_binary_answers asked six hardcoded stages
        //   whether the binary knew them, and skipped any stage it could not find named in
        //   StatusRenderer.cs. The renderer builds that command by interpolation, so it carries the
        //   literal "sdlc {name}" exactly once and never names intent, spec or plan at all -- meaning
        //   half the stages were asserted solely because that one string existed, and renaming it to
        //   $"sdlc {stage}" would have silently skipped them while the test stayed green.
        //
        //   Every_skill_in_the_plugin_is_one_the_binary_knows_about asked the same question of every
        //   skill on disk, which needs six names excluded because six skills are legitimately not
        //   stages. That exclusion list is the shape D-09 refuses: a new non-stage skill fails it
        //   spuriously, and a stage renamed into it stops being checked at all.
        //
        // Read from the binary instead and neither list is needed, because Stages.Skills IS the set. The
        // other direction is not lost: the skills on disk are held to CONCEPT.md's inventory and to the
        // skills index by Every_source_that_lists_the_skills_agrees_with_the_ones_on_disk.
        IReadOnlyCollection<string> stages = Sdlc.Cli.Stages.Skills;

        Assert.IsTrue(
            stages.Count > 0,
            "The binary names no stage skills, so this would pass with every skill directory deleted.");

        HashSet<string> shipped = new(
            Skills().Select(path => Path.GetFileName(Path.GetDirectoryName(path))!),
            StringComparer.Ordinal);

        foreach (string stage in stages)
        {
            Assert.IsTrue(
                shipped.Contains(stage),
                $"The binary explains '{stage}' as a skill and tells a person to ask their agent for it, "
                + "and the plugin ships no such skill.");
        }
    }


    [TestMethod]
    public void Every_source_that_lists_the_skills_agrees_with_the_ones_on_disk()
    {
        // Four places say which skills exist, and each drifts from disk differently. A skill added and
        // written down nowhere reads as undocumented to the next session, which then works from
        // recollection. One written down and never built sends a person to a slash command that does not
        // answer. One with a row and no section of its own is the shape that reads complete and is not --
        // AGENTS.md says a skill change updates its row, its own section and the count in the heading, and
        // the first two are structural, so this is where they are held.
        //
        // One test over a table rather than one test per source, because the extraction, the set
        // comparison and the both-directions reporting were written out three times and the only thing
        // that differed was where the names came from. The failure message names the source, which is
        // what a test name was carrying before.
        //
        // Every row is symmetric. The sections row used to assert one direction only, so a section for a
        // skill nobody ships stayed on the page with nothing to notice.
        HashSet<string> disk = new(
            Skills().Select(path => Path.GetFileName(Path.GetDirectoryName(path))!),
            StringComparer.Ordinal);

        Assert.IsTrue(
            disk.Count > 0,
            "No skills were found on disk, so every comparison below would pass against an empty set.");

        string concept = File.ReadAllText(Path.Combine(Root, "CONCEPT.md"));

        int start = concept.IndexOf("## 15. Skills", StringComparison.Ordinal);
        int end = concept.IndexOf("### 15.1", StringComparison.Ordinal);

        Assert.IsTrue(start >= 0 && end > start, "The concept no longer has a skills inventory to check.");

        string index = File.ReadAllText(Path.Combine(Root, "skills", "README.md"));

        // The skills list is the one-line list now, and the index carries the depth. Every row there
        // starts with the skill's name as a link to its section here, so that is what is read.
        string sheet = File.ReadAllText(Path.Combine(Root, "docs", "reference", "skills.md"));

        // The components document summarises the skills in one row each and sends a reader to the index
        // for the rest. That is a fourth list, so it is held here rather than promised: a summary that
        // silently loses a skill is worse than no summary, because it reads complete.
        string components = File.ReadAllText(Path.Combine(Root, "docs", "README.md"));

        // Scoped to its skills section, because that document is tables end to end and an unscoped
        // match reads the four solution keys as skills.
        int skills = components.IndexOf("## 10. The skills", StringComparison.Ordinal);
        Assert.IsTrue(skills >= 0, "The components document's skills section has moved or been renamed.");

        int after = components.IndexOf("\n## ", skills, StringComparison.Ordinal);
        string summary = after > skills ? components[skills..after] : components[skills..];

        (string Source, HashSet<string> Listed)[] sources =
        [
            ("the concept's skills inventory (§15)", NamesMatching(concept[start..end], @"^\| `(?<name>[a-z-]+)`")),
            ("the skills list", NamesMatching(sheet, @"^\| \[`(?<name>[a-z-]+)`\]")),
            ("the skills index's own sections", NamesMatching(index, @"^### `(?<name>[a-z-]+)`")),
            ("the components document's skills table", NamesMatching(summary, @"^\| `(?<name>[a-z-]+)` \|")),
        ];

        foreach ((string source, HashSet<string> listed) in sources)
        {
            Assert.IsTrue(
                listed.Count > 0,
                $"Nothing was extracted from {source}, so it would agree with any set of skills at all. "
                + "The document moved or its shape changed.");

            Assert.AreEqual(
                0,
                disk.Except(listed, StringComparer.Ordinal).Count(),
                $"Shipped and missing from {source}: "
                + string.Join(", ", disk.Except(listed, StringComparer.Ordinal)));

            Assert.AreEqual(
                0,
                listed.Except(disk, StringComparer.Ordinal).Count(),
                $"Named in {source} and not shipped: "
                + string.Join(", ", listed.Except(disk, StringComparer.Ordinal)));
        }
    }

    /// <summary>Every markdown document in the repository that a reader could follow a link out of.</summary>
    /// <remarks>
    /// Shared by the two rules that read links, so widening the exclusions widens both. It asserts the
    /// count rather than returning whatever it found: a sweep that silently matched nothing is the vacuous
    /// success these rules exist to refuse, and the exclusions are the likeliest cause.
    /// </remarks>
    /// <returns>The full path of each document.</returns>
    private static string[] Documents()
    {
        string[] documents = [.. Directory
            .EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
            .Where(path => !Ignored(Path.GetRelativePath(Root, path)))];

        Assert.IsTrue(
            documents.Length > 20,
            "Almost no documents were read, so the caller is passing by finding nothing. The tree moved "
            + "or the exclusions are swallowing it.");

        return documents;
    }

    /// <summary>The <c>name</c> group of every match, as a set.</summary>
    /// <param name="text">What to read.</param>
    /// <param name="pattern">A multiline pattern carrying a <c>name</c> group.</param>
    /// <returns>Every distinct name the pattern found.</returns>
    private static HashSet<string> NamesMatching(string text, string pattern) =>
        new(
            Regex.Matches(text, pattern, RegexOptions.Multiline).Select(match => match.Groups["name"].Value),
            StringComparer.Ordinal);

    [TestMethod]
    public void The_readme_counts_what_the_repository_actually_ships()
    {
        // A number in a readme is a claim somebody will check, and the first one that is wrong makes the
        // rest not worth reading. These two are countable, so they are asserted rather than trusted; the
        // decision count is deliberately not stated, because it drifts every time one is added.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        int skills = Skills().Count();

        // Counted in force rather than by heading. The two were the same number for as long as nothing
        // had ever been superseded, and a supersession is exactly when they diverge: the old rule keeps
        // its id and stays on the page verbatim, so it is still a `## CON_` heading and is no longer a
        // rule anybody is judged against. The readme's claim is about what binds, and `sdlc constraints`
        // prints the same number, so this asserts what both of them mean.
        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(
            File.ReadAllText(RepoPaths.Library),
            RepoPaths.Library);

        Assert.IsTrue(parsed.IsSuccess, string.Join("; ", parsed.Errors.Select(error => error.Message)));

        int constraints = parsed.Value.Count(constraint => constraint.IsInForce);

        Assert.AreEqual(18, skills, "The readme says eighteen skills.");
        Assert.AreEqual(18, constraints, "The readme says eighteen global constraints.");

        StringAssert.Contains(readme, "eighteen skills");
        StringAssert.Contains(readme, "eighteen global constraints");
    }

    [TestMethod]
    public void Every_document_stating_how_many_rules_a_profile_ships_states_the_right_number()
    {
        // The documents got this wrong for three iterations in the other direction: three of them said
        // the profile tier resolved rules while no profile shipped any, and nothing failed because
        // nothing compared a document to the tier. The count is small enough to be tempting to write in
        // prose, so it is held here -- in all four places, because the one that goes stale is whichever
        // was not on somebody's list.
        string[] paths =
        [
            Path.Combine(Root, "docs", "README.md"),
            Path.Combine(Root, "docs", "guides", "declaring-the-stack.md"),
            Path.Combine(Root, "docs", "guides", "running-a-cycle.md"),
            Path.Combine(Root, "CONCEPT.md"),
        ];

        const string Profile = "dotnet-default";

        Result<IReadOnlyList<Constraint>> parsed = ConstraintParser.Parse(
            File.ReadAllText(RepoPaths.ProfileLibrary(Profile)),
            RepoPaths.ProfileLibrary(Profile));

        Assert.IsTrue(parsed.IsSuccess, string.Join("; ", parsed.Errors.Select(error => error.Message)));

        int rules = parsed.Value.Count(rule => rule.IsInForce);

        Assert.AreEqual(
            4,
            rules,
            "The number every document below states. A deliberate addition updates it here and in each "
            + "of them, which is the point of asserting it rather than trusting it.");

        string[] words = ["none", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

        foreach (string path in paths)
        {
            string text = File.ReadAllText(path);

            Assert.IsTrue(
                text.Contains("ships " + words[rules], StringComparison.OrdinalIgnoreCase)
                || text.Contains("ships " + words[rules] + " rules", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetRelativePath(Root, path)} does not say that the framework ships {words[rules]} "
                + "stack rules. Either it stopped saying how many, in which case take it off this list, or it "
                + "is now wrong about a number a reader will check.");
        }
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
    public void The_version_report_carries_no_carriage_return()
    {
        // A pipeline compares the first line to "sdlc <version>" on Linux, and a binary packed on Windows
        // once printed CRLF from its raw string literal, so an agent carrying the right version reinstalled it.
        Assert.IsFalse(Sdlc.Cli.Release.Text.Contains('\r', StringComparison.Ordinal));
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

        string[] documents = Documents();

        foreach (string path in documents)
        {
            string directory = Path.GetDirectoryName(path)!;

            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(path), @"`(?<path>[A-Za-z0-9/._-]*[/-][A-Za-z0-9/._-]*\.md)`"))
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
    public void Every_document_folder_index_and_the_readme_name_every_document_on_disk()
    {
        // Two lists of the same files per folder, and the duplication is deliberate: a reader landing on
        // the repository front page should see what is there without a click, and a reader already in
        // one of these folders should not have to climb out to find out what is beside them. AGENTS.md
        // records that as an accepted exception to a list living in a single place -- and the price of
        // the exception is this test, because an exception paid for by a promise is the copy that goes
        // stale.
        //
        // One rule over the four folders rather than one test each, because a guide, a concept, a
        // reference list and a sample drift from their indexes the same way. A fifth kind of document is a fifth
        // folder and a fifth entry here, which is what keeps it from arriving undescribed.
        //
        // The index's own list is taken from its section headings rather than from its table. Both have
        // to be there and the heading is the half a reader scrolls to, so keying on it means a document
        // described in a table row and nowhere else fails rather than passing as documented.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        foreach (string folder in (string[])["guides", "mechanisms", "reference", "samples"])
        {
            string directory = Path.Combine(Root, "docs", folder);

            HashSet<string> disk = new(
                Directory.EnumerateFiles(directory, "*.md")
                    .Select(Path.GetFileName)
                    .Where(name => !string.Equals(name, "README.md", StringComparison.Ordinal))!,
                StringComparer.Ordinal);

            Assert.IsTrue(
                disk.Count > 0,
                $"Nothing was found in docs/{folder}/, so this would pass by finding nothing.");

            string index = File.ReadAllText(Path.Combine(directory, "README.md"));

            HashSet<string> named = new(
                Regex.Matches(index, @"^## `(?<file>[a-z0-9-]+\.md)`", RegexOptions.Multiline)
                    .Select(match => match.Groups["file"].Value),
                StringComparer.Ordinal);

            Assert.AreEqual(
                0,
                disk.Except(named, StringComparer.Ordinal).Count(),
                $"On disk and not described by docs/{folder}/README.md: "
                + string.Join(", ", disk.Except(named, StringComparer.Ordinal)));

            Assert.AreEqual(
                0,
                named.Except(disk, StringComparer.Ordinal).Count(),
                $"Described by docs/{folder}/README.md and gone: "
                + string.Join(", ", named.Except(disk, StringComparer.Ordinal)));

            // And the root readme's own table, which is the second list. Every document it names is
            // already asserted to exist; what this adds is the other direction -- one nobody added to it.
            HashSet<string> front = new(
                Regex.Matches(readme, $@"`docs/{folder}/(?<file>[a-z0-9-]+\.md)`")
                    .Select(match => match.Groups["file"].Value),
                StringComparer.Ordinal);

            front.Remove("README.md");

            Assert.AreEqual(
                0,
                disk.Except(front, StringComparer.Ordinal).Count(),
                $"In docs/{folder}/ and missing from the Where to read more table in README.md: "
                + string.Join(", ", disk.Except(front, StringComparer.Ordinal)));

            Assert.AreEqual(
                0,
                front.Except(disk, StringComparer.Ordinal).Count(),
                $"In the Where to read more table in README.md and gone from docs/{folder}/: "
                + string.Join(", ", front.Except(disk, StringComparer.Ordinal)));
        }
    }

    [TestMethod]
    public void The_skills_list_states_what_each_skill_takes_as_the_skill_declares_it()
    {
        // A reference list is a copy by nature, so it is held to the thing it copies. The name of each skill
        // is held by the sources rule above; this holds the one column a reader types from. The skill's
        // own argument-hint is what a host shows when it offers the skill, so a list that drifted
        // from it would teach a different invocation from the one the host prompts for.
        string sheet = File.ReadAllText(Path.Combine(Root, "docs", "reference", "skills.md"));

        var rows = Regex
            .Matches(sheet, @"^\| \[`(?<name>[a-z-]+)`\]\([^)]*\) \| `(?<takes>[^`]*)` \|", RegexOptions.Multiline)
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => match.Groups["takes"].Value.Replace(@"\|", "|", StringComparison.Ordinal),
                StringComparer.Ordinal);

        Assert.IsTrue(rows.Count > 0, "No rows were read from the skills list; its shape changed.");

        foreach (string path in Skills())
        {
            string name = Path.GetFileName(Path.GetDirectoryName(path))!;

            // Quoted in the frontmatter, and an embedded quote is escaped there; the sheet shows neither.
            // One quote off each end rather than Trim: a value ending in an escaped quote ends in two.
            string quoted = Value(Frontmatter(path), "argument-hint", path);
            string hint = (quoted.Length >= 2 && quoted[0] == '"' && quoted[^1] == '"' ? quoted[1..^1] : quoted)
                .Replace("\\\"", "\"", StringComparison.Ordinal);

            Assert.IsTrue(rows.TryGetValue(name, out string? takes), $"The skills list has no row for {name}.");

            Assert.AreEqual(
                hint,
                takes,
                $"The skills list says {name} takes something other than its argument-hint. Change "
                + "the row to match the skill, or the skill and the row together.");
        }
    }

    [TestMethod]
    public void The_refusals_list_names_every_refusal_the_guide_describes()
    {
        // The same price for the same exception. Every entry in the refusals guide is a heading a reader
        // can land on, and every line of the list links to one by name. Compared in both
        // directions, so a refusal added to the guide and not the sheet fails as loudly as a line left
        // pointing at an entry that was removed -- and each link's anchor is checked against the heading
        // it names, because a dead anchor still renders as a link.
        string guide = File.ReadAllText(Path.Combine(Root, "docs", "guides", "handling-refusals.md"));
        string sheet = File.ReadAllText(Path.Combine(Root, "docs", "reference", "refusals.md"));

        HashSet<string> entries = new(
            Regex.Matches(guide, @"^### (?<name>.+?)\s*$", RegexOptions.Multiline)
                .Select(match => match.Groups["name"].Value),
            StringComparer.Ordinal);

        MatchCollection links = Regex.Matches(
            sheet,
            @"\[(?<name>[^\]]+)\]\(\.\./guides/handling-refusals\.md#(?<anchor>[^)]+)\)");

        HashSet<string> lines = new(links.Select(match => match.Groups["name"].Value), StringComparer.Ordinal);

        Assert.IsTrue(entries.Count > 0, "No entries were read from the refusals guide; its shape changed.");
        Assert.IsTrue(lines.Count > 0, "No lines were read from the refusals list; its shape changed.");

        Assert.AreEqual(
            0,
            entries.Except(lines, StringComparer.Ordinal).Count(),
            "In the refusals guide and missing from the list: "
            + string.Join(", ", entries.Except(lines, StringComparer.Ordinal)));

        Assert.AreEqual(
            0,
            lines.Except(entries, StringComparer.Ordinal).Count(),
            "In the refusals list and not an entry in the guide: "
            + string.Join(", ", lines.Except(entries, StringComparer.Ordinal)));

        foreach (Match link in links)
        {
            // The slug a renderer gives a heading: lower case, punctuation dropped, spaces to hyphens. The
            // headings are lower case already, so the case is asserted on the anchor rather than folded.
            string anchor = link.Groups["anchor"].Value;
            string slug = Regex
                .Replace(link.Groups["name"].Value, "[^A-Za-z0-9 -]", string.Empty)
                .Replace(' ', '-');

            Assert.IsFalse(
                anchor.Any(char.IsUpper),
                $"The anchor for '{link.Groups["name"].Value}' has capitals, and a rendered heading's never does.");

            Assert.IsTrue(
                string.Equals(slug, anchor, StringComparison.OrdinalIgnoreCase),
                $"The refusals list links '{link.Groups["name"].Value}' to an anchor its heading "
                + "does not produce, so the link lands at the top of the guide.");
        }
    }

    [TestMethod]
    public void The_tools_index_names_every_tool_on_disk()
    {
        // The same rule pointed at the directory nobody visits twice a year. These two tools exist
        // because the suite cannot check what they check, so a tool that goes undescribed is a check
        // nobody knows to run -- which is how "generate a solution and build it" was a procedure
        // somebody reconstructed, in the shape that let two template defects reach a commit.
        //
        // Build output is excluded by the same rule the document scan uses, so a tool's bin/ and obj/
        // are not read as tools. Everything else immediately under tools/ is one, which means a new
        // script is covered the day it appears rather than the day somebody remembers this test.
        string tools = Path.Combine(Root, "tools");

        HashSet<string> disk = new(
            Directory.EnumerateFileSystemEntries(tools)
                .Select(path => Path.GetFileName(path)!)
                .Where(name => !string.Equals(name, "README.md", StringComparison.Ordinal))
                .Where(name => !Ignored(name)),
            StringComparer.Ordinal);

        Assert.IsTrue(disk.Count > 0, "No tools were found, so this would pass by finding nothing.");

        string index = File.ReadAllText(Path.Combine(tools, "README.md"));

        // A directory is named with its trailing slash and a file without one, because that is how a
        // reader tells them apart. Trimmed here so the comparison is about identity rather than spelling.
        HashSet<string> named = new(
            Regex.Matches(index, @"^## `(?<tool>[^`]+)`", RegexOptions.Multiline)
                .Select(match => match.Groups["tool"].Value.TrimEnd('/')),
            StringComparer.Ordinal);

        Assert.AreEqual(
            0,
            disk.Except(named, StringComparer.Ordinal).Count(),
            "Under tools/ and not described by tools/README.md: "
            + string.Join(", ", disk.Except(named, StringComparer.Ordinal)));

        Assert.AreEqual(
            0,
            named.Except(disk, StringComparer.Ordinal).Count(),
            "Described by tools/README.md and gone: "
            + string.Join(", ", named.Except(disk, StringComparer.Ordinal)));
    }

    [TestMethod]
    public void The_factory_index_names_every_file_on_disk()
    {
        // The tools rule pointed at the factory bundle. Each file there is one a person copies onto a
        // host or builds from, so a file the index does not describe is one somebody runs without being
        // told what it trusts -- and the image mounts the host's Docker socket.
        string factory = Path.Combine(Root, "factory");

        HashSet<string> disk = new(
            Directory.EnumerateFileSystemEntries(factory)
                .Select(path => Path.GetFileName(path)!)
                .Where(name => !string.Equals(name, "README.md", StringComparison.Ordinal))
                .Where(name => !Ignored(name)),
            StringComparer.Ordinal);

        Assert.IsTrue(disk.Count > 0, "No files were found under factory/, so this would pass by finding nothing.");

        string index = File.ReadAllText(Path.Combine(factory, "README.md"));

        HashSet<string> named = new(
            Regex.Matches(index, @"^## `(?<file>[^`]+)`", RegexOptions.Multiline)
                .Select(match => match.Groups["file"].Value),
            StringComparer.Ordinal);

        Assert.AreEqual(
            0,
            disk.Except(named, StringComparer.Ordinal).Count(),
            "Under factory/ and not described by factory/README.md: "
            + string.Join(", ", disk.Except(named, StringComparer.Ordinal)));

        Assert.AreEqual(
            0,
            named.Except(disk, StringComparer.Ordinal).Count(),
            "Described by factory/README.md and gone: "
            + string.Join(", ", named.Except(disk, StringComparer.Ordinal)));
    }

    [TestMethod]
    public void The_factory_image_builds_on_the_sdk_band_global_json_pins()
    {
        // The Dockerfile names an SDK image tag, and global.json the SDK. Two places one decision lives,
        // so the copy is held to the original: the image's band is global.json's major and minor.
        using var pin = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "global.json")));
        string version = pin.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
        string band = string.Join('.', version.Split('.').Take(2));

        string dockerfile = File.ReadAllText(Path.Combine(Root, "factory", "Dockerfile"));
        Match argument = Regex.Match(dockerfile, @"^ARG DOTNET_SDK=(?<band>\S+)\s*$", RegexOptions.Multiline);

        Assert.IsTrue(argument.Success, "factory/Dockerfile no longer declares ARG DOTNET_SDK, which this test reads.");
        Assert.AreEqual(
            band,
            argument.Groups["band"].Value,
            $"global.json pins {version}, so the factory image builds on the {band} SDK images.");
    }

    [TestMethod]
    public void The_pipeline_builds_the_factory_image()
    {
        // The same reason as the scaffold steps: the suite reads the Dockerfile as text, so the only
        // place it is ever built is CI, and a deleted step is silent.
        string pipeline = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));

        StringAssert.Contains(
            pipeline,
            "docker build -f factory/Dockerfile",
            "Nothing else builds the factory's agent image, so without this step a broken one ships.");

        StringAssert.Contains(
            pipeline,
            "--entrypoint sdlc ai-sdlc-factory-agent version",
            "Building the image proves the layers ran, not that the binary inside answers -- and its "
            + "version line is what the factory pipeline compares.");
    }

    [TestMethod]
    public void The_readme_says_what_is_not_done_rather_than_only_what_is()
    {
        // The design's own rule, pointed at its readme: a thing that appears finished and is not is worse
        // than one that never pretended. Somebody deciding whether to adopt this needs the second list.
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));

        StringAssert.Contains(readme, "Where it stands");

        // One per host the table claims and nobody has run. A host added to SupportedHosts without a
        // line here would be a readme that reads as three working hosts, which is the claim this
        // whole section exists to stop the project making. Claude Code is absent on purpose: it is
        // the host the framework is developed and exercised on.
        foreach (string gap in (string[])
        [
            "The Copilot install has never been run",
            "The Antigravity adapter has never been run",
        ])
        {
            StringAssert.Contains(
                readme,
                gap,
                "The known gap has to be in the readme, not only in a commit message. A package that "
                + "conforms to a standard is not the same claim as a package somebody installed.");
        }
    }

    [TestMethod]
    public void The_pr_skill_carries_the_same_caveat_the_pipeline_does()
    {
        // D-32's own reasoning, applied consistently: the caveat goes where somebody meets the path. The
        // generated pipeline warns and the skill did not, which would have left an agent confidently
        // running an untried command and reading the failure as its own mistake.
        string pr = File.ReadAllText(Path.Combine(Root, "skills", "pr", "SKILL.md"));

        StringAssert.Contains(pr, "az repos pr create");
        StringAssert.Contains(
            pr,
            "never been run",
            "The skill names the Azure DevOps command, so it has to say the path is untried.");
    }

    [TestMethod]
    public void Every_markdown_link_a_document_states_resolves_with_the_case_it_states()
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
        // Scoped to every document rather than to AGENTS.md, because the argument above is about what a
        // browser does and the readme is the document most likely to be read in one. AGENTS.md was the
        // scope only because its dead link is what prompted the rule. The sibling rule above reads
        // backticked paths, which is a different syntax and does not check case, so neither subsumes the
        // other.
        string[] documents = Documents();
        int inspected = 0;

        foreach (string document in documents)
        {
            string directory = Path.GetDirectoryName(document)!;
            string relative = Path.GetRelativePath(Root, document);

            string[] targets = [.. Regex
                .Matches(File.ReadAllText(document), @"\]\((?<path>[^)\s#]+)\)")
                .Select(match => match.Groups["path"].Value)
                .Where(path => !path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)];

            foreach (string target in targets)
            {
                // Resolved from the document that names it, the way a reader following it would.
                string full = Path.GetFullPath(
                    Path.Combine(directory, target.Replace('/', Path.DirectorySeparatorChar)));

                inspected++;

                Assert.IsTrue(
                    File.Exists(full) || Directory.Exists(full),
                    $"{relative} links to {target}, which does not exist.");

                // A link to a directory may carry a trailing slash -- `guides/` -- and GetFileName returns
                // nothing for a path that ends in a separator, which made the case check compare against
                // an empty name and fail on a link that was perfectly good. Trimmed first, so the
                // comparison is against the last real segment either way.
                string trimmed = Path.TrimEndingDirectorySeparator(full);
                string name = Path.GetFileName(trimmed);

                Assert.IsTrue(
                    Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(trimmed)!, name)
                        .Any(entry => string.Equals(Path.GetFileName(entry), name, StringComparison.Ordinal)),
                    $"{relative} links to {target}, which exists under a different capitalisation. That "
                    + "resolves in a clone and is a dead link when the file is rendered on the host.");
            }
        }

        Assert.IsTrue(
            inspected > 0,
            "No markdown links were checked at all, so this would pass however they were written. "
            + "AGENTS.md alone used to carry enough of them for that to be impossible.");
    }

    [TestMethod]
    public void Agents_md_sends_a_session_to_the_skill_reference_rather_than_restating_it()
    {
        // What AGENTS.md must not do is describe the skills itself. A second description is the one that
        // goes stale, and a session working from the stale copy has no way to tell -- the same reason no
        // skill restates a constraint. So it points at skills/README.md, and says that the surface and the
        // documents describing it move together, which is the part of the rule a test can actually hold.
        string agents = File.ReadAllText(Path.Combine(Root, "AGENTS.md"));

        StringAssert.Contains(
            agents,
            "skills/README.md",
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

    /// <summary>One skill's frontmatter lines, between the two fences.</summary>
    private static string[] Frontmatter(string path) =>
        [.. File.ReadAllText(path)
            .ReplaceLineEndings()
            .Split(Environment.NewLine)
            .SkipWhile(line => line != "---")
            .Skip(1)
            .TakeWhile(line => line != "---")];

    /// <summary>One frontmatter field's value, failing the test when the field is absent.</summary>
    private static string Value(string[] frontmatter, string key, string path)
    {
        string? line = Array.Find(frontmatter, candidate =>
            candidate.StartsWith(key + ": ", StringComparison.Ordinal));

        Assert.IsNotNull(line, $"{path} declares no '{key}'.");

        return line[(key.Length + 2)..].Trim();
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
