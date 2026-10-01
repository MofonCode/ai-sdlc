namespace Sdlc.Core.Constraints;

/// <summary>
/// One rule an artifact is judged against, with the three clauses that make it arguable rather
/// than arbitrary.
/// </summary>
/// <param name="Id">The constraint's identity and wording revision.</param>
/// <param name="Statement">The rule in one line, as the heading states it.</param>
/// <param name="AppliesTo">Which artifact, and at what moment.</param>
/// <param name="Forbids">What becomes illegal. Never a restatement of <paramref name="Statement"/>.</param>
/// <param name="Why">What goes wrong if the rule is absent — the cost it avoids.</param>
/// <param name="SupersededBy">
/// The constraint that replaced this one, when what it forbids has changed. A superseded
/// constraint stays on the page so a finding that cited it remains readable.
/// </param>
public sealed record Constraint(
    ConstraintId Id,
    string Statement,
    string AppliesTo,
    string Forbids,
    string Why,
    ConstraintId? SupersededBy = null)
{
    /// <summary>
    /// Whether this constraint still binds. A superseded constraint is kept for the record and is
    /// not part of an effective set.
    /// </summary>
    public bool IsInForce => SupersededBy is null;
}
