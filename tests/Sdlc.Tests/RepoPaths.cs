namespace Sdlc.Tests;

/// <summary>
/// Locates the repository root from the test binary's location, so tests read the files this
/// repository actually ships rather than copies staged into the output directory. A copy would
/// let a test pass against a stale artifact, which is the failure mode these tests exist to
/// prevent.
/// </summary>
internal static class RepoPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    public static string Root => RootLazy.Value;

    public static string At(params string[] parts) => Path.Combine([Root, .. parts]);

    public static string Schemas => At("schemas");

    public static string Fixtures => At("tests", "fixtures");

    public static string Skills => At("skills");

    public static string Docs => At("docs");

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            // constraints.md and the plugin manifest together identify this repository's root
            // and nothing else on the path to it.
            bool isRoot = File.Exists(Path.Combine(dir.FullName, "constraints.md"))
                && File.Exists(Path.Combine(dir.FullName, ".claude-plugin", "plugin.json"));

            if (isRoot)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find the repository root above '{AppContext.BaseDirectory}'. " +
            "Expected an ancestor holding both constraints.md and .claude-plugin/plugin.json.");
    }
}
