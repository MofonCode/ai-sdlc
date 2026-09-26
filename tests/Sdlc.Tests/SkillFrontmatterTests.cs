using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Sdlc.Tests;

/// <summary>
/// Every skill's frontmatter, read by a real YAML parser rather than by a regex.
/// </summary>
/// <remarks>
/// THIS EXISTS BECAUSE A REGEX SAID YES WHERE THE PARSER SAID NO. <c>SkillTests</c> asserts
/// that <c>run</c> contains the text <c>disable-model-invocation: true</c>, and it did — but
/// its <c>argument-hint</c> was the unquoted <c>[cycle-id ...] or --all</c>, which YAML reads
/// as a flow sequence followed by junk. The real parser therefore failed the whole block and
/// dropped EVERY field in it, so the skill that launches N unattended processes was
/// model-invocable at runtime while 333 tests reported it was not.
/// <para>
/// Installing the plugin is what said so. That is the same lesson as every other entry in
/// this suite's history: a check on markup is worth exactly what the parser behind it is, and
/// a hand-rolled parser agrees with the real one right up until it matters.
/// </para>
/// <para>
/// So the rule enforced here is stricter than "it parses": EVERY VALUE MUST BE A SCALAR. An
/// unquoted <c>[cycle-id]</c> parses cleanly as a one-item sequence, which is not the string
/// the author meant and is one trailing word away from taking the whole file down. Requiring
/// a scalar removes the class rather than the instance.
/// </para>
/// </remarks>
[TestClass]
public sealed class SkillFrontmatterTests
{
    private static string SkillDir => Path.Combine(AppContext.BaseDirectory, "skills");

    /// <summary>
    /// The skills whose side effects are real enough that only a person may start them:
    /// <c>run</c> launches unattended processes, <c>init</c> writes a charter,
    /// <c>absorb-retro</c> proposes charter amendments, <c>grill</c> convenes a team at team
    /// prices, and <c>maintain</c> files intents.
    /// </summary>
    private static readonly string[] UserInvokedOnly =
    [
        "run", "init", "absorb-retro", "grill", "maintain",
    ];

    public static IEnumerable<object[]> AllSkills() =>
        Directory.EnumerateDirectories(SkillDir)
            .Select(d => new object[] { Path.GetFileName(d) });

    /// <summary>
    /// Splits the frontmatter block out of a SKILL.md and parses it the way the loader does.
    /// A parse failure is returned rather than thrown so each test can name its own
    /// consequence.
    /// </summary>
    private static Dictionary<string, object?> Frontmatter(string skill)
    {
        string path = Path.Combine(SkillDir, skill, "SKILL.md");
        string[] lines = File.ReadAllLines(path);

        Assert.IsTrue(lines.Length > 0 && lines[0].Trim() == "---",
            $"{skill}: SKILL.md must open with a YAML frontmatter fence");

        int end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        Assert.IsTrue(end > 0, $"{skill}: the frontmatter block is never closed");

        string block = string.Join('\n', lines.Skip(1).Take(end - 1));

        try
        {
            IDeserializer reader = new DeserializerBuilder().Build();
            return reader.Deserialize<Dictionary<string, object?>>(block)
                ?? throw new AssertFailedException($"{skill}: frontmatter parsed to nothing");
        }
        catch (YamlException e)
        {
            throw new AssertFailedException(
                $"{skill}: frontmatter does not parse, so the skill loads with EMPTY metadata " +
                $"and every field below is silently dropped — {e.Message}", e);
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllSkills))]
    public void FrontmatterParsesAndEveryValueIsAScalar(string skill)
    {
        ArgumentNullException.ThrowIfNull(skill);

        foreach (KeyValuePair<string, object?> field in Frontmatter(skill))
        {
            // A sequence or a mapping here means YAML read something other than the string the
            // author wrote. `argument-hint: [cycle-id]` is the whole family: harmless as a
            // one-item list, fatal the moment a word follows the bracket.
            //
            // Tested as "not a collection" rather than "is a string" deliberately: whether a
            // parser hands back `true` as a bool or as text is its own business and not
            // something this rule has an opinion about. Brackets and braces are.
            bool isCollection = field.Value is System.Collections.IEnumerable and not string;

            Assert.IsFalse(isCollection,
                $"{skill}: `{field.Key}` parses as a collection, not a scalar. Quote the value — " +
                "an unquoted bracket is a YAML sequence, and one trailing word turns it into a " +
                "parse error that drops the entire block.");
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllSkills))]
    public void ThePARSEDNameMatchesTheDirectory(string skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        Dictionary<string, object?> fm = Frontmatter(skill);

        Assert.IsTrue(fm.TryGetValue("name", out object? name),
            $"{skill}: no name survives parsing, so nothing forms /sdlc:{skill}");
        Assert.AreEqual(skill, name as string,
            "the name must match the directory, because the directory is what forms the command");

        Assert.IsTrue(fm.TryGetValue("description", out object? description) &&
                      !string.IsNullOrWhiteSpace(description as string),
            $"{skill}: no description survives parsing, so the skill is invisible to delegation");
    }

    [TestMethod]
    public void SideEffectingSkillsAreNotModelInvocableAFTERParsing()
    {
        // THE ONE THIS SUITE GOT WRONG. Asserting the text is present proves nothing about
        // what the loader receives: `run` carried the line and the loader saw no fields at all.
        foreach (string skill in UserInvokedOnly)
        {
            Dictionary<string, object?> fm = Frontmatter(skill);

            Assert.IsTrue(fm.TryGetValue("disable-model-invocation", out object? flag),
                $"{skill} has real side effects, and after parsing it carries no " +
                "disable-model-invocation at all — a model can start it");

            // ToString rather than a cast: whether the parser returns a bool or the text
            // "true" is its business, and this rule only cares that the answer is yes.
            Assert.IsTrue(
                string.Equals(flag?.ToString(), "true", StringComparison.OrdinalIgnoreCase),
                $"{skill}: disable-model-invocation must be true, and parses as '{flag}'");
        }
    }
}
