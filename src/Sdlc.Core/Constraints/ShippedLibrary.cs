using System.Globalization;
using System.Reflection;

namespace Sdlc.Core.Constraints;

/// <summary>
/// The global constraint library the binary carries.
/// </summary>
/// <remarks>
/// <strong>Embedded rather than shipped beside the binary, for the reason the scaffold templates are.</strong>
/// <c>Sdlc.Core.csproj</c> makes the argument once already: a file read from a path beside the binary
/// is a file that is missing in exactly the case nobody tests. That is not hypothetical here -- the
/// packed tool shipped no library at all, so a global install could run <c>help</c> and
/// <c>version</c> and nothing else, both generated pipelines installed a gate that could never pass,
/// and the turn-end check failed open. D-41 is the decision.
/// <para>
/// A copied file has no arrangement in which "the library is missing" cannot happen. A resource does:
/// the only way to reach that state is to point <c>SDLC_CONSTRAINTS</c> at nothing, which is a
/// person's own doing and says so.
/// </para>
/// <para>
/// <strong>This is a floor, not a replacement for the file.</strong> A library found beside the gate
/// or above it still wins, which is what keeps the framework's own development loop: editing
/// <c>CONSTRAINTS.md</c> at the repository root and running the build output picks the edit up with no
/// rebuild. The embedded copy is what an installed tool falls through to.
/// </para>
/// </remarks>
internal static class ShippedLibrary
{
    private const string Resource = "Sdlc.Core.CONSTRAINTS.md";

    /// <summary>
    /// What the global tier is recorded as having been read from, when it came from here.
    /// </summary>
    /// <remarks>
    /// Not a path, deliberately, and this is why <see cref="ConstraintDocument"/> calls it a source.
    /// The honest answer for an installed tool would have been a path inside the dotnet tool store,
    /// which tells a reader less than the release does: the question behind "where did these rules
    /// come from" is which version wrote them.
    /// </remarks>
    public static string Source { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"embedded in sdlc {Release.Version}");

    /// <summary>The library's text.</summary>
    /// <returns>The document, as the build embedded it.</returns>
    public static string Text() => _text.Value;

    private static readonly Lazy<string> _text = new(Read);

    private static string Read()
    {
        using Stream? stream = typeof(ShippedLibrary).Assembly.GetManifestResourceStream(Resource);

        if (stream is null)
        {
            // Unreachable unless the build stopped embedding it, which a test asserts against. Said
            // out loud rather than returned as an empty set: a gate judging against no rules at all
            // would pass everything, which is the one failure this file exists to make impossible.
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The constraint library is not embedded in this build. Expected the resource '{Resource}'. "
                + $"Available: {string.Join(", ", typeof(ShippedLibrary).Assembly.GetManifestResourceNames())}"));
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
