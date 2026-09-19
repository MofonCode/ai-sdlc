using System.Text.Json;

namespace AiSdlc.Tests;

/// <summary>
/// The CI workflow and the packs' scaffold manifests, held against each other.
/// </summary>
/// <remarks>
/// A solution kind declares what must already be present before it will scaffold, and refuses
/// to install any of it - that refusal protects a person's workstation, where a global install
/// is irreversible and outlives the repository that caused it. A hosted runner is not a
/// workstation: it is ephemeral and declared in a reviewed file, so the repository's own CI
/// installs what the runner lacks. That split only works while the two documents agree. A
/// prerequisite added to a manifest and not to the workflow otherwise surfaces as a preflight
/// halting mid-suite on the runner, which reads as a broken test rather than as the missing
/// setup step it is.
/// </remarks>
[TestClass]
public sealed class ScaffoldPrerequisiteTests
{
    /// <summary>The workflow that runs the suite, read as text - there is no YAML parser here.</summary>
    /// <remarks>
    /// Deliberately no dependency: the gate hook makes the same call and says so in a comment.
    /// Everything asserted below is a question about which lines name what, and in what order,
    /// which text answers exactly as well as a parse tree would.
    /// </remarks>
    private const string WorkflowPath = ".github/workflows/checks.yml";

    /// <summary>The line that runs the suite. Every prerequisite must be named before it.</summary>
    private const string SuiteProject = "tests/AiSdlc.Tests/AiSdlc.Tests.csproj";

    /// <summary>The plugin that is the engine; every other plugin is a stack pack.</summary>
    private const string EnginePlugin = "sdlc-core";

    /// <summary>The scaffold manifest every pack ships, beside its own SKILL.md.</summary>
    private const string ScaffoldManifest = "scaffold.json";

    /// <summary>The verb a manifest may never use, because the machine is not the scaffold's to change.</summary>
    private const string InstallVerb = "install";

    /// <summary>
    /// A prerequisite no manifest declares and no workflow provides, for the two rows below
    /// that have to reason about one. Shared so the absence asserted in one is the absence the
    /// other fabricates a workflow around.
    /// </summary>
    private const string NeverNamed = "a-template-nobody-has-added-yet";

    /// <summary>One thing a kind requires to be present before it will scaffold.</summary>
    /// <param name="Pack">The pack whose manifest declares it.</param>
    /// <param name="Kind">The solution kind that requires it.</param>
    /// <param name="Subject">What the workflow has to name - see <see cref="SubjectOf"/>.</param>
    /// <param name="Probe">The whole command line, for an error message somebody can act on.</param>
    private sealed record Prerequisite(string Pack, string Kind, string Subject, string Probe);

    /// <summary>
    /// What a preflight check requires the runner to already have.
    /// </summary>
    /// <remarks>
    /// This is the rule the whole check turns on, and it is stated here rather than spread
    /// across the assertions. <b>The subject of a preflight is its last argument that is not an
    /// option flag; a check with no such argument is probing the tool itself, and its subject
    /// is the command.</b> A command line names the thing it is asking about last, which is
    /// what makes this hold as the manifest changes rather than only for the checks that exist
    /// today.
    /// <para>
    /// That rule is also the answer to "not every prerequisite needs an install step". The
    /// check never asks <i>how</i> CI provides something, only that the workflow names it
    /// before the suite runs. The SDK is named by the runner's own setup step and needs nothing
    /// further; a project template is named only because an install step was added for it. A
    /// prerequisite named by neither fails here. There is no exemption list, which is the point:
    /// an exemption list is the thing that quietly grows a row nobody re-examines.
    /// </para>
    /// <para>
    /// <b>"Names it" means the workflow names it, not a comment beside the workflow.</b> Only
    /// the part of a line the runner acts on counts - see <see cref="Instructions"/>. A comment
    /// is the one kind of text in the file that can outlive the thing it describes without any
    /// consequence: delete the step, leave the sentence about it, and nothing anywhere notices.
    /// That is the vacuous pass this check exists to refuse, so a name has to sit in the
    /// document the runner reads, where deleting the step deletes the name with it.
    /// </para>
    /// <para>
    /// The limit, stated rather than discovered: a probe that does not put its subject last -
    /// a bare subcommand, say - yields a subject the workflow will not name, and this check
    /// fails. That is noisy, and it is the correct direction to be wrong in. The alternative is
    /// a check that quietly decides a prerequisite is satisfied.
    /// </para>
    /// </remarks>
    private static string SubjectOf(string command, IReadOnlyList<string> args)
    {
        for (int i = args.Count - 1; i >= 0; i--)
        {
            if (!args[i].StartsWith("-", StringComparison.Ordinal))
            {
                return args[i];
            }
        }

        return command;
    }

