using System.Text.RegularExpressions;

namespace Sdlc.Core.Operate;

/// <summary>
/// Strips personal data from text before it leaves the telemetry store (D-89).
/// </summary>
/// <remarks>
/// <para><strong>Before anything is filed, not after.</strong> Logs carry personal data, and a work item is
/// read by everybody with access to the board and kept for as long as the board is — so what is removed is
/// removed before it is written anywhere.</para>
/// <para><strong>Patterns, so it errs wide.</strong> Email addresses, IP addresses, bearer tokens and
/// credentials in query strings, long digit runs that could be card or account numbers, and identifiers that
/// look like GUIDs. A message that loses an id it needed is a lead a person can still read; one that keeps an
/// email address is a leak that cannot be taken back.</para>
/// </remarks>
public static partial class Scrub
{
    /// <summary>Replaces what looks like personal data with a label saying what was there.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each match replaced.</returns>
    public static string Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string scrubbed = Bearer().Replace(text, "Bearer [token]");
        scrubbed = Secret().Replace(scrubbed, "$1=[secret]");
        scrubbed = Email().Replace(scrubbed, "[email]");
        scrubbed = Guid().Replace(scrubbed, "[id]");
        scrubbed = IPv4().Replace(scrubbed, "[ip]");
        scrubbed = IPv6().Replace(scrubbed, "[ip]");
        return Digits().Replace(scrubbed, "[number]");
    }

    [GeneratedRegex("Bearer\\s+[A-Za-z0-9\\-._~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex Bearer();

    [GeneratedRegex("(?i)\\b(password|pwd|secret|token|api[_-]?key|access[_-]?key|sig)=[^&\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex Secret();

    [GeneratedRegex("[A-Za-z0-9._%+\\-]+@[A-Za-z0-9.\\-]+\\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex("\\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex Guid();

    [GeneratedRegex("\\b(?:\\d{1,3}\\.){3}\\d{1,3}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex IPv4();

    [GeneratedRegex("\\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex IPv6();

    [GeneratedRegex("\\b\\d{9,}\\b", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();
}
