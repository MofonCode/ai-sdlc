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
/// <para>Four keys and no more. There is deliberately no baseline field: constraints govern a change
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
/// <para><strong><c>integration_branch</c> names where approved plans wait and built cycles land</strong>
/// (D-83). The factory checks it out and targets it, and a cycle's diff is measured from where it
/// diverged from it, so the framework has to know the name. Absent, it reads as
/// <see cref="DefaultIntegrationBranch"/>; a solution with no such branch still measures from its
/// default branch, because the branch named here is tried first and only when it exists.</para>
/// <param name="Platform">Where it is pushed, and whether a pull-request check governs the merge.</param>
/// <param name="IntegrationBranch">The integration branch: where plans wait and built cycles land.</param>
/// <param name="Telemetry">
/// Where production's errors are read from, as <c>appinsights:&lt;application id&gt;</c>, or null when the
/// solution declares none (D-89). Optional, and written only when set: most solutions have no Operate run.
/// The tracker Operate files into is the platform's own, so it needs no key of its own.
/// </param>
public sealed record SolutionDocument(
    SolutionOrigin Origin,
    SolutionStandard Standard,
    Platform Platform = Platform.GitHub,
    string IntegrationBranch = SolutionDocument.DefaultIntegrationBranch,
    string? Telemetry = null)
{
    /// <summary>The integration branch a solution has when it names none. D-83.</summary>
    public const string DefaultIntegrationBranch = "development";

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
    /// <param name="integrationBranch">Where approved plans wait and built cycles land.</param>
    /// <param name="telemetry">Where production's errors are read from, or null to leave the key out.</param>
    /// <returns>One line per key, without the fences.</returns>
    public static string[] Keys(
        SolutionOrigin origin,
        SolutionStandard standard,
        Platform platform,
        string integrationBranch = DefaultIntegrationBranch,
        string? telemetry = null) =>
    [
        "origin: " + NameOf(origin),
        "standard: " + NameOf(standard),
        "platform: " + NameOf(platform),
        "integration_branch: " + integrationBranch,
        .. telemetry is null ? Array.Empty<string>() : ["telemetry: " + telemetry],
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

        // Absent is the default, for the reason the other added keys are: a file written before the key
        // existed must still read. A value git would refuse as a branch name fails, because the factory
        // would check it out and every diff would be measured from it.
        string integration = string.IsNullOrWhiteSpace(shape?.IntegrationBranch)
            ? DefaultIntegrationBranch
            : shape.IntegrationBranch.Trim();

        if (!IsBranchName(integration))
        {
            errors.Add(new Error(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: integration_branch is '{integration}', which git would not take as a branch name.")));
        }

        return errors.Count > 0
            ? Result.Fail<SolutionDocument>(errors)
            : Result.Ok(new SolutionDocument(
                origin,
                standard,
                platform,
                integration,
                string.IsNullOrWhiteSpace(shape?.Telemetry) ? null : shape.Telemetry.Trim()));
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

    /// <summary>
    /// The integration branch a solution on disk names, or the default when it cannot be read.
    /// </summary>
    /// <remarks>
    /// Never a failure. Every caller is measuring a diff, and a missing or malformed solution document is
    /// reported by <c>doctor</c> and by every command that reads the whole file; refusing to measure over
    /// it here would be a second report of the same thing from somewhere that cannot fix it.
    /// </remarks>
    /// <param name="solutionRoot">The solution's root.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The branch name.</returns>
    public static async Task<string> IntegrationBranchOf(string solutionRoot, CancellationToken cancellationToken)
    {
        if (!File.Exists(PathIn(solutionRoot)))
        {
            return DefaultIntegrationBranch;
        }

        Result<SolutionDocument> read = await ReadFile(solutionRoot, cancellationToken).ConfigureAwait(false);
        return read.IsSuccess ? read.Value.IntegrationBranch : DefaultIntegrationBranch;
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

    /// <summary>
    /// Whether git would take a name as a branch, by the rules of <c>git check-ref-format</c> that a
    /// person is likely to break.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>True when it is usable.</returns>
    private static bool IsBranchName(string name) =>
        name.Length > 0
        && !name.StartsWith('-')
        && !name.StartsWith('/')
        && !name.EndsWith('/')
        && !name.EndsWith(".lock", StringComparison.Ordinal)
        && !name.Contains("..", StringComparison.Ordinal)
        && !name.Contains("//", StringComparison.Ordinal)
        && !name.Contains("@{", StringComparison.Ordinal)
        && !name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || "~^:?*[\\".Contains(c, StringComparison.Ordinal));

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

        public string? IntegrationBranch { get; set; }

        public string? Telemetry { get; set; }
    }
}
