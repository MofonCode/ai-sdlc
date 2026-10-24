using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace Sdlc.Core.Themes;

/// <summary>
/// One semantic slot and what fills it.
/// </summary>
/// <remarks>
/// <strong>A role, never a colour's name.</strong> <c>cancel-button</c> is a role; <c>blue-400</c> is a
/// value wearing a role's clothes. The difference is what survives a rebrand — the values change and the
/// roles do not — and it is what gives a stage something to look up when it builds a page, which a flat
/// list of hex codes does not.
/// </remarks>
/// <param name="Role">The slot, in lowercase-hyphen form.</param>
/// <param name="Value">What fills it, or null when the slot is declared and unfilled.</param>
public sealed record Slot(string Role, string? Value);

/// <summary>
/// A solution's theme: the roles its surfaces use, and the one place their values live.
/// </summary>
/// <remarks>
/// <para><strong>A register, not a decision.</strong> There is no argument in a hex code — just a list
/// that has to stay consistent. The <em>reason</em> for a theme (a brand, a contrast target) is a
/// decision and belongs in <c>decisions.md</c>; what belongs here is the values, so each sits in one
/// place and neither duplicates the other.</para>
///
/// <para><strong>Machine-readable, because one source is the whole point.</strong> A markdown page of hex
/// codes that a stylesheet also holds is two places the truth lives, and the one that goes stale is
/// always the one a tool trusts. JSON is importable by every front-end toolchain, so a stylesheet reads
/// this file rather than copying out of it, and a linter can read the same bytes to refuse a literal
/// colour that is not in here (§2.3 — mechanical, so below a constraint).</para>
///
/// <para><strong>It is not protected, unlike the rule file and the decision record.</strong> Nobody is
/// judged against a colour: no constraint reads this, so an agent that writes it cannot satisfy a rule by
/// doing so. Changing a value is ordinary declared work in a cycle. What <c>propose theme</c> is for is
/// a well-formed one arriving in the first place.</para>
///
/// <para><strong>Blank says which kind of blank it is.</strong> An empty theme could mean <em>not
/// decided yet</em> or <em>deliberately none</em>, and those read identically in a file of empty values.
/// <see cref="Settled"/> is the line that separates them, because guessing wrong in either direction
/// wastes a conversation.</para>
/// </remarks>
/// <param name="Settled">
/// Whether somebody has decided this is the theme. False in a skeleton nobody has filled; true says
/// the values are the answer, including when a solution deliberately has none.
/// </param>
/// <remarks>
/// <strong>One theme, not a set of them.</strong> This is a flat map of role to value, so it cannot
/// express light and dark. That is a known cost of the name (D-71): supporting two would need a level
/// above the roles, which is a change to the file rather than a flag — so the answer to "add dark mode"
/// is that the shape changes, and it is said here rather than discovered by whoever asks.
/// </remarks>
/// <param name="Slots">The roles, in the order the file lists them.</param>
public sealed record Theme(bool Settled, IReadOnlyList<Slot> Slots)
{
    /// <summary>The file a theme lives in, inside the framework's directory.</summary>
    public const string FileName = "theme.json";

    // Camel case on the way out, case-insensitive on the way in. This file is read by a front end as
    // well as by the gate, so it is spelled the way JSON is spelled there rather than the snake case the
    // findings rounds use -- those are the framework's own format and nothing outside reads them. The
    // insensitive read is what keeps a hand-edited file working either way.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    /// <summary>
    /// The roles a skeleton declares.
    /// </summary>
    /// <remarks>
    /// <para>Every one of these is a slot a page cannot be built without naming, which is the admission
    /// test: a role earns its place by being something a surface has to choose, not by being a colour
    /// somebody likes. So there is no <c>accent-2</c> and no <c>blue</c>.</para>
    /// <para>Deliberately short. A long skeleton is a guess with the authority of a default, and the
    /// roles a solution actually needs are the ones it adds. Font families are here because a type choice
    /// drifts exactly the way a colour does and has nowhere else to live.</para>
    /// </remarks>
    public static IReadOnlyList<string> Skeleton { get; } =
    [
        "surface",
        "surface-raised",
        "text",
        "text-muted",
        "primary",
        "primary-text",
        "secondary",
        "button",
        "button-text",
        "confirm",
        "cancel",
        "danger",
        "border",
        "font-body",
        "font-heading",
        "font-mono",
    ];

    /// <summary>Whether every declared role has a value.</summary>
    public bool IsComplete => Slots.Count > 0 && Slots.All(slot => !string.IsNullOrWhiteSpace(slot.Value));

    /// <summary>The roles declared and left empty.</summary>
    public IReadOnlyList<string> Unfilled =>
        [.. Slots.Where(slot => string.IsNullOrWhiteSpace(slot.Value)).Select(slot => slot.Role)];

    /// <summary>
    /// Reads a theme.
    /// </summary>
    /// <param name="json">The file's contents.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The theme, or a failure saying what is wrong with it.</returns>
    public static Result<Theme> Read(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Shape? shape;

        try
        {
            shape = JsonSerializer.Deserialize<Shape>(json, _options);
        }
        catch (JsonException error)
        {
            return Result.Fail<Theme>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: is not valid JSON. {error.Message}"));
        }

        if (shape?.Roles is null)
        {
            return Result.Fail<Theme>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: has no 'roles' object. A theme with no roles declares nothing."));
        }

        List<IError> errors = [];
        List<Slot> slots = [];

        foreach ((string role, string? value) in shape.Roles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                errors.Add(new Error($"{source}: holds a role with no name."));
                continue;
            }

            slots.Add(new Slot(role.Trim(), string.IsNullOrWhiteSpace(value) ? null : value.Trim()));
        }

        return errors.Count > 0
            ? Result.Fail<Theme>(errors)
            : Result.Ok(new Theme(shape.Settled, slots));
    }

    /// <summary>
    /// Writes a theme as the file holds it.
    /// </summary>
    /// <remarks>
    /// Here rather than in the proposer, so what a skeleton looks like and what a reader accepts are one
    /// definition. A proposer composing its own JSON is how the two drift apart.
    /// </remarks>
    /// <param name="theme">The theme to write.</param>
    /// <returns>The JSON, ending in a newline.</returns>
    public static string Write(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        Shape shape = new()
        {
            Settled = theme.Settled,
            Roles = theme.Slots.ToDictionary(slot => slot.Role, slot => slot.Value, StringComparer.Ordinal),
        };

        return JsonSerializer.Serialize(shape, _options) + Environment.NewLine;
    }

    /// <summary>A skeleton: every role in <see cref="Skeleton"/>, no values, and not settled.</summary>
    /// <returns>The theme a solution starts from.</returns>
    public static Theme Empty() =>
        new(Settled: false, [.. Skeleton.Select(role => new Slot(role, null))]);

    // Constructed by System.Text.Json through reflection, which is why it looks unused.
    private sealed class Shape
    {
        public bool Settled { get; set; }

        public Dictionary<string, string?>? Roles { get; set; }
    }
}
