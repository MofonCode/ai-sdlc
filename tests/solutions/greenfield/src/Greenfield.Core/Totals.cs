namespace Greenfield.Core;

public static class Totals
{
    /// <summary>
    /// Totals a sequence of amounts. Throws on an empty sequence, which is the defect the sample
    /// artifacts describe: callers guard the call site instead, and the guard has been copied twice.
    /// </summary>
    public static decimal Sum(IEnumerable<decimal> amounts) => amounts.Aggregate((a, b) => a + b);
}
