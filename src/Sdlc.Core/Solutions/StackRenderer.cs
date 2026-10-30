using System.Globalization;
using System.Text;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Renders what a solution declares it is built from, and where its build settings stand.
/// </summary>
/// <remarks>
/// In the binary for the reason the constraint renderer gives: a skill shows this verbatim, so
/// formatting it at the call site would let hosts drift. It ends with the gap rather than with the
/// tables, because the tables are a person's own file and the gap is the one thing here they could not
/// have read off it. What the stack does not mention comes just before the gap, for the same reason.
/// </remarks>
public static class StackRenderer
{
    private const string Indent = "    ";

    /// <summary>
    /// Renders a declared stack and the comparison against disk.
    /// </summary>
    /// <param name="response">What the declared stack holds.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ReadStackDeclaration.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        if (!response.Placed)
        {
            output.AppendLine("No declared stack.");
            output.AppendLine();
            output.AppendLine("Nothing says what this solution is built from, and no cycle opens until something");
            output.AppendLine("does. 'sdlc draft techstack' writes a draft of the framework's default, or of nothing");
            output.AppendLine("with --empty, and a person renames it into place.");
            return output.ToString();
        }

        int on = response.Rules.Count(rule => rule.On);

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Declared stack: {response.Defaults.Count} defaults, {on} of {response.Rules.Count} listed stack rules on."));

        output.AppendLine();
        output.AppendLine("Defaults");

        if (response.Defaults.Count == 0)
        {
            output.AppendLine(Indent + "none -- nothing is pinned, so each plan chooses freely");
        }

        int width = response.Defaults.Count == 0 ? 0 : response.Defaults.Max(row => row.Concern.Length) + 2;

        foreach (StackTable.Row row in response.Defaults)
        {
            output.AppendLine(Indent + row.Concern.PadRight(width) + (row.Choice.Length == 0 ? "--" : row.Choice));
        }

        output.AppendLine();
        output.AppendLine("Stack rules");

        if (response.Rules.Count == 0)
        {
            output.AppendLine(Indent + "none listed, so none applies");
        }

        foreach (StackRules.Row rule in response.Rules)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{rule.Id,-10}{(rule.On ? "on" : "off"),-5}{rule.Title}"));
        }

        output.AppendLine();
        output.AppendLine("Build settings");

        int file = response.Settings.Max(state => state.Setting.Path.Length) + 2;

        foreach (ReadStackDeclaration.SettingState state in response.Settings)
        {
            output.AppendLine(Indent + state.Setting.Path.PadRight(file) + Describe(state));
        }

        foreach (string unknown in response.Unknown)
        {
            output.AppendLine(Indent + unknown.PadRight(file) + "a row names it, and this release ships no such file");
        }

        output.AppendLine();

        // Before the gap, because it is the half a release changes: the build settings compare the
        // stack to the disk, and this compares it to what the binary ships.
        if (StackCoverage.Describe(response.Unmentioned, Indent) is { Count: > 0 } unmentioned)
        {
            foreach (string line in unmentioned)
            {
                output.AppendLine(line);
            }

            output.AppendLine();
        }

        foreach (string line in Gap(response))
        {
            output.AppendLine(line);
        }

        return output.ToString();
    }

    /// <summary>One file's state, in the words a plan acts on.</summary>
    /// <param name="state">What the stack says and what is on disk.</param>
    /// <returns>The phrase that follows the path.</returns>
    private static string Describe(ReadStackDeclaration.SettingState state) => (state.Declaration, state.Present) switch
    {
        (ReadStackDeclaration.Declaration.Declared, true) => "declared, present",
        (ReadStackDeclaration.Declaration.Declared, false) => "declared, MISSING",
        (ReadStackDeclaration.Declaration.Declined, true) => "declined, and one is here -- this repository's own",
        (ReadStackDeclaration.Declaration.Declined, false) => "declined",
        (_, true) => "no row, and one is here",
        _ => "no row",
    };

    /// <summary>
    /// What stands between the declaration and the repository, and what closes it.
    /// </summary>
    /// <remarks>
    /// Said in full when there is a gap, because the obvious reading of "declared, missing" is that
    /// something should now write the file -- and nothing does on the strength of a row. The remedy is
    /// a plan, and whether this cycle's plan is the one is a question about its intent that a file
    /// listing cannot answer.
    /// </remarks>
    private static string[] Gap(ReadStackDeclaration.Response response)
    {
        IReadOnlyList<BuildSettings.Setting> gap = response.Gap;

        if (gap.Count == 0)
        {
            return
            [
                "Nothing the stack declares is missing. Present means a file of that name is here, not",
                "that it is as the framework wrote it: each is created once and is a person's to amend.",
            ];
        }

        return
        [
            string.Create(
                CultureInfo.InvariantCulture,
                $"Gap: {gap.Count} declared and not here -- {string.Join(", ", gap.Select(setting => setting.Path))}."),
            string.Empty,
            "A row writes nothing. A cycle whose approved plan declares one of these files brings it:",
            "'sdlc scaffold <id>' writes what the plan names and leaves alone what is already there.",
            "Whether that cycle is the one in hand is its intent's to say -- a plan about something",
            "else names the gap and leaves it, because Directory.Build.props makes warnings errors and",
            "closing this is a change with a scope of its own.",
        ];
    }
}
