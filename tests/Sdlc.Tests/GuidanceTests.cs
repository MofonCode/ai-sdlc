using FluentResults;
using Sdlc.Core.Guidance;

namespace Sdlc.Tests;

/// <summary>
/// Guidance: how to do one kind of thing well, handed to a plan whose declared files switch it on, and overridden by
/// the solution at any point (D-195).
/// </summary>
[TestClass]
public sealed class GuidanceTests
{
    [TestMethod]
    public async Task Every_shipped_entry_reads_and_the_pipeline_entry_switches_on_for_a_pipeline_file()
    {
        using var workspace = CycleWorkspace.Create();

        Result<IReadOnlyList<GuidanceEntry>> all = await GuidanceLibrary.All(workspace.Root, CancellationToken.None);

        Assert.IsTrue(all.IsSuccess, string.Join("; ", all.Errors.Select(e => e.Message)));
        GuidanceEntry pipeline = all.Value.Single(entry => entry.Name == "pipeline");
        Assert.IsFalse(pipeline.Solution);
        Assert.IsTrue(pipeline.Matches("azure-pipelines.yml"));
        Assert.IsTrue(pipeline.Matches(".github\\workflows\\sdlc-verify.yml"));
        Assert.IsFalse(pipeline.Matches("src/Orders/OrderService.cs"));

        // D-201: how an agent's image is kept current, switched on by the factory and the image's own directory.
        GuidanceEntry image = all.Value.Single(entry => entry.Name == "agent-image");
        Assert.IsTrue(image.Matches(".github/workflows/sdlc-factory.yml"));
        Assert.IsTrue(image.Matches("agent-image/Dockerfile"));
        Assert.IsFalse(image.Matches("src/Orders/Dockerfile"), "An application's own image is not the agent's.");

        // D-218: a check that reads the repository, switched on by the architecture tests that hold one.
        GuidanceEntry checks = all.Value.Single(entry => entry.Name == "repository-checks");
        Assert.IsTrue(checks.Matches("tests/Demo.Architecture.Tests/LayeringTests.cs"));
        Assert.IsFalse(checks.Matches("src/Orders/OrderService.cs"));
    }

    [TestMethod]
    public async Task A_solutions_entry_replaces_the_frameworks_of_the_same_name_and_adds_its_own()
    {
        using var workspace = CycleWorkspace.Create();
        string own = Path.Combine(workspace.Root, ".sdlc", GuidanceLibrary.DirectoryName);
        Directory.CreateDirectory(own);
        await File.WriteAllTextAsync(Path.Combine(own, "pipeline.md"), "---\nname: pipeline\nfiles: azure-pipelines\n---\n\n# Our pipeline\n\nOne job; we accept the wait.\n");
        await File.WriteAllTextAsync(Path.Combine(own, "migrations.md"), "---\nname: migrations\nfiles: Migrations/\n---\n\n# Migrations\n\nOne per change.\n");

        IReadOnlyList<GuidanceEntry> all = (await GuidanceLibrary.All(workspace.Root, CancellationToken.None)).Value;

        GuidanceEntry pipeline = all.Single(entry => entry.Name == "pipeline");
        Assert.IsTrue(pipeline.Solution, "The solution's word wins over the framework's.");
        Assert.AreEqual("Our pipeline", pipeline.Title);
        Assert.IsTrue(all.Any(entry => entry.Name == "migrations" && entry.Solution));
    }

    [TestMethod]
    public void A_plan_is_handed_only_the_entries_its_declared_files_switch_on()
    {
        GuidanceEntry pipeline = new("pipeline", "Building the pipeline", ["azure-pipelines"], "# Building the pipeline", false);
        GuidanceEntry migrations = new("migrations", "Migrations", ["Migrations/"], "# Migrations", true);

        IReadOnlyList<(GuidanceEntry Entry, string File)> matched = GuidanceLibrary.For([pipeline, migrations], ["src/Api/Program.cs", "azure-pipelines.yml"]);

        Assert.AreEqual("pipeline", matched.Single().Entry.Name);
        StringAssert.Contains(GuidanceLibrary.RenderFor(matched), "because the plan declares azure-pipelines.yml");
        StringAssert.Contains(GuidanceLibrary.RenderFor([]), "No guidance applies");
    }

    [TestMethod]
    public void An_entry_without_files_to_switch_it_on_is_refused()
    {
        Result<GuidanceEntry> read = GuidanceEntry.Read("---\nname: loose\n---\n\n# Loose\n", "loose.md", solution: true);

        Assert.IsTrue(read.IsFailed);
        StringAssert.Contains(read.Errors[0].Message, "the files that switch it on");
    }
}
