using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Sdlc.Core.Constraints;

namespace Sdlc.Tests;

[TestClass]
public sealed class EffectiveConstraintsTests
{
    private const string Global = """
        ## CON_01 — An intent states the problem

        **Applies to:** an intent, at approval.

        **Forbids:** an intent naming an implementation.

        **Why:** the spec would have nothing to test.

        ## CON_09 — No artifact restates another's content

        **Applies to:** every artifact.

        **Forbids:** an artifact reproducing what another owns.

        **Why:** two copies diverge.

        ## CON_20 — A retired rule

        **Applies to:** a spec, at approval.

        **Forbids:** something that is now forbidden elsewhere.

        **Why:** it was replaced.

        **Superseded-by:** CON_21
        """;

    private const string Solution = """
        ## LOC_01 — A local rule about plans

        **Applies to:** a plan, at approval.

        **Forbids:** a plan that does not name its owner.

        **Why:** nobody knows who to ask.
        """;

    [TestMethod]
    public async Task The_effective_set_merges_every_tier_in_resolution_order()
    {
        ConstraintSet set = await Resolve(null).ConfigureAwait(false);

        Assert.AreEqual(3, set.Constraints.Count);
        CollectionAssert.AreEqual(
            new[] { "CON_01", "CON_09", "LOC_01" },
            set.Constraints.Select(c => c.Id.ToString()).ToArray(),
            "Global resolves before solution, so a reader sees the framework's rules then the local ones.");
    }

    [TestMethod]
    public async Task A_superseded_constraint_is_reported_separately_and_does_not_bind()
    {
        ConstraintSet set = await Resolve(null).ConfigureAwait(false);

        Assert.IsFalse(set.Constraints.Any(c => c.Id.Number == 20));
        Assert.AreEqual("CON_20", set.Superseded.Single().Id.ToString());
    }

    [TestMethod]
    public async Task Narrowing_to_an_artifact_keeps_the_rules_that_name_it()
    {
        ConstraintSet set = await Resolve("intent").ConfigureAwait(false);

        CollectionAssert.AreEquivalent(
            new[] { "CON_01", "CON_09" },
            set.Constraints.Select(c => c.Id.ToString()).ToArray());
    }

    [TestMethod]
    public async Task A_rule_that_binds_every_artifact_survives_every_narrowing()
    {
        foreach (string artifact in new[] { "intent", "spec", "plan", "verify", "diff" })
        {
            ConstraintSet set = await Resolve(artifact).ConfigureAwait(false);
            Assert.IsTrue(
                set.Constraints.Any(c => c.Id.Number == 9),
                $"CON_09 says it applies to every artifact, so narrowing to {artifact} must keep it.");
        }
    }

    [TestMethod]
    public async Task Narrowing_to_an_artifact_nothing_binds_returns_an_empty_set_rather_than_failing()
    {
        ConstraintSet set = await Resolve("retrospective").ConfigureAwait(false);

        Assert.AreEqual(1, set.Constraints.Count, "Only the every-artifact rule survives.");
    }

    [TestMethod]
    public async Task A_malformed_document_fails_the_query_rather_than_yielding_a_partial_set()
    {
        StubDocuments documents = new(
            [new ConstraintDocument(ConstraintTier.Global, "bad.md", "## CON_01 — A rule\n\n**Forbids:** x\n")]);

        using Gate gate = Resolver(documents);

        Result<ConstraintSet> result = await gate
            .Send<EffectiveConstraints.Request, ConstraintSet>(new EffectiveConstraints.Request());

        Assert.IsTrue(
            result.IsFailed,
            "A set assembled from a document that does not parse would be silently short of rules.");
    }

    private static async Task<ConstraintSet> Resolve(string? artifact)
    {
        StubDocuments documents = new(
        [
            new ConstraintDocument(ConstraintTier.Global, "global.md", Global),
            new ConstraintDocument(ConstraintTier.Solution, "solution.md", Solution),
        ]);

        using Gate gate = Resolver(documents);

        Result<ConstraintSet> result = await gate
            .Send<EffectiveConstraints.Request, ConstraintSet>(new EffectiveConstraints.Request(artifact));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }

    /// <summary>
    /// The gate with the documents substituted.
    /// </summary>
    /// <remarks>
    /// The solution root is irrelevant to this operation and is never read: it resolves constraints
    /// from <see cref="IConstraintDocuments"/>, and a stub registered ahead of <c>AddSdlc</c> means
    /// the real file reader is never built. The temp path is there because the container asks for a
    /// root, not because this test has anything to do with a solution.
    /// </remarks>
    private static Gate Resolver(StubDocuments documents) =>
        Gate.For(Path.GetTempPath(), services => services.AddSingleton<IConstraintDocuments>(documents));

    private sealed class StubDocuments(IReadOnlyList<ConstraintDocument> documents) : IConstraintDocuments
    {
        public Task<IReadOnlyList<ConstraintDocument>> Enumerate(CancellationToken cancellationToken) =>
            Task.FromResult(documents);
    }
}
