using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Cycles;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Evidence;

/// <summary>What one cycle claims.</summary>
/// <param name="Cycle">The cycle.</param>
/// <param name="Files">The files its plan declared.</param>
/// <param name="NeedsServices">
/// Whether its plan stands up a backing service, which caps how wide a batch may run.
/// </param>
public sealed record Claim(CycleId Cycle, IReadOnlyList<string> Files, bool NeedsServices);

/// <summary>Two cycles that claim a file in common.</summary>
/// <param name="First">The earlier cycle, which goes first.</param>
/// <param name="Second">The later cycle, which rebases on it.</param>
/// <param name="Files">What they both claim.</param>
public sealed record Collision(CycleId First, CycleId Second, IReadOnlyList<string> Files);

/// <summary>Asks which cycles may be built together.</summary>
public sealed record ClaimGraphQuery : IRequest<ClaimGraph>;

/// <summary>
/// Which cycles can be built at once, and which have to wait for which.
/// </summary>
/// <param name="Claims">What each cycle claims, in cycle-id order.</param>
/// <param name="Collisions">Every pair that overlaps.</param>
/// <param name="Groups">Cycles that may run together, each group in cycle-id order.</param>
public sealed record ClaimGraph(
    IReadOnlyList<Claim> Claims,
    IReadOnlyList<Collision> Collisions,
    IReadOnlyList<IReadOnlyList<CycleId>> Groups);

/// <summary>
/// Computes which approved cycles collide, before anything is built.
/// </summary>
/// <remarks>
/// Plans declare the files they claim, so a collision is knowable up front rather than discoverable in a
/// merge. This is deterministic work -- a set intersection over a file table -- so the binary owns it. A
/// batch skill that worked the graph out by reading plans would get it slightly differently each run, and
/// the thing it decides is what runs concurrently.
/// <para>
/// <strong>It schedules rather than resolves.</strong> A merge you did not have to do is worth more than a
/// merge you did well, and the ordering it picks is cycle-id order, which is chronological -- taken rather
/// than declared, because the skill that would have declared dependencies was removed with <c>split</c>.
/// </para>
/// </remarks>
/// <param name="solution">The solution to read.</param>
public sealed class ClaimGraphHandler(SolutionRoot solution) : IRequestHandler<ClaimGraphQuery, ClaimGraph>
{
    /// <summary>
    /// Words in a plan that mean it stands up something a developer would otherwise start by hand.
    /// </summary>
    /// <remarks>
    /// A heuristic, and it errs toward caution: a false positive costs a batch some concurrency, while a
    /// miss means two runs sharing a named volume -- the same database files, two writers -- which fails
    /// strangely rather than loudly.
    /// </remarks>
    private static readonly string[] ServiceWords =
    [
        "AppHost", "app host", "Aspire", "SQL Server", "SqlServer", "Redis", "Service Bus",
        "ServiceBus", "Cosmos", "container", "docker", "migration", "WithDataVolume",
    ];

