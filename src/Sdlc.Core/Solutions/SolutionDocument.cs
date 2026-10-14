using System.Globalization;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using Sdlc.Core.Init;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sdlc.Core.Solutions;

/// <summary>Where a governed solution's code came from.</summary>
/// <remarks>
/// A historical fact, and the one key that must never change. It decided whether a profile's defaults
/// bind until <see cref="SolutionStandard"/> existed, and now it only decides what that key means when
/// it is absent: a scaffolded solution starts on the profile's patterns, an adopted one on its own.
/// </remarks>
public enum SolutionOrigin
{
    /// <summary>The framework created the solution, so its defaults are the solution's defaults.</summary>
    Scaffolded,

    /// <summary>The code existed first. Constraints govern the change, never what predates adoption.</summary>
    Adopted,
}

/// <summary>Which code standard this solution's new work is held to.</summary>
/// <remarks>
/// Separate from <see cref="SolutionOrigin"/> because the two answer different questions and only one of
/// them is allowed to move. The origin is where the code came from, and it is a historical fact. The
/// standard is a decision a team is entitled to revisit, and an adopted solution that has decided to
/// migrate needs somewhere to say so.
/// <para>
/// Deriving it from the origin is what the framework did before this key existed, and it left no way to
/// opt in: a team that wanted the profile's conventions had the origin saying the surrounding code wins
/// for the life of the repository. D-56.
/// </para>
/// </remarks>
public enum SolutionStandard
{
    /// <summary>The surrounding code wins. What an adopted solution starts as.</summary>
    Existing,

    /// <summary>The profile's patterns win, and a plan departing from them has to say why.</summary>
    Profile,
}

