using System.Globalization;
using System.Text;

namespace Sdlc.Core.Solutions;

/// <summary>Renders how a solution is set to run, one line per value.</summary>
public static class SettingsRenderer
{
    private const string Indent = "    ";

    /// <summary>
    /// Renders the view.
    /// </summary>
    /// <remarks>
    /// Every line says where its value came from, so a default never reads as a blank, and how it is changed,
    /// which is always a command or a person's file and never this command: it has no option that writes.
    /// </remarks>
    /// <param name="response">What was read.</param>
    /// <returns>The text to print.</returns>
    public static string Render(ReadSettings.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        output.AppendLine(response.Release is { } release
            ? string.Create(CultureInfo.InvariantCulture, $"Settings, under release {release} as this solution pins it.")
            : "Settings, under whichever release runs: this solution pins none.");
        output.AppendLine();

        Group(output, response, ReadSettings.SettingGroup.Solution, "How this solution runs, from .sdlc/solution.md", "change with");
        Group(output, response, ReadSettings.SettingGroup.Elsewhere, "What binds it, each shown by its own command", "read with");
        Group(output, response, ReadSettings.SettingGroup.Machine, "Set on this machine, and stated nowhere in the repository", "effect");
        Group(output, response, ReadSettings.SettingGroup.Fixed, "Fixed by this release, each by a decision", "decided in");

        if (!response.Complete)
        {
            output.AppendLine("solution.md cannot be read, so its values are shown as unknown rather than as their defaults:");
            output.AppendLine();

            foreach (string problem in response.Problems)
            {
                output.AppendLine(Indent + problem);
            }

            output.AppendLine();
        }

        output.AppendLine("Nothing here writes. A value in solution.md is changed in the draft 'sdlc draft solution'");
        output.AppendLine("prints, renamed into place by a person; a fixed value is changed by changing the decision.");

        return output.ToString();
    }

    private static void Group(
        StringBuilder output,
        ReadSettings.Response response,
        ReadSettings.SettingGroup group,
        string heading,
        string changeLabel)
    {
        List<ReadSettings.Setting> settings = [.. response.Settings.Where(setting => setting.Group == group)];

        if (settings.Count == 0)
        {
            return;
        }

        int name = settings.Max(setting => setting.Name.Length) + 2;
        int value = Math.Min(settings.Max(setting => setting.Value.Length), 44) + 2;

        output.AppendLine(heading);

        foreach (ReadSettings.Setting setting in settings)
        {
            string source = Source(setting.Source);
            string tail = source.Length == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{changeLabel} {setting.Change}")
                : string.Create(CultureInfo.InvariantCulture, $"{source,-13}{changeLabel} {setting.Change}");

            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{setting.Name.PadRight(name)}{setting.Value.PadRight(value)}{tail}"));
        }

        output.AppendLine();
    }

    /// <summary>How a source reads in the view. Empty where the group heading already says it.</summary>
    private static string Source(ReadSettings.SettingSource source) => source switch
    {
        ReadSettings.SettingSource.Declared => "declared",
        ReadSettings.SettingSource.Default => "default",
        ReadSettings.SettingSource.NotDeclared => "not set",
        ReadSettings.SettingSource.Unreadable => "unreadable",
        _ => string.Empty,
    };
}
