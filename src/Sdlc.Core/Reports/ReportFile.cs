using System.Globalization;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Reports;

/// <summary>
/// Writes a report beside the solution, where a person can work from it rather than from scrollback.
/// </summary>
/// <remarks>
/// <para><strong>A report is not a record, and the difference is load-bearing.</strong> Every other file
/// the framework writes is read back by something — the gate reads the rounds, <c>verify</c> reads the
/// evidence, <c>land</c> reads the ledger. Nothing reads one of these, ever. The moment a tool did, a
/// stale one would be state that a tool trusts, which is the trap iteration 2's corpus fell into and
/// §19 item 3 still carries forward: an artifact whose existence means <em>done</em> silently loses work when
/// it is mistaken.</para>
///
/// <para><strong>So the directory ignores itself.</strong> It carries its own <c>.gitignore</c> rather
/// than an entry merged into the repository's, which keeps the whole thing a file the framework named
/// under D-40 — nothing of a team's is touched, and an existing solution gets the ignore the first time
/// it writes a report rather than needing an adoption re-run. Tracking these would be worse than untidy:
/// an untracked report left in the tree reaches a build's diff as a file no plan declared, and
/// <c>CON_07.2</c> is the rule that a change does only what its plan said.</para>
/// </remarks>
public static class ReportFile
{
    /// <summary>The directory reports are written to, inside the framework's own.</summary>
    public const string DirectoryName = "reports";

    /// <summary>
    /// What the directory's own ignore file says.
    /// </summary>
    /// <remarks>
    /// The negation keeps the ignore file itself tracked, so a clone arrives already ignoring reports
    /// rather than ignoring them only on the machine that wrote one.
    /// </remarks>
    private static readonly string[] _ignore =
    [
        "# Reports are not records. Nothing a command writes here is read back by anything,",
        "# and an untracked one reaching a build's diff would be a file no plan declared.",
        "*",
        "!.gitignore",
    ];

    /// <summary>
    /// Writes one report and returns where it went.
    /// </summary>
    /// <param name="solutionRoot">The root of the solution being reported on.</param>
    /// <param name="kind">
    /// What the report is of — <c>constraints</c>, <c>suggestions</c>. Becomes the second half of the
    /// file name, after the timestamp, so a directory listing sorts by when and reads by what.
    /// </param>
    /// <param name="body">The rendered report, exactly as it was printed.</param>
    /// <param name="at">When the report was produced, which names the file.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The path written.</returns>
    public static async Task<string> Write(
        string solutionRoot,
        string kind,
        string body,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(body);

        string directory = Path.Combine(solutionRoot, Cycle.SdlcDirectoryName, DirectoryName);
        Directory.CreateDirectory(directory);

        string ignore = Path.Combine(directory, ".gitignore");
        if (!File.Exists(ignore))
        {
            await File.WriteAllLinesAsync(ignore, _ignore, cancellationToken).ConfigureAwait(false);
        }

        string path = Path.Combine(
            directory,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{at.ToUniversalTime():yyyyMMdd_HHmm_ss}-{kind}.md"));

        // The heading is on the file rather than on what was printed, because the terminal copy needs
        // no warning -- a person is looking at it as it is produced. A file found weeks later does.
        string[] lines =
        [
            string.Create(CultureInfo.InvariantCulture, $"# {kind} — {at.ToUniversalTime():u}"),
            string.Empty,
            "A report, not a record. Safe to delete, read back by nothing, and already out of date if",
            "a cycle has run since. Re-run the command rather than trusting this.",
            string.Empty,
            "---",
            string.Empty,
            body,
        ];

        await File.WriteAllTextAsync(
            path,
            string.Join(Environment.NewLine, lines),
            cancellationToken).ConfigureAwait(false);

        return path;
    }
}
