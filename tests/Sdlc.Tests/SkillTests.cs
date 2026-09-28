using YamlDotNet.RepresentationModel;

namespace Sdlc.Tests;

/// <summary>
/// Every shipped skill's frontmatter is read with a real YAML parser, because v02 proved what
/// happens otherwise: a hand-rolled regex reported that a skill declared
/// <c>disable-model-invocation</c> while the real parser had dropped every field in the file. The
/// skill was model-invocable at runtime and 333 tests said it was not.
/// </summary>
[TestClass]
public sealed class SkillTests
{
    // Skills whose effects reach outside the repository, or commit a team to a process. A model
    // must never decide on its own that now is the moment for one of these.
    private static readonly string[] MustBeHumanInvoked = ["land", "run", "init"];

    private static IEnumerable<string> SkillDirectories =>
        Directory.EnumerateDirectories(RepoPaths.Skills).OrderBy(d => d, StringComparer.Ordinal);

    private static YamlMappingNode Frontmatter(string skillFile)
    {
        string text = File.ReadAllText(skillFile);

        Assert.IsTrue(
            text.StartsWith("---", StringComparison.Ordinal),
            $"{skillFile} has no frontmatter block.");

        int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        Assert.IsTrue(end > 0, $"{skillFile} has an unterminated frontmatter block.");

        YamlStream yaml = new();
        yaml.Load(new StringReader(text[3..end]));

        Assert.AreEqual(1, yaml.Documents.Count, $"{skillFile} frontmatter is not one YAML document.");

        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static string? Scalar(YamlMappingNode map, string key)
    {
        YamlScalarNode probe = new(key);
        return map.Children.TryGetValue(probe, out YamlNode? value) ? ((YamlScalarNode)value).Value : null;
    }

    [TestMethod]
    public void ThereAreSkills()
    {
        Assert.IsTrue(SkillDirectories.Any(), "No skills found. A plugin with no skills does nothing.");
    }

    [TestMethod]
    public void EverySkillFrontmatterParsesAndNamesItself()
    {
        foreach (string dir in SkillDirectories)
        {
            string file = Path.Combine(dir, "SKILL.md");
            Assert.IsTrue(File.Exists(file), $"{dir} has no SKILL.md.");

            YamlMappingNode map = Frontmatter(file);
            string expected = Path.GetFileName(dir);

            Assert.AreEqual(
                expected,
                Scalar(map, "name"),
                $"{expected}/SKILL.md declares a name that does not match its directory. The " +
                "directory is what the invocation resolves against.");
        }
    }

    [TestMethod]
    public void EverySkillDescribesWhenToUseIt()
    {
        foreach (string dir in SkillDirectories)
        {
            YamlMappingNode map = Frontmatter(Path.Combine(dir, "SKILL.md"));
            string name = Path.GetFileName(dir);
            string? description = Scalar(map, "description");

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(description),
                $"{name} has no description. The description is the only part always in context, " +
                "and it is how relevance is judged.");

            Assert.IsTrue(
                description!.Length >= 60,
                $"{name}'s description is too short to say when to use it: '{description}'.");

            Assert.IsTrue(
                description.Contains("Use ", StringComparison.Ordinal),
                $"{name}'s description does not say when to use it. A description that only says " +
                "what a skill does leaves the model guessing about the trigger.");
        }
    }

    [TestMethod]
    public void OutwardFacingSkillsAreHumanInvokedOnly()
    {
        foreach (string name in MustBeHumanInvoked)
        {
            string file = Path.Combine(RepoPaths.Skills, name, "SKILL.md");
            Assert.IsTrue(File.Exists(file), $"Expected a skill named {name}.");

            YamlMappingNode map = Frontmatter(file);

            Assert.AreEqual(
                "true",
                Scalar(map, "disable-model-invocation"),
                $"{name} reaches outside this repository or commits a team to a process, so it " +
                "must be human-invoked. Without disable-model-invocation: true a model can start " +
                "it on its own.");
        }
    }

    [TestMethod]
    [DataRow("intent", "intent.schema.json")]
    [DataRow("spec", "spec.schema.json")]
    [DataRow("plan", "plan.schema.json")]
    public void ASkillNamesEveryFrontmatterFieldItsArtifactRequires(string skill, string schemaName)
    {
        // A skill cannot read its schema - a solution session cannot reach the plugin root - so the
        // required fields have to be stated in the body. The intent skill once had that text removed
        // while stripping unreachable paths, and produced intents with no frontmatter at all.
        using System.Text.Json.JsonDocument schema = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Schemas, schemaName)));

        string body = File.ReadAllText(Path.Combine(RepoPaths.Skills, skill, "SKILL.md"));

        foreach (System.Text.Json.JsonElement required in schema.RootElement.GetProperty("required").EnumerateArray())
        {
            string field = required.GetString()!;

            Assert.IsTrue(
                body.Contains(field, StringComparison.Ordinal),
                $"{schemaName} requires '{field}' but the {skill} skill never mentions it, so the " +
                "artifact it writes will be missing a field nothing downstream can supply.");
        }
    }

    [TestMethod]
    public void NoSkillRestatesAConstraint()
    {
        // CON_09: an artifact that reproduces what another owns creates two sources that diverge.
        // A skill may cite a constraint by id and explain why it matters; it may not copy the rule.
        foreach (string dir in SkillDirectories)
        {
            string body = File.ReadAllText(Path.Combine(dir, "SKILL.md"));

            Assert.IsFalse(
                body.Contains("**Forbids:**", StringComparison.Ordinal),
                $"{Path.GetFileName(dir)} copies a Forbids clause. Cite the constraint by id " +
                "instead; constraints.md owns the wording.");
        }
    }
}
