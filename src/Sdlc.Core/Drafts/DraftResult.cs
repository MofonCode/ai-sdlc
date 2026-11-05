namespace Sdlc.Core.Drafts;

/// <summary>
/// What a drafter produced: text to place, or what stands in the way.
/// </summary>
/// <remarks>
/// Shared by the drafters whose output is one block of text. The approval drafter has a result of its
/// own, because it carries the cycle's state for the renderer to show alongside the entry.
/// <para>
/// Every one of these exists because a tool never writes the file it describes. A drafter that could
/// also write it would need a flag saying when, and that flag is the exception the hook rules are
/// written to have nowhere to live.
/// </para>
/// </remarks>
/// <param name="Entry">The text to place, when there is any.</param>
/// <param name="Instructions">What to do with it, or what is in the way.</param>
/// <param name="MayDraft">Whether text was produced.</param>
public sealed record DraftResult(string? Entry, string Instructions, bool MayDraft)
{
    /// <summary>A drafter that produced text.</summary>
    /// <param name="entry">The text to place.</param>
    /// <param name="instructions">What to do with it.</param>
    /// <returns>The result.</returns>
    public static DraftResult Drafted(string entry, string instructions) =>
        new(entry, instructions, MayDraft: true);

    /// <summary>A drafter that produced nothing, and why.</summary>
    /// <param name="why">What stands in the way.</param>
    /// <returns>The result.</returns>
    public static DraftResult Refused(string why) => new(Entry: null, why, MayDraft: false);
}
