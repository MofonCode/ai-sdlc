using Json.Schema;

namespace Sdlc.Tests;

/// <summary>
/// Loads every schema exactly once for the whole test run.
/// <para>
/// <c>JsonSchema.FromFile</c> registers a schema in a process-wide registry under its own
/// <c>$id</c> — which is how a relative <c>$ref</c> between two of these files resolves — and
/// registering the same id twice throws. So no test may load a schema itself: two test classes
/// each loading them independently is exactly the collision this type exists to prevent.
/// </para>
/// </summary>
internal static class Schemas
{
    private static readonly Lazy<IReadOnlyDictionary<string, JsonSchema>> ByFileName = new(Load);

    public static JsonSchema Get(string fileName) => ByFileName.Value[fileName];

    public static IReadOnlyCollection<string> FileNames => (IReadOnlyCollection<string>)ByFileName.Value.Keys;

    public static readonly EvaluationOptions Options = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true,
    };

    private static Dictionary<string, JsonSchema> Load()
    {
        Dictionary<string, JsonSchema> loaded = new(StringComparer.Ordinal);

        foreach (string path in Directory
            .EnumerateFiles(RepoPaths.Schemas, "*.schema.json")
            .OrderBy(p => p, StringComparer.Ordinal))
        {
            loaded[Path.GetFileName(path)] = JsonSchema.FromFile(path);
        }

        return loaded;
    }

    /// <summary>Renders a failed evaluation compactly enough to read in a test message.</summary>
    public static string Describe(EvaluationResults results)
    {
        if (results.IsValid)
        {
            return "valid";
        }

        IEnumerable<string> messages = (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Take(6);

        return string.Join(" | ", messages);
    }
}
