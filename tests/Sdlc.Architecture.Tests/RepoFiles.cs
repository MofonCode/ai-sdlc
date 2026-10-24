using System.Reflection;
using System.Text.RegularExpressions;

namespace Sdlc.Architecture.Tests;

/// <summary>
/// Reads the repository's own files from inside a test run.
/// </summary>
/// <remarks>
/// These rules assert the shape of the repository, so they read <c>.csproj</c> files off disk rather
/// than inspecting loaded assemblies. Reflection would only see what this project already references,
/// and referencing every project under test would put this project inside the graph it polices.
/// <para>
/// <strong>Locating the root is the part that has already gone wrong once, in the repository this is
/// ported from.</strong> That version looked for the solution file and treated the directory holding
/// it as the root. The solution lived at <c>src/MarketSense.slnx</c>, so the root resolved to
/// <c>src/</c>, the scan then looked in <c>src/src</c> and <c>src/tests</c> — neither of which
/// exists — and a <c>.Where(Directory.Exists)</c> discarded both. The test examined zero files, found
/// zero violations and passed. It had never checked anything, and that was proven by adding a real
/// violation and watching it still report success.
/// </para>
/// <para>
/// So: anchor on two markers that are unambiguously at the root, and throw when a directory to scan
/// is missing rather than filtering it away. A rule with nothing to inspect must fail, not pass.
/// </para>
/// </remarks>
internal static class RepoFiles
{
    private static readonly Regex ProjectReferencePattern =
        new(@"<ProjectReference\s+[^>]*Include\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex PackageReferencePattern =
        new(@"<PackageReference\s+[^>]*Include\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// The directories, relative to the root, that hold projects.
    /// </summary>
    /// <remarks>
    /// <strong>Add to this list in the same change that creates the directory.</strong> A project
    /// directory not listed here is a directory these rules do not police, silently.
    /// <see cref="StructureTests.Every_directory_holding_projects_is_scanned"/> fails when that
    /// happens, so the list cannot rot unnoticed — but fixing it there means editing this line.
    /// </remarks>
    private static readonly string[] ProjectDirectories = ["src", "tests", "tools"];

    /// <summary>Exposed so the coverage guard can check this list against what is on disk.</summary>
    internal static IReadOnlyList<string> ScannedDirectories => ProjectDirectories;

    /// <summary>
    /// The repository root.
    /// </summary>
    /// <remarks>
    /// Two anchors rather than one. The constraint library sits at the root and nowhere else, and
    /// <c>src/</c> beside it is the second condition that stops a partial match from resolving to a
    /// subdirectory — which is exactly how the ported version came to scan nothing.
    /// </remarks>
    /// <returns>The root directory.</returns>
    /// <exception cref="InvalidOperationException">No directory above the test assembly has both.</exception>
    internal static string FindRepoRoot()
    {
        string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        for (int hop = 0; hop < 15 && !string.IsNullOrEmpty(directory); hop++)
        {
            if (File.Exists(Path.Combine(directory, "CONSTRAINTS.md"))
                && Directory.Exists(Path.Combine(directory, "src")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            "Could not find the repository root walking up from "
            + Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            + ". Looked for a directory holding both CONSTRAINTS.md and src/.");
    }

    /// <summary>
    /// Every project file in the repository.
    /// </summary>
    /// <remarks>
    /// Throws when a directory that should exist does not, and when the scan turns up nothing. Both
    /// mean a rule is about to pass without inspecting anything, which is worse than failing because
    /// the build still goes green.
    /// </remarks>
    /// <returns>The absolute paths.</returns>
    /// <exception cref="InvalidOperationException">A listed directory is missing, or nothing was found.</exception>
    internal static IReadOnlyList<string> AllProjectFiles()
    {
        string root = FindRepoRoot();
        List<string> files = [];

        foreach (string sub in ProjectDirectories)
        {
            string directory = Path.Combine(root, sub);

            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException(
                    $"Expected a project directory '{sub}' under {root}, and it is not there. "
                    + "Refusing to scan a partial tree: a rule that inspects half the repository "
                    + "reports success for the half it cannot see. Either the directory was renamed, "
                    + "in which case update RepoFiles.ProjectDirectories, or the root resolved wrongly.");
            }

            // Source only. Build output holds generated project files, and a rule that polices a
            // generated file is a rule that fails for a reason nobody can act on.
            files.AddRange(
                Directory
                    .EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
                    .Where(path => !IsBuildOutput(path)));
        }

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"Found no project files under {root}. A rule that inspects nothing passes "
                + "vacuously, which is the one outcome these tests exist to make impossible.");
        }

        return files;
    }

    /// <summary>
    /// Every C# source file in the repository, excluding build output.
    /// </summary>
    /// <remarks>
    /// Throws on an empty result for the same reason <see cref="AllProjectFiles"/> does. The rule
    /// that reads this is looking for something that should not be there, so an empty scan and a
    /// clean repository are indistinguishable unless one of them fails.
    /// </remarks>
    /// <returns>The absolute paths.</returns>
    /// <exception cref="InvalidOperationException">Nothing was found.</exception>
    internal static IReadOnlyList<string> SourceFiles()
    {
        string root = FindRepoRoot();
        List<string> files = [];

        foreach (string sub in ProjectDirectories)
        {
            string directory = Path.Combine(root, sub);

            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException(
                    $"Expected a project directory '{sub}' under {root}, and it is not there.");
            }

            files.AddRange(
                Directory
                    .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !IsBuildOutput(path)));
        }

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"Found no C# source under {root}. A rule looking for a forbidden construct cannot "
                + "tell an empty scan from a clean repository, so an empty scan fails.");
        }

        return files;
    }

    /// <summary>Whether a path runs through a build-output directory.</summary>
    /// <param name="path">The path to judge.</param>
    /// <returns>True when any segment is <c>obj</c> or <c>bin</c>.</returns>
    internal static bool IsBuildOutput(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A project's name, as a reference to it would spell it.</summary>
    /// <param name="projectPath">The project file.</param>
    /// <returns>The file name without its extension.</returns>
    internal static string ProjectName(string projectPath) =>
        Path.GetFileNameWithoutExtension(projectPath);

    /// <summary>The names of the projects a project references.</summary>
    /// <param name="projectPath">The project file.</param>
    /// <returns>The referenced project names, without paths or extensions.</returns>
    internal static IReadOnlyList<string> ProjectReferences(string projectPath) =>
        [.. ProjectReferencePattern
            .Matches(File.ReadAllText(projectPath))
            .Select(match => Path.GetFileNameWithoutExtension(match.Groups[1].Value.Replace('\\', '/')))];

    /// <summary>The packages a project references.</summary>
    /// <param name="projectPath">The project file.</param>
    /// <returns>The package ids.</returns>
    internal static IReadOnlyList<string> PackageReferences(string projectPath) =>
        [.. PackageReferencePattern
            .Matches(File.ReadAllText(projectPath))
            .Select(match => match.Groups[1].Value)];

    /// <summary>Finds one project by name.</summary>
    /// <param name="name">The project's name, without an extension.</param>
    /// <returns>Its absolute path.</returns>
    /// <exception cref="InvalidOperationException">No project carries that name.</exception>
    internal static string FindProject(string name)
    {
        string? match = AllProjectFiles().FirstOrDefault(path => ProjectName(path) == name);

        return match ?? throw new InvalidOperationException(
            $"Could not find {name}.csproj. If the project was renamed, update the rule that names "
            + "it rather than deleting the rule: one that cannot find its subject is one that no "
            + "longer runs.");
    }
}