    [TestMethod]
    public void EveryDeclaredPrerequisite_IsNamedByCIBeforeTheSuiteRuns()
    {
        List<Prerequisite> prerequisites = Prerequisites();
        Assert.IsTrue(
            prerequisites.Count > 0,
            "no pack declares a preflight check, so this check is asserting nothing - either a"
                + " manifest stopped declaring its prerequisites or it stopped being found");

        List<string> missing = NotNamedBeforeTheSuiteRuns(WorkflowLines(), prerequisites);

        Assert.AreEqual(
            0,
            missing.Count,
            WorkflowPath + " does not name these prerequisites before it runs the suite, so the"
                + " kind that needs them cannot be proven on a hosted runner - add a step that"
                + " provides them, and name them in the step itself; a comment naming them does"
                + " not count, because a comment survives the step's deletion:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// The subject rule picks out a named prerequisite, and an unnamed one would fail the check.
    /// </summary>
    /// <remarks>
    /// Without this, the check above passes on a day when every subject happens to be the tool
    /// the workflow already sets up, and nobody can tell that from a check with teeth. The two
    /// halves here are the teeth: the rule reaches past the command to the thing being probed,
    /// and a prerequisite the workflow does not name is genuinely absent from it rather than
    /// matching some unrelated line. Both are asserted against a fabricated prerequisite, so
    /// they stay true whatever the real manifests come to declare.
    /// </remarks>
    [TestMethod]
    public void TheSubjectRule_ReachesPastTheToolToTheThingBeingProbed()
    {
        Assert.AreEqual(
            "dotnet",
            SubjectOf("dotnet", ["--version"]),
            "a probe with nothing but option flags is asking about the tool itself");

        Assert.AreEqual(
            "aspire-apphost",
            SubjectOf("dotnet", ["new", "list", "aspire-apphost"]),
            "a probe for a named thing must yield that name, not the command that asked");

        Assert.AreEqual(
            NeverNamed,
            SubjectOf("dotnet", ["new", "list", NeverNamed]),
            "the rule must work for a prerequisite that does not exist yet - that is the case"
                + " it is for");

        string[] lines = WorkflowLines();
        Assert.IsFalse(
            Array.Exists(lines, l => Names(l, NeverNamed)),
            WorkflowPath + " appears to name " + NeverNamed + ", which cannot be right and"
                + " means the matching rule is finding things that are not there");
    }

    /// <summary>
    /// A prerequisite named only by a comment is not named at all.
    /// </summary>
    /// <remarks>
    /// The other way this check could pass while proving nothing, and the reason
    /// <see cref="Instructions"/> exists. The check above deliberately never asks <i>how</i> CI
    /// provides a prerequisite - but without this, "names it" is satisfied by a sentence
    /// describing a step that is no longer there, and the whole rule reduces to "somebody once
    /// wrote the word down".
    /// <para>
    /// Both documents below are fabricated rather than read from disk, and they differ by
    /// exactly one thing: the step. The comment naming the subject is in both. So the pass is
    /// the step's doing and the failure is the step's absence, and both stay true as the real
    /// workflow changes around them.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void APrerequisiteNamedOnlyByAComment_IsNotNamedAtAll()
    {
        Prerequisite fabricated = new(
            "fabricated-pack", "fabricated-kind", NeverNamed, "dotnet new list " + NeverNamed);

        string[] upToTheStep =
        [
            "jobs:",
            "  test:",
            "    runs-on: windows-latest",
            "    steps:",
            "      - uses: actions/checkout@v4",
            "",
            "      # Provides: " + NeverNamed,
        ];
        string[] theStep =
        [
            "      - name: install " + NeverNamed,
            "        run: dotnet new install Fabricated.Templates::1.0.0",
        ];
        string[] runningTheSuite =
        [
            "",
            "      - name: dotnet test",
            "        run: dotnet test " + SuiteProject + " --nologo",
        ];

        List<string> whenTheStepIsThere = NotNamedBeforeTheSuiteRuns(
            [.. upToTheStep, .. theStep, .. runningTheSuite], [fabricated]);
        Assert.AreEqual(
            0,
            whenTheStepIsThere.Count,
            "a step naming the prerequisite is what providing it looks like, and this check has"
                + " to accept it or it rejects every workflow:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, whenTheStepIsThere));

        List<string> whenOnlyTheCommentIsThere = NotNamedBeforeTheSuiteRuns(
            [.. upToTheStep, .. runningTheSuite], [fabricated]);
        Assert.AreEqual(
            1,
            whenOnlyTheCommentIsThere.Count,
            "the step was deleted and only the comment naming " + NeverNamed + " was left behind,"
                + " which is a workflow that provides nothing - this check has to fail on it,"
                + " and it did not");
        Assert.IsTrue(
            whenOnlyTheCommentIsThere[0].Contains(NeverNamed, StringComparison.Ordinal),
            "the failure has to name the prerequisite that is missing, or nobody can act on it:"
                + Environment.NewLine
                + whenOnlyTheCommentIsThere[0]);
    }

    /// <summary>
    /// No manifest installs its own prerequisite, whatever CI does.
    /// </summary>
    /// <remarks>
    /// The other half of the same rule. CI installing a template is fine because the runner is
    /// thrown away; a manifest doing it reaches a person's machine, where the install outlives
    /// the repository and was never agreed to. This catches the verb, which is the shape the
    /// mistake takes - it is not a general guarantee that a run action leaves the machine
    /// alone, and nothing here is a substitute for reading the manifest.
    /// </remarks>
    [TestMethod]
    public void NoManifest_InstallsItsOwnPrerequisite()
    {
        List<string> violations = [];

        foreach ((string pack, string manifestPath) in PackManifests())
        {
            using JsonDocument document = Marketplace.Parse(manifestPath);
            foreach (JsonProperty kind in Kinds(document))
            {
                if (!kind.Value.TryGetProperty("actions", out JsonElement actions)
                    || actions.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement action in actions.EnumerateArray())
                {
                    if (!string.Equals(Text(action, "kind"), "run", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    List<string> args = Args(action);
                    if (args.Exists(a => a.Equals(InstallVerb, StringComparison.OrdinalIgnoreCase)))
                    {
                        violations.Add(
                            "  " + pack + " / " + kind.Name + ": "
                                + Text(action, "command") + " " + string.Join(" ", args));
                    }
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            "a scaffold manifest may declare what must already be present and may not put it"
                + " there; the preflight's remediation is where the install command belongs, for"
                + " the owner to run themselves:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Which of <paramref name="prerequisites"/> the workflow does not name before it runs the
    /// suite, each already spelled out for a failure message.
    /// </summary>
    /// <remarks>
    /// Takes the workflow as lines rather than reading it, so the same rule that judges the
    /// real file can be pointed at a fabricated one and shown to fail.
    /// </remarks>
    private static List<string> NotNamedBeforeTheSuiteRuns(
        string[] lines, IReadOnlyList<Prerequisite> prerequisites)
    {
        int suiteLine = Array.FindIndex(
            lines, l => Instructions(l).Contains(SuiteProject, StringComparison.Ordinal));
        Assert.IsTrue(
            suiteLine >= 0,
            "the workflow never runs " + SuiteProject
                + ", so there is no point before which a prerequisite has to be provided");

        List<string> missing = [];
        foreach (Prerequisite prerequisite in prerequisites)
        {
            bool provided = false;
            for (int i = 0; i < suiteLine && !provided; i++)
            {
                provided = Names(Instructions(lines[i]), prerequisite.Subject);
            }

            if (!provided)
            {
                missing.Add(
                    "  " + prerequisite.Pack + " / " + prerequisite.Kind + ": "
                        + prerequisite.Subject + "  (probed by: " + prerequisite.Probe + ")");
            }
        }

        return missing;
    }

    /// <summary>
    /// The part of a line the runner acts on - the line with any comment cut off it.
    /// </summary>
    /// <remarks>
    /// YAML's own rule: a <c>#</c> opens a comment when it starts the line or follows
    /// whitespace. Quoting is not tracked, so a <c>#</c> inside a quoted string can be read as
    /// a comment and cut instructions short. That is the direction to be wrong in - a line cut
    /// short can only make a prerequisite read as unprovided, never as provided, so the check
    /// gets noisier and never quieter.
    /// </remarks>
    private static string Instructions(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
            {
                return line[..i];
            }
        }

        return line;
    }

    private static string[] WorkflowLines()
    {
        string path = Path.Combine(
            Repo.Root, WorkflowPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path), WorkflowPath + " is missing, and the suite runs from it");
        return File.ReadAllLines(path);
    }

    /// <summary>Every preflight check every pack declares, across every kind.</summary>
    /// <remarks>
    /// The pack enumeration below is a small copy of the one in PluginStructureTests rather than
    /// a shared helper, because the two files are owned separately; the convention it encodes -
    /// a pack's manifest sits beside its own SKILL.md - is checked there.
    /// </remarks>
    private static List<Prerequisite> Prerequisites()
    {
        List<Prerequisite> prerequisites = [];

        foreach ((string pack, string manifestPath) in PackManifests())
        {
            using JsonDocument document = Marketplace.Parse(manifestPath);
            foreach (JsonProperty kind in Kinds(document))
            {
                if (!kind.Value.TryGetProperty("preflight", out JsonElement preflight)
                    || preflight.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement check in preflight.EnumerateArray())
                {
                    string command = Text(check, "command");
                    List<string> args = Args(check);
                    prerequisites.Add(
                        new Prerequisite(
                            pack,
                            kind.Name,
                            SubjectOf(command, args),
                            (command + " " + string.Join(" ", args)).Trim()));
                }
            }
        }

        return prerequisites;
    }

    /// <summary>
    /// Every stack pack's manifest. The engine ships none, and asserting one of it would be
    /// asserting the opposite of charter invariant I1.
    /// </summary>
    private static IEnumerable<(string Pack, string ManifestPath)> PackManifests()
    {
        foreach (Marketplace.Plugin plugin in Marketplace.Plugins())
        {
            if (string.Equals(plugin.Name, EnginePlugin, StringComparison.Ordinal))
            {
                continue;
            }

            string manifest = Path.Combine(
                plugin.Directory, "skills", plugin.Name, ScaffoldManifest);
            if (File.Exists(manifest))
            {
                yield return (plugin.Name, manifest);
            }
        }
    }

    private static IEnumerable<JsonProperty> Kinds(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("kinds", out JsonElement kinds)
            || kinds.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (JsonProperty kind in kinds.EnumerateObject())
        {
            yield return kind;
        }
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

    private static List<string> Args(JsonElement element)
    {
        List<string> args = [];
        if (!element.TryGetProperty("args", out JsonElement declared)
            || declared.ValueKind != JsonValueKind.Array)
        {
            return args;
        }

        foreach (JsonElement arg in declared.EnumerateArray())
        {
            if (arg.ValueKind == JsonValueKind.String)
            {
                args.Add(arg.GetString() ?? string.Empty);
            }
        }

        return args;
    }

    /// <summary>
    /// Whether a line names <paramref name="subject"/> as a whole word.
    /// </summary>
    /// <remarks>
    /// A plain substring match is how this check would go wrong quietly: a two-letter subject
    /// matches inside half the workflow's own keywords and every prerequisite reads as
    /// provided. Word characters bound it on both sides, so "dotnet" matches inside
    /// "setup-dotnet@v4" and "dotnet-version" - which do name the SDK - and a subject never
    /// matches the middle of an unrelated identifier.
    /// </remarks>
    private static bool Names(string line, string subject)
    {
        if (subject.Length == 0)
        {
            return false;
        }

        int from = 0;
        while (from <= line.Length - subject.Length)
        {
            int at = line.IndexOf(subject, from, StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }

            int end = at + subject.Length;
            if ((at == 0 || !IsWordCharacter(line[at - 1]))
                && (end == line.Length || !IsWordCharacter(line[end])))
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }

    private static bool IsWordCharacter(char character) =>
        char.IsLetterOrDigit(character) || character == '_';
}
