namespace Sdlc.Core.Harvest;

/// <summary>
/// One part of what a completed cycle can give back: each is read by its own skill, answered on its own, and
/// a cycle closes only once every one of them has been answered for it.
/// </summary>
public enum HarvestAspect
{
    /// <summary>The rules its findings argued, for <c>constraints.md</c>.</summary>
    Constraints,

    /// <summary>The choices it settled, for <c>decisions.md</c>.</summary>
    Decisions,

    /// <summary>Its open suggestions: each taken further, declined, or ignored.</summary>
    Suggestions,

    /// <summary>What it built with that the declared stack never mentions, for <c>techstack.md</c>.</summary>
    Stack,

    /// <summary>The areas its plan named, for <c>areas.md</c>.</summary>
    Areas,
}

/// <summary>The aspects' names, as a command line and the ledger write them.</summary>
public static class HarvestAspects
{
    /// <summary>Every aspect, in the order the playbook runs them.</summary>
    public static IReadOnlyList<HarvestAspect> All { get; } = Enum.GetValues<HarvestAspect>();

    /// <summary>An aspect's name: lower case, as typed.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <returns>Its name.</returns>
    public static string NameOf(HarvestAspect aspect) => aspect switch
    {
        HarvestAspect.Constraints => "constraints",
        HarvestAspect.Decisions => "decisions",
        HarvestAspect.Suggestions => "suggestions",
        HarvestAspect.Stack => "stack",
        HarvestAspect.Areas => "areas",
        _ => throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "Unknown aspect."),
    };

    /// <summary>Reads an aspect's name.</summary>
    /// <param name="text">What was typed.</param>
    /// <param name="aspect">The aspect, when it names one.</param>
    /// <returns>True when it does.</returns>
    public static bool TryParse(string? text, out HarvestAspect aspect)
    {
        aspect = default;

        return !string.IsNullOrWhiteSpace(text)
            && !text.Trim().All(char.IsAsciiDigit)
            && Enum.TryParse(text.Trim(), ignoreCase: true, out aspect)
            && Enum.IsDefined(aspect);
    }

    /// <summary>The draft the aspect's records are written to, beside the real file, or null when it writes none.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <returns>The draft's file name.</returns>
    public static string? DraftOf(HarvestAspect aspect) => aspect switch
    {
        HarvestAspect.Constraints => "constraints.draft.md",
        HarvestAspect.Decisions => "decisions.draft.md",
        HarvestAspect.Stack => "techstack.draft.md",
        HarvestAspect.Areas => "areas.draft.md",
        _ => null,
    };

    /// <summary>The words that name every aspect, for a usage line.</summary>
    public static string Listed { get; } = string.Join(", ", All.Select(NameOf));
}
