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

    /// <summary>
    /// The global constraints, at the plugin root. A solution session cannot read this path - it is
    /// sandboxed to the solution - so skills receive the contents through a load-time injection
    /// running with the harness's privileges instead. See tools/Get-Constraints.ps1.
    /// </summary>
    public static string Constraints => At("constraints.md");

    public static string Fixtures => At("tests", "fixtures");

    public static string Skills => At("skills");

    public static string Docs => At("docs");

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            // The plugin manifest and the skills directory together identify this repository's
            // root and nothing else on the path to it.
            bool isRoot = File.Exists(Path.Combine(dir.FullName, ".claude-plugin", "plugin.json"))
                && Directory.Exists(Path.Combine(dir.FullName, "skills"));

            if (isRoot)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find the repository root above '{AppContext.BaseDirectory}'. " +
            "Expected an ancestor holding both .claude-plugin/plugin.json and skills/.");
    }
}
