using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using FluentResults;

namespace Sdlc.Core.Init;

/// <summary>
/// The branch policy adoption sets as its default, as the commands a person runs to set it (D-136).
/// </summary>
/// <remarks>
/// <para><strong>The default, on both platforms.</strong> Nothing reaches the integration branch or the default
/// branch except by pull request, nobody bypasses that, and the pipeline that runs the chain is a required check
/// on both. A pull request into the integration branch is squashed, so one cycle is one commit. One into the
/// default branch is merged with a merge commit, so the two branches keep sharing history and the next promotion
/// carries only what is new (D-88). No approval is required: a person cannot approve their own pull request, and
/// a team that wants reviewers asks for them with <see cref="BranchReviews"/>, on both branches alike. A team may
/// change any of it; this is where it starts.</para>
/// <para><strong>Printed, not applied.</strong> The binary never holds a token and changes nothing on the
/// remote, and setting a policy needs an administrator. The platforms' own CLIs carry the credentials, as they
/// do when <c>doctor</c> reads the policy back (D-106).</para>
/// <para><strong>Which branch is the default is not written here.</strong> A GitHub ruleset names it as
/// <c>~DEFAULT_BRANCH</c>, and on Azure DevOps the command that finds it is printed first.</para>
/// <para><strong>A trunk is one branch, and gets one policy.</strong> When the integration branch is the default
/// branch, the two policies would cover one branch -- squash only and merge commits only -- and leave no way to merge.
/// So the integration branch's policy is the only one, and there is no promotion: every pull request into the trunk
/// is judged by the chain. Whether it is a trunk is the platform's answer, asked by whoever calls this; when nobody
/// could ask, both are printed with a note saying which to skip.</para>
/// </remarks>
/// <param name="Text">What to print: every command, in order, with what each one does.</param>
/// <param name="Files">Files the commands read, by full path, with their contents.</param>
public sealed record BranchPolicyScript(string Text, IReadOnlyList<(string Path, string Text)> Files)
{
    /// <summary>Where the ruleset files are written, relative to the solution root. Under reports, which ignores itself.</summary>
    public static readonly string Directory = Path.Combine(".sdlc", "reports", "branch-policy");

    /// <summary>The script for a platform.</summary>
    /// <param name="platform">Where the repository is hosted.</param>
    /// <param name="integration">The integration branch.</param>
    /// <param name="solutionRoot">The solution's root, where the ruleset files are written.</param>
    /// <param name="reviews">Who must approve a pull request; none when omitted.</param>
    /// <param name="defaultBranch">The repository's default branch as the platform named it, or null when it could not
    /// be asked.</param>
    /// <returns>The script, or why there is none.</returns>
    public static Result<BranchPolicyScript> For(
        Platform platform,
        string integration,
        string solutionRoot,
        BranchReviews? reviews = null,
        string? defaultBranch = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(integration);
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        reviews ??= BranchReviews.None;

        if (platform != Platform.Local && reviews.Check(platform) is { IsFailed: true } refused)
        {
            return refused;
        }

        Shape shape = defaultBranch is null
            ? Shape.Unknown
            : string.Equals(defaultBranch, integration, StringComparison.Ordinal) ? Shape.Trunk : Shape.TwoBranches;

        return platform switch
        {
            Platform.GitHub => GitHub(integration, solutionRoot, reviews, shape),
            Platform.AzureDevOps => AzureDevOps(integration, reviews, shape),
            _ => Result.Fail("This solution is local, so there is no remote to hold a branch policy. Re-run adoption with --platform github or --platform azure-devops once it has one."),
        };
    }

