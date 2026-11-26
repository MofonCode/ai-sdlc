using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Whether a solution's build already fails on a vulnerable package, read from its build settings, and the sentence
/// every reader is handed when it does (D-222).
/// </summary>
/// <remarks>
/// <para><strong>The security perspective asks for a vulnerable-package scan, and the scaffolded build already is
/// one.</strong> <c>Directory.Build.props</c> sets <c>NuGetAudit</c> with warnings as errors (§11.4), so a restore
/// that pulls a package with a known vulnerability at the set level fails the build. Nothing told a reader so, and
/// in one governed solution's 23 cycles the security reader asked for the scan's output in every one, at every
/// stage, for a fact the settings file states.</para>
/// <para><strong>Read from the file, never assumed from the profile.</strong> A team may switch the audit off, lower
/// its level, or let an audit warning through; any of those and the build is no longer the audit, so the reader is
/// told nothing and asks as before. A file that does not parse says nothing either.</para>
/// </remarks>
internal static class PackageAudit
{
    // The audit's own warnings, one per severity: let any through and the build passes a vulnerable package.
    private static readonly string[] _codes = ["NU1901", "NU1902", "NU1903", "NU1904"];

    /// <summary>The sentence a reader is handed when the build fails on a vulnerable package.</summary>
    /// <param name="root">The solution's root.</param>
    /// <returns>The sentence, or null when the settings do not make the build the audit.</returns>
    public static string? Fails(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        string path = Path.Combine(root, "Directory.Build.props");

        if (!File.Exists(path))
        {
            return null;
        }

        XDocument document;

        try
        {
            document = XDocument.Load(path);
        }
        catch (XmlException)
        {
            return null;
        }

        string? Last(string name) => document.Descendants()
            .Where(element => element.Name.LocalName == name)
            .Select(element => element.Value.Trim())
            .LastOrDefault();

        bool On(string name) => string.Equals(Last(name), "true", StringComparison.OrdinalIgnoreCase);

        if (!On("NuGetAudit") || !On("TreatWarningsAsErrors"))
        {
            return null;
        }

        string allowed = string.Join(";", document.Descendants()
            .Where(element => element.Name.LocalName is "NoWarn" or "WarningsNotAsErrors")
            .Select(element => element.Value));

        if (_codes.Any(code => allowed.Contains(code, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string level = Last("NuGetAuditLevel") is { Length: > 0 } set ? set : "low";
        string mode = Last("NuGetAuditMode") is { Length: > 0 } declared ? declared : "the SDK's default";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"How this solution audits its dependencies: `Directory.Build.props` sets `NuGetAudit` and treats warnings as errors (mode {mode}, level {level}), so restoring a package with a known vulnerability at {level} or above fails the build. A build that passed is that audit's clean result: no separate vulnerable-package scan is asked for.");
    }
}
