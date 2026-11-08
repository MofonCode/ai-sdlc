namespace Sdlc.Core.Constraints;

/// <summary>What a rule judged item by item is judged over (D-119).</summary>
public enum JudgedPer
{
    /// <summary>Each of the spec's acceptance criteria.</summary>
    Criterion,

    /// <summary>Each of the intent's wanted outcomes and must-not-change items.</summary>
    Outcome,
}

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
/// <param name="Judged">
/// When the rule is decided one item at a time, what the items are; null for a rule judged over the
/// document whole. A reader answers such a rule with a row per item, so every instance of a repeated
/// gap surfaces in one round rather than one a round (D-119).
/// </param>
/// <param name="Areas">
/// The areas a solution rule applies to, as its <c>Areas:</c> line names them; null or empty for a rule that
/// applies everywhere. Only this solution's own rules carry one (D-150).
/// </param>
public sealed record Constraint(
    ConstraintId Id,
    string Statement,
    string AppliesTo,
    string Forbids,
    string Why,
    ConstraintId? SupersededBy = null,
    JudgedPer? Judged = null,
    IReadOnlyList<string>? Areas = null)
{
    /// <summary>The areas it applies to; empty when it applies everywhere.</summary>
    public IReadOnlyList<string> InAreas => Areas ?? [];

    /// <summary>
    /// Whether this constraint still binds. A superseded constraint is kept for the record and is
    /// not part of an effective set.
    /// </summary>
    public bool IsInForce => SupersededBy is null;
}
