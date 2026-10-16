using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdlc.Core.Init;

/// <summary>
/// Which release last wrote a solution's framework-owned files, recorded in the one of them a person reads.
/// </summary>
/// <remarks>
/// <para><strong>Why there is a stamp at all.</strong> Adoption's update mode refreshes the hook
/// configuration, the pipeline and <c>.sdlc/README.md</c> -- but only when a person runs it, and nothing said
/// when somebody should. A binary upgraded on a machine left every solution on it with the previous release's
/// hooks and a pipeline pinning the previous release's tool, and no command could tell (D-93).</para>
///
/// <para><strong>In <c>.sdlc/README.md</c>, and only there.</strong> Adoption rewrites every file it owns
/// in one run, so one stamp speaks for all of them. It is the file that is the framework's outright and is
/// markdown; a hook configuration is a host's JSON with a schema of its own, and the pipeline already pins
/// the version it installs.</para>
///
/// <para><strong>Never in a person's file.</strong> Adoption creates <c>solution.md</c> once and never
/// rewrites it, so a version there would be frozen at adoption while reading as current. And a person's
/// stack needs no version to say what is new: a row that says no is how a release knows not to ask.</para>
///
/// <para><strong>Written and read here,</strong> so the line and the pattern that finds it cannot drift
/// apart.</para>
/// </remarks>
public static class FrameworkStamp
{
    private static readonly Regex _pattern = new(
        @"^Written by `sdlc (?<version>[^`\s]+)`",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline,
        TimeSpan.FromSeconds(1));

    /// <summary>The line adoption writes.</summary>
    /// <param name="version">The release writing it.</param>
    /// <returns>The line, with what to do when it disagrees with the binary.</returns>
    public static string Line(string version) => string.Create(
        CultureInfo.InvariantCulture,
        $"Written by `sdlc {version}`. After upgrading the binary, a person runs `sdlc init` here to refresh what the framework owns, and `sdlc doctor` says when that is due.");

    /// <summary>The release a README says wrote it.</summary>
    /// <param name="readme">The README's text.</param>
    /// <returns>The version, or null when the file carries no stamp -- written before stamps existed.</returns>
    public static string? Read(string readme)
    {
        ArgumentNullException.ThrowIfNull(readme);

        Match match = _pattern.Match(readme.ReplaceLineEndings("\n"));

        return match.Success ? match.Groups["version"].Value : null;
    }

    /// <summary>
    /// How a stamped release stands against the running one.
    /// </summary>
    /// <remarks>
    /// Compared on the numeric part, with any prerelease label set aside, and as unequal text where either
    /// will not parse -- a build that says <c>unknown</c> is never reported as current.
    /// </remarks>
    /// <param name="stamped">What the README says.</param>
    /// <param name="running">What this binary is.</param>
    /// <returns>Negative when the files are older, zero when they agree, positive when they are newer.</returns>
    public static int Compare(string stamped, string running)
    {
        ArgumentNullException.ThrowIfNull(stamped);
        ArgumentNullException.ThrowIfNull(running);

        if (string.Equals(stamped, running, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return Version.TryParse(stamped.Split('-')[0], out Version? left)
            && Version.TryParse(running.Split('-')[0], out Version? right)
            && left != right
            ? left.CompareTo(right)
            : -1;
    }
}
