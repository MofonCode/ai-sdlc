using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Git;
using Sdlc.Core.Proposals;
using Sdlc.Core.Successors;

namespace Sdlc.Tests;

/// <summary>
/// A successor can say it is a gap in the framework, and mining lists it to be carried there (D-173).
/// </summary>
[TestClass]
public sealed class FrameworkGapTests
{
    private const string Gap = "the adjudicator is never handed the coverage";

    [TestMethod]
    public void A_successor_reads_its_framework_line_and_one_without_it_has_none()
    {
        Result<Successor> marked = Successor.Parse("coverage-in-verify", SuccessorTests.File("20261103_0900_00", "framework: " + Gap + "\n"));
        Result<Successor> plain = Successor.Parse("export-totals", SuccessorTests.File("20261103_0900_00"));

        Assert.IsTrue(marked.IsSuccess, string.Join("; ", marked.Errors.Select(error => error.Message)));
        Assert.AreEqual(Gap, marked.Value.Framework);
        Assert.IsNull(plain.Value.Framework);
    }

    [TestMethod]
    public async Task Mining_lists_every_successor_marked_as_a_framework_gap_and_no_other()
    {
        // MarketSense filed two framework defects as its own successors, and they sat beside its real work.
        using var workspace = CycleWorkspace.Create();
        workspace.WriteAllArtifacts();
        string directory = Successor.DirectoryOf(workspace.Root);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "coverage-in-verify.md"), SuccessorTests.File(workspace.Cycle.Id.ToString(), "framework: " + Gap + "\n"));
        await File.WriteAllTextAsync(Path.Combine(directory, "export-totals.md"), SuccessorTests.File(workspace.Cycle.Id.ToString()));

        ProposeMining.Response report;
        using (var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(new FakeRepository())))
        {
            Result<ProposeMining.Response> asked = await gate.Send<ProposeMining.Query, ProposeMining.Response>(new ProposeMining.Query());
            Assert.IsTrue(asked.IsSuccess, string.Join("; ", asked.Errors.Select(error => error.Message)));
            report = asked.Value;
        }

        CollectionAssert.AreEqual(new[] { "coverage-in-verify" }, report.FrameworkGaps!.Select(gap => gap.Slug).ToArray());
        string rendered = ProposeMining.Render(report, "proposed.md");
        StringAssert.Contains(rendered, "Gaps in the framework");
        StringAssert.Contains(rendered, "coverage-in-verify -- " + Gap);
    }
}
