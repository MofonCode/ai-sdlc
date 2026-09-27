using System.Text.Json;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace Sdlc.Tests;

/// <summary>
/// The fixtures are the substrate every later phase is validated against, so their integrity is
/// asserted here. An expectation citing a constraint that does not exist, or an artifact nobody
/// evaluates, would make a whole phase look tested when it was not.
/// </summary>
[TestClass]
public sealed class FixtureTests
{
    private static string ArtifactsDir => Path.Combine(RepoPaths.Fixtures, "artifacts");

    private static IEnumerable<string> ExpectationFiles =>
        Directory.EnumerateFiles(ArtifactsDir, "*.expected.json");

    [TestMethod]
    public void EveryExpectationMatchesItsSchema()
    {
        JsonSchema schema = Schemas.Get("expectation.schema.json");

        foreach (string file in ExpectationFiles)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
            EvaluationResults results = schema.Evaluate(doc.RootElement, Schemas.Options);

            Assert.IsTrue(
                results.IsValid,
                $"{Path.GetFileName(file)}: {Schemas.Describe(results)}");
        }
    }

    [TestMethod]
    public void EveryArtifactHasAnExpectationAndViceVersa()
    {
        string[] artifacts = [.. Directory
            .EnumerateFiles(ArtifactsDir, "*.md")
            .Select(Path.GetFileName)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)];

        string[] claimed = [.. ExpectationFiles
            .Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement.GetProperty("artifact").GetString()!)
            .OrderBy(n => n, StringComparer.Ordinal)];

        CollectionAssert.AreEqual(
            artifacts,
            claimed,
            "Every golden artifact needs exactly one expectation, and every expectation must name " +
            "an artifact that exists. Otherwise a fixture is either unevaluated or phantom.");
    }

    [TestMethod]
    public void ExpectationsCiteConstraintsThatExist()
    {
        HashSet<string> globalIds = [.. System.Text.RegularExpressions.Regex
            .Matches(File.ReadAllText(RepoPaths.Constraints), @"^## (CON-\d{2,})",
                System.Text.RegularExpressions.RegexOptions.Multiline,
                TimeSpan.FromSeconds(2))
            .Select(m => m.Groups[1].Value)];

        foreach (string file in ExpectationFiles)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));

            foreach (JsonElement cited in doc.RootElement.GetProperty("must_cite_blocking").EnumerateArray())
            {
                string id = cited.GetString()!;

                Assert.IsTrue(
                    globalIds.Contains(id),
                    $"{Path.GetFileName(file)} expects a finding citing {id}, which constraints.md does not define.");
            }
        }
    }

    [TestMethod]
    public void EveryConstraintAboutAnArtifactHasAFailingFixture()
    {
        // A constraint no fixture exercises is a rule nobody has ever seen fire. This covers the
        // ones an interrogation of a single artifact can reach; CON-07 through CON-11 need a diff,
        // a verify document or a waiver, and are exercised in later phases.
        string[] reachable = ["CON-01", "CON-02", "CON-03", "CON-04", "CON-05", "CON-06"];

        HashSet<string> exercised = [.. ExpectationFiles
            .SelectMany(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement
                .GetProperty("must_cite_blocking").EnumerateArray()
                .Select(e => e.GetString()!))];

        foreach (string id in reachable)
        {
            Assert.IsTrue(
                exercised.Contains(id),
                $"{id} is checkable against a single artifact but no fixture expects it to fire.");
        }
    }

    [TestMethod]
    public void ArtifactFrontmatterParsesWithARealYamlParser()
    {
        // v02 learned this the hard way: a hand-rolled frontmatter regex reported fields that the
        // real parser had dropped, and the suite agreed with the regex.
        foreach (string file in Directory.EnumerateFiles(ArtifactsDir, "*.md"))
        {
            string text = File.ReadAllText(file);

            Assert.IsTrue(
                text.StartsWith("---", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} has no frontmatter.");

            int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
            Assert.IsTrue(end > 0, $"{Path.GetFileName(file)} has an unterminated frontmatter block.");

            YamlStream yaml = new();
            yaml.Load(new StringReader(text[3..end]));

            Assert.AreEqual(1, yaml.Documents.Count, $"{Path.GetFileName(file)} frontmatter is not one YAML document.");
        }
    }

    [TestMethod]
    [DataRow("greenfield")]
    [DataRow("brownfield")]
    public void FixtureSolutionDeclaresItsProfile(string solutionName)
    {
        string path = Path.Combine(RepoPaths.Fixtures, "solutions", solutionName, ".sdlc", "solution.md");

        Assert.IsTrue(File.Exists(path), $"{solutionName} has no .sdlc/solution.md.");

        string text = File.ReadAllText(path);
        int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);

        YamlStream yaml = new();
        yaml.Load(new StringReader(text[3..end]));

        YamlMappingNode root = (YamlMappingNode)yaml.Documents[0].RootNode;
        string[] keys = [.. root.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value!)];

        CollectionAssert.Contains(keys, "stack", $"{solutionName} does not declare a stack.");
        CollectionAssert.Contains(keys, "origin", $"{solutionName} does not declare an origin.");
        CollectionAssert.DoesNotContain(keys, "baseline",
            "There is no baseline: constraints govern the change, not code that predates adoption.");
    }

    [TestMethod]
    [DataRow("greenfield")]
    [DataRow("brownfield")]
    public void FixtureMaterializesAsAGitRepositoryAndIsDiscarded(string solutionName)
    {
        string path;

        using (FixtureWorkspace workspace = FixtureWorkspace.Materialize(solutionName))
        {
            path = workspace.Path;

            Assert.IsTrue(Directory.Exists(Path.Combine(path, ".git")), "No git repository was initialized.");
            Assert.IsTrue(File.Exists(Path.Combine(path, ".sdlc", "solution.md")), "The profile was not copied.");
            Assert.AreEqual(40, workspace.BaseCommit.Length, "No base commit to diff against.");
            Assert.AreEqual(string.Empty, workspace.Run("status", "--porcelain").Trim(),
                "A freshly materialized workspace must be clean, or a later diff includes the fixture itself.");

            // Mutating the copy must not reach the checked-in fixture.
            File.WriteAllText(Path.Combine(path, "SCRATCH.txt"), "written by a test");
            Assert.AreNotEqual(string.Empty, workspace.Run("status", "--porcelain").Trim());
        }

        Assert.IsFalse(Directory.Exists(path), "The workspace was not discarded.");
        Assert.IsFalse(
            File.Exists(Path.Combine(RepoPaths.Fixtures, "solutions", solutionName, "SCRATCH.txt")),
            "The checked-in fixture was mutated. Skills must only ever run against a copy.");
    }
}
