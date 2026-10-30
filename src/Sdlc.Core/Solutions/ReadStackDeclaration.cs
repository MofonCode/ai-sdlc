using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Solutions;

/// <summary>
/// Reads what a solution declares it is built from, and compares the build settings it takes against
/// what is on disk. Reads; never writes.
/// </summary>
/// <remarks>
/// <para><strong>What a plan runs before it chooses a shape</strong>, beside <c>sdlc decisions</c> and
/// <c>sdlc theme</c>: the rows <c>CON_15</c> judges it against, the stack rules switched on, and the
/// one comparison a file listing can decide -- which build settings the stack declares and which are
/// actually there. Whether the code uses what the defaults declare needs reading the code, and stays
/// with the stage that reads it.</para>
///
/// <para><strong>It reports a gap and closes none.</strong> A declared file that is missing arrives
/// through a cycle whose approved plan declares it, never on the strength of a row, because the file
/// arriving is the moment a build can start failing (D-77). So this is a comparison and the remedy is a
/// plan.</para>
///
/// <para><strong>Present means a file of that name exists, and nothing more.</strong> Each is created
/// once and is a person's to amend, and a repository may have had its own before the framework did, so
/// there is no version of "the framework's" to compare against and this does not pretend to one.</para>
/// </remarks>
public static class ReadStackDeclaration
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddReadStackDeclarationRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what this solution declares it is built from.</summary>
    public sealed record Query : IRequest<Response>;

    /// <summary>What a declared stack says about one build-settings file.</summary>
    public enum Declaration
    {
        /// <summary>The stack has no row for it, so it says nothing either way.</summary>
        NoRow,

        /// <summary>A row names it and switches it off: offered, and declined.</summary>
        Declined,

        /// <summary>A row names it and switches it on.</summary>
        Declared,
    }

    /// <summary>One build-settings file: what the stack says, and whether it is there.</summary>
    /// <param name="Setting">The file.</param>
    /// <param name="Declaration">What the declared stack says about it.</param>
    /// <param name="Present">Whether a file of that name is on disk.</param>
    public sealed record SettingState(
        BuildSettings.Setting Setting,
        Declaration Declaration,
        bool Present)
    {
        /// <summary>Whether the stack takes this file and it is not there yet.</summary>
        public bool Missing => Declaration == Declaration.Declared && !Present;
    }

    /// <summary>
    /// What the declared stack holds.
    /// </summary>
    /// <param name="Path">Where it lives, whether or not it is there yet.</param>
    /// <param name="Placed">Whether a person has placed one.</param>
    /// <param name="Defaults">The declared defaults, in document order.</param>
    /// <param name="Rules">The stack-rule rows, in document order.</param>
    /// <param name="Settings">Every build-settings file the framework ships, with its state.</param>
    /// <param name="Unknown">Build-settings rows naming a file the framework does not ship.</param>
    /// <param name="Unmentioned">What this release ships that the stack has no row for (D-93).</param>
    public sealed record Response(
        string Path,
        bool Placed,
        IReadOnlyList<StackTable.Row> Defaults,
        IReadOnlyList<StackRules.Row> Rules,
        IReadOnlyList<SettingState> Settings,
        IReadOnlyList<string> Unknown,
        StackCoverage.Unmentioned Unmentioned)
    {
        /// <summary>The files the stack takes that are not on disk, in the order they are written.</summary>
        public IReadOnlyList<BuildSettings.Setting> Gap =>
            [.. Settings.Where(state => state.Missing).Select(state => state.Setting)];
    }

    /// <param name="solution">The root of the solution being read.</param>
    private sealed class Handler(SolutionRoot solution) : IRequestHandler<Query, Response>
    {
        /// <inheritdoc />
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string path = Path.Combine(solution.Path, Cycle.SdlcDirectoryName, Cycle.TechStackFileName);
            bool placed = File.Exists(path);

            string text = placed
                ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
                : string.Empty;

            IReadOnlyList<BuildSettings.Row> rows = BuildSettings.Rows(text);

            return Result.Ok(new Response(
                path,
                placed,
                StackTable.Rows(text),
                StackRules.Rows(text),
                [.. BuildSettings.All.Select(setting => new SettingState(
                    setting,
                    DeclarationOf(setting, rows),
                    File.Exists(Path.Combine(solution.Path, setting.OnDisk))))],
                [.. rows.Where(row => BuildSettings.Find(row.File) is null).Select(row => row.File)],
                StackCoverage.Of(text)));
        }

        /// <summary>What the rows say about one file.</summary>
        /// <remarks>
        /// The last row naming it wins, which is what a person editing a table expects of a line they
        /// added below an older one -- and it means a duplicate cannot switch a file on that the line
        /// they are looking at switches off.
        /// </remarks>
        private static Declaration DeclarationOf(
            BuildSettings.Setting setting,
            IReadOnlyList<BuildSettings.Row> rows)
        {
            BuildSettings.Row? row = rows.LastOrDefault(candidate =>
                BuildSettings.Find(candidate.File) == setting);

            return row is null
                ? Declaration.NoRow
                : row.On ? Declaration.Declared : Declaration.Declined;
        }
    }
}
