using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Sdlc.Validate;

/// <summary>
/// Evaluates a live artifact against the schema that governs it.
/// <para>
/// The hook that runs on every write, <c>tools/Assert-DeclaredFile.ps1</c>'s sibling
/// <c>Assert-ArtifactShape.ps1</c>, compares top-level key names and nothing else, because Windows
/// PowerShell 5.1 has no JSON Schema evaluator. So the rules that matter most were checked only
/// against fixtures: <c>verify.schema.json</c> says "pass requires that no blocking finding is still
/// open" and "escalated requires passes_spent of exactly 3", and both are rules about values. A real
/// cycle could commit a verify document claiming <c>pass</c> with an open blocking finding and nothing
/// would object.
/// </para>
/// <para>
/// This is that missing half: the same evaluator the test suite uses, pointed at a file on disk.
/// </para>
/// </summary>
internal static class Program
{
    /// <summary>Which schema governs which kind of artifact.</summary>
    private static readonly (string Kind, string Schema)[] _byKind =
    [
        ("intent", "intent.schema.json"),
        ("spec", "spec.schema.json"),
        ("plan", "plan.schema.json"),
        ("verify", "verify.schema.json"),
        ("solution", "solution.schema.json"),
    ];

    private static readonly EvaluationOptions _options = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "usage: Sdlc.Validate <artifact> [artifact ...]" + Environment.NewLine +
                Environment.NewLine +
                "  Each argument is a path to a live artifact: an intent, spec, plan, verify or" + Environment.NewLine +
                "  solution markdown file, or a findings JSON document. A directory is walked for" + Environment.NewLine +
                "  the artifacts it holds." + Environment.NewLine +
                Environment.NewLine +
                "  Exit 0 when every artifact is valid, 1 when any is not, 2 on a usage error.");

            return 2;
        }

        string schemaDirectory = FindSchemas();

        if (schemaDirectory.Length == 0)
        {
            Console.Error.WriteLine(
                "Could not find a schemas/ directory. Run this from inside the ai-sdlc repository, or " +
                "set SDLC_SCHEMAS to the directory holding *.schema.json.");

            return 2;
        }

        Dictionary<string, JsonSchema> schemas = LoadSchemas(schemaDirectory);

        List<string> artifacts = [];

        foreach (string argument in args)
        {
            if (Directory.Exists(argument))
            {
                artifacts.AddRange(Collect(argument));
            }
            else if (File.Exists(argument))
            {
                artifacts.Add(argument);
            }
            else
            {
                Console.Error.WriteLine($"not found: {argument}");
                return 2;
            }
        }

        if (artifacts.Count == 0)
        {
            Console.Error.WriteLine("No artifact found to validate, so nothing was checked.");
            return 2;
        }

        int invalid = 0;
        int skipped = 0;

        foreach (string path in artifacts.OrderBy(p => p, StringComparer.Ordinal))
        {
            string? schemaName = SchemaFor(path);

            if (schemaName is null)
            {
                Console.WriteLine($"SKIP  {Relative(path)} — no schema governs this file");
                skipped++;
                continue;
            }

            if (!schemas.TryGetValue(schemaName, out JsonSchema? schema))
            {
                Console.WriteLine($"SKIP  {Relative(path)} — {schemaName} is not in {schemaDirectory}");
                skipped++;
                continue;
            }

            JsonDocument? instance;

            try
            {
                instance = Read(path);
            }
            catch (Exception e) when (e is JsonException or IOException or YamlDotNet.Core.YamlException)
            {
                Console.WriteLine($"FAIL  {Relative(path)} ({schemaName})");
                Console.WriteLine($"        could not be read: {e.Message}");
                invalid++;
                continue;
            }

            if (instance is null)
            {
                Console.WriteLine($"FAIL  {Relative(path)} ({schemaName})");
                Console.WriteLine("        has no frontmatter, so there is nothing to evaluate");
                invalid++;
                continue;
            }

            using (instance)
            {
                EvaluationResults results = schema.Evaluate(instance.RootElement, _options);

                if (results.IsValid)
                {
                    Console.WriteLine($"OK    {Relative(path)} ({schemaName})");
                    continue;
                }

                Console.WriteLine($"FAIL  {Relative(path)} ({schemaName})");

                foreach (string message in Errors(results))
                {
                    Console.WriteLine($"        {message}");
                }
            }

            invalid++;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{artifacts.Count - invalid - skipped} valid, {invalid} invalid, {skipped} skipped.");

        return invalid > 0 ? 1 : 0;
    }

    /// <summary>Every artifact under a directory that a schema governs.</summary>
    private static IEnumerable<string> Collect(string directory)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            if (SchemaFor(path) is not null)
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// The schema governing a path, or null when none does.
    /// <para>
    /// Findings are matched on the round suffix a skill writes — <c>intent.r1.json</c> — rather than on
    /// being JSON, so an unrelated JSON file in a cycle directory is skipped rather than failed against
    /// a schema that was never meant for it.
    /// </para>
    /// </summary>
    private static string? SchemaFor(string path)
    {
        string name = Path.GetFileName(path);

        // The kind is the first dot-segment, so this covers a live artifact (`plan.md`) and a golden
        // fixture (`plan.good.md`, `spec.untestable.md`) with one rule. The fixture corpus is worth
        // reaching: every interrogation test judges those files, so one that does not satisfy its own
        // schema makes every expectation built on it meaningless.
        string kind = name.Split('.')[0];

        if (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            foreach ((string artifact, string schema) in _byKind)
            {
                if (kind.Equals(artifact, StringComparison.OrdinalIgnoreCase))
                {
                    return schema;
                }
            }

            return null;
        }

        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // An expectation states what an interrogation of one golden artifact must conclude.
        if (name.EndsWith(".expected.json", StringComparison.OrdinalIgnoreCase))
        {
            return "expectation.schema.json";
        }

        // Findings are matched on the directory a skill writes them into rather than on being JSON, so
        // an unrelated JSON file in a cycle is skipped rather than failed against a foreign schema.
        if (Path.GetDirectoryName(path) is { } directory
            && Path.GetFileName(directory).Equals("findings", StringComparison.OrdinalIgnoreCase))
        {
            return "findings.schema.json";
        }

        return null;
    }

    /// <summary>Reads an artifact as JSON: a JSON file directly, a markdown file's frontmatter.</summary>
    private static JsonDocument? Read(string path)
    {
        string text = File.ReadAllText(path);

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonDocument.Parse(text);
        }

        string? frontmatter = Frontmatter(text);

        if (frontmatter is null)
        {
            return null;
        }

        // A real parser, never a regex: v02 hand-rolled frontmatter matching and reported fields the
        // parser had actually dropped.
        YamlStream yaml = [];
        yaml.Load(new StringReader(frontmatter));

        if (yaml.Documents.Count == 0)
        {
            return null;
        }

        JsonNode? root = ToJson(yaml.Documents[0].RootNode);

        return root is null ? null : JsonDocument.Parse(root.ToJsonString());
    }

    /// <summary>
    /// Converts a YAML node to JSON, keeping scalar types.
    /// <para>
    /// This is the whole reason the conversion is written out rather than handed to YamlDotNet's
    /// <c>JsonCompatible</c> serializer. Deserializing to <c>object</c> makes every scalar a string, so
    /// <c>passes_spent: 0</c> arrived as <c>"0"</c> and the schema rejected a valid verify document for
    /// the wrong reason. YAML 1.2 decides by quoting: a plain scalar that reads as null, a boolean or a
    /// number is that type, and a quoted one is always a string.
    /// </para>
    /// </summary>
    private static JsonNode? ToJson(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                JsonObject o = [];

                foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
                {
                    if (entry.Key is YamlScalarNode { Value: { } key })
                    {
                        o[key] = ToJson(entry.Value);
                    }
                }

                return o;

            case YamlSequenceNode sequence:
                JsonArray a = [];

                foreach (YamlNode item in sequence.Children)
                {
                    a.Add(ToJson(item));
                }

                return a;

            case YamlScalarNode scalar:
                return Scalar(scalar);

            default:
                return null;
        }
    }

    private static JsonValue? Scalar(YamlScalarNode scalar)
    {
        string? value = scalar.Value;

        if (value is null)
        {
            return null;
        }

        // Quoted is always a string: `round: "1"` is deliberately text, and guessing otherwise would
        // silently change what the author wrote.
        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted)
        {
            return JsonValue.Create(value);
        }

        if (value.Length == 0 || value is "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        if (value is "true" or "True" or "TRUE")
        {
            return JsonValue.Create(true);
        }

        if (value is "false" or "False" or "FALSE")
        {
            return JsonValue.Create(false);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
        {
            return JsonValue.Create(whole);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
        {
            return JsonValue.Create(real);
        }

        return JsonValue.Create(value);
    }

    /// <summary>The text between the opening and closing frontmatter fences, or null when absent.</summary>
    private static string? Frontmatter(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return null;
        }

        int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);

        return end < 0 ? null : text[3..end];
    }

    /// <summary>
    /// The errors that actually made the document invalid.
    /// <para>
    /// A schema like this one is mostly conditionals, and every conditional that declines to apply still
    /// reports its mismatch. A verify document with <c>outcome: pass</c> was told <c>findings</c> "should
    /// contain at least 1 matching items" — which is the rule for <c>blocked</c>, a branch that did not
    /// apply — and the one real error sat underneath four lines of that.
    /// </para>
    /// <para>
    /// The rule is general rather than a list of paths to ignore: <b>a detail beneath a node that passed
    /// is not a failure.</b> A declined conditional passes, so everything under it goes; a conditional
    /// that applied and failed does not pass, so its errors stay.
    /// </para>
    /// </summary>
    private static IEnumerable<string> Errors(EvaluationResults results)
    {
        List<EvaluationResults> all = [.. results.Details ?? []];

        HashSet<string> passed =
        [
            .. all.Where(d => d.IsValid).Select(d => d.EvaluationPath.ToString()),
        ];

        return all
            .Where(d => d.Errors is { Count: > 0 })
            .Where(d => !IsUnder(d.EvaluationPath.ToString(), passed))
            .SelectMany(d => d.Errors!.Select(e =>
                $"{(d.InstanceLocation.ToString().Length == 0 ? "(root)" : d.InstanceLocation.ToString())}: {e.Value}"))
            .Distinct(StringComparer.Ordinal)
            .Take(12);
    }

    private static bool IsUnder(string path, HashSet<string> ancestors)
    {
        foreach (string ancestor in ancestors)
        {
            if (ancestor.Length > 0
                && path.Length > ancestor.Length
                && path.StartsWith(ancestor + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The schemas directory: SDLC_SCHEMAS if set, else the nearest one above this assembly.
    /// </summary>
    private static string FindSchemas()
    {
        string? configured = Environment.GetEnvironmentVariable("SDLC_SCHEMAS");

        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "schemas");

            if (File.Exists(Path.Combine(candidate, "verify.schema.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }

    /// <summary>
    /// Loads every schema once. <c>JsonSchema.FromFile</c> registers each under its own <c>$id</c>,
    /// which is how a relative <c>$ref</c> between two of these files resolves; registering one twice
    /// throws, so this happens exactly here.
    /// </summary>
    private static Dictionary<string, JsonSchema> LoadSchemas(string directory)
    {
        Dictionary<string, JsonSchema> loaded = new(StringComparer.Ordinal);

        foreach (string path in Directory
            .EnumerateFiles(directory, "*.schema.json")
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            loaded[Path.GetFileName(path)] = JsonSchema.FromFile(path);
        }

        return loaded;
    }

    private static string Relative(string path)
    {
        string current = Directory.GetCurrentDirectory();

        return path.StartsWith(current, StringComparison.OrdinalIgnoreCase)
            ? path[current.Length..].TrimStart(Path.DirectorySeparatorChar, '/')
            : path;
    }
}
