using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using MofonCode.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;
using Sdlc.Core.Decisions;
using Sdlc.Core.Solutions;
using Sdlc.Core.Successors;

namespace Sdlc.Core.Reports;

/// <summary>
/// Resolves the ids a report or a hand-back names -- a rule, a decision, a successor -- to what each says, so a
/// person reads <c>LOC_03 — &lt;its statement&gt;</c> rather than a bare label, and nobody retypes the text.
/// </summary>
/// <remarks>
/// <para><strong>The binary resolves the text; the agent never retypes it.</strong> A rule's statement comes from
/// the constraints in force, a decision's from <c>decisions.md</c>, a successor's outcome from its file under
/// <c>.sdlc/successors/</c>. Files the binary records keep the bare id, since that is what it matches on; this is
/// only for what a person reads.</para>
/// <para><strong>An id that resolves to nothing says so</strong> rather than being dropped, because a name
/// that points nowhere is the thing a person most needs to see.</para>
/// </remarks>
public static class IdTexts
{
    // A rule id, a decision id, or successor:<slug>, wherever it stands in a line.
    private static readonly Regex _id = new(
        @"(?<![A-Za-z0-9_])(?:(?:CON|PRO|LOC)_[0-9]{2,4}(?:\.[0-9]{1,3})?|DEC_[0-9]{2,4}|successor:[a-z0-9]+(?:-[a-z0-9]+)*)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Registers this operation's handler.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddIdTextsRequestHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddTransient<IRequestHandler<Query, Response>, Handler>();
    }

    /// <summary>Asks what each id says.</summary>
    /// <param name="Ids">The ids, as written: <c>LOC_03</c>, <c>DEC_24</c>, <c>successor:deployment</c>.</param>
    public sealed record Query(IReadOnlyList<string> Ids) : IRequest<Response>;

    /// <summary>Each id with its text, in the order asked.</summary>
    /// <param name="Named">The ids.</param>
    public sealed record Response(IReadOnlyList<Named> Named);

    /// <summary>One id and what it says.</summary>
    /// <param name="Id">The id, as written.</param>
    /// <param name="Text">What it says, or null when it resolves to nothing.</param>
    /// <param name="Standing">
    /// Whether it binds or exists now: a rule in force, a standing decision, a successor that has a file or was
    /// started. False for one superseded, and for one not found.
    /// </param>
    public sealed record Named(string Id, string? Text, bool Standing);

    /// <summary>Every rule id, decision id and <c>successor:&lt;slug&gt;</c> in a text, once each, in order.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The ids, as written.</returns>
    public static IReadOnlyList<string> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return [.. _id.Matches(text).Select(match => match.Value).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Whether a text is one id this resolves: a rule, a decision, or <c>successor:&lt;slug&gt;</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>True when the whole text is one such id.</returns>
    public static bool IsId(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Match match = _id.Match(text.Trim());
        return match.Success && match.Length == text.Trim().Length;
    }

    /// <summary>The <c>Named:</c> block: one line per id with its text, or nothing when there are none.</summary>
    /// <param name="named">The ids.</param>
    /// <returns>The text.</returns>
    public static string Render(IReadOnlyList<Named> named)
    {
        ArgumentNullException.ThrowIfNull(named);

        if (named.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder text = new();
        text.AppendLine("Named:");

        foreach (Named id in named)
        {
            text.AppendLine("  " + id.Id + " — " + (id.Text ?? "not found"));
        }

        return text.ToString();
    }

    private sealed class Handler(SolutionRoot solution, Mediator mediator) : IRequestHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            List<string> ids = [.. request.Ids.Select(id => id.Trim().Trim('`')).Distinct(StringComparer.Ordinal)];
            EffectiveConstraints.Response? rules = null;
            ReadDecisions.Response? decisions = null;
            Dictionary<string, CycleId>? started = null;
            List<Named> named = [];

            foreach (string id in ids)
            {
                if (ConstraintId.TryParse(id, out ConstraintId? rule))
                {
                    if (rules is null)
                    {
                        Result<EffectiveConstraints.Response> read = await mediator
                            .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(), cancellationToken)
                            .ConfigureAwait(false);

                        if (read.IsFailed)
                        {
                            return read.ToResult<Response>();
                        }

                        rules = read.Value;
                    }

                    named.Add(Rule(id, rule.Value, rules));
                }
                else if (DecisionId.TryParse(id, out DecisionId? decision))
                {
                    if (decisions is null)
                    {
                        Result<ReadDecisions.Response> read = await mediator
                            .Send<ReadDecisions.Query, ReadDecisions.Response>(new ReadDecisions.Query(IncludeSuperseded: true), cancellationToken)
                            .ConfigureAwait(false);

                        if (read.IsFailed)
                        {
                            return read.ToResult<Response>();
                        }

                        decisions = read.Value;
                    }

                    Decision? found = decisions.Decisions.FirstOrDefault(entry => entry.Id.Number == decision.Value.Number);
                    named.Add(found is null
                        ? new Named(id, null, false)
                        : new Named(id, found.Statement + (found.IsStanding ? string.Empty : " (superseded by " + found.SupersededBy + ")"), found.IsStanding));
                }
                else if (id.StartsWith(SuccessorReference.Prefix, StringComparison.Ordinal) && Successor.IsSlug(id[SuccessorReference.Prefix.Length..]))
                {
                    started ??= await ReadSuccessors.Started(solution.Path, cancellationToken).ConfigureAwait(false);
                    named.Add(await SuccessorText(id, id[SuccessorReference.Prefix.Length..], started, cancellationToken).ConfigureAwait(false));
                }
                else
                {
                    named.Add(new Named(id, null, false));
                }
            }

            return Result.Ok(new Response(named));
        }

        // In force, it binds: its statement. Superseded, it is kept for the record and says what replaced it. A rule
        // is matched by tier and number, since a revision is part of the id's equality and a cited one may lag.
        private static Named Rule(string id, ConstraintId rule, EffectiveConstraints.Response rules)
        {
            Constraint? inForce = rules.Constraints.FirstOrDefault(entry => entry.Id.Tier == rule.Tier && entry.Id.Number == rule.Number);

            if (inForce is not null)
            {
                return new Named(id, inForce.Statement, true);
            }

            Constraint? superseded = rules.Superseded.FirstOrDefault(entry => entry.Id.Tier == rule.Tier && entry.Id.Number == rule.Number);

            return superseded is null
                ? new Named(id, null, false)
                : new Named(id, superseded.Statement + " (superseded by " + superseded.SupersededBy + ")", false);
        }

        private async Task<Named> SuccessorText(string id, string slug, Dictionary<string, CycleId> started, CancellationToken cancellationToken)
        {
            string path = Path.Combine(Successor.DirectoryOf(solution.Path), slug + ".md");

            if (File.Exists(path))
            {
                Result<Successor> parsed = Successor.Parse(slug, await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));

                return new Named(id, parsed.IsSuccess ? Flat(parsed.Value.Outcome) : "its file is here but does not read", true);
            }

            return started.TryGetValue(slug, out CycleId cycle)
                ? new Named(id, string.Create(CultureInfo.InvariantCulture, $"started as cycle {cycle}"), true)
                : new Named(id, null, false);
        }

        private static string Flat(string text) =>
            string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
