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
    /// Finds the global constraint library, where a file supplies it.
    /// </summary>
    /// <remarks>
    /// Looks at <see cref="LibraryVariable"/> first, then beside the running assembly, then up the
    /// tree from it — which is what finds the repository copy when the gate is run from its build
    /// output rather than from an installed tool, and is why editing <c>CONSTRAINTS.md</c> here takes
    /// effect without a rebuild.
    /// <para>
    /// <strong>Finding nothing is a success with no path, not a failure.</strong> The binary carries
    /// the library (D-41), so a null path means "use the embedded copy" and every command still runs.
    /// Before that, this returning false stopped the whole container from opening, which is how an
    /// installed tool came to answer nothing but <c>help</c> and <c>version</c>.
    /// </para>
    /// <para>
    /// The one real failure is <see cref="LibraryVariable"/> pointing at a file that is not there.
    /// That is a person's explicit instruction about which rules bind, so falling back to the embedded
    /// copy would judge their work against rules they did not choose, silently. It refuses instead.
    /// </para>
    /// </remarks>
    /// <param name="path">The library's path, or null to use the copy the binary carries.</param>
    /// <param name="why">Why the configured override could not be used, when that is the case.</param>
    /// <returns>False only when <see cref="LibraryVariable"/> names a file that is not there.</returns>
    public static bool Library(out string? path, [NotNullWhen(false)] out string? why)
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

        // No file anywhere, which is the ordinary case for an installed tool. The embedded copy
        // answers, so this is not a refusal and nothing is said: a line of explanation on every
        // command would be noise about the normal case.
        path = null;
        why = null;
        return true;
    }
}