    /// <summary>A GitHub ruleset in the shape adoption sets.</summary>
    /// <param name="name">The ruleset's name.</param>
    /// <param name="reference">The ref it covers: <c>refs/heads/&lt;branch&gt;</c>, or <c>~DEFAULT_BRANCH</c>.</param>
    /// <param name="mergeMethod">The one merge method allowed: <c>squash</c> or <c>merge</c>.</param>
    /// <param name="reviews">Who must approve; none when omitted. A bypass team is written as <c>"&lt;team id&gt;"</c>,
    /// which the rulesets API refuses until a person puts the team's number in its place.</param>
    /// <returns>The ruleset, as JSON the rulesets API accepts.</returns>
    public static string Ruleset(string name, string reference, string mergeMethod, BranchReviews? reviews = null)
    {
        reviews ??= BranchReviews.None;

        return new JsonObject
        {
            ["name"] = name,
            ["target"] = "branch",
            ["enforcement"] = "active",
            ["bypass_actors"] = reviews.SelfApprove && reviews.BypassTeam is not null
                ? new JsonArray(new JsonObject { ["actor_id"] = TeamId, ["actor_type"] = "Team", ["bypass_mode"] = "pull_request" })
                : new JsonArray(),
            ["conditions"] = new JsonObject
            {
                ["ref_name"] = new JsonObject { ["include"] = new JsonArray(reference), ["exclude"] = new JsonArray() },
            },
            ["rules"] = new JsonArray(
                new JsonObject { ["type"] = "deletion" },
                new JsonObject { ["type"] = "non_fast_forward" },
                new JsonObject
                {
                    ["type"] = "pull_request",
                    ["parameters"] = new JsonObject
                    {
                        ["required_approving_review_count"] = reviews.Required,
                        ["dismiss_stale_reviews_on_push"] = false,
                        ["require_code_owner_review"] = reviews.Owners.Count > 0,
                        ["require_last_push_approval"] = false,
                        ["required_review_thread_resolution"] = false,
                        ["allowed_merge_methods"] = new JsonArray(mergeMethod),
                    },
                },
                new JsonObject
                {
                    ["type"] = "required_status_checks",
                    ["parameters"] = new JsonObject
                    {
                        ["strict_required_status_checks_policy"] = true,
                        ["required_status_checks"] = new JsonArray(new JsonObject { ["context"] = Diagnostics.CliBranchPolicies.GitHubCheck }),
                    },
                }),
        }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>Where a bypass team's number goes in a ruleset, until a person puts it there.</summary>
    private const string TeamId = "<team id>";

    /// <summary>Whether the integration branch is the default branch, as far as the platform said.</summary>
    private enum Shape
    {
        Unknown,
        TwoBranches,
        Trunk,
    }

    private static void UnknownNote(StringBuilder text, string integration, Shape shape)
    {
        if (shape == Shape.Unknown)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"# If {integration} is the repository's default branch, it is a trunk: skip this one. Two policies on one");
            text.AppendLine("# branch leave no way to merge. The platform could not be asked which branch is the default.");
        }
    }

    private static Result<BranchPolicyScript> GitHub(string integration, string solutionRoot, BranchReviews reviews, Shape shape)
    {
        string integrationFile = Path.Combine(Directory, "integration.json");
        string defaultFile = Path.Combine(Directory, "default-branch.json");
        static string Slashes(string path) => path.Replace('\\', '/');

        StringBuilder text = new();
        text.AppendLine("Branch policy for GitHub, as rulesets (D-136). Run each command once, from this repository, signed in to");
        text.AppendLine("'gh' as an administrator of it. Running one again adds a second ruleset; change one in Settings > Rules.");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"# {integration}: pull requests only, squashed, the 'verify' check required, nobody bypasses.");

        if (shape == Shape.Trunk)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"# {integration} is the default branch too, so it is a trunk: this is its one ruleset, and there is no promotion.");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"gh api repos/{{owner}}/{{repo}}/rulesets --method POST --input {Slashes(integrationFile)}");
        text.AppendLine();

        if (shape != Shape.Trunk)
        {
            text.AppendLine("# The default branch: pull requests only, by merge commit, the 'verify' check required, nobody bypasses.");
            text.AppendLine(CultureInfo.InvariantCulture, $"# On it, 'verify' runs 'sdlc promotion', which admits a pull request only from {integration} or a release/* branch.");
            UnknownNote(text, integration, shape);
            text.AppendLine(CultureInfo.InvariantCulture, $"gh api repos/{{owner}}/{{repo}}/rulesets --method POST --input {Slashes(defaultFile)}");
            text.AppendLine();
        }

        GitHubReviews(text, reviews, shape);
        text.AppendLine("Any of it may be changed; this is the default. Then 'sdlc doctor' reads it back.");

        List<(string Path, string Text)> files =
        [
            (Path.Combine(solutionRoot, integrationFile), Ruleset($"sdlc: {integration}", "refs/heads/" + integration, "squash", reviews)),
        ];

        if (shape != Shape.Trunk)
        {
            files.Add((Path.Combine(solutionRoot, defaultFile), Ruleset("sdlc: default branch", "~DEFAULT_BRANCH", "merge", reviews)));
        }

        return new BranchPolicyScript(text.ToString().ReplaceLineEndings("\n"), files);
    }

    private static void GitHubReviews(StringBuilder text, BranchReviews reviews, Shape shape)
    {
        string rulesets = shape == Shape.Trunk ? "The ruleset requires" : "Both rulesets require";
        string files = shape == Shape.Trunk ? "in the file" : "in both files";

        if (!reviews.Any)
        {
            text.AppendLine("No approval is required, because nobody can approve their own pull request. 'sdlc branch-policy");
            text.AppendLine("--approvals <n>' asks for reviewers, and '--owner <user or team>' names who must be one.");
            return;
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"{rulesets} {Approvals(reviews.Required)} on every pull request.");

        if (reviews.Owners.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("# Who must approve: a code owner, on every file. Commit this line as .github/CODEOWNERS through a pull");
            text.AppendLine("# request. GitHub reads it from the branch a pull request merges into, so until it has merged any approval");
            text.AppendLine("# counts, and after it, changing who must approve takes a reviewed pull request of its own.");
            text.AppendLine("* " + string.Join(' ', reviews.Handles));
        }

        if (reviews.BypassTeam is { } team && reviews.SelfApprove)
        {
            text.AppendLine();
            text.AppendLine("# GitHub never counts an author's approval of their own pull request, so the rulesets let one team bypass");
            text.AppendLine("# them on a pull request, and only there: a member merges their own without another approval by ticking the");
            text.AppendLine("# bypass box. It would merge one without the required check too, so merge only when the check is green.");
            text.AppendLine(CultureInfo.InvariantCulture, $"# Run this first, and put the number it prints in place of \"{TeamId}\", quotes and all, {files}.");
            text.AppendLine(CultureInfo.InvariantCulture, $"gh api orgs/{{owner}}/teams/{team} --jq .id");
        }

        text.AppendLine();
    }

    private static string Approvals(int count) =>
        count == 1 ? "1 approval" : string.Create(CultureInfo.InvariantCulture, $"{count} approvals");

    private static Result<BranchPolicyScript> AzureDevOps(string integration, BranchReviews reviews, Shape shape)
    {
        string pipeline = Initialise.PipelinePath(Platform.AzureDevOps).Replace('\\', '/');

        StringBuilder text = new();
        text.AppendLine("Branch policy for Azure DevOps (D-136). Run each command once, signed in to 'az' as a project administrator,");
        text.AppendLine("with 'az devops configure --defaults organization=<url> project=<name>' set. Fill in the three values the first");
        text.AppendLine("commands print. Any policy on a branch makes pull requests the only way in.");
        text.AppendLine();
        text.AppendLine("# The repository's id, its default branch, and the id of the pipeline that runs " + pipeline + ".");
        text.AppendLine("az repos show --repository <repository> --query \"[id, defaultBranch]\" --output tsv");
        text.AppendLine("az pipelines list --query \"[?process.yamlFilename=='/" + pipeline + "'].id\" --output tsv");
        text.AppendLine();

        (string Branch, bool Squash, string What)[] branches = shape == Shape.Trunk
            ?
            [
                (integration, true, $"{integration}, the default branch too, so a trunk with no promotion: squashed, and the pipeline required: it runs the chain."),
            ]
            :
            [
                (integration, true, $"{integration}: squashed, and the pipeline required: it runs the chain, and on the default branch 'sdlc promotion'."),
                ("<default branch>", false, "The default branch: by merge commit, no fast-forward, and the pipeline required: it runs 'sdlc promotion'."),
            ];

        foreach ((string branch, bool squash, string what) in branches)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"# {what}");

            if (!squash)
            {
                UnknownNote(text, integration, shape);
            }

            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"az repos policy merge-strategy create --repository-id <repository id> --branch {branch} --blocking true --enabled true " +
                $"--allow-squash {(squash ? "true" : "false")} --allow-no-fast-forward {(squash ? "false" : "true")} --allow-rebase false --allow-rebase-merge false");
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"az repos policy build create --repository-id <repository id> --branch {branch} --build-definition-id <pipeline id> --display-name verify --blocking true --enabled true --queue-on-source-update-only false --manual-queue-only false --valid-duration 0");

            if (reviews.Required > 0)
            {
                text.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"az repos policy approver-count create --repository-id <repository id> --branch {branch} --blocking true --enabled true " +
                    $"--minimum-approver-count {reviews.Required} --creator-vote-counts {(reviews.SelfApprove ? "true" : "false")} --allow-downvotes false --reset-on-source-push false");
            }

            if (reviews.Owners.Count > 0)
            {
                text.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"az repos policy required-reviewer create --repository-id <repository id> --branch {branch} --blocking true --enabled true " +
                    $"--required-reviewer-ids \"{string.Join(';', reviews.Owners.Select(o => o.TrimStart('@')))}\" --message \"The owners approve every pull request (D-136).\"");
            }

            text.AppendLine();
        }

        text.AppendLine("Nobody bypasses: in Project settings > Repositories > Security, no group or user may hold");
        text.AppendLine("'Bypass policies when completing pull requests' or 'Bypass policies when pushing'. The CLI cannot set that.");
        text.AppendLine();
        if (!reviews.Any)
        {
            text.AppendLine("No minimum reviewer policy is added, because nobody can approve their own pull request. 'sdlc branch-policy");
            text.AppendLine("--approvals <n>' adds one, and '--owner <user or group>' names who must approve.");
        }
        else
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Every pull request needs {Approvals(reviews.Required)}{(reviews.SelfApprove ? ", and its author's own vote counts" : "")}.");

            if (reviews.Owners.Count > 0)
            {
                text.AppendLine("Azure DevOps has no CODEOWNERS file: each name listed is a required reviewer, so name a group to let any one");
                text.AppendLine("of its members approve.");

                if (reviews.SelfApprove)
                {
                    text.AppendLine("For an owner's vote to count on their own pull request, tick 'Allow requestors to approve their own changes'");
                    text.AppendLine("on the required reviewer policy, in the branch's policies under Automatically included reviewers.");
                }
            }
        }

        text.AppendLine("Any of it may be changed; this is the default. Then 'sdlc doctor' reads it back.");

        return new BranchPolicyScript(text.ToString().ReplaceLineEndings("\n"), []);
    }
}
