namespace Greenfield.Core;

public static class Totals
{
    public static decimal Sum(IEnumerable<decimal> amounts) => amounts.Sum();
}
