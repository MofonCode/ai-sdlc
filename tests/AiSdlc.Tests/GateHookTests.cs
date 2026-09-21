using System.Text.Json;

namespace AiSdlc.Tests;

/// <summary>
/// Behaviour of the PreToolUse gate hook.
/// </summary>
/// <remarks>
/// This hook runs on every edit in every repo the owner opens, so its failure modes are
/// asymmetric: a false silence reads as approval, and a false alarm gets the plugin
/// uninstalled. Both directions are pinned here.
/// </remarks>
[TestClass]
public sealed class GateHookTests
{
    private static string Payload(string filePath) =>
        JsonSerializer.Serialize(new { tool_input = new { file_path = filePath } });

    private static Repo.RunResult Edit(TempRepo repo, string filePath, bool block = false)
    {
        Dictionary<string, string?> env = new();
        if (block)
        {
            env["SDLC_GATE"] = "block";
        }

        return Repo.PowerShell(
            Repo.GateHook,
            workingDirectory: repo.Path,
            standardInput: Payload(filePath),
            environment: env);
    }

    private static Repo.RunResult Raw(TempRepo repo, string stdin) =>
        Repo.PowerShell(Repo.GateHook, workingDirectory: repo.Path, standardInput: stdin);

    // --- silence where silence is correct ---------------------------------------------

    [TestMethod]
    public void RepoWithNoChain_StaysSilent()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Edit(repo, "src/Thing.cs");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.DoesNotMatch(result.StdErr, new System.Text.RegularExpressions.Regex("sdlc-gate"));
    }

    [TestMethod]
    public void FileClaimedByApprovedPlan_StaysSilent()
    {
        using TempRepo repo = TempRepo.Create();
        Repo.RunResult result = Edit(repo, "src/Storage/Writer.cs");

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StdErr.Trim());
    }

    [TestMethod]
    [DataRow("README.md")]
    [DataRow("notes.txt")]
    [DataRow("config.json")]
    [DataRow("workflow.yml")]
    [DataRow(".gitignore")]
    [DataRow(".editorconfig")]
    public void ArtifactsAndConfig_AreNeverGated(string file)
    {
        using TempRepo repo = TempRepo.Create();

        // These are how you get OUT of an ungated state, or are edited while setting a repo
        // up. Gating them would make the framework impossible to adopt.
        Assert.AreEqual(0, Edit(repo, file).ExitCode);
        Assert.AreEqual(string.Empty, Edit(repo, file).StdErr.Trim());
    }

    [TestMethod]
    public void EditingTheChainItself_IsNeverGated()
    {
        using TempRepo repo = TempRepo.Create();
        Assert.AreEqual(string.Empty, Edit(repo, ".sdlc/audit-log/notes.cs").StdErr.Trim());
    }

    // --- warnings where the discipline has slipped ------------------------------------

    [TestMethod]
    public void FileNoSliceClaims_WarnsAboutUnplannedScope()
    {
        using TempRepo repo = TempRepo.Create();
        Repo.RunResult result = Edit(repo, "src/Storage/Rogue.cs");

        Assert.AreEqual(0, result.ExitCode, "the gate is advisory by default");
        StringAssert.Contains(result.StdErr, "unplanned scope");
    }

    [TestMethod]
    public void DraftPlan_WarnsThatNothingIsApproved()
    {
        using TempRepo repo = TempRepo.Create(planStatus: "draft");
        Repo.RunResult result = Edit(repo, "src/Storage/Writer.cs");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StdErr, "no approved plan");
        StringAssert.Contains(result.StdErr, "none live");
    }

    [TestMethod]
    public void NoPlanAtAll_WarnsThatNoPlanExists()
    {
        using TempRepo repo = TempRepo.Create(planStatus: null);
        Repo.RunResult result = Edit(repo, "src/Storage/Writer.cs");

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StdErr, "no approved plan");
        StringAssert.Contains(result.StdErr, "no plan.md exists");
    }

    // --- the half-finished refactor these tests exist to prevent -----------------------

    [TestMethod]
    public void NonDefaultArtifactRoot_IsFoundFromTheCharter()
    {
        using TempRepo repo = TempRepo.Create(rootDirectory: "docs/sdlc", charterRoot: "docs/sdlc/");

        // A gate that goes quiet on a non-default root is worse than no gate, because
        // silence reads as approval. This is the exact regression that shipped once.
        Assert.AreEqual(string.Empty, Edit(repo, "src/Storage/Writer.cs").StdErr.Trim());
        StringAssert.Contains(Edit(repo, "src/Storage/Rogue.cs").StdErr, "unplanned scope");
    }

    // --- opt-in enforcement ------------------------------------------------------------

    [TestMethod]
    public void BlockMode_RefusesUnplannedEdit()
    {
        using TempRepo repo = TempRepo.Create();
        Assert.AreEqual(2, Edit(repo, "src/Storage/Rogue.cs", block: true).ExitCode);
    }

    [TestMethod]
    public void BlockMode_StillAllowsAClaimedFile()
    {
        using TempRepo repo = TempRepo.Create();
        Assert.AreEqual(0, Edit(repo, "src/Storage/Writer.cs", block: true).ExitCode);
    }

    // --- robustness: this must never be the reason a session breaks --------------------

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{}")]
    [DataRow("")]
    [DataRow("[]")]
    [DataRow("{\"tool_input\": null}")]
    [DataRow("{\"tool_input\": {}}")]
    [DataRow("{\"tool_input\": {\"file_path\": null}}")]
    public void MalformedPayload_ExitsZeroAndSaysNothing(string stdin)
    {
        using TempRepo repo = TempRepo.Create();
        Repo.RunResult result = Raw(repo, stdin);

        Assert.AreEqual(0, result.ExitCode, "payload was: " + stdin);
        StringAssert.DoesNotMatch(
            result.StdErr,
            new System.Text.RegularExpressions.Regex("Exception|cannot be found|ParentContains"),
            "the hook must not print a stack trace on every edit");
    }
}
