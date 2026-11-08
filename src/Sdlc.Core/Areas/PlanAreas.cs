using System.Globalization;
using System.Text.RegularExpressions;
using FluentResults;
using Sdlc.Core.Artifacts;

namespace Sdlc.Core.Areas;

/// <summary>What one line of a plan's <c>## Areas</c> section says.</summary>
internal enum PlanAreaKind
{
    /// <summary>An area <c>areas.md</c> already defines. Optional: the binary works these out itself.</summary>
    Existing,

    /// <summary>A name the plan proposes for files no area covers yet: an area suggestion (D-150).</summary>
    Proposed,

    /// <summary>Files that belong to no area, such as a one-off script.</summary>
    None,
}

/// <summary>One line of a plan's <c>## Areas</c> section.</summary>
/// <param name="Kind">What it says.</param>
/// <param name="Name">The area's name; null for <see cref="PlanAreaKind.None"/>.</param>
/// <param name="Files">The files or globs it lists.</param>
internal sealed record PlanArea(PlanAreaKind Kind, string? Name, IReadOnlyList<string> Files)
{
    /// <summary>Whether a path is one this line lists.</summary>
    /// <param name="path">The path, relative to the solution's root.</param>
    /// <returns>True when a file or glob it lists matches.</returns>
    public bool Lists(string path) => Files.Any(glob => AreaGlob.Matches(glob, path));
}

/// <summary>
/// Reads a plan's <c>## Areas</c> section, and checks that every file it declares has an area (D-150).
/// </summary>
/// <remarks>
/// <para><strong>The plan is where names come from</strong>, because it is the first document that declares
/// files. A declared file either falls in an area <c>areas.md</c> defines, or the plan names what it would call
/// the area (<c>- new trade-import: `src/Brokers/**`</c>), or says it belongs to none (<c>- none:
/// `tools/once.ps1`</c>). Which files are uncovered is a listing, so the binary checks it; what to call them is
/// language, so the plan says it.</para>
/// <para>A proposed name becomes a suggestion of kind <c>area</c> in the cycle's suggestions ledger when the
/// plan's round is recorded. Mining collects them; <c>steer</c> never takes one in, because an area is not
/// something the cycle delivers (D-128).</para>
/// </remarks>
internal static class PlanAreas
{
    /// <summary>The section's heading.</summary>
    public const string Section = "Areas";

    /// <summary>The perspective an area suggestion is recorded under, which no reader is.</summary>
    public const string Perspective = "areas";

    private static readonly Regex _line = new(
        @"^\s*[-*]\s+(?<head>[^:]+?)\s*(?::\s*(?<files>.*))?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>Whether a plan has the section at all.</summary>
    /// <param name="markdown">The plan.</param>
    /// <returns>True when it has a <c>## Areas</c> heading.</returns>
    public static bool Has(string markdown) => ArtifactSections.Read(markdown).ContainsKey(Section);

    /// <summary>Reads the section.</summary>
    /// <param name="markdown">The plan.</param>
    /// <returns>Each line, or what is malformed; empty when the plan has no section.</returns>
    public static Result<IReadOnlyList<PlanArea>> Read(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        if (!ArtifactSections.Read(markdown).TryGetValue(Section, out IReadOnlyList<string>? lines))
        {
            return Result.Ok<IReadOnlyList<PlanArea>>([]);
        }

        List<PlanArea> areas = [];
        List<string> errors = [];

        foreach (string line in lines.Where(line => line.Trim().Length > 0))
        {
            Match match = _line.Match(line);

            if (!match.Success)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"'{line.Trim()}' is not an area line. Write '- <area>', '- new <name>: <files>' or '- none: <files>'."));
                continue;
            }

            string head = match.Groups["head"].Value.Trim().Trim('`');
            List<string> files = Files(match.Groups["files"].Value);
            string[] words = head.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            (PlanAreaKind kind, string? name) = words switch
            {
                ["none"] => (PlanAreaKind.None, (string?)null),
                ["new", var proposed] => (PlanAreaKind.Proposed, proposed.Trim('`')),
                [var existing] => (PlanAreaKind.Existing, existing),
                _ => (PlanAreaKind.Existing, string.Empty),
            };

            if (kind != PlanAreaKind.None && !AreaParser.IsName(name))
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"'{line.Trim()}' names '{name}', which is not an area name: lower case words joined by hyphens."));
                continue;
            }

            if (kind != PlanAreaKind.Existing && files.Count == 0)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"'{line.Trim()}' lists no files. A proposed area or a 'none' says which files it means."));
                continue;
            }

            areas.Add(new PlanArea(kind, name, files));
        }

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<PlanArea>>(errors.ConvertAll(message => new Error(message)))
            : Result.Ok<IReadOnlyList<PlanArea>>(areas);
    }

    /// <summary>
    /// The declared files neither an area <c>areas.md</c> defines nor a line of the section accounts for.
    /// </summary>
    /// <param name="declared">The files the plan declares.</param>
    /// <param name="defined">The areas the solution defines.</param>
    /// <param name="lines">The section's lines.</param>
    /// <returns>The files left without an area; empty when every one has an answer.</returns>
    public static IReadOnlyList<string> Unaccounted(IReadOnlyList<string> declared, IReadOnlyList<Area> defined, IReadOnlyList<PlanArea> lines)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(defined);
        ArgumentNullException.ThrowIfNull(lines);

        return [.. declared.Where(file =>
            !defined.Any(area => area.Covers(file))
            && !lines.Any(line => line.Kind != PlanAreaKind.Existing && line.Lists(file)))];
    }

    private static List<string> Files(string text) =>
        [.. text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(file => file.Trim('`', ' '))
            .Where(file => file.Length > 0)];
}
