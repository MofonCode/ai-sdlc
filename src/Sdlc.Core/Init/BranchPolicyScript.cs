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
/// a team that wants reviewers raises the count. A team may change any of it; this is where it starts.</para>
/// <para><strong>Printed, not applied.</strong> The binary never holds a token and changes nothing on the
/// remote, and setting a policy needs an administrator. The platforms' own CLIs carry the credentials, as they
/// do when <c>doctor</c> reads the policy back (D-106).</para>
/// <para><strong>Which branch is the default is not written here.</strong> A GitHub ruleset names it as
/// <c>~DEFAULT_BRANCH</c>, and on Azure DevOps the command that finds it is printed first.</para>
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
    /// <returns>The script, or why there is none.</returns>
    public static Result<BranchPolicyScript> For(Platform platform, string integration, string solutionRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(integration);
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        return platform switch
        {
            Platform.GitHub => GitHub(integration, solutionRoot),
            Platform.AzureDevOps => AzureDevOps(integration),
            _ => Result.Fail("This solution is local, so there is no remote to hold a branch policy. Re-run adoption with --platform github or --platform azure-devops once it has one."),
        };
    }

    /// <summary>A GitHub ruleset in the shape adoption sets.</summary>
    /// <param name="name">The ruleset's name.</param>
    /// <param name="reference">The ref it covers: <c>refs/heads/&lt;branch&gt;</c>, or <c>~DEFAULT_BRANCH</c>.</param>
    /// <param name="mergeMethod">The one merge method allowed: <c>squash</c> or <c>merge</c>.</param>
    /// <returns>The ruleset, as JSON the rulesets API accepts.</returns>
    public static string Ruleset(string name, string reference, string mergeMethod) => new JsonObject
    {
        ["name"] = name,
        ["target"] = "branch",
        ["enforcement"] = "active",
        ["bypass_actors"] = new JsonArray(),
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
                    ["required_approving_review_count"] = 0,
                    ["dismiss_stale_reviews_on_push"] = false,
                    ["require_code_owner_review"] = false,
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

    private static Result<BranchPolicyScript> GitHub(string integration, string solutionRoot)
    {
        string integrationFile = Path.Combine(Directory, "integration.json");
        string defaultFile = Path.Combine(Directory, "default-branch.json");
        static string Slashes(string path) => path.Replace('\\', '/');

        StringBuilder text = new();
        text.AppendLine("Branch policy for GitHub, as rulesets (D-136). Run each command once, from this repository, signed in to");
        text.AppendLine("'gh' as an administrator of it. Running one again adds a second ruleset; change one in Settings > Rules.");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"# {integration}: pull requests only, squashed, the 'verify' check required, nobody bypasses.");
        text.AppendLine(CultureInfo.InvariantCulture, $"gh api repos/{{owner}}/{{repo}}/rulesets --method POST --input {Slashes(integrationFile)}");
        text.AppendLine();
        text.AppendLine("# The default branch: pull requests only, by merge commit, the 'verify' check required, nobody bypasses.");
        text.AppendLine(CultureInfo.InvariantCulture, $"# On it, 'verify' runs 'sdlc promotion', which admits a pull request only from {integration} or a release/* branch.");
        text.AppendLine(CultureInfo.InvariantCulture, $"gh api repos/{{owner}}/{{repo}}/rulesets --method POST --input {Slashes(defaultFile)}");
        text.AppendLine();
        text.AppendLine("No approval is required, because nobody can approve their own pull request. A team that wants reviewers");
        text.AppendLine("raises 'Required approvals' on either ruleset. Any of it may be changed; this is the default.");
        text.AppendLine("Then 'sdlc doctor' reads it back.");

        return new BranchPolicyScript(
            text.ToString().ReplaceLineEndings("\n"),
            [
                (Path.Combine(solutionRoot, integrationFile), Ruleset($"sdlc: {integration}", "refs/heads/" + integration, "squash")),
                (Path.Combine(solutionRoot, defaultFile), Ruleset("sdlc: default branch", "~DEFAULT_BRANCH", "merge")),
            ]);
    }

    private static Result<BranchPolicyScript> AzureDevOps(string integration)
    {
        string pipeline = Initialise.PipelinePath(Platform.AzureDevOps);

        StringBuilder text = new();
        text.AppendLine("Branch policy for Azure DevOps (D-136). Run each command once, signed in to 'az' as a project administrator,");
        text.AppendLine("with 'az devops configure --defaults organization=<url> project=<name>' set. Fill in the three values the first");
        text.AppendLine("commands print. Any policy on a branch makes pull requests the only way in.");
        text.AppendLine();
        text.AppendLine("# The repository's id, its default branch, and the id of the pipeline that runs " + pipeline + ".");
        text.AppendLine("az repos show --repository <repository> --query \"[id, defaultBranch]\" --output tsv");
        text.AppendLine("az pipelines list --query \"[?process.yamlFilename=='/" + pipeline + "'].id\" --output tsv");
        text.AppendLine();

        foreach ((string branch, bool squash, string what) in (ReadOnlySpan<(string, bool, string)>)
        [
            (integration, true, $"{integration}: squashed"),
            ("<default branch>", false, "The default branch: by merge commit, no fast-forward"),
        ])
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"# {what}, and the pipeline required: it runs the chain, and on the default branch 'sdlc promotion'.");
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"az repos policy merge-strategy create --repository-id <repository id> --branch {branch} --blocking true --enabled true " +
                $"--allow-squash {(squash ? "true" : "false")} --allow-no-fast-forward {(squash ? "false" : "true")} --allow-rebase false --allow-rebase-merge false");
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"az repos policy build create --repository-id <repository id> --branch {branch} --build-definition-id <pipeline id> --display-name verify --blocking true --enabled true --queue-on-source-update-only false --manual-queue-only false --valid-duration 0");
            text.AppendLine();
        }

        text.AppendLine("Nobody bypasses: in Project settings > Repositories > Security, no group or user may hold");
        text.AppendLine("'Bypass policies when completing pull requests' or 'Bypass policies when pushing'. The CLI cannot set that.");
        text.AppendLine();
        text.AppendLine("No minimum reviewer policy is added, because nobody can approve their own pull request. A team that wants");
        text.AppendLine("reviewers adds one. Any of it may be changed; this is the default. Then 'sdlc doctor' reads it back.");

        return new BranchPolicyScript(text.ToString().ReplaceLineEndings("\n"), []);
    }
}
