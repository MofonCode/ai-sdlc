using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Cycles;
using Sdlc.Core.Factory;
using Sdlc.Core.Git;

namespace Sdlc.Tests;

/// <summary>
/// The morning's page (D-84, D-85, D-86).
/// </summary>
[TestClass]
public sealed class SitrepTests
{
    private const string A = "20261015_1930_45";
    private const string B = "20261015_2002_17";
    private const string C = "20261015_2104_11";

    [TestMethod]
    public async Task Built_cycles_are_read_from_their_own_branches_and_sorted_into_ready_and_needing_a_person()
    {
        FakeRepository git = Remote(
            (A, "build-unattended", "pass", ["src/OrderService.cs"]),
            (B, "build-unattended", "blocked", ["src/Discounts.cs"]),
            (C, "build", null, ["src/Export.cs"]));

        Sitrep.Response page = await Read(git);

        CollectionAssert.AreEqual(new[] { A }, page.Ready.Select(b => b.Id.ToString()).ToArray());
        CollectionAssert.AreEquivalent(new[] { B, C }, page.NeedsPerson.Select(b => b.Id.ToString()).ToArray());

        string rendered = SitrepRenderer.Render(page);
        StringAssert.Contains(rendered, "blocked -> `remediate`");
        StringAssert.Contains(rendered, "no verify to read");
    }

    [TestMethod]
    public async Task Cycles_sharing_a_declared_file_are_flagged_and_the_more_contained_one_merges_first()
    {
        // D-86: the claim graph advises the order rather than scheduling the builds.
        FakeRepository git = Remote(
            (A, "build-unattended", "pass", ["src/OrderService.cs", "src/Orders.cs", "src/Totals.cs"]),
            (B, "build-unattended", "pass", ["src/OrderService.cs"]),
            (C, "build-unattended", "pass", ["src/Export.cs"]));

        Sitrep.Response page = await Read(git);

        Sitrep.Overlap overlap = page.Overlaps.Single();
        Assert.AreEqual(B, overlap.First.ToString(), "Fewer files goes first.");
        Assert.AreEqual(A, overlap.Second.ToString());
        Assert.AreEqual(C, page.Ready[0].Id.ToString(), "A cycle that overlaps nothing goes before both.");
        StringAssert.Contains(SitrepRenderer.Render(page), "expect it to conflict after this merges");
    }

    [TestMethod]
    public async Task With_nothing_built_every_section_says_so_rather_than_vanishing()
    {
        Sitrep.Response page = await Read(new FakeRepository());

        string rendered = SitrepRenderer.Render(page);

        StringAssert.Contains(rendered, "## Ready to merge, in this order");
        StringAssert.Contains(rendered, "## Needs a person");
        StringAssert.Contains(rendered, "There is no `development` branch yet.");
    }

    private static FakeRepository Remote(params (string Id, string Kind, string? Outcome, string[] Files)[] cycles)
    {
        FakeRepository git = new() { Remote = [] };
        List<string> branches = [];

        foreach ((string id, string kind, string? outcome, string[] files) in cycles)
        {
            string branch = "cycle/" + id + "/" + kind;
            string at = "origin/" + branch + ":.sdlc/cycles/" + id + "/";
            branches.Add(branch);

            git.Files[at + "intent.md"] = "# Cycle " + id + "\n";
            git.Files[at + "plan.md"] = "## Files\n\n" + string.Join("\n", files.Select(f => "- `" + f + "` - changed")) + "\n";

            if (outcome is not null)
            {
                git.Files[at + "verify.md"] = "---\noutcome: " + outcome + "\n---\n";
            }
        }

        git.Remote = branches;
        return git;
    }

    private static async Task<Sitrep.Response> Read(FakeRepository git)
    {
        using var workspace = CycleWorkspace.Create();
        using var gate = Gate.For(workspace.Root, services => services.AddSingleton<IRepository>(git));

        Result<Sitrep.Response> result = await gate.Send<Sitrep.Query, Sitrep.Response>(new Sitrep.Query());

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
