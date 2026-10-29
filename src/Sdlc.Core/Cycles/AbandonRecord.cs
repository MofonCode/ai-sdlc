using System.Globalization;
using FluentResults;
using Sdlc.Core.Artifacts;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sdlc.Core.Cycles;

/// <summary>
/// Why a cycle stopped.
/// </summary>
/// <remarks>
/// An abandoned cycle keeps its directory. The reason is the only thing the cycle produced, and a
/// repository holding the intents that were tried and dropped -- with the objections that killed them
/// -- is readable history. Abandonment is terminal: a cycle with one of these never builds, even if it
/// also holds an approval.
/// </remarks>
/// <param name="Cycle">The cycle that stopped.</param>
/// <param name="By">Who stopped it.</param>
/// <param name="At">When, in UTC.</param>
/// <param name="Reason">Why, in the author's own words.</param>
public sealed record AbandonRecord(string Cycle, string By, DateTimeOffset At, string Reason)
{
    /// <summary>
    /// Reads an <c>abandoned.md</c>.
    /// </summary>
    /// <remarks>
    /// <strong>Nothing in the binary calls this, and it is not dead.</strong> Abandonment is read by
    /// existence alone — <see cref="Cycle.IsAbandoned"/> asks whether the file is there, because the
    /// fact that stops a cycle is its presence and not anything inside it. What this is for is the
    /// other direction: <c>sdlc draft abandon</c> prints a record for a person to place, and the
    /// suite reads that output back through here. A drafter whose output cannot be parsed prints
    /// something nobody can use, and this is the only thing that would notice.
    /// </remarks>
    /// <param name="text">The file's contents.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The record, or a failure saying what is wrong with it.</returns>
    public static Result<AbandonRecord> Read(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Result<FrontmatterDocument> split = Frontmatter.Read(text, source);
        if (split.IsFailed)
        {
            return split.ToResult<AbandonRecord>();
        }

        Shape? shape;
        try
        {
            shape = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build()
                .Deserialize<Shape>(split.Value.Yaml);
        }
        catch (YamlException error)
        {
            return Result.Fail<AbandonRecord>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: the frontmatter is not valid YAML. {error.Message}"));
        }

        List<IError> errors = [];

        if (string.IsNullOrWhiteSpace(shape?.By))
        {
            errors.Add(new Error($"{source}: names nobody. Abandoning a cycle is a decision somebody took."));
        }

        if (shape?.At is null)
        {
            // Same reason the approval ledger insists: git does not store mtimes, so a clone sets every
            // file's to the moment of checkout and every abandonment would read as just now.
            errors.Add(new Error($"{source}: has no timestamp. It is written in the file, never taken from it."));
        }

        // The reason is the body rather than a frontmatter value, because it is prose a person writes
        // and the one thing worth reading here. A record with none has thrown the objections away,
        // which is the whole failure this file exists to prevent.
        string reason = Prose(split.Value.Body);
        if (reason.Length == 0)
        {
            errors.Add(new Error(
                $"{source}: says nothing about why. An agent stuck on a blocking finding has an obvious escape in abandoning, and the written reason is what keeps the objection on the record."));
        }

        // A suggestion the cycle accepted is drafted as a declining line with no reason (D-115): deleted, it
        // returns to the pool; kept, it needs the why. Kept without one, it declines nothing.
        foreach (string id in Findings.SuggestionDecisions.DeclinedIn(split.Value.Body, withReason: false))
        {
            errors.Add(new Error(
                $"{source}: declines {id} and says nothing about why. Write the reason after the colon, or delete the line to return the suggestion to the pool."));
        }

        return errors.Count > 0
            ? Result.Fail<AbandonRecord>(errors)
            : Result.Ok(new AbandonRecord(
                shape!.Cycle ?? string.Empty,
                shape.By!.Trim(),
                shape.At!.Value.ToUniversalTime(),
                reason));
    }

    /// <summary>
    /// The body with its headings stripped, leaving what a person actually wrote.
    /// </summary>
    /// <remarks>
    /// The drafter prints a heading for the reason to go under, so a record holding only that heading
    /// has no reason in it. Counting the scaffolding as prose would let the one file that exists to
    /// carry a reason pass with nobody having given one, which is the whole check defeated by the thing
    /// meant to help.
    /// </remarks>
    /// <param name="body">Everything after the closing fence.</param>
    /// <returns>The prose, trimmed, or an empty string when there is none.</returns>
    private static string Prose(string body)
    {
        IEnumerable<string> lines = body
            .ReplaceLineEndings()
            .Split(Environment.NewLine)
            .Where(line => !line.TrimStart().StartsWith('#'))
            .Where(line => !Findings.SuggestionDecisions.IsDeclineLine(line));

        return string.Join(Environment.NewLine, lines).Trim();
    }

    // CA1812 does not fire: it is in NoWarn repository-wide, so the attribute that stood
    // here suppressed nothing and its justification -- "scoped here rather than suppressed
    // repository-wide" -- asserted the inverse of what the build does. Kept as a comment,
    // which reads the same and claims nothing: this shape is constructed by YamlDotNet
    // through reflection, which is why it looks dead. F-18.
    private sealed class Shape
    {
        public string? Cycle { get; set; }

        public string? By { get; set; }

        public DateTimeOffset? At { get; set; }
    }
}
