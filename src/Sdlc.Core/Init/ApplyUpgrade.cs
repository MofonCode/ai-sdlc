using System.Diagnostics;
using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Chain;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Init;

/// <summary>
/// Moves a solution to this release and puts the move on a branch of its own: <c>sdlc upgrade apply</c> (D-213).
/// </summary>
/// <remarks>
/// <para><strong>The release being moved to does the moving.</strong> The upgrade pipeline a solution carries was
/// written by the release it is on, so a fault in how an older release moved a solution stayed until a person moved it
/// by hand. Now the pipeline only installs the release asked for and hands over to it, and this is everything after:
/// adoption, the factory's merge, what an upgrade may carry staged and committed, the upgrade check asked of the
/// result, and with <c>--push</c> the branch pushed and the pull request opened. A fix to any of it reaches every
/// solution on its next move.</para>
/// <para><strong>Answered by this machine's release</strong>, as <c>sdlc upgrade</c> is: run locally it moves the
/// solution to the release this machine has, and in the pipeline to the one just installed. A release older than the
/// pin is refused: the way back is reverting the upgrade's pull request.</para>
/// <para><strong>Checked before it leaves.</strong> The commit is held to the same upgrade check the pull request will
/// run, as the same release, and a commit that fails it is left on its branch and never pushed.</para>
/// <para><strong>A solution's own code is not its to touch.</strong> It stages only the paths the upgrade check reads
/// as an upgrade, and names anything else the move left changed.</para>
/// </remarks>
public static class ApplyUpgrade
{
    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplyUpgradeRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Command, Response>, Handler>();
    }

    /// <summary>Asks for the solution to be moved to this release.</summary>
    /// <param name="Push">Push the branch and open the pull request, once the upgrade check holds.</param>
    public sealed record Command(bool Push = false) : IRequest<Response>;

    /// <summary>What the move did.</summary>
    /// <param name="From">The release the integration branch pinned, or null when it pinned none.</param>
    /// <param name="To">This release.</param>
    /// <param name="Branch">The branch the move was committed on, or null when there was nothing to commit.</param>
    /// <param name="Integration">The branch it was taken from and its pull request targets.</param>
    /// <param name="Platform">The platform the solution records.</param>
    /// <param name="Changes">What the releases between the two changed, oldest first.</param>
    /// <param name="Committed">Each path committed.</param>
    /// <param name="LeftOut">Each path the move left changed that an upgrade may not carry, so was not committed.</param>
    /// <param name="Notes">What adoption and the factory's merge said about a file a team also writes in.</param>
    /// <param name="Verdict">The upgrade check on the commit, or null when nothing was committed.</param>
    /// <param name="PullRequest">Where the pull request is, when it was pushed and opened.</param>
    /// <param name="Unpublished">Why it was not pushed or opened, when that was asked and failed.</param>
    public sealed record Response(
        string? From,
        string To,
        string? Branch,
        string Integration,
        Platform Platform,
        IReadOnlyList<ReleaseChanges.Change> Changes,
        IReadOnlyList<string> Committed,
        IReadOnlyList<string> LeftOut,
        IReadOnlyList<string> Notes,
        VerifyUpgrade.Response? Verdict,
        string? PullRequest = null,
        string? Unpublished = null)
    {
        /// <summary>Whether the move committed something and the upgrade check holds for it.</summary>
        public bool Holds => Verdict is { Holds: true };
    }

    /// <summary>The branch a move to a release is committed on.</summary>
    /// <param name="release">The release.</param>
    /// <returns>The branch.</returns>
    public static string BranchFor(string release) => "upgrade/sdlc-" + release;

    private sealed class Handler(SolutionRoot solution, Mediator mediator, IRepository repository) : IRequestHandler<Command, Response>
    {
        public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string root = solution.Path;
            Result<SolutionDocument> read = await SolutionDocument.ReadFile(root, cancellationToken).ConfigureAwait(false);

            if (read.IsFailed)
            {
                return read.ToResult<Response>();
            }

            SolutionDocument document = read.Value;
            string integration = document.IntegrationBranch;

            if (!string.Equals(repository.CurrentBranch(root), integration, StringComparison.Ordinal))
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"An upgrade is taken from {integration}, and this is {repository.CurrentBranch(root) ?? "no branch"}. Switch to {integration}, bring it up to date, and run it again."));
            }

            HashSet<string> carried = new(VerifyUpgrade.Paths, StringComparer.Ordinal);
            List<string> before = [.. repository.UncommittedChanges(root).Select(Slashed)];
            string[] mixed = [.. before.Where(path => carried.Contains(path) || VerifyUpgrade.IsDependencies(path))];

            if (mixed.Length > 0)
            {
                return Result.Fail(
                    "These are changed and not committed, and an upgrade would commit them with its own: "
                    + string.Join(", ", mixed) + ". Commit or stash them first.");
            }

            string? from = PinnedRelease.PinnedIn(repository.ReadAt(root, "HEAD", PinnedRelease.ManifestPath));

            if (from is not null && FrameworkStamp.Compare(from, Release.Version) > 0)
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"This solution pins sdlc {from}, and this is {Release.Version}: an older release does not move a solution back. To go back, revert the pull request that moved it."));
            }

            string? @base = repository.CommitOf(root, "HEAD");

            if (@base is null)
            {
                return Result.Fail("git could not say which commit this is.");
            }

            List<string> notes = [];

            Result<Initialise.Response> adopted = await mediator
                .Send<Initialise.Command, Initialise.Response>(new Initialise.Command(), cancellationToken)
                .ConfigureAwait(false);

            if (adopted.IsFailed)
            {
                return adopted.ToResult<Response>();
            }

            foreach (Initialise.InitAction action in adopted.Value.Actions)
            {
                if (action.Change is Initialise.InitChange.Moved
                    || (action.Change is Initialise.InitChange.Updated && action.Why is { } why && why.StartsWith("Merged", StringComparison.Ordinal))
                    || (action.Change is Initialise.InitChange.Printed && carried.Contains(Slashed(action.Path))))
                {
                    notes.Add(action.Change is Initialise.InitChange.Updated ? action.Why! : Slashed(action.Path) + ": " + action.Why);
                }
            }

            if (document.Platform != Platform.Local && HasFactory(root, document.Platform))
            {
                Result<WriteFactoryPipeline.Response> factory = await mediator
                    .Send<WriteFactoryPipeline.Command, WriteFactoryPipeline.Response>(new WriteFactoryPipeline.Command(Keep: true), cancellationToken)
                    .ConfigureAwait(false);

                if (factory.IsFailed)
                {
                    return factory.ToResult<Response>();
                }

                if (!factory.Value.Kept)
                {
                    notes.Add(WriteFactoryPipeline.Render(factory.Value).Trim().ReplaceLineEndings(" "));
                }
            }

            HashSet<string> already = new(before, StringComparer.Ordinal);
            List<string> changed = [.. repository.UncommittedChanges(root).Select(Slashed).Where(path => !already.Contains(path)).Distinct(StringComparer.Ordinal)];
            List<string> candidates = [.. changed.Where(path => carried.Contains(path)).Order(StringComparer.Ordinal)];
            List<string> leftOut = [.. changed.Where(path => !carried.Contains(path)).Order(StringComparer.Ordinal)];
            IReadOnlyList<ReleaseChanges.Change> changes = ReleaseChanges.Between(from, Release.Version);

            // Staged, then asked what is staged: a file rewritten with other line endings shows as changed and stages
            // as nothing, and only git can say which.
            List<string> committed = [];

            if (candidates.Count > 0)
            {
                Result<string> staged = repository.Git(root, "add -- " + string.Join(' ', candidates.Select(path => "\"" + path + "\"")));

                if (staged.IsFailed)
                {
                    return staged.ToResult<Response>();
                }

                Result<string> listed = repository.Git(root, "diff --cached --name-only --no-renames");
                committed = listed.IsSuccess
                    ? [.. listed.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Slashed).Order(StringComparer.Ordinal)]
                    : candidates;
            }

            if (committed.Count == 0)
            {
                return Result.Ok(new Response(from, Release.Version, null, integration, document.Platform, changes, [], leftOut, notes, null));
            }

            string branch = BranchFor(Release.Version);
            Result<string> switched = repository.Git(root, $"switch -C \"{branch}\"");

            if (switched.IsFailed)
            {
                return switched.ToResult<Response>();
            }

            // What is staged is the commit: staging a path again would refuse one already staged as gone.
            Result<string> commit = repository.Git(root, $"commit -q -m \"{Title(Release.Version)}\"");

            if (commit.IsFailed)
            {
                return Result.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"git could not commit the move on {branch}, which holds it staged: {string.Join(" ", commit.Errors.Select(error => error.Message))}"));
            }

            Result<VerifyUpgrade.Response> checkedUpgrade = await mediator
                .Send<VerifyUpgrade.Query, VerifyUpgrade.Response>(new VerifyUpgrade.Query(committed, @base), cancellationToken)
                .ConfigureAwait(false);

            if (checkedUpgrade.IsFailed)
            {
                return checkedUpgrade.ToResult<Response>();
            }

            var response = new Response(from, Release.Version, branch, integration, document.Platform, changes, committed, leftOut, notes, checkedUpgrade.Value);

            if (!request.Push || !response.Holds)
            {
                return Result.Ok(response);
            }

            Result<string> published = await Publish(root, response, cancellationToken).ConfigureAwait(false);

            return Result.Ok(published.IsSuccess
                ? response with { PullRequest = published.Value }
                : response with { Unpublished = string.Join(" ", published.Errors.Select(error => error.Message)) });
        }

        private async Task<Result<string>> Publish(string root, Response move, CancellationToken cancellationToken)
        {
            if (Release.Version.EndsWith("-local", StringComparison.Ordinal))
            {
                return Result.Fail("a build nobody published opens no pull request (D-135).");
            }

            if (move.Platform == Platform.Local)
            {
                return Result.Fail("a local solution has no platform to open a pull request on.");
            }

            Result<string> pushed = repository.Git(root, $"push --force origin \"{move.Branch}\"");

            if (pushed.IsFailed)
            {
                return pushed;
            }

            foreach ((string tool, string arguments) in PullRequestCommands(move, Environment.GetEnvironmentVariable))
            {
                Result<string> ran = await Run(tool, arguments, root, cancellationToken).ConfigureAwait(false);

                if (ran.IsFailed)
                {
                    return ran;
                }

                // The query asks for an open pull request from the branch: one found is the answer, and the push
                // has brought it up to date.
                bool query = arguments.StartsWith("pr list", StringComparison.Ordinal) || arguments.StartsWith("repos pr list", StringComparison.Ordinal);

                if (query && ran.Value.Trim() is { Length: > 0 } open)
                {
                    return Result.Ok("open already, now carrying sdlc " + move.To + ": " + open);
                }
            }

            return Result.Ok("opened from " + move.Branch + " into " + move.Integration);
        }
    }

    /// <summary>The commands that open the pull request for a move, in order, each with its tool.</summary>
    /// <remarks>
    /// Each platform's own command line, as the pipeline already had: <c>gh</c> with <c>GH_TOKEN</c> on GitHub,
    /// <c>az repos</c> with <c>AZURE_DEVOPS_EXT_PAT</c> on Azure DevOps, which a pipeline run sets and a person's own
    /// sign-in replaces. A pull request already open for the branch is left as it is: the push brought it up to date.
    /// </remarks>
    /// <param name="move">The move.</param>
    /// <param name="environment">Reads a variable: on Azure DevOps, where the run says the repository is.</param>
    /// <returns>The commands.</returns>
    public static IReadOnlyList<(string Tool, string Arguments)> PullRequestCommands(Response move, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(environment);

        string title = Quote(Title(move.To));
        string body = Quote(Body(move));

        if (move.Platform == Platform.GitHub)
        {
            return
            [
                ("gh", $"pr list --head \"{move.Branch}\" --state open --json url --jq .[0].url"),
                ("gh", $"pr create --base \"{move.Integration}\" --head \"{move.Branch}\" --title {title} --body {body}"),
            ];
        }

        string organization = environment("SYSTEM_COLLECTIONURI") is { Length: > 0 } uri ? $" --organization \"{uri}\"" : string.Empty;
        string project = environment("SYSTEM_TEAMPROJECT") is { Length: > 0 } name ? $" --project \"{name}\"" : string.Empty;
        string repository = environment("BUILD_REPOSITORY_NAME") is { Length: > 0 } repo ? $" --repository \"{repo}\"" : string.Empty;
        string where = organization + project + repository;

        return
        [
            ("az", "extension add --name azure-devops --only-show-errors"),
            ("az", $"repos pr list{where} --source-branch \"{move.Branch}\" --status active --query [0].pullRequestId -o tsv"),
            ("az", $"repos pr create{where} --source-branch \"{move.Branch}\" --target-branch \"{move.Integration}\" --title {title} --description {body} --output none"),
        ];
    }

    /// <summary>The title of a move's commit and pull request.</summary>
    /// <param name="release">The release moved to.</param>
    /// <returns>The title.</returns>
    public static string Title(string release) => "Move to sdlc " + release;

    /// <summary>The body of a move's pull request: what it moved, and what a person checks before merging.</summary>
    /// <param name="move">The move.</param>
    /// <returns>The body.</returns>
    public static string Body(Response move)
    {
        ArgumentNullException.ThrowIfNull(move);

        StringBuilder body = new();
        body.Append(CultureInfo.InvariantCulture, $"Moves this solution from sdlc {move.From ?? "(none)"} to {move.To}: the pin, and what adoption and 'sdlc factory' wrote as {move.To}. The pull-request check reads it as an upgrade.");

        foreach (string note in move.Notes)
        {
            body.Append(' ').Append(note);
        }

        body.Append(" What the release asks of a solution: https://github.com/MofonCode/ai-sdlc/blob/main/CHANGELOG.md. Open cycles keep the release their branch pins. To go back, revert this pull request.");
        return body.ToString();
    }

    /// <summary>Renders a move for a person or a pipeline log.</summary>
    /// <param name="move">The move.</param>
    /// <returns>The text, ending in a newline.</returns>
    public static string Render(Response move)
    {
        ArgumentNullException.ThrowIfNull(move);

        StringBuilder output = new();
        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Moving from sdlc {move.From ?? "(none)"} to {move.To}."));

        foreach (string note in move.Notes)
        {
            output.AppendLine("  " + note);
        }

        if (move.LeftOut.Count > 0)
        {
            output.AppendLine("  Left out, because an upgrade may not carry it: " + string.Join(", ", move.LeftOut) + ".");
        }

        if (move.Branch is null)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"sdlc {move.To} writes nothing new here."));
            return output.ToString();
        }

        output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Committed {move.Committed.Count} file(s) on {move.Branch}."));

        if (move.Verdict is { } verdict)
        {
            output.Append(VerifyUpgrade.Render(verdict));
        }

        if (!move.Holds)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Not pushed: the pull request would be refused. The commit is on {move.Branch} to look at."));
        }
        else if (move.PullRequest is { } opened)
        {
            output.AppendLine("The pull request: " + opened);
        }
        else if (move.Unpublished is { } why)
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Committed, but not pushed or opened: {why} Push {move.Branch} and open its pull request into {move.Integration} by hand."));
        }
        else
        {
            output.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Push {move.Branch} and open its pull request into {move.Integration}, or run this again with --push."));
        }

        return output.ToString();
    }

    private static bool HasFactory(string root, Platform platform) =>
        File.Exists(Path.Combine(root, FactoryPipeline.PathOf(platform)))
        || (Initialise.RetiredPathOf(FactoryPipeline.PathOf(platform)) is { } retired && File.Exists(Path.Combine(root, retired)));

    private static string Slashed(string path) => path.Replace('\\', '/');

    private static string Quote(string text) => "\"" + text.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static async Task<Result<string>> Run(string tool, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(ToolPath.Resolve(tool), arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        try
        {
            using Process process = Process.Start(start) ?? throw new InvalidOperationException(tool + " did not start.");

            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            string said = await output.ConfigureAwait(false);
            string first = (await error.ConfigureAwait(false)).Trim().Split('\n', 2)[0].Trim();

            return process.ExitCode == 0
                ? Result.Ok(said)
                : Result.Fail<string>($"'{tool} {arguments.Split(' ', 3)[0]} {arguments.Split(' ', 3).ElementAtOrDefault(1)}' failed{(first.Length > 0 ? ": " + first : string.Empty)}");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Result.Fail<string>($"'{tool}' is not installed here, so the pull request cannot be opened from this run.");
        }
    }
}
