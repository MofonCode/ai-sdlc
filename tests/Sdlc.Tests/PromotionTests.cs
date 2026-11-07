using System.Text.Json.Nodes;
using FluentResults;
using Sdlc.Core.Chain;
using Sdlc.Core.Init;

namespace Sdlc.Tests;

/// <summary>
/// Which branches may promote into the default branch, and the branch policy adoption sets as its default (D-136).
/// </summary>
[TestClass]
public sealed class PromotionTests
{
    [TestMethod]
    [DataRow("cycle/20261106_0930_00/build", "development")]
    [DataRow("cycle/20261106_0930_00/forward", "development")]
    [DataRow("cycle/20261106_0930_00", "release/1.2")]
    [DataRow("refs/heads/cycle/20261106_0930_00", "refs/heads/development")]
    public void A_pull_request_into_the_integration_branch_or_a_release_is_judged_by_the_chain(string source, string target)
    {
        Assert.AreEqual(PromotionVerdict.NotAPromotion, Promotion.Judge(source, target, "development"));
    }

    [TestMethod]
    [DataRow("development", "main")]
    [DataRow("release/1.2", "main")]
    [DataRow("refs/heads/development", "refs/heads/master")]
    public void The_integration_branch_and_a_release_branch_may_promote(string source, string target)
    {
        Assert.AreEqual(PromotionVerdict.Allowed, Promotion.Judge(source, target, "development"));
    }

    [TestMethod]
    [DataRow("feature/x", "main")]
    [DataRow("cycle/20261106_0930_00/build", "main")]
    [DataRow("release/", "main")]
    [DataRow("developments", "main")]
    public void Anything_else_into_the_default_branch_is_refused(string source, string target)
    {
        PromotionVerdict verdict = Promotion.Judge(source, target, "development");

        Assert.AreEqual(PromotionVerdict.Refused, verdict);
        StringAssert.Contains(Promotion.Explain(verdict, source, target, "development"), "no chain judged");
    }

    [TestMethod]
    public void A_solution_whose_integration_branch_is_named_otherwise_promotes_from_that()
    {
        Assert.AreEqual(PromotionVerdict.Allowed, Promotion.Judge("integration", "main", "integration"));
        Assert.AreEqual(PromotionVerdict.Refused, Promotion.Judge("development", "main", "integration"));
    }

    [TestMethod]
    public void GitHub_gets_two_rulesets_that_nobody_bypasses_squash_in_and_merge_commits_out()
    {
        using TemporaryDirectory scratch = new();

        Result<BranchPolicyScript> script = BranchPolicyScript.For(Platform.GitHub, "development", scratch.Path);

        Assert.IsTrue(script.IsSuccess);
        Assert.HasCount(2, script.Value.Files);
        StringAssert.Contains(script.Value.Text, "gh api repos/{owner}/{repo}/rulesets --method POST --input .sdlc/reports/branch-policy/integration.json");

        JsonNode integration = JsonNode.Parse(script.Value.Files[0].Text)!;
        JsonNode main = JsonNode.Parse(script.Value.Files[1].Text)!;

        Assert.AreEqual("refs/heads/development", integration["conditions"]!["ref_name"]!["include"]![0]!.GetValue<string>());
        Assert.AreEqual("~DEFAULT_BRANCH", main["conditions"]!["ref_name"]!["include"]![0]!.GetValue<string>());
        Assert.AreEqual("squash", MergeMethod(integration));
        Assert.AreEqual("merge", MergeMethod(main));

        foreach (JsonNode ruleset in (JsonNode[])[integration, main])
        {
            Assert.AreEqual(0, ruleset["bypass_actors"]!.AsArray().Count, "Nobody bypasses.");
            Assert.AreEqual(0, Rule(ruleset, "pull_request")["required_approving_review_count"]!.GetValue<int>(), "Nobody can approve their own pull request.");
            Assert.AreEqual("verify", Rule(ruleset, "required_status_checks")["required_status_checks"]![0]!["context"]!.GetValue<string>());
        }
    }

    [TestMethod]
    public void Azure_DevOps_gets_a_merge_strategy_and_the_pipeline_required_on_both_branches()
    {
        Result<BranchPolicyScript> script = BranchPolicyScript.For(Platform.AzureDevOps, "development", Path.GetTempPath());

        Assert.IsTrue(script.IsSuccess);
        StringAssert.Contains(script.Value.Text, "--branch development --blocking true --enabled true --allow-squash true --allow-no-fast-forward false");
        StringAssert.Contains(script.Value.Text, "--branch <default branch> --blocking true --enabled true --allow-squash false --allow-no-fast-forward true");
        StringAssert.Contains(script.Value.Text, "az repos policy build create --repository-id <repository id> --branch <default branch>");
        StringAssert.Contains(script.Value.Text, "Bypass policies when completing pull requests");
    }

