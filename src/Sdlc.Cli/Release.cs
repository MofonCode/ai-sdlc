using System.Globalization;

namespace Sdlc.Cli;

/// <summary>
/// Which version of the gate this is.
/// </summary>
/// <remarks>
/// The binary and the plugin are two halves of one install, which is why this prints both rather than
/// one. The package carries the plugin and <c>sdlc claude</c> starts a session with it (D-131), but a
/// plugin installed separately is fetched on its own and can disagree -- and a skill calling a verb an
/// older binary does not have is the failure that shape produces.
/// </remarks>
internal static class Release
{
    /// <summary>
    /// The binary's own version.
    /// </summary>
    /// <remarks>
    /// Read from <c>Sdlc.Core</c> rather than from this assembly, because the pipeline templates pin
    /// the version a pull request installs and those templates live in the core. Both assemblies carry
    /// the same <c>VersionPrefix</c>, so reading either would give the same answer today -- and two
    /// readers of one fact is how they stop agreeing later. (This said "the tool manifest init writes"
    /// until F-32; D-37 decided that no manifest is written.)
    /// </remarks>
    public static string Version { get; } = Sdlc.Core.Release.Version;

    /// <summary>What <c>sdlc version</c> writes to standard output.</summary>
    public static string Text { get; } = Compose();

    /// <remarks>
    /// Line endings are normalised because a raw string literal carries the ones its source file was
    /// checked out with, and a binary packed on Windows would otherwise print CRLF on Linux -- where the
    /// pipelines compare the first line to <c>sdlc &lt;version&gt;</c> and a trailing carriage return
    /// makes an agent that already has the right version reinstall it.
    /// </remarks>
    private static string Compose() => Body().ReplaceLineEndings("\n");

    private static string Body() => string.Create(
        CultureInfo.InvariantCulture,
        $"""
        sdlc {Version}

        Two versions, and they are meant to match.

            this binary        {Version}
            the plugin         packed beside this binary, and 'sdlc claude' starts a session with
                               it, so the two cannot disagree. A plugin installed separately and
                               started with plain 'claude' can: 'claude plugin details sdlc@ai-sdlc'
                               reports it, and a number that is not {Version} means one half was
                               installed without the other.


        """);
}
