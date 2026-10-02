using System.Globalization;
using System.Reflection;
using Sdlc.Core.Cycles;

namespace Sdlc.Cli;

/// <summary>
/// Which version of the gate this is.
/// </summary>
/// <remarks>
/// Three numbers exist in a governed solution and only two of them move together, which is why this
/// prints all three rather than one. The binary and the plugin are two halves of one install that are
/// fetched separately, so they can disagree -- and a skill calling a verb an older binary does not have
/// is the failure that shape produces. The artifact format is a third thing entirely: it says which
/// envelope a file was written in, so a release that changes no format must not move it.
/// </remarks>
internal static class Release
{
    /// <summary>The binary's own version.</summary>
    public static string Version { get; } = Read();

    /// <summary>What <c>sdlc version</c> writes to standard output.</summary>
    public static string Text { get; } = Compose();

    /// <summary>
    /// Reads the version the build stamped on this assembly.
    /// </summary>
    /// <remarks>
    /// The informational version rather than the assembly version, because that is the one carrying
    /// the <c>VersionPrefix</c> the props set and any prerelease label with it. The SDK appends
    /// <c>+&lt;commit sha&gt;</c> to it, which is noise to a person asking what they have installed.
    /// </remarks>
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

    private static string Compose() => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        sdlc {Version}

        Three versions, and only the first two are meant to match.

            this binary        {Version}
            the plugin         ships separately. 'claude plugin details sdlc@ai-sdlc' reports it,
                               and a number that is not {Version} means one half was installed
                               without the other.
            artifact format    {Cycle.FrameworkVersion}, written into every artifact's framework_version. It
                               tracks the file envelope rather than the release, so it stays put
                               across releases that change no format.


        """);
}
