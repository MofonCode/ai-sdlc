namespace AiSdlc.Tests;

/// <summary>
/// Behaviour of the slice manifest assertion.
/// </summary>
/// <remarks>
/// This check is the only thing standing between an overlapping manifest and two agents
/// editing the same file at the same time, so a false PASS is the expensive direction and
/// every rejection case is pinned, not just the happy path.
/// </remarks>
[TestClass]
public sealed class SliceManifestTests
{
    private static Repo.RunResult Check(TempRepo repo, string plan) =>
        Repo.PowerShell(Repo.ManifestCheck, ["-Plan", repo.WritePlan(plan)]);

    [TestMethod]
    public void DisjointManifests_Pass()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: audit-log
            status: approved
            slices:
              - id: storage
                files: ["src/Storage/Writer.cs", "tests/Storage/WriterTests.cs"]
                depends_on: []
              - id: api
                files: ["src/Api/Endpoint.cs"]
                depends_on: [storage]
            ---
            """);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdOut, "manifests disjoint");
    }

    [TestMethod]
    public void IndependentSlices_AreReportedAsOneWave()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            slices:
              - id: a
                files: ["src/A.cs"]
                depends_on: []
              - id: b
                files: ["src/B.cs"]
                depends_on: []
              - id: c
                files: ["src/C.cs"]
                depends_on: [a, b]
            ---
            """);

        // The approver is agreeing to a shape of fan-out, so the shape has to be shown.
        Assert.AreEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdOut, "wave 1: a, b");
    }

    [TestMethod]
    public void DeepDependencyChain_IsCalledOutAsSequential()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            slices:
              - id: a
                files: ["src/A.cs"]
                depends_on: []
              - id: b
                files: ["src/B.cs"]
                depends_on: [a]
              - id: c
                files: ["src/C.cs"]
                depends_on: [b]
            ---
            """);

        Assert.AreEqual(0, result.ExitCode, result.Combined);
        StringAssert.Contains(result.StdOut, "this plan is sequential");
    }

    [TestMethod]
    public void OverlappingManifests_AreRejected()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: audit-log
            status: approved
            slices:
              - id: storage
                files: ["src/Storage/Writer.cs", "src/Storage/IStore.cs"]
                depends_on: []
              - id: api
                files: ["src/Api/Endpoint.cs", "src/Storage/IStore.cs"]
                depends_on: []
            ---
            """);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Combined, "OVERLAP storage <-> api: src/Storage/IStore.cs");
    }

    [TestMethod]
    public void PathsAreNormalisedBeforeComparison()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            slices:
              - id: a
                files: ["./src/A.cs"]
                depends_on: []
              - id: b
                files: ["src/A.cs"]
                depends_on: []
            ---
            """);

        // Same file, two spellings. Without normalisation this passes and two implementers
        // are handed the same file.
        Assert.AreEqual(1, result.ExitCode, result.Combined);
        StringAssert.Contains(result.Combined, "OVERLAP");
    }

    [TestMethod]
    public void EmptyManifest_IsRejected()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            slices:
              - id: a
                files: ["src/A.cs"]
                depends_on: []
              - id: b
                files: []
                depends_on: []
            ---
            """);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Combined, "empty manifest");
    }

    [TestMethod]
    public void DependencyCycle_IsRejected()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            slices:
              - id: a
                files: ["src/A.cs"]
                depends_on: [b]
              - id: b
                files: ["src/B.cs"]
                depends_on: [a]
            ---
            """);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Combined, "dependency cycle");
    }

    [TestMethod]
    public void UnknownDependency_IsRejected()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            slices:
              - id: a
                files: ["src/A.cs"]
                depends_on: [ghost]
            ---
            """);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Combined, "is not a slice in this plan");
    }

    [TestMethod]
    public void PlanWithNoFrontmatter_IsRejected()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, "# just a heading");

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Combined, "no frontmatter");
    }

    [TestMethod]
    public void FrontmatterWithNoSlices_IsRejected()
    {
        using TempRepo repo = TempRepo.CreateWithoutChain();
        Repo.RunResult result = Check(repo, """
            ---
            slug: x
            status: approved
            ---
            """);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(result.Combined, "no slices found");
    }
}
