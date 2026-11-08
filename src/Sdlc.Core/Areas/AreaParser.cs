using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;

namespace Sdlc.Core.Areas;

/// <summary>
/// Reads a solution's <c>areas.md</c>, and the <c>Areas:</c> line a rule or decision carries (D-150).
/// </summary>
/// <remarks>
/// The same shape as the constraint and decision parsers: a heading per entry, bold labelled clauses beneath,
/// every malformed entry reported at once rather than the first.
/// </remarks>
public static class AreaParser
{
    /// <summary>The file a solution's areas live in, inside the framework's directory.</summary>
    public const string FileName = "areas.md";

    private static readonly Regex _name = new(
        @"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _heading = new(
        @"^##\s+(?<name>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex _clause = new(
        @"^\*\*(?<label>Files|Is):\*\*\s*(?<body>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>Whether a name is one an area may take: lower case words joined by hyphens.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True when it is well formed.</returns>
    public static bool IsName(string? name) => name is not null && _name.IsMatch(name);

    /// <summary>Parses every area in a document.</summary>
    /// <param name="markdown">The document's text.</param>
    /// <param name="source">What to call the document in an error message.</param>
    /// <returns>The areas in document order, or a failure naming every malformed one.</returns>
    public static Result<IReadOnlyList<Area>> Parse(string markdown, string source)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        List<Area> areas = [];
        List<string> errors = [];
        string? name = null;
        Dictionary<string, StringBuilder> clauses = new(StringComparer.Ordinal);
        string? open = null;

        void Close()
        {
            if (name is null)
            {
                return;
            }

            string files = clauses.TryGetValue("Files", out StringBuilder? f) ? f.ToString().Trim() : string.Empty;
            string isText = clauses.TryGetValue("Is", out StringBuilder? i) ? i.ToString().Trim() : string.Empty;

            if (!IsName(name))
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{source}: '{name}' is not an area name. A name is lower case words joined by hyphens, such as market-data."));
            }
            else if (areas.Exists(area => area.Name == name))
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{source}: {name} appears more than once."));
            }
            else if (files.Length == 0)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{source}: {name} has no 'Files' line. An area is the files it covers, so it needs at least one glob."));
            }
            else
            {
                areas.Add(new Area(name, Globs(files), isText));
            }

            name = null;
            open = null;
            clauses.Clear();
        }

        foreach (string raw in markdown.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            Match heading = _heading.Match(line);

            if (heading.Success)
            {
                Close();
                name = heading.Groups["name"].Value;
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                Close();
                continue;
            }

            if (name is null)
            {
                continue;
            }

            Match clause = _clause.Match(line);

            if (clause.Success)
            {
                open = clause.Groups["label"].Value;
                clauses[open] = new StringBuilder(clause.Groups["body"].Value.Trim());
            }
            else if (open is not null && line.Trim().Length > 0)
            {
                clauses[open].Append(' ').Append(line.Trim());
            }
            else
            {
                open = null;
            }
        }

        Close();

        return errors.Count > 0
            ? Result.Fail<IReadOnlyList<Area>>(errors.ConvertAll(message => new Error(message)))
            : Result.Ok<IReadOnlyList<Area>>(areas);
    }

    /// <summary>
    /// The area names an entry's <c>Areas:</c> line lists: comma separated, backticks and spaces ignored.
    /// </summary>
    /// <param name="line">The line's body, or null when the entry has none.</param>
    /// <returns>The names, in the order written, without duplicates; empty for a global entry.</returns>
    public static IReadOnlyList<string> Names(string? line) =>
        string.IsNullOrWhiteSpace(line)
            ? []
            : [.. line
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => name.Trim('`', '.', ' '))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)];

    private static List<string> Globs(string files) =>
        [.. files
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(glob => glob.Trim('`', ' '))
            .Where(glob => glob.Length > 0)];
}