    /// <inheritdoc />
    public async Task<Result<ClaimGraph>> Handle(
        ClaimGraphQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<Claim> claims = [];

        foreach (Cycle cycle in CycleIndex.All(solution.Path))
        {
            string plan = cycle.PathOf(ArtifactKind.Plan);

            // A cycle with no plan claims nothing, because there is nothing to have claimed with. It is
            // skipped rather than reported: this answers "what may run together", and a cycle that cannot
            // run is not part of that answer.
            if (!File.Exists(plan))
            {
                continue;
            }

            string text = await File.ReadAllTextAsync(plan, cancellationToken).ConfigureAwait(false);

            claims.Add(new Claim(
                cycle.Id,
                DeclaredFiles.Read(text),
                ServiceWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))));
        }

        List<Collision> collisions = [];

        for (int first = 0; first < claims.Count; first++)
        {
            for (int second = first + 1; second < claims.Count; second++)
            {
                List<string> shared = [.. claims[first].Files
                    .Intersect(claims[second].Files, StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.Ordinal)];

                if (shared.Count > 0)
                {
                    collisions.Add(new Collision(claims[first].Cycle, claims[second].Cycle, shared));
                }
            }
        }

        return Result.Ok(new ClaimGraph(claims, collisions, Partition(claims, collisions)));
    }

    /// <summary>
    /// Groups cycles so that nothing in a group collides with anything else in it.
    /// </summary>
    /// <remarks>
    /// Greedy rather than optimal: each cycle joins the first group it does not collide with, in cycle-id
    /// order. An optimal packing would run marginally wider and would also mean a batch's ordering changed
    /// when an unrelated cycle was added, which makes a failed run harder to reason about than it is worth.
    /// </remarks>
    private static IReadOnlyList<IReadOnlyList<CycleId>> Partition(
        IReadOnlyList<Claim> claims,
        IReadOnlyList<Collision> collisions)
    {
        List<List<CycleId>> groups = [];

        foreach (Claim claim in claims)
        {
            List<CycleId>? home = groups.Find(group => !group.Any(member => Collides(collisions, member, claim.Cycle)));

            if (home is null)
            {
                groups.Add([claim.Cycle]);
                continue;
            }

            home.Add(claim.Cycle);
        }

        return [.. groups.Select(group => (IReadOnlyList<CycleId>)group)];
    }

    private static bool Collides(IReadOnlyList<Collision> collisions, CycleId one, CycleId other) =>
        collisions.Any(collision =>
            (collision.First == one && collision.Second == other)
            || (collision.First == other && collision.Second == one));
}

/// <summary>
/// Renders the claim graph for a batch run.
/// </summary>
/// <remarks>
/// Read by a person deciding whether a batch is worth starting, and by the skill that runs it. So it says
/// what may run together, what has to wait, and what each wait is about -- a group count on its own tells
/// nobody whether the serialisation is reasonable.
/// </remarks>
public static class ClaimGraphRenderer
{
    private const string Indent = "  ";

    /// <summary>Renders a claim graph.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The rendered text, ending in a newline.</returns>
    public static string Render(ClaimGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        System.Text.StringBuilder output = new();

        output.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Indent}claims - {graph.Claims.Count} cycle{(graph.Claims.Count == 1 ? string.Empty : "s")} with a plan"));

        output.AppendLine();

        if (graph.Claims.Count == 0)
        {
            output.AppendLine(Indent + "Nothing to build.");
            return output.ToString();
        }

        foreach (Claim claim in graph.Claims)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}{claim.Cycle}  {claim.Files.Count} file{(claim.Files.Count == 1 ? string.Empty : "s")}{(claim.NeedsServices ? "  needs backing services" : string.Empty)}"));
        }

        output.AppendLine();

        for (int index = 0; index < graph.Groups.Count; index++)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}group {index + 1}   {string.Join(", ", graph.Groups[index])}"));
        }

        if (graph.Collisions.Count == 0)
        {
            output.AppendLine();
            output.AppendLine(Indent + "No collisions. Every cycle may run at once.");
            return output.ToString();
        }

        output.AppendLine();
        output.AppendLine(Indent + "Collisions, which is why there is more than one group:");
        output.AppendLine();

        foreach (Collision collision in graph.Collisions)
        {
            output.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{Indent}    {collision.First} and {collision.Second}"));

            foreach (string file in collision.Files)
            {
                output.AppendLine(Indent + "        " + file);
            }
        }

        output.AppendLine();
        output.AppendLine(Indent + "The later cycle rebases on the earlier one. The order is cycle id, which is");
        output.AppendLine(Indent + "chronological -- taken rather than declared, because nothing in a plan says");
        output.AppendLine(Indent + "which cycle depends on which.");

        if (graph.Claims.Any(claim => claim.NeedsServices))
        {
            output.AppendLine();
            output.AppendLine(Indent + "Some plans stand up backing services. Those run under a low cap, and the run");
            output.AppendLine(Indent + "id belongs on container names, volume names and database names -- not only on");
            output.AppendLine(Indent + "ports. Two app hosts sharing a named volume is two writers on one database.");
        }

        return output.ToString();
    }
}
