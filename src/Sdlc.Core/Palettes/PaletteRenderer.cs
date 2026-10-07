using System.Globalization;
using System.Text;

namespace Sdlc.Core.Palettes;

/// <summary>
/// Renders a palette for a person, and proposes one for a solution that has none.
/// </summary>
/// <remarks>
/// Both outputs live here for the reason the constraint renderer gives: a skill shows this verbatim, so
/// formatting it at the call site would let hosts drift. The proposal is the same file a reader accepts,
/// printed — <see cref="Palette.Write"/> composes it, so what is proposed and what is read are one
/// definition.
/// </remarks>
public static class PaletteRenderer
{
    private const string Indent = "    ";

    /// <summary>
    /// Renders what the solution's palette holds.
    /// </summary>
    /// <param name="response">The palette, or the absence of one.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ReadPalette.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        if (response.Palette is not { } palette)
        {
            output.AppendLine("No palette.");
            output.AppendLine();
            output.AppendLine("This solution has no palette. That is correct for one with no front-end surface");
            output.AppendLine("and no report; nothing here judges which it is. For one that does,");
            output.AppendLine("'sdlc propose palette' prints a skeleton of roles for a person to fill.");
            return output.ToString();
        }

        // Three states, not two. A skeleton nobody has filled and a palette somebody settled on are
        // different answers to "what do we use", and so is a settled palette with a role left empty --
        // which usually means the role does not apply rather than that somebody forgot.
        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Palette: {palette.Slots.Count} roles, {Filled(palette)}. {State(palette)}"));

        output.AppendLine();

        foreach (Slot slot in palette.Slots)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{slot.Role,-18}{slot.Value ?? "--"}"));
        }

        output.AppendLine();
        output.AppendLine("These are roles rather than colour names, so a value may change and the role may");
        output.AppendLine("not. Read a value from here; never copy one into a stylesheet, because the copy is");
        output.AppendLine("the one that goes stale. Nothing here is enforced by a constraint -- a linter that");
        output.AppendLine("refuses a literal outside this file is the layer that can fail a build.");

        if (!palette.Settled)
        {
            output.AppendLine();
            output.AppendLine("Not settled: this is a skeleton rather than an answer. Set \"settled\": true once");
            output.AppendLine("somebody has decided, so an empty role reads as deliberate rather than unfinished.");
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders a palette a person may place, and says what is already there.
    /// </summary>
    /// <remarks>
    /// <para>For a solution with none this is the skeleton. For one that has a palette it proposes
    /// nothing and says so: the roles are declared, the values are a person's to choose, and a tool
    /// inventing a brand's colours would be the one kind of proposal nobody asked for. Filling an empty
    /// role is the only work left, and naming which are empty is the useful half.</para>
    /// <para>Unlike the rule file and the decision record, this one is not protected — so a person may
    /// paste it, and so may an ordinary cycle that declared the file. Nobody is judged against a colour,
    /// which is why no hook rule is needed to keep this honest.</para>
    /// </remarks>
    /// <param name="response">What the solution has today.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Propose(ReadPalette.Response response)
    {
        ArgumentNullException.ThrowIfNull(response);

        StringBuilder output = new();

        if (response.Palette is { } existing)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"This solution already has a palette: {existing.Slots.Count} roles, {Filled(existing)}."));
            output.AppendLine();

            if (existing.Unfilled.Count == 0)
            {
                output.AppendLine("Every role has a value, so there is nothing to propose. A value is changed by a");
                output.AppendLine("cycle that declares the file, like any other declared work.");
                return output.ToString();
            }

            output.AppendLine("Unfilled roles:");
            output.AppendLine();

            foreach (string role in existing.Unfilled)
            {
                output.AppendLine(Indent + role);
            }

            output.AppendLine();
            output.AppendLine("A tool does not choose these. A colour is a brand's or a designer's, and inventing");
            output.AppendLine("one would be the only proposal here nobody asked for -- so this names the gaps and");
            output.AppendLine("stops. If a role does not apply to this solution, leave it empty and set");
            output.AppendLine("\"settled\": true, which is what says the emptiness is deliberate.");
            return output.ToString();
        }

        output.AppendLine("Create " + response.Path + " with this, then fill the values:");
        output.AppendLine();

        foreach (string line in Palette.Write(Palette.Empty())
            .ReplaceLineEndings()
            .Split(Environment.NewLine))
        {
            output.AppendLine(line.Length == 0 ? string.Empty : Indent + line);
        }

        output.AppendLine("Roles, not colour names. A value may change and a role may not, which is what makes");
        output.AppendLine("this survive a rebrand and what lets a stage look one up rather than guess.");
        output.AppendLine();
        output.AppendLine("Add the roles this solution actually needs and delete the ones it does not -- a long");
        output.AppendLine("skeleton is a guess with the authority of a default. Then set \"settled\": true, which");
        output.AppendLine("is what separates a palette somebody chose from one nobody has filled in yet.");
        output.AppendLine();
        output.AppendLine("Import this file rather than copying values out of it: the copy is the one that goes");
        output.AppendLine("stale. Nothing enforces that -- a linter refusing a literal colour outside this file");
        output.AppendLine("is mechanical, so it belongs below a constraint and can fail a build.");

        return output.ToString();
    }

    private static string State(Palette palette) => palette.Settled
        ? "Settled."
        : "A skeleton, not yet settled.";

    private static string Filled(Palette palette)
    {
        int filled = palette.Slots.Count - palette.Unfilled.Count;

        return palette.Unfilled.Count == 0
            ? "all filled"
            : string.Create(CultureInfo.InvariantCulture, $"{filled} filled");
    }
}
