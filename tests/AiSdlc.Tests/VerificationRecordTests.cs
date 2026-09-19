using System.Text.RegularExpressions;

namespace AiSdlc.Tests;

/// <summary>
/// The verification record is described in two places that must agree: the schema the verify
/// skill writes from, and the output template the verifier agent produces.
/// </summary>
/// <remarks>
/// Two documents describing one artifact, with nothing comparing them, is a shape this
/// repository has already been bitten by - the stack pack kept a file table beside its manifest
/// and neither knew about the other. It happened here too and went unnoticed for longer: the
/// agent's template ended with the verdict while the schema said it comes first, and the
/// template omitted two sections the schema required outright.
///
/// These checks read the two instruction documents against each other. They deliberately never
/// read a produced verification.md: records describe what was true when they were written, and
/// validating old ones against a newer schema would make history fail.
/// </remarks>
[TestClass]
public sealed class VerificationRecordTests
{
    private static readonly string SchemaPath = Path.Combine(
        Repo.Root, "plugins", "sdlc-core", "skills", "sdlc-router", "references", "artifact-schemas.md");

    private static readonly string AgentPath = Path.Combine(
        Repo.Root, "plugins", "sdlc-core", "agents", "verifier.md");

    /// <summary>The section that has to come first, and the one that turns findings into a decision.</summary>
    private const string SummarySection = "Summary";
    private const string OptionsSection = "Options";

    /// <summary>
    /// The required-section names from the verification.md schema, in declared order.
    /// </summary>
    private static List<string> SchemaSections()
    {
        string[] lines = File.ReadAllLines(SchemaPath);
        List<string> sections = [];
        bool inVerificationSchema = false;
        bool inRequiredList = false;

        foreach (string line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                // Each schema lives under its own heading; stop at the next one.
                if (inVerificationSchema)
                {
                    break;
                }

                inVerificationSchema = line.Contains("verification.md", StringComparison.Ordinal);
                continue;
            }

            if (!inVerificationSchema)
            {
                continue;
            }

            if (line.StartsWith("Required sections", StringComparison.Ordinal))
            {
                inRequiredList = true;
                continue;
            }

            // The gate paragraph closes the list.
            if (inRequiredList && line.StartsWith("Gate:", StringComparison.Ordinal))
            {
                break;
            }

            if (!inRequiredList)
            {
                continue;
            }

            Match match = Regex.Match(line, @"^- \*\*(?<name>[^*]+)\*\*");
            if (match.Success)
            {
                sections.Add(match.Groups["name"].Value.Trim());
            }
        }

        return sections;
    }

    /// <summary>
    /// The headings in the verifier agent's output template, in declared order.
    /// </summary>
    private static List<string> TemplateSections()
    {
        string[] lines = File.ReadAllLines(AgentPath);
        List<string> sections = [];
        bool inOutput = false;
        bool inFence = false;

        foreach (string line in lines)
        {
            if (!inOutput)
            {
                inOutput = line.StartsWith("## Output", StringComparison.Ordinal);
                continue;
            }

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                // The template is the first fenced block under Output; the prose after it is
                // guidance, not headings.
                if (inFence)
                {
                    break;
                }

                inFence = true;
                continue;
            }

            if (inFence && line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add(line[3..].Trim());
            }
        }

        return sections;
    }

    [TestMethod]
    public void SchemaAndAgentTemplate_DescribeTheSameRecord()
    {
        List<string> schema = SchemaSections();
        List<string> template = TemplateSections();

        Assert.IsTrue(
            schema.Count > 0 && template.Count > 0,
            $"read {schema.Count} section(s) from the schema and {template.Count} from the agent"
                + " template, so this check asserted nothing");

        List<string> problems = [];

        foreach (string section in schema.Except(template, StringComparer.Ordinal))
        {
            problems.Add(
                $"'{section}' is required by {Marketplace.Rel(SchemaPath)} and absent from the"
                + $" agent's output template, so a verifier following the template omits it");
        }

        foreach (string section in template.Except(schema, StringComparer.Ordinal))
        {
            problems.Add(
                $"'{section}' is in the agent's output template and is not a required section of"
                + $" {Marketplace.Rel(SchemaPath)}, so the schema does not describe what gets written");
        }

        Assert.AreEqual(
            0,
            problems.Count,
            "the verification record is described in two documents and they disagree:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void SchemaAndAgentTemplate_AgreeOnOrder()
    {
        List<string> schema = SchemaSections();
        List<string> template = TemplateSections();

        Assert.IsTrue(
            schema.Count > 0 && template.Count > 0,
            "read no sections from one of the two documents, so this check asserted nothing");

        // Only meaningful once the sets match; the sibling check reports that failure better.
        if (schema.Except(template, StringComparer.Ordinal).Any()
            || template.Except(schema, StringComparer.Ordinal).Any())
        {
            return;
        }

        CollectionAssert.AreEqual(
            schema,
            template,
            "the schema declares its sections in order and the agent's template emits a different"
                + " one. A reader who trusts the record's shape - including the one looking for the"
                + " verdict first - is reading whichever document nobody updated."
                + Environment.NewLine
                + "schema:   " + string.Join(", ", schema)
                + Environment.NewLine
                + "template: " + string.Join(", ", template));
    }

    [TestMethod]
    public void TheRecordOpensWithASummaryAndClosesWithOptions()
    {
        List<string> schema = SchemaSections();
        List<string> template = TemplateSections();

        Assert.IsTrue(
            schema.Count > 0 && template.Count > 0,
            "read no sections from one of the two documents, so this check asserted nothing");

        // A record nobody reads gets summarised by whoever relays it, from memory.
        Assert.AreEqual(SummarySection, schema[0], "the schema's first required section");
        Assert.AreEqual(SummarySection, template[0], "the agent template's first section");

        // Findings without options leave the reader to invent the choices.
        Assert.AreEqual(OptionsSection, schema[^1], "the schema's last required section");
        Assert.AreEqual(OptionsSection, template[^1], "the agent template's last section");
    }

    [TestMethod]
    public void TheAgent_IsToldToWriteOptionsRatherThanLeaveThemInferred()
    {
        // The heading alone is satisfiable by an empty section. What makes Options load-bearing
        // is the instruction, so this asserts the instruction is present rather than trusting
        // that a heading implies one.
        string agent = File.ReadAllText(AgentPath);

        Assert.IsTrue(
            agent.Contains("never present a single course as inevitable", StringComparison.OrdinalIgnoreCase),
            Marketplace.Rel(AgentPath)
                + " names an Options section but does not tell the verifier to name every course"
                + " open to the owner. Without that, the section is a heading over a recommendation.");
    }
}
