using System.Globalization;
using FluentResults;
using MofonCode.Core;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>
/// The rules a reader was handed, as the set a blocking finding of its may cite (D-174).
/// </summary>
/// <remarks>
/// <para><strong>A blocker cites a rule in force for the document it judges, or it does not block.</strong> A reader
/// is handed the set <c>sdlc constraints --for</c> renders, and a citation outside it -- a superseded rule, a spec's
/// rule raised against a plan, an id nobody issued -- would otherwise stop a cycle on a rule nobody could show it
/// broke. The comparison is by tier and number, never by revision: a revision forbids exactly what its predecessor
/// did (§7.4).</para>
/// <para><strong>Which set a reader was handed.</strong> The rules reader and a perspective get the document's own;
/// the diff reader gets the change's, <c>diff</c>; a perspective reading a verification reads the change too, so it
/// gets both.</para>
/// <para><strong>And the line each rule is quoted by</strong> (D-177). A finding carries its rule's own line beside the
/// id (D-63), copied verbatim from the set its reader was handed, so the set's lines are what a quoted line is held
/// to. The current revision's line is the one handed, so it is the one compared.</para>
/// </remarks>
internal static class CitedRules
{
    /// <summary>The artifact name the diff reader's rules are matched by.</summary>
    internal const string DiffArtifact = "diff";

    /// <summary>The key a citation is compared by: its tier and number, without the revision.</summary>
    /// <param name="id">A citation, as written.</param>
    /// <returns>The key, or null when it is not a constraint id at all.</returns>
    internal static string? Key(string? id) =>
        ConstraintId.TryParse(id?.Trim(), out ConstraintId? parsed)
            ? Key(parsed.Value)
            : null;

    /// <summary>The key a rule is compared by.</summary>
    /// <param name="id">The rule's id.</param>
    /// <returns>Its tier and number.</returns>
    internal static string Key(ConstraintId id) =>
        string.Create(CultureInfo.InvariantCulture, $"{ConstraintId.PrefixOf(id.Tier)}_{id.Number}");

    /// <summary>The rules in force for one artifact name, each key mapped to the rule's own line.</summary>
    /// <param name="mediator">To ask for the effective set.</param>
    /// <param name="artifact">The artifact name, as <c>sdlc constraints --for</c> takes it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The keys and their lines, or why the set could not be read.</returns>
    internal static async Task<Result<Dictionary<string, string>>> InForce(Mediator mediator, string artifact, CancellationToken cancellationToken)
    {
        Result<EffectiveConstraints.Response> set = await mediator
            .Send<EffectiveConstraints.Query, EffectiveConstraints.Response>(new EffectiveConstraints.Query(artifact), cancellationToken)
            .ConfigureAwait(false);

        if (set.IsFailed)
        {
            return set.ToResult<Dictionary<string, string>>();
        }

        Dictionary<string, string> lines = new(StringComparer.Ordinal);

        foreach (Constraint rule in set.Value.Constraints)
        {
            lines[Key(rule.Id)] = rule.Statement;
        }

        return Result.Ok(lines);
    }

    /// <summary>Whether a quoted line is the rule's own, after only whitespace, quotes and dashes are evened out.</summary>
    /// <remarks>
    /// Not fuzzy, on purpose: a paraphrase is the second version of a rule D-63 exists to prevent, and a match that
    /// forgave a changed word would forgive exactly that. What it forgives is what a copy through JSON, a terminal or a
    /// model's typography changes without changing a word.
    /// </remarks>
    /// <param name="quoted">The line a finding carries.</param>
    /// <param name="line">The rule's own line.</param>
    /// <returns>True when they are the same words.</returns>
    internal static bool SameLine(string? quoted, string line) =>
        string.Equals(Even(quoted), Even(line), StringComparison.Ordinal);

    private static string Even(string? text) =>
        string.Join(' ', (text ?? string.Empty)
            .Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"')
            .Replace('–', '-').Replace('—', '-').Replace('‑', '-').Replace('−', '-')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>What each kind of reader of one document was handed.</summary>
    /// <param name="mediator">To ask for the effective sets.</param>
    /// <param name="artifact">The document being judged.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sets, or why one could not be read.</returns>
    internal static async Task<Result<Handed>> For(Mediator mediator, ArtifactKind artifact, CancellationToken cancellationToken)
    {
        Result<Dictionary<string, string>> own = await InForce(mediator, Cycle.NameOf(artifact), cancellationToken).ConfigureAwait(false);

        if (own.IsFailed)
        {
            return own.ToResult<Handed>();
        }

        Result<Dictionary<string, string>> change = await InForce(mediator, DiffArtifact, cancellationToken).ConfigureAwait(false);

        if (change.IsFailed)
        {
            return change.ToResult<Handed>();
        }

        HashSet<string> document = new(own.Value.Keys, StringComparer.Ordinal);
        HashSet<string> changed = new(change.Value.Keys, StringComparer.Ordinal);
        HashSet<string> perspectives = new(document, StringComparer.Ordinal);

        if (artifact == ArtifactKind.Verify)
        {
            perspectives.UnionWith(changed);
        }

        Dictionary<string, string> lines = new(own.Value, StringComparer.Ordinal);

        foreach ((string key, string line) in change.Value)
        {
            lines.TryAdd(key, line);
        }

        return Result.Ok(new Handed(document, changed, perspectives, lines));
    }

    /// <summary>The rules each kind of reader of one document was handed, as keys.</summary>
    /// <param name="Document">The rules reader's: the document's own.</param>
    /// <param name="Change">The diff reader's.</param>
    /// <param name="Perspectives">A perspective's, before its own list narrows it.</param>
    /// <param name="Lines">Every handed rule's own line, by key: what a quoted line is held to (D-177).</param>
    internal sealed record Handed(
        IReadOnlySet<string> Document,
        IReadOnlySet<string> Change,
        IReadOnlySet<string> Perspectives,
        IReadOnlyDictionary<string, string> Lines)
    {
        /// <summary>The rule's own line when a citation quotes it otherwise, or null when it matches or no handed rule is cited.</summary>
        /// <param name="cites">The citation.</param>
        /// <returns>The line it should carry, or null.</returns>
        internal string? Misquoted(Citation? cites) =>
            Key(cites?.Id) is { } key && Lines.TryGetValue(key, out string? line) && !SameLine(cites!.Heading, line)
                ? line
                : null;

        /// <summary>The set a reader of this name was handed.</summary>
        /// <param name="reader">The reader: <c>rules</c>, <c>diff</c>, or a perspective's name.</param>
        /// <returns>Its set.</returns>
        internal IReadOnlySet<string> Of(string reader) => reader switch
        {
            ReaderAnswers.Rules => Document,
            ReaderAnswers.Diff => Change,
            _ => Perspectives,
        };

        /// <summary>The set the reader that raised a recorded finding was handed.</summary>
        /// <param name="finding">The finding.</param>
        /// <returns>Its reader's set.</returns>
        internal IReadOnlySet<string> Of(Finding finding) =>
            finding.Perspective is not null
                ? Perspectives
                : finding.Id.StartsWith(ReaderAnswers.Diff + ".", StringComparison.Ordinal)
                    ? Change
                    : Document;
    }
}
