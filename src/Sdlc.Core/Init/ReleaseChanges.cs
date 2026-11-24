namespace Sdlc.Core.Init;

/// <summary>
/// What each release changed in the files the framework writes into a solution that a later release has to undo: the
/// list <c>sdlc upgrade apply</c> carries, so a solution any number of releases behind is moved by the release it is
/// moving to (D-213).
/// </summary>
/// <remarks>
/// <para><strong>Up only.</strong> The way back is reverting the upgrade's pull request, which restores every file as
/// the older release wrote it; nothing here could, because the binary does not carry older releases' templates.</para>
/// <para><strong>Each change is read from the files, not the version.</strong> A pipeline is moved when it is still
/// at its old path, a step dropped when it is still in the file, so a solution moved partly by hand, or several
/// releases behind, takes exactly what it still needs, and a second run takes nothing. The release orders them and
/// says where each came from.</para>
/// <para><strong>A change may only touch what an upgrade may carry.</strong> A test holds every path here to the
/// paths the upgrade check reads as an upgrade, so a change that reached further would fail there rather than refuse
/// every solution's next upgrade.</para>
/// <para><strong>A change joins the list in the change that makes it</strong>: a pipeline moved or a step a template
/// stops writing. Nothing can test that one was forgotten; a reviewer asks.</para>
/// </remarks>
public static class ReleaseChanges
{
    /// <summary>What a change does.</summary>
    public enum Kind
    {
        /// <summary>A pipeline the framework writes moved: <see cref="Change.What"/> is where it was, <see cref="Change.To"/> where it is.</summary>
        PipelineMoved,

        /// <summary>A template stopped writing a step by this name, so a merge leaves it out (D-212).</summary>
        StepRetired,
    }

    /// <summary>One change.</summary>
    /// <param name="Release">The release line that made it.</param>
    /// <param name="What">The path it moved from, or the step's name.</param>
    /// <param name="Kind">What it does.</param>
    /// <param name="To">Where a moved pipeline lives now; null for a step.</param>
    public sealed record Change(string Release, string What, Kind Kind, string? To = null);

    /// <summary>Every change, oldest release first.</summary>
    /// <remarks>Paths are written out rather than asked of the classes that own them, so nothing here waits on them.</remarks>
    public static IReadOnlyList<Change> All { get; } =
    [
        new("0.21", "install the gate", Kind.StepRetired),
        new("0.21", "install the gate and the agent", Kind.StepRetired),
        new("0.21", "prove the agent signs in and the gate refuses", Kind.StepRetired),
        new("0.21", "prove the gate refuses", Kind.StepRetired),
        new("0.36", "install sdlc, the agent and the plugin", Kind.StepRetired),
        new("0.53", "azure-pipelines.yml", Kind.PipelineMoved, ".azuredevops/sdlc-verify.yml"),
        new("0.53", "azure-pipelines.factory.yml", Kind.PipelineMoved, ".azuredevops/sdlc-factory.yml"),
        new("0.53", "azure-pipelines.upgrade.yml", Kind.PipelineMoved, ".azuredevops/sdlc-upgrade.yml"),
        new("0.55", "build the queue", Kind.StepRetired),
    ];

    /// <summary>Every step name a release retired.</summary>
    public static IReadOnlyList<string> RetiredSteps { get; } =
        [.. All.Where(change => change.Kind == Kind.StepRetired).Select(change => change.What)];

    /// <summary>Every pipeline a release moved, with where it lives now.</summary>
    public static IReadOnlyList<(string Retired, string Current)> MovedPipelines { get; } =
        [.. All.Where(change => change.Kind == Kind.PipelineMoved).Select(change => (change.What, change.To!))];

    /// <summary>The changes made after one release, up to and including another: what a move between them takes.</summary>
    /// <param name="from">The release moved from, or null for none.</param>
    /// <param name="to">The release moved to.</param>
    /// <returns>The changes, oldest first.</returns>
    public static IReadOnlyList<Change> Between(string? from, string to)
    {
        ArgumentNullException.ThrowIfNull(to);

        return
        [
            .. All.Where(change =>
                (from is null || FrameworkStamp.Compare(change.Release + ".0", Line(from) + ".0") > 0)
                && FrameworkStamp.Compare(change.Release + ".0", Line(to) + ".0") <= 0),
        ];
    }

    private static string Line(string version)
    {
        string[] parts = PinnedRelease.Core(version).Split('.');
        return parts.Length >= 2 ? parts[0] + "." + parts[1] : parts[0];
    }
}
