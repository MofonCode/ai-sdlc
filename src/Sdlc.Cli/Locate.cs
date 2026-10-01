using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Sdlc.Cli;

/// <summary>Finds the files the gate needs that are not in the solution it is governing.</summary>
internal static class Locate
{
    /// <summary>The library file's name, at the root of the framework's own tree.</summary>
    public const string LibraryFileName = "CONSTRAINTS.md";

    /// <summary>
    /// The environment variable that overrides where the library is read from. Set by the test
    /// suite, and by anyone running a build of the gate against a library that is not beside it.
    /// </summary>
    public const string LibraryVariable = "SDLC_CONSTRAINTS";

    /// <summary>
    /// Finds the global constraint library.
    /// </summary>
    /// <remarks>
    /// Looks at <see cref="LibraryVariable"/> first, then beside the running assembly, then up the
    /// tree from it — which is what finds the repository copy when the gate is run from its build
    /// output rather than from an installed tool.
    /// </remarks>
    /// <param name="path">The library's path, when it was found.</param>
    /// <param name="why">What was looked at and not found, when it was not.</param>
    /// <returns>True when the library was found.</returns>
    public static bool Library([NotNullWhen(true)] out string? path, [NotNullWhen(false)] out string? why)
    {
        string? configured = Environment.GetEnvironmentVariable(LibraryVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                path = configured;
                why = null;
                return true;
            }

            path = null;
            why = string.Create(
                CultureInfo.InvariantCulture,
                $"{LibraryVariable} is set to '{configured}', and no file is there.");
            return false;
        }

        string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, LibraryFileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                why = null;
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        path = null;
        why = string.Create(
            CultureInfo.InvariantCulture,
            $"Could not find {LibraryFileName} beside the gate or above it. Set {LibraryVariable} to its path.");
        return false;
    }
}
