namespace Greenfield.Core;

/// <summary>
/// The two call sites the fixture artifacts refer to. Both guard before calling
/// <see cref="Totals.Sum"/> because it throws on an empty sequence, and the guard is the duplication
/// the intent fixture describes.
/// </summary>
public static class Invoicing
{
    public static decimal LineTotal(IReadOnlyCollection<decimal> lines) =>
        lines.Count == 0 ? 0m : Totals.Sum(lines);

    public static decimal CreditTotal(IReadOnlyCollection<decimal> credits) =>
        credits.Count == 0 ? 0m : Totals.Sum(credits);
}
