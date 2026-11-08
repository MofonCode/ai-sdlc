using System.Globalization;
using System.Text;

namespace Sdlc.Core.Areas;

/// <summary>Renders the areas as text.</summary>
public static class AreasRenderer
{
    /// <summary>Renders what each area covers and carries, and what is wrong.</summary>
    /// <param name="response">The areas.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(ReadAreas.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder text = new();

        if (response.Areas.Count == 0)
        {
            text.AppendLine(response.Recorded ? "No areas defined." : "No areas file.");
            text.AppendLine();
            text.AppendLine("Every rule and decision applies everywhere, so every reader is handed each one in full. Areas");
            text.AppendLine("are named from the plans' '## Areas' sections: once three cycles propose one over the same");
            text.AppendLine("files, '/sdlc:mine' proposes it, and a person places .sdlc/areas.md.");
            return text.ToString();
        }

        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Areas: {response.Areas.Count}. Entries with no area, which every reader gets in full: {response.Global}."));

        foreach (ReadAreas.Standing standing in response.Areas)
        {
            text.AppendLine();
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {standing.Area.Name,-20} {standing.Files} file{(standing.Files == 1 ? string.Empty : "s")}{(standing.Files == 0 ? "  -- COVERS NOTHING: a round refuses until this is fixed" : string.Empty)}"));
            text.AppendLine("    is       " + standing.Area.Is);
            text.AppendLine("    files    " + string.Join(", ", standing.Area.Files));
            text.AppendLine("    carried  " + (standing.Entries.Count == 0 ? "by nothing yet" : string.Join(", ", standing.Entries)));
        }

        if (response.Unknowns.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Named by an entry and defined nowhere -- shown in full everywhere until fixed:");

            foreach (ReadAreas.Unknown unknown in response.Unknowns)
            {
                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {unknown.Entry} names '{unknown.Name}'"));
            }
        }

        return text.ToString();
    }
}
