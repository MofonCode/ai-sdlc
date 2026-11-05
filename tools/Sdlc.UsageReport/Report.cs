using System.Globalization;
using System.Text;

namespace Sdlc.UsageReport;

/// <summary>Plain-text tables and the numbers in them.</summary>
internal static class Report
{
    /// <summary>Prints one table with a title, columns padded to their widest cell.</summary>
    /// <param name="title">The table's title.</param>
    /// <param name="header">The column names.</param>
    /// <param name="rows">The rows.</param>
    public static void Table(string title, string[] header, IEnumerable<string[]> rows)
    {
        List<string[]> all = [header, .. rows];
        int[] widths = [.. Enumerable.Range(0, header.Length).Select(column => all.Max(row => row[column].Length))];

        Console.WriteLine();
        Console.WriteLine(title);

        foreach (string[] row in all)
        {
            StringBuilder line = new("  ");

            for (int column = 0; column < row.Length; column++)
            {
                line.Append(row[column].PadRight(widths[column] + 2));
            }

            Console.WriteLine(line.ToString().TrimEnd());
        }
    }

    /// <summary>A token count in millions or thousands.</summary>
    /// <param name="count">The count.</param>
    /// <returns>As 1.2M or 340k.</returns>
    public static string Tokens(double count) => count >= 1_000_000
        ? (count / 1_000_000).ToString("0.0", CultureInfo.InvariantCulture) + "M"
        : (count / 1_000).ToString("0", CultureInfo.InvariantCulture) + "k";

    /// <summary>One part of a whole, as a percentage.</summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole.</param>
    /// <returns>As 40%.</returns>
    public static string Share(double part, double whole) => whole <= 0
        ? "-"
        : (part / whole).ToString("0%", CultureInfo.InvariantCulture);

    /// <summary>The middle value.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The median, or 0 when there are none.</returns>
    public static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.Order()];

        return sorted.Length == 0
            ? 0
            : sorted.Length % 2 == 1
                ? sorted[sorted.Length / 2]
                : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
    }

    /// <summary>Every transcript as one CSV row, for a spreadsheet.</summary>
    /// <param name="sessions">The transcripts.</param>
    /// <returns>The lines, header first.</returns>
    public static IEnumerable<string> Csv(IEnumerable<SessionUsage> sessions)
    {
        yield return "folder,session,role,start,calls,input,cache_write,cache_read,output,max_context,weighted,head";

        foreach (SessionUsage session in sessions)
        {
            yield return string.Join(
                ',',
                Quote(session.Folder),
                session.Session,
                Quote(session.Role),
                session.Start?.UtcDateTime.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
                session.Calls.ToString(CultureInfo.InvariantCulture),
                session.Input.ToString(CultureInfo.InvariantCulture),
                session.CacheWrite.ToString(CultureInfo.InvariantCulture),
                session.CacheRead.ToString(CultureInfo.InvariantCulture),
                session.Output.ToString(CultureInfo.InvariantCulture),
                session.MaxContext.ToString(CultureInfo.InvariantCulture),
                session.Weighted.ToString("0", CultureInfo.InvariantCulture),
                Quote(session.Head));
        }
    }

    private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
