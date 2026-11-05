namespace Sdlc.Core.Findings;

/// <summary>
/// Two or more rules that cannot both be satisfied by any version of one artifact.
/// </summary>
/// <remarks>
/// <para><strong>Not a finding, and not a third severity.</strong> A finding is an objection to the
/// artifact: a correct artifact exists and this is not it. A conflict is an objection to the rule set
/// as it lands on this situation: no correct artifact exists, because every edit that satisfies one
/// rule breaks the other. D-30 deliberately reduced three severities to two, and a <c>conflict</c>
/// severity would resurrect exactly what it removed.</para>
///
/// <para><strong>This is the one place multiplicity belongs.</strong> A finding carries one citation
/// because it is one objection resting on one rule, which is what keeps the three dispositions
/// meaning something. A conflict is about more than one rule by definition, so
/// <paramref name="Between"/> is a list — two entries or more.</para>
///
/// <para><strong>No id and no disposition, unlike a finding.</strong> A conflict is a per-round
/// observation rather than a tracked objection. It is settled the way everything else here is
/// settled, by the next fresh reader's silence (D-49): a conflict declared in round two and not
/// declared in round three means the refiner found an exit, and `CON_16`'s <c>Out of scope</c> and
/// `CON_14`'s <c>## Open</c> exist to be exactly that. So the conflicts that stand are the latest
/// round's, which is what <see cref="FindingsReader.StandingConflicts"/> returns.</para>
///
/// <para><strong>One declaration does not settle anything.</strong> A declared conflict is an
/// assertion, and nothing in this framework advances on assertion — it is also an escape hatch, since
/// an agent stuck on a blocker it cannot answer has an obvious out in calling it a conflict. What
/// makes it usable is that a reader is handed only the constraints, the artifact and the artifact
/// above it, and never the prior rounds: two readers declaring the same conflict are two uninvested
/// processes agreeing with no knowledge of each other. That corroboration is counted rather than
/// trusted, and the counting is not here.</para>
/// </remarks>
/// <param name="Between">
/// The rules that cannot both hold, each as the id and the one line its heading states. Two or more:
/// one rule cannot conflict with itself, and a single-entry conflict is a finding wearing the wrong
/// shape. More than two is permitted — three mutually unsatisfiable rules is conceivable, and capping
/// it at two would be a limit with no argument behind it.
/// </param>
/// <param name="Statement">
/// Why each remedy excludes the other, in full. A conflict with no explanation is unarguable, which is
/// what <c>CON_10.2</c> objects to in a gate refusal and what the same reasoning objects to here.
/// </param>
/// <param name="Locus">Where in the artifact the two rules meet.</param>
public sealed record Conflict(
    IReadOnlyList<Citation>? Between,
    string? Statement,
    string? Locus = null)
{
    /// <summary>
    /// The ids of the rules in conflict, as a reader meets them on one line.
    /// </summary>
    /// <remarks>
    /// Beside the model rather than at each call site, for the reason <c>ConstraintRenderer</c> gives
    /// for living beside its own: the status panel and the findings history both show this, and two
    /// formats would be two answers to one question.
    /// </remarks>
    /// <returns>The ids joined, or an empty string when nothing was named.</returns>
    public string Ids() => Between is null
        ? string.Empty
        : string.Join(" and ", Between.Select(citation => citation.Id));
}