/// <summary>
/// What a solution is, where its code came from, and where it is pushed.
/// </summary>
/// <remarks>
/// <para>Three keys and no more. There is deliberately no baseline field: constraints govern a change
/// rather than code that predates adoption, so an existing repository is not in violation on its first
/// day and has nothing to record as forgiven. <c>standard</c> is not one either -- it says which
/// conventions new work follows, and nothing at all about the code already there.</para>
/// <para><strong><c>platform</c> is recorded rather than inferred, and that is the whole reason it is a
/// key.</strong> A solution with no pipeline is either local by choice or a broken install, and nothing
/// on disk tells those apart -- so <c>doctor</c> reported every local repository as missing the one check
/// that is the guarantee, which is true and useless. Absent, it reads as GitHub: that is what every
/// solution adopted before the key existed chose by default, so reading it any other way would silently
/// reclassify them (D-53).</para>
/// </remarks>
/// <param name="Origin">Where the code came from.</param>
/// <param name="Standard">Which conventions this solution's new work follows.</param>
/// <param name="Platform">Where it is pushed, and whether a pull-request check governs the merge.</param>
public sealed record SolutionDocument(
    SolutionOrigin Origin,
    SolutionStandard Standard,
    Platform Platform = Platform.GitHub)
{
    /// <summary>
    /// What the standard is when the key is absent.
    /// </summary>
    /// <remarks>
    /// The origin decided this on its own before the key existed, so deriving the absent value from it
    /// reads every solution adopted before now exactly as it already behaved. Defaulting to
    /// <see cref="SolutionStandard.Profile"/> instead would hold a decade of existing code's successors
    /// to conventions nobody chose, silently, on the next upgrade of the binary.
    /// </remarks>
    /// <param name="origin">Where the code came from.</param>
    /// <returns>The standard that origin implies.</returns>
    public static SolutionStandard DefaultFor(SolutionOrigin origin) => origin switch
    {
        SolutionOrigin.Scaffolded => SolutionStandard.Profile,
        SolutionOrigin.Adopted => SolutionStandard.Existing,
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    /// <summary>
    /// The frontmatter keys, in the order the file carries them.
    /// </summary>
    /// <remarks>
    /// <strong>One home for the block, because three callers write it and a key added to two of them is
    /// a migration that silently does not take.</strong> Adoption writes it, <c>draft solution</c> writes
    /// it beside the real file, and adoption prints it again when a flag asks for a standard the
    /// protected file will not let it change. Each had its own list, with nothing asserting the three
    /// agreed -- the shape F-22 names, one level down.
    /// </remarks>
    /// <param name="origin">Where the code came from.</param>
    /// <param name="standard">Which conventions new work follows.</param>
    /// <param name="platform">Where it is pushed.</param>
    /// <returns>One line per key, without the fences.</returns>
    public static string[] Keys(
        SolutionOrigin origin,
        SolutionStandard standard,
        Platform platform) =>
    [
        "origin: " + NameOf(origin),
        "standard: " + NameOf(standard),
        "platform: " + NameOf(platform),
    ];

    /// <summary>
    /// Reads a <c>solution.md</c>.
    /// </summary>
    /// <param name="text">The file's contents.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The document, or a failure saying what is wrong with it.</returns>
    public static Result<SolutionDocument> Read(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Result<FrontmatterDocument> split = Frontmatter.Read(text, source);
        if (split.IsFailed)
        {
            return split.ToResult<SolutionDocument>();
        }

        Shape? shape;
        try
        {
            shape = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<Shape>(split.Value.Yaml);
        }
        catch (YamlException error)
        {
            return Result.Fail<SolutionDocument>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: the frontmatter is not valid YAML. {error.Message}"));
        }

        List<IError> errors = [];

        if (!TryReadOrigin(shape?.Origin, out SolutionOrigin origin))
        {
            errors.Add(new Error(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: origin is '{shape?.Origin}'. Expected scaffolded or adopted.")));
        }

        // Absent is GitHub, which is what the option defaulted to before this key existed. A missing key
        // is not a malformed one: every solution adopted before now would otherwise fail to read at all.
        if (!TryReadPlatform(shape?.Platform, out Platform platform))
        {
            errors.Add(new Error(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: platform is '{shape?.Platform}'. Expected github, azure-devops or local.")));
        }

        // Absent derives from the origin, which decided this alone before the key existed. A value
        // nobody recognises still fails, for the reason a misspelt platform does.
        if (!TryReadStandard(shape?.Standard, origin, out SolutionStandard standard))
        {
            errors.Add(new Error(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: standard is '{shape?.Standard}'. Expected existing or profile.")));
        }

        return errors.Count > 0
            ? Result.Fail<SolutionDocument>(errors)
            : Result.Ok(new SolutionDocument(origin, standard, platform));
    }

    /// <summary>
    /// Reads the solution document of a solution on disk.
    /// </summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The document, or a failure naming the path.</returns>
    public static async Task<Result<SolutionDocument>> ReadFile(
        string solutionRoot,
        CancellationToken cancellationToken)
    {
        string path = PathIn(solutionRoot);

        if (!File.Exists(path))
        {
            return Result.Fail<SolutionDocument>(string.Create(
                CultureInfo.InvariantCulture,
                $"{path} does not exist. This repository holds no governed solution yet; run 'sdlc init'."));
        }

        string text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Read(text, path);
    }

    /// <summary>Where the solution document lives inside a solution.</summary>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <returns>The path, whether or not the file is there.</returns>
    public static string PathIn(string solutionRoot) =>
        Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, Cycle.SolutionFileName);

    /// <summary>How an origin is written in the file.</summary>
    /// <param name="origin">The origin.</param>
    /// <returns>Its name.</returns>
    public static string NameOf(SolutionOrigin origin) => origin switch
    {
        SolutionOrigin.Scaffolded => "scaffolded",
        SolutionOrigin.Adopted => "adopted",
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    /// <summary>How a standard is written in the file.</summary>
    /// <param name="standard">The standard.</param>
    /// <returns>Its name.</returns>
    public static string NameOf(SolutionStandard standard) => standard switch
    {
        SolutionStandard.Existing => "existing",
        SolutionStandard.Profile => "profile",
        _ => throw new ArgumentOutOfRangeException(nameof(standard)),
    };

    /// <summary>How a platform is written in the file.</summary>
    /// <param name="platform">The platform.</param>
    /// <returns>Its name.</returns>
    public static string NameOf(Platform platform) => platform switch
    {
        Platform.GitHub => "github",
        Platform.AzureDevOps => "azure-devops",
        Platform.Local => "local",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };

    /// <summary>
    /// Reads the platform key, treating its absence as GitHub.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="TryReadOrigin"/>, a missing value succeeds. The key was added after solutions
    /// had been adopted, and refusing to read a file written before it existed would make an upgrade of
    /// the binary look like a corrupted repository. A value nobody recognises still fails: that is a typo,
    /// and reading <c>platform: gitub</c> as GitHub would hide it.
    /// </remarks>
    private static bool TryReadPlatform(string? text, out Platform platform)
    {
        platform = Platform.GitHub;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        switch (text.Trim().Replace("-", string.Empty, StringComparison.Ordinal))
        {
            case string github when github.Equals("github", StringComparison.OrdinalIgnoreCase):
                platform = Platform.GitHub;
                return true;
            case string ado when ado.Equals("azuredevops", StringComparison.OrdinalIgnoreCase)
                || ado.Equals("ado", StringComparison.OrdinalIgnoreCase):
                platform = Platform.AzureDevOps;
                return true;
            case string local when local.Equals("local", StringComparison.OrdinalIgnoreCase):
                platform = Platform.Local;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Reads the standard key, deriving its absence from the origin.
    /// </summary>
    /// <remarks>
    /// Absent succeeds, on the same terms as <see cref="TryReadPlatform"/>: the key was added after
    /// solutions had been adopted, and refusing to read a file written before it existed would make an
    /// upgrade of the binary look like a corrupted repository.
    /// </remarks>
    /// <param name="text">The value, or null when the key is absent.</param>
    /// <param name="origin">Where the code came from, which decides what absent means.</param>
    /// <param name="standard">The standard read.</param>
    /// <returns>True unless a value was given that is neither of the two.</returns>
    private static bool TryReadStandard(string? text, SolutionOrigin origin, out SolutionStandard standard)
    {
        standard = DefaultFor(origin);

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        switch (text.Trim())
        {
            case string existing when existing.Equals("existing", StringComparison.OrdinalIgnoreCase):
                standard = SolutionStandard.Existing;
                return true;
            case string profile when profile.Equals("profile", StringComparison.OrdinalIgnoreCase):
                standard = SolutionStandard.Profile;
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadOrigin(string? text, out SolutionOrigin origin)
    {
        origin = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        switch (text.Trim())
        {
            case string scaffolded when scaffolded.Equals("scaffolded", StringComparison.OrdinalIgnoreCase):
                origin = SolutionOrigin.Scaffolded;
                return true;
            case string adopted when adopted.Equals("adopted", StringComparison.OrdinalIgnoreCase):
                origin = SolutionOrigin.Adopted;
                return true;
            default:
                return false;
        }
    }

    // CA1812 does not fire: it is in NoWarn repository-wide, so the attribute that stood
    // here suppressed nothing and its justification -- "scoped here rather than suppressed
    // repository-wide" -- asserted the inverse of what the build does. Kept as a comment,
    // which reads the same and claims nothing: this shape is constructed by YamlDotNet
    // through reflection, which is why it looks dead. F-18.
    private sealed class Shape
    {
        public string? Origin { get; set; }

        public string? Standard { get; set; }

        public string? Platform { get; set; }
    }
}
