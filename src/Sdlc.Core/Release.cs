using System.Reflection;

namespace Sdlc.Core;

/// <summary>
/// Which release of the gate this is.
/// </summary>
/// <remarks>
/// It lives here rather than beside <c>sdlc version</c> because two things need it and they are in
/// different assemblies: the version report a person reads, and the pinned <c>dotnet tool install</c>
/// step in the pipelines <c>Templates.Pipeline</c> writes, so a pull request checks the chain with the
/// release it was written against (D-37). Two readers of one stamped attribute would eventually
/// disagree about what "the version" means -- a prerelease label kept in one and trimmed in the other
/// is enough -- and the one a pipeline pins by is not the one a person would check.
/// <para>
/// This said "the tool manifest <c>init</c> writes" until F-32. D-37 decided the opposite and says so:
/// no tool manifest is written into an adopted solution. The reasoning above survives the correction
/// unchanged; only the second consumer was named wrongly.
/// </para>
/// </remarks>
public static class Release
{
    /// <summary>
    /// The release version, as the build stamped it.
    /// </summary>
    /// <remarks>
    /// The informational version rather than the assembly version, because that is the one carrying
    /// the <c>VersionPrefix</c> the props set and any prerelease label with it. The SDK appends
    /// <c>+&lt;commit sha&gt;</c>, which is noise to a person asking what they have installed and is
    /// not a version NuGet would restore, so it is trimmed.
    /// </remarks>
    public static string Version { get; } = Read();

    private static string Read()
    {
        string? stamped = typeof(Release).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(stamped))
        {
            // Reachable only from a build that stripped the attribute. Saying so beats printing
            // nothing where a version belongs, which would read as a broken install.
            return "unknown";
        }

        int plus = stamped.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? stamped.Trim() : stamped[..plus].Trim();
    }
}
