#:package JsonSchema.Net
#:package YamlDotNet

// Versions are deliberately absent: central package management owns them in
// Directory.Packages.props, and NU1008 refuses an inline one outright — which is the pack's
// own rule about a version having exactly one home, enforced by the restore.
//
// A file-based app defaults to being AOT-publishable, so the trimming analysers fire on the
// reflection both a YAML reader and System.Text.Json need. Nothing publishes this ahead of
// time — `dotnet run` JITs it — so the analysers are measuring a scenario that does not exist.
// The strict gates from Directory.Build.props all stay on.
#:property PublishAot=false
#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property EnableSingleFileAnalyzer=false

// Validates one artifact against one shipped schema, and says nothing else.
//
// WHY THIS EXISTS. The six schemas under schemas/ were exercised only by test fixtures, so
// nothing checked a real artifact at the moment it was written. The findings schema carries
// the five anti-inflation rules — blocking requires a citation, unclassified can never block,
// a mechanical finding never sits at open, a location pair is only for a contradiction, a
// dismissal states its reason — and three of those were enforced by the workflow's own code
// while two were enforced nowhere. A schema nothing validates against is a convention.
//
// WHY IT IS C# AND NOT POWERSHELL. The schema files are the authority for these rules. A
// PowerShell reimplementation of them would be a second representation with nothing binding
// the two, which is the duplication this framework exists to catch. .NET has a JSON Schema
// 2020-12 validator and a YAML reader; Windows PowerShell 5.1 has neither.
//
// WHY IT IS A FILE-BASED APP. It adds no project to the solution and nothing to build: the
// plugin ships this one file, and `dotnet run` compiles and caches it on first use. A second
// .csproj would be a build step every consumer pays for to run a validator.
//
// IT DECIDES NOTHING ABOUT WHICH SCHEMA APPLIES. The caller names both the schema and the
// input, because mapping an artifact path to its kind already lives in Get-SdlcFacts.ps1 and
// having two answers to that question is the same duplication one level down.

using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;

string? schemaPath = null;
string? inputPath = null;
bool frontmatter = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--schema" when i + 1 < args.Length: schemaPath = args[++i]; break;
        case "--input" when i + 1 < args.Length: inputPath = args[++i]; break;
        case "--frontmatter": frontmatter = true; break;
        default:
            return Fail($"unrecognised argument '{args[i]}'. " +
                "Usage: --schema <file> --input <file> [--frontmatter]");
    }
}

if (schemaPath is null || inputPath is null)
{
    return Fail("both --schema and --input are required");
}

if (!File.Exists(schemaPath))
{
    return Fail($"no schema at '{schemaPath}'");
}

if (!File.Exists(inputPath))
{
    return Fail($"no input at '{inputPath}'");
}

string json;
try
{
    json = frontmatter ? FrontmatterAsJson(inputPath) : File.ReadAllText(inputPath);
}
catch (Exception e) when (e is IOException or InvalidDataException or YamlDotNet.Core.YamlException)
{
    // A document whose frontmatter does not parse is not "valid until proven otherwise": the
    // loader would drop every field in it, so this reports the same way a rule violation does.
    return Fail($"could not read {inputPath}: {e.Message}");
}

JsonSchema schema;
JsonDocument instance;
try
{
    schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
    instance = JsonDocument.Parse(json);
}
catch (JsonException e)
{
    return Fail($"{inputPath} is not valid JSON: {e.Message}");
}

// RequireFormatValidation matches the test suite's options exactly. Without it a `format`
// keyword is annotation rather than assertion, so the validator would accept what the suite
// rejects — two answers about one schema, which is the thing being avoided.
EvaluationResults results = schema.Evaluate(
    instance.RootElement,
    new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });

List<string> errors = [];
if (!results.IsValid)
{
    // TWO PASSES, BECAUSE THE FIRST REAL DOCUMENT DROWNED THE ANSWER. A 28-finding interrogation
    // was refused with 130-odd errors of which TWO were the cause; the rest came from branches the
    // instance was never required to satisfy — each `oneOf` alternative a citation is not, and each
    // `if` whose condition simply did not hold. "Required properties [disposition] are not present"
    // was reported for every finding, from the `if` of a rule that only fires WHEN a disposition
    // exists.
    //
    // A check that buries its cause is the erosion this project keeps warning about: it is not
    // ignored because it is wrong, it is ignored because reading it costs more than trusting it.
    Collect(results, errors, skipBranchNoise: true);

    if (errors.Count == 0)
    {
        // Never "invalid, and here is nothing". If the filter leaves nothing, the unfiltered list is
        // worse to read but it is still the truth.
        Collect(results, errors, skipBranchNoise: false);
    }

    if (errors.Count == 0)
    {
        errors.Add("the document does not satisfy the schema, which reported no detail");
    }
}