    [TestMethod]
    public void A_named_owner_on_GitHub_is_a_code_owner_review_with_the_line_to_commit()
    {
        using TemporaryDirectory scratch = new();

        Result<BranchPolicyScript> script = BranchPolicyScript.For(
            Platform.GitHub, "development", scratch.Path, new BranchReviews(0, ["peteralmanzar"], false, null));

        Assert.IsTrue(script.IsSuccess);
        StringAssert.Contains(script.Value.Text, "* @peteralmanzar");
        StringAssert.Contains(script.Value.Text, ".github/CODEOWNERS");

        foreach ((string _, string text) in script.Value.Files)
        {
            JsonNode ruleset = JsonNode.Parse(text)!;
            Assert.AreEqual(1, Rule(ruleset, "pull_request")["required_approving_review_count"]!.GetValue<int>(), "An owner must approve, so one approval at least.");
            Assert.IsTrue(Rule(ruleset, "pull_request")["require_code_owner_review"]!.GetValue<bool>());
            Assert.AreEqual(0, ruleset["bypass_actors"]!.AsArray().Count);
        }
    }

    [TestMethod]
    public void Self_approval_on_GitHub_is_a_team_that_bypasses_on_a_pull_request_only()
    {
        using TemporaryDirectory scratch = new();

        Result<BranchPolicyScript> script = BranchPolicyScript.For(
            Platform.GitHub, "development", scratch.Path, new BranchReviews(1, ["@peteralmanzar"], true, "owners"));

        Assert.IsTrue(script.IsSuccess);
        StringAssert.Contains(script.Value.Text, "gh api orgs/{owner}/teams/owners --jq .id");
        StringAssert.Contains(script.Value.Text, "* @peteralmanzar");

        foreach ((string _, string text) in script.Value.Files)
        {
            JsonNode actor = JsonNode.Parse(text)!["bypass_actors"]![0]!;
            Assert.AreEqual("Team", actor["actor_type"]!.GetValue<string>());
            Assert.AreEqual("pull_request", actor["bypass_mode"]!.GetValue<string>(), "Never a push straight to the branch.");
            Assert.AreEqual("<team id>", actor["actor_id"]!.GetValue<string>(), "A placeholder the API refuses until it is filled in.");
        }
    }

    [TestMethod]
    public void Self_approval_on_GitHub_without_a_team_is_refused_rather_than_pretended()
    {
        Result<BranchPolicyScript> script = BranchPolicyScript.For(
            Platform.GitHub, "development", Path.GetTempPath(), new BranchReviews(1, [], true, null));

        Assert.IsTrue(script.IsFailed);
        StringAssert.Contains(script.Errors[0].Message, "never counts an author's approval");
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void Self_approval_with_nothing_to_approve_and_a_negative_count_are_refused(Platform platform)
    {
        Assert.IsTrue(BranchPolicyScript.For(platform, "development", Path.GetTempPath(), new BranchReviews(0, [], true, "owners")).IsFailed);
        Assert.IsTrue(BranchPolicyScript.For(platform, "development", Path.GetTempPath(), new BranchReviews(-1, [], false, null)).IsFailed);
    }

    [TestMethod]
    public void Azure_DevOps_gets_an_approver_count_and_a_required_reviewer_on_both_branches()
    {
        Result<BranchPolicyScript> script = BranchPolicyScript.For(
            Platform.AzureDevOps, "development", Path.GetTempPath(), new BranchReviews(2, ["@leads"], true, null));

        Assert.IsTrue(script.IsSuccess);

        foreach (string branch in (string[])["development", "<default branch>"])
        {
            StringAssert.Contains(script.Value.Text, $"az repos policy approver-count create --repository-id <repository id> --branch {branch} --blocking true --enabled true --minimum-approver-count 2 --creator-vote-counts true");
            StringAssert.Contains(script.Value.Text, $"az repos policy required-reviewer create --repository-id <repository id> --branch {branch} --blocking true --enabled true --required-reviewer-ids \"leads\"");
        }
    }

    [TestMethod]
    public void By_default_Azure_DevOps_gets_no_reviewer_policy()
    {
        Result<BranchPolicyScript> script = BranchPolicyScript.For(Platform.AzureDevOps, "development", Path.GetTempPath());

        Assert.DoesNotContain("approver-count", script.Value.Text);
        Assert.DoesNotContain("required-reviewer", script.Value.Text);
    }

    [TestMethod]
    public void A_local_solution_has_no_branch_policy_to_print()
    {
        Assert.IsTrue(BranchPolicyScript.For(Platform.Local, "development", Path.GetTempPath()).IsFailed);
    }

    [TestMethod]
    [DataRow(Platform.GitHub)]
    [DataRow(Platform.AzureDevOps)]
    public void Both_pipelines_ask_whether_a_pull_request_is_a_promotion_and_skip_the_chain_when_it_is(Platform platform)
    {
        string pipeline = Templates.Pipeline(platform);

        StringAssert.Contains(pipeline, "sdlc promotion");
        StringAssert.Contains(
            pipeline,
            platform == Platform.GitHub ? "steps.promotion.outputs.promotion != 'true'" : "ne(variables.promotion, 'true')",
            "A promotion has no cycle, so the chain is not asked of it.");
    }

    private static string MergeMethod(JsonNode ruleset) =>
        Rule(ruleset, "pull_request")["allowed_merge_methods"]!.AsArray().Single()!.GetValue<string>();

    private static JsonNode Rule(JsonNode ruleset, string type) =>
        ruleset["rules"]!.AsArray().Single(rule => rule!["type"]!.GetValue<string>() == type)!["parameters"]!;

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdlc-policy", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A scratch directory something still has open. Nothing depends on it going.
            }
        }
    }
}
