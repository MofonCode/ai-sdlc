using System.Reflection;

namespace Sdlc.Tests;

/// <summary>Finds the repository's own files from inside a test run.</summary>
internal static class RepoPaths
{
    /// <summary>
    /// The repository root, found by walking up from the test assembly until the shipped constraint
    /// library is beside it.
    /// </summary>
    /// <remarks>
    /// Walking up rather than taking a relative path from the test project: the output directory's
    /// depth changes with configuration and target framework, and a hard-coded number of parent hops
    /// breaks the first time either does.
    /// </remarks>
    public static string Root { get; } = Find();

    /// <summary>The shipped global constraint library.</summary>
    public static string Library => Path.Combine(Root, "CONSTRAINTS.md");

    /// <summary>This repository's own banned-API list, the one its build reads.</summary>
    public static string BannedSymbols => Path.Combine(Root, "BannedSymbols.txt");

    private static string Find()
    {
        string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "CONSTRAINTS.md")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            "Could not find the repository root: no CONSTRAINTS.md above the test assembly.");
    }
}