Report(results.IsValid, errors);
return results.IsValid ? 0 : 1;

// Walks the evaluation tree and keeps only nodes that actually carry a complaint. The list
// output is a tree of every subschema visited, most of which passed.
static void Collect(EvaluationResults node, List<string> into, bool skipBranchNoise)
{
    if (node.Errors is { Count: > 0 } complaints && !(skipBranchNoise && IsBranchNoise(node)))
    {
        foreach (KeyValuePair<string, string> error in complaints)
        {
            string where = node.InstanceLocation.ToString();
            string message = string.IsNullOrEmpty(where) ? error.Value : $"{where}: {error.Value}";

            // Deduplicated: one cause reported once. The same complaint arrives from several
            // subschemas for the same instance location, and repeating it adds no information.
            if (!into.Contains(message))
            {
                into.Add(message);
            }
        }
    }

    // Recursion continues regardless: a real violation can sit beneath a branch node, and each
    // child is judged on its own path.
    foreach (EvaluationResults child in node.Details ?? [])
    {
        Collect(child, into, skipBranchNoise);
    }
}

// An error is noise when it comes from a subschema the instance was never obliged to satisfy.
//
// `oneOf` and `anyOf`: a citation is exactly one of four kinds, so THREE alternatives always fail
// and always complain. `if`: its whole job is to not match, and a failed condition is how a
// conditional rule declines to apply rather than a defect.
//
// `then` is deliberately NOT here. Everything under it is a rule that fired and was broken —
// blocking without a citation, a location pair on the wrong category, a dismissal with no reason —
// which is the half of the output worth reading.
static bool IsBranchNoise(EvaluationResults node)
{
    string path = node.EvaluationPath.ToString();

    return path.Contains("/oneOf/", StringComparison.Ordinal)
        || path.Contains("/anyOf/", StringComparison.Ordinal)
        || path.Contains("/if/", StringComparison.Ordinal)
        || path.EndsWith("/if", StringComparison.Ordinal);
}

// Reads the YAML frontmatter block and converts it to JSON with YAML'S OWN type rules, rather
// than by guessing in the caller. Every schema here wants `charter-version` as an integer and
// `pack-version` as a string or null, so handing the validator strings for all three would
// report a correct artifact as broken — and a check that reports healthy artifacts as broken
// gets switched off, which is the erosion failure one layer up.
static string FrontmatterAsJson(string path)
{
    string[] lines = File.ReadAllLines(path);
    if (lines.Length == 0 || lines[0].Trim() != "---")
    {
        throw new InvalidDataException("no YAML frontmatter block");
    }

    int end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
    if (end < 0)
    {
        throw new InvalidDataException("the frontmatter block is never closed");
    }

    string block = string.Join('\n', lines.Skip(1).Take(end - 1));

    IDeserializer reader = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    object? parsed = reader.Deserialize<object?>(block);
    return JsonSerializer.Serialize(Normalise(parsed));
}

// YamlDotNet returns Dictionary<object, object> and List<object>; System.Text.Json needs
// string keys. Scalars arrive already typed by the YAML core schema.
static JsonNode? Normalise(object? value)
{
    switch (value)
    {
        case null:
            return null;
        case IDictionary<object, object> map:
            JsonObject obj = [];
            foreach (KeyValuePair<object, object> pair in map)
            {
                obj[pair.Key?.ToString() ?? string.Empty] = Normalise(pair.Value);
            }

            return obj;
        case IList<object> list:
            JsonArray array = [];
            foreach (object item in list)
            {
                array.Add(Normalise(item));
            }

            return array;
        case bool flag:
            return JsonValue.Create(flag);
        case int or long or short or byte:
            return JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
        case float or double or decimal:
            return JsonValue.Create(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
        default:
            return JsonValue.Create(value.ToString());
    }
}

// One output shape whether it passed, failed, or could not be attempted. A caller that has to
// tell "invalid" from "could not run" by parsing prose is a caller that will get it wrong.
static void Report(bool ok, List<string> errors)
{
    // The relaxed encoder because a schema complaint quotes the property it is about, and the
    // default escaping renders that as " — which turns the one part of the message a
    // person needs into something they have to decode.
    Console.WriteLine(JsonSerializer.Serialize(
        new { ok, errors },
        new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
}

static int Fail(string message)
{
    Report(false, [message]);
    return 2;
}
