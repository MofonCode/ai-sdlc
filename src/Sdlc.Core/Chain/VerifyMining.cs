using System.Globalization;
using System.Text;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Areas;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Mining;
using Sdlc.Core.Solutions;

namespace Sdlc.Core.Chain;

/// <summary>
/// The check on a pull request that carries a mining sitting and no cycle: the records a person placed, the
/// ledger, successors, and the deletion of mined cycles' folders (D-152).
/// </summary>
/// <remarks>
/// <para><strong>A husk can only leave through a pull request</strong>, and a pull request with no governed cycle
/// is refused (D-45). Mining is the one other thing that reaches the integration branch without a cycle,
/// because it is not a change to the code: nothing in it is built, and every file it touches is one a person
/// places or the binary writes.</para>
/// <para><strong>Chosen by what the diff carries</strong>, like an upgrade (D-133): the branch can be called
/// anything. One path outside the records, the ledger and the successors -- a source file, a cycle's document
/// rather than its deletion -- and it is not mining, and is refused for having no chain.</para>
/// </remarks>
public static class VerifyMining
{
    private static readonly HashSet<string> _records = new(StringComparer.OrdinalIgnoreCase)
    {
        Cycle.SdlcDirectoryName + "/" + FileConstraintDocuments.SolutionConstraintsFileName,
        Cycle.SdlcDirectoryName + "/" + DecisionParser.FileName,
        Cycle.SdlcDirectoryName + "/" + Cycle.TechStackFileName,
        Cycle.SdlcDirectoryName + "/" + AreaParser.FileName,
        Cycle.SdlcDirectoryName + "/" + MinedLedger.FileName,
    };

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddVerifyMiningRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, VerifyUpgrade.Response>, Handler>();
    }

    /// <summary>Checks one pull request's changes.</summary>
    /// <param name="Changed">The paths it changes, relative to the root.</param>
    public sealed record Query(IReadOnlyList<string> Changed) : IRequest<VerifyUpgrade.Response>;

    /// <summary>Whether a pull request's changes are only what a mining sitting may change.</summary>
    /// <param name="changed">The changed paths, relative to the root, with either separator.</param>
    /// <returns>True when there is at least one, and every one is.</returns>
    public static bool Carries(IReadOnlyList<string> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        string cycles = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/";

        return changed.Count > 0 && changed.Select(Normalised).All(path =>
            _records.Contains(path)
            || Successors.Successor.IsSuccessorPath(path)
            || path.StartsWith(cycles, StringComparison.Ordinal));
    }

    /// <summary>Renders the verdict.</summary>
    /// <param name="verdict">What the check found.</param>
    /// <returns>The text.</returns>
    public static string Render(VerifyUpgrade.Response verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        StringBuilder output = new();
        output.AppendLine();
        output.AppendLine("  mining - records, the ledger, and husks deleted");
        output.AppendLine();

        foreach (VerifyChain.ChainCheck check in verdict.Checks)
        {
            output.AppendLine((check.Passed ? "    [ok]  " : "    [no]  ") + check.Name);
            output.AppendLine("          " + check.Detail);
        }

        output.AppendLine();
        output.AppendLine(verdict.Holds
            ? "  It holds, and needs no cycle: it carries what a person placed from mining, the ledger, and the folders of cycles the ledger records as mined."
            : "  It does not hold. A mining pull request carries the records, the ledger, successors and mined cycles' deletions, and nothing else.");

        return output.ToString();
    }

    private static string Normalised(string path) => path.Replace('\\', '/').TrimStart('/');

    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Query, VerifyUpgrade.Response>
    {
        public async Task<Result<VerifyUpgrade.Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<VerifyChain.ChainCheck> checks = [];
            Result<MinedLedger> ledger = await MinedLedger.Read(solution.Path, cancellationToken).ConfigureAwait(false);

            checks.Add(ledger.IsFailed
                ? new VerifyChain.ChainCheck("the mining ledger reads", false, string.Join(" ", ledger.Errors.Select(error => error.Message)))
                : new VerifyChain.ChainCheck("the mining ledger reads", true, string.Create(CultureInfo.InvariantCulture, $"{ledger.ValueOrDefault?.Cycles.Count ?? 0} mined cycles recorded.")));

            // Each cycle path is a deletion of a cycle the ledger records: a husk. Anything else under the cycles
            // directory is a cycle's own work, which only that cycle's pull request carries.
            string cycles = Cycle.SdlcDirectoryName + "/" + Cycle.CyclesDirectoryName + "/";
            List<string> notHusks = [];
            HashSet<string> deleted = new(StringComparer.Ordinal);

            foreach (string path in request.Changed.Select(Normalised).Where(path => path.StartsWith(cycles, StringComparison.Ordinal)))
            {
                string id = path[cycles.Length..].Split('/')[0];
                bool gone = !File.Exists(Path.Combine(solution.Path, path));

                if (!gone || ledger.ValueOrDefault?.Find(id) is null)
                {
                    notHusks.Add(path);
                }
                else
                {
                    deleted.Add(id);
                }
            }

            checks.Add(notHusks.Count == 0
                ? new VerifyChain.ChainCheck("every cycle it touches is a husk it deletes", true, string.Create(CultureInfo.InvariantCulture, $"{deleted.Count} mined cycle folder{(deleted.Count == 1 ? string.Empty : "s")} deleted."))
                : new VerifyChain.ChainCheck("every cycle it touches is a husk it deletes", false, "carries " + string.Join(", ", notHusks.Take(5)) + (notHusks.Count > 5 ? " and more" : string.Empty) + ": a cycle's folder leaves only once the ledger records it mined, and only by deletion."));

            checks.Add(await RecordsRead(cancellationToken).ConfigureAwait(false));

            if (request.Changed.Select(Normalised).Any(Successors.Successor.IsSuccessorPath))
            {
                checks.Add(await VerifyChain.SuccessorsSound(mediator, cancellationToken).ConfigureAwait(false));
            }

            return Result.Ok(new VerifyUpgrade.Response(null, null, checks));
        }

        // The records as placed: each parses, no area covers nothing, and no entry names an area nobody defines.
        private async Task<VerifyChain.ChainCheck> RecordsRead(CancellationToken cancellationToken)
        {
            const string Name = "the records it places read, and their areas hold";
            List<string> problems = [];
            string sdlc = Path.Combine(solution.Path, Cycle.SdlcDirectoryName);

            Result<IReadOnlyList<Area>> areas = await AreaScope.ReadDefined(solution.Path, cancellationToken).ConfigureAwait(false);
            problems.AddRange(areas.Errors.Select(error => error.Message));
            HashSet<string> names = new((areas.ValueOrDefault ?? []).Select(area => area.Name), StringComparer.Ordinal);
            problems.AddRange(AreaScope.CoveringNothing(areas.ValueOrDefault ?? [], solution.Path).Select(name => name + " covers no file"));

            string rules = Path.Combine(sdlc, FileConstraintDocuments.SolutionConstraintsFileName);

            if (File.Exists(rules))
            {
                Result<IReadOnlyList<Constraint>> read = ConstraintParser.Parse(await File.ReadAllTextAsync(rules, cancellationToken).ConfigureAwait(false), rules);
                problems.AddRange(read.Errors.Select(error => error.Message));
                problems.AddRange((read.ValueOrDefault ?? []).Where(rule => rule.IsInForce).SelectMany(rule => rule.InAreas.Where(name => !names.Contains(name)).Select(name => rule.Id + " names '" + name + "'")));
            }

            string decisions = Path.Combine(sdlc, DecisionParser.FileName);

            if (File.Exists(decisions))
            {
                Result<IReadOnlyList<Decision>> read = DecisionParser.Parse(await File.ReadAllTextAsync(decisions, cancellationToken).ConfigureAwait(false), decisions);
                problems.AddRange(read.Errors.Select(error => error.Message));
                problems.AddRange((read.ValueOrDefault ?? []).Where(decision => decision.IsStanding).SelectMany(decision => decision.InAreas.Where(name => !names.Contains(name)).Select(name => decision.Id + " names '" + name + "'")));
            }

            return problems.Count == 0
                ? new VerifyChain.ChainCheck(Name, true, "the rules, the decisions and the areas parse, every area covers a file, and every area an entry names is defined.")
                : new VerifyChain.ChainCheck(Name, false, string.Join("; ", problems));
        }
    }
}
