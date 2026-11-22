using System.Globalization;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Series;

/// <summary>
/// Starts a series: its branch, <c>series/&lt;id&gt;</c>, from the integration branch, in a worktree beside the
/// repository, with its record written and committed there.
/// </summary>
/// <remarks>
/// <para><strong>A series stops at a number of waves or an empty backlog, whichever comes first.</strong> The person
/// states the number when they start it; an open-ended run is spend nobody chose, so there is no default.</para>
/// <para><strong>What it may do follows the approval setting, read once, here.</strong> Under <c>approval: none</c> it
/// plans and builds wave after wave; under <c>approval: required</c> it builds only cycles a person already approved,
/// because planning past that point would break the guarantee the setting exists for.</para>
/// </remarks>
public static class StartSeries
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddStartSeriesRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for a series to start.</summary>
    /// <param name="Waves">How many waves it may run, at least one.</param>
    public sealed record Command(int Waves) : IRequest<Response>;

    /// <summary>The series started.</summary>
    /// <param name="Id">Its id.</param>
    /// <param name="Branch">Its branch.</param>
    /// <param name="Worktree">Its worktree's full path.</param>
    /// <param name="From">The reference it started from.</param>
    /// <param name="Record">Its record, as written.</param>
    public sealed record Response(string Id, string Branch, string Worktree, string From, SeriesRecord Record);

    /// <summary>What a person reads once a series has started: what it will do, and the next command.</summary>
    /// <param name="started">The series.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response started)
    {
        ArgumentNullException.ThrowIfNull(started);

        string does = started.Record.Approval == ApprovalRequirement.None
            ? "approval: none, so it plans and builds every ready successor and parked cycle, wave after wave, asking nobody."
            : "approval: required, so it builds only cycles a person has already approved, in wave order, and plans nothing.";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            Started series {started.Id} on {started.Branch}, from {started.From}, in {started.Worktree}: at most {started.Record.Limit} wave{(started.Record.Limit == 1 ? string.Empty : "s")}.
            The solution declares {does}
            Next: 'sdlc series run {started.Id} --solution "{started.Worktree}"' starts its own process there; it ends in one pull request.

            """);
    }

    private sealed class Handler(SolutionRoot solution, IRepository repository, TimeProvider clock) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Waves < 1)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"A series runs at least one wave, and {request.Waves} is not one. Say how many it may run, as in 'sdlc series start --waves 3': it stops there or when nothing is ready, whichever comes first."));
            }

            string root = solution.Path;

            if (!repository.IsRepository(root))
            {
                return Result.Fail<Response>("A series runs on a branch of its own, and this is not a git repository.");
            }

            string integration = await SolutionDocument.IntegrationBranchOf(root, cancellationToken).ConfigureAwait(false);

            if (new[] { "origin/" + integration, integration }.FirstOrDefault(name => repository.RefExists(root, name)) is not { } from)
            {
                return Result.Fail<Response>(string.Create(
                    CultureInfo.InvariantCulture,
                    $"There is no {integration} branch here or on origin for the series to start from. Fetch it, or create it, and start again."));
            }

            ApprovalRequirement approval = await SolutionDocument.ApprovalOf(root, cancellationToken).ConfigureAwait(false);
            string id = RunBranch.Allocate(clock);
            string branch = RunBranch.NameOf(RunKind.Series, id);
            string worktree = Path.GetFullPath(Path.Combine(root, RunBranch.WorktreePath(root, RunKind.Series, id)));

            Result<string> opened = repository.Git(root, string.Create(
                CultureInfo.InvariantCulture,
                $"worktree add --no-track -b {branch} \"{worktree}\" {from}"));

            if (opened.IsFailed)
            {
                return opened.ToResult<Response>();
            }

            var record = SeriesRecord.Start(id, clock.GetUtcNow(), request.Waves, approval);
            string path = SeriesRecord.PathIn(worktree, id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, record.Write(), cancellationToken).ConfigureAwait(false);

            Result committed = repository.Commit(
                worktree,
                [SeriesRecord.RelativeDirectory(id) + "/" + SeriesRecord.FileName],
                string.Create(CultureInfo.InvariantCulture, $"Start a series of {request.Waves} wave{(request.Waves == 1 ? string.Empty : "s")}"));

            return committed.IsFailed
                ? committed
                : Result.Ok(new Response(id, branch, worktree, from, record));
        }
    }
}
