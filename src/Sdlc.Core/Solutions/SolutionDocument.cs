using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentResults;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Cycles;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sdlc.Core.Solutions;

/// <summary>Where a governed solution's code came from.</summary>
/// <remarks>
/// This decides whether a profile's defaults bind. A scaffolded solution takes the profile's patterns;
/// in an adopted one the existing standard wins, because the profile proposes a shape for new work and
/// does not rewrite the conventions of a codebase that predates it.
/// </remarks>
public enum SolutionOrigin
{
    /// <summary>The framework created the solution, so its defaults are the solution's defaults.</summary>
    Scaffolded,

    /// <summary>The code existed first. Constraints govern the change, never what predates adoption.</summary>
    Adopted,
}

/// <summary>
/// What a solution is, and which profile governs it.
/// </summary>
/// <remarks>
/// Two keys and no more. There is deliberately no baseline field: constraints govern a change rather
/// than code that predates adoption, so an existing repository is not in violation on its first day
/// and has nothing to record as forgiven.
/// </remarks>
/// <param name="Profile">The stack profile package that applies.</param>
/// <param name="Origin">Where the code came from.</param>
public sealed record SolutionDocument(string Profile, SolutionOrigin Origin)
{
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

        if (string.IsNullOrWhiteSpace(shape?.Profile))
        {
            errors.Add(new Error(
                $"{source}: no profile. Nothing can resolve which constraints and templates apply without one."));
        }

        if (!TryReadOrigin(shape?.Origin, out SolutionOrigin origin))
        {
            errors.Add(new Error(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: origin is '{shape?.Origin}'. Expected scaffolded or adopted.")));
        }

        return errors.Count > 0
            ? Result.Fail<SolutionDocument>(errors)
            : Result.Ok(new SolutionDocument(shape!.Profile!.Trim(), origin));
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

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Constructed by YamlDotNet through reflection, which the analyzer cannot see. Scoped here rather than suppressed repository-wide, where it would hide a class that really is dead.")]
    private sealed class Shape
    {
        public string? Profile { get; set; }

        public string? Origin { get; set; }
    }
}
