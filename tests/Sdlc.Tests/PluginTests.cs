using System.Text.Json;
using System.Text.Json.Nodes;

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
    public void Every_guide_a_guide_sends_somebody_to_exists()
    {
        // The same rule one level down. These cross-reference each other heavily, and three of the four
        // were referenced by name before any of them was written.
        //
        // A guide reference is one that carries a directory or a hyphenated name. That is not a
        // convention invented for this test: the guides are named for what they walk through, so they
        // hyphenate, while every file the framework writes is a single word -- intent.md, approval.md,
        // solution.md, techstack.md. Keying on the shape means no list of artifact names to exclude, and
        // no list that goes stale the first time the envelope gains a file.
        string guides = Path.Combine(Root, "docs", "guides");
        Assert.IsTrue(Directory.Exists(guides), "The guides directory is gone.");

        foreach (string path in Directory.EnumerateFiles(
            Path.Combine(Root, "docs"), "*.md", SearchOption.AllDirectories))
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
