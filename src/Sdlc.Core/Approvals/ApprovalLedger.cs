using System.Globalization;
using FluentResults;
using Sdlc.Core.Artifacts;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Sdlc.Core.Approvals;

/// <summary>What a person decided at the gate.</summary>
public enum GateDecision
{
    /// <summary>The three planning documents are agreed, and the build may run.</summary>
    Approved,

    /// <summary>Sent back. The reason says what has to change.</summary>
    ChangesRequested,

    /// <summary>Refused outright.</summary>
    Denied,

    /// <summary>
    /// A standing approval is withdrawn, returning the cycle to its pre-approval state.
    /// </summary>
    /// <remarks>
    /// Without this a wrong plan has no legal move. Remediation may not edit a planning document and
    /// refinement refuses once the cycle is approved, so the only path left is to keep trying to make
    /// wrong code satisfy a wrong plan until the remediation budget runs out. A reopen is not an
    /// approval, so <see cref="ApprovalLedger.IsApproved"/> turns false and the gate refuses the build
    /// again -- that is the whole mechanism.
    /// </remarks>
    Reopened,
}

/// <summary>One entry in a cycle's gate ledger.</summary>
/// <param name="Decision">What was decided.</param>
/// <param name="By">Who decided it.</param>
/// <param name="At">When, in UTC.</param>
/// <param name="Hashes">
/// The canonical hash of each planning document as it stood when the decision was taken, keyed by
/// file name. All three are recorded because the three are approved as one act.
/// </param>
/// <param name="Reason">
/// Why. Required on anything but an approval, and free to carry on one. There is deliberately no
/// field recording findings let through: a blocking finding cannot be waived by any mechanism, so an
/// entry has nothing to record there.
/// </param>
/// <param name="Rounds">
/// For each planning document, the latest round of its findings ledger when the decision was taken and
/// that round's <c>entry_sha256</c> (D-107). Keyed by file name; empty on an entry written before it.
/// <para>
/// <strong>A hash and not a count.</strong> There was once a <c>findings_rounds</c> field, a vestige of
/// the model D-27 removed: a number, hand-pasted, read by nothing. A ledger is rewritten on every round, so
/// its own chain proves only that it is consistent -- this is what makes it a check, because it lives in a
/// file no agent can write. It pins a prefix: rounds after the anchored one are allowed, and a blocker
/// raised in one still refuses the build.
/// </para>
/// </param>
public sealed record GateEntry(
    GateDecision Decision,
    string By,
    DateTimeOffset At,
    IReadOnlyDictionary<string, string> Hashes,
    string? Reason,
    IReadOnlyDictionary<string, RoundAnchor>? Rounds = null);

/// <summary>Where one document's findings ledger stood when a decision was taken (D-107).</summary>
/// <param name="Round">The latest round it had.</param>
/// <param name="Sha256">That round's entry hash.</param>
public sealed record RoundAnchor(int Round, string Sha256);

/// <summary>
/// A cycle's gate history: an append-only ledger of decisions, read top to bottom.
/// </summary>
/// <param name="Cycle">The cycle the ledger belongs to.</param>
/// <param name="FrameworkVersion">The version whose format the entries were written in.</param>
/// <param name="Decisions">Every decision, in the order they were taken.</param>
public sealed record ApprovalLedger(
    string Cycle,
    string? FrameworkVersion,
    IReadOnlyList<GateEntry> Decisions)
{
    /// <summary>
    /// The decision that stands: the last one recorded.
    /// </summary>
    /// <remarks>
    /// A later approval supersedes an earlier refusal and a later reopen supersedes an approval, so
    /// order decides rather than presence. The file is append-only, which is what makes the last entry
    /// meaningful.
    /// </remarks>
    public GateEntry? Current => Decisions.Count > 0 ? Decisions[^1] : null;

    /// <summary>Whether the cycle's standing decision is an approval.</summary>
    public bool IsApproved => Current?.Decision == GateDecision.Approved;

    /// <summary>
    /// Reads a ledger from an <c>approval.md</c>.
    /// </summary>
    /// <param name="text">The file's contents.</param>
    /// <param name="source">What to call it in an error message.</param>
    /// <returns>The ledger, or a failure saying what is wrong with it.</returns>
    public static Result<ApprovalLedger> Read(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Result<FrontmatterDocument> split = Frontmatter.Read(text, source);
        if (split.IsFailed)
        {
            return split.ToResult<ApprovalLedger>();
        }

        IDeserializer reader = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        LedgerShape? shape;
        try
        {
            shape = reader.Deserialize<LedgerShape>(split.Value.Yaml);
        }
        catch (YamlException error)
        {
            return Result.Fail<ApprovalLedger>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: the frontmatter is not valid YAML. {error.Message}"));
        }

        if (shape?.Decisions is null || shape.Decisions.Count == 0)
        {
            return Result.Fail<ApprovalLedger>(string.Create(
                CultureInfo.InvariantCulture,
                $"{source}: no decisions. An approval file with no entry approves nothing."));
        }

        List<GateEntry> entries = [];
        List<IError> errors = [];

        for (int index = 0; index < shape.Decisions.Count; index++)
        {
            EntryShape entry = shape.Decisions[index];
            string position = string.Create(CultureInfo.InvariantCulture, $"{source}: decision {index + 1}");

            if (!TryReadDecision(entry.Decision, out GateDecision decision))
            {
                errors.Add(new Error(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{position} has decision '{entry.Decision}'. Expected approved, changes_requested, denied or reopened.")));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.By))
            {
                errors.Add(new Error($"{position} names nobody. An approval nobody signed is not an approval."));
                continue;
            }

            if (entry.At is null)
            {
                // A file's modified time cannot stand in: git does not store mtimes, so a clone sets
                // every file's to the moment of checkout and every approval would read as just now.
                errors.Add(new Error($"{position} has no timestamp. It is written in the entry, never taken from the file."));
                continue;
            }

            if (decision != GateDecision.Approved && string.IsNullOrWhiteSpace(entry.Reason))
            {
                errors.Add(new Error(
                    $"{position} refuses the cycle and says nothing about why. The reason is what the author has to work from."));
                continue;
            }

            Dictionary<string, RoundAnchor> anchors = new(StringComparer.Ordinal);
            bool malformed = false;

            foreach ((string document, AnchorShape anchor) in entry.Rounds ?? [])
            {
                if (anchor?.Round is not { } round || round < 1 || string.IsNullOrWhiteSpace(anchor.Sha256))
                {
                    errors.Add(new Error(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{position} anchors {document} without a round number and its hash. 'sdlc draft approval' prints the anchor; copy it whole.")));
                    malformed = true;
                    continue;
                }

                anchors[document] = new RoundAnchor(round, anchor.Sha256.Trim());
            }

            if (malformed)
            {
                continue;
            }

            entries.Add(new GateEntry(
                decision,
                entry.By.Trim(),
                entry.At.Value.ToUniversalTime(),
                entry.Hashes ?? [],
                string.IsNullOrWhiteSpace(entry.Reason) ? null : entry.Reason.Trim(),
                anchors));
        }

        return errors.Count > 0
            ? Result.Fail<ApprovalLedger>(errors)
            : Result.Ok(new ApprovalLedger(shape.Cycle ?? string.Empty, shape.FrameworkVersion, entries));
    }

    /// <summary>
    /// How a decision is written in the file.
    /// </summary>
    /// <remarks>
    /// The reader and the printer have to agree, so the spelling lives here once rather than in each.
    /// </remarks>
    /// <param name="decision">The decision.</param>
    /// <returns>Its name in the ledger.</returns>
    public static string NameOf(GateDecision decision) => decision switch
    {
        GateDecision.Approved => "approved",
        GateDecision.ChangesRequested => "changes_requested",
        GateDecision.Denied => "denied",
        GateDecision.Reopened => "reopened",
        _ => throw new ArgumentOutOfRangeException(nameof(decision)),
    };

    private static bool TryReadDecision(string? text, out GateDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        switch (text.Trim().Replace("_", string.Empty, StringComparison.Ordinal))
        {
            case string approved when approved.Equals("approved", StringComparison.OrdinalIgnoreCase):
                decision = GateDecision.Approved;
                return true;
            case string changes when changes.Equals("changesrequested", StringComparison.OrdinalIgnoreCase):
                decision = GateDecision.ChangesRequested;
                return true;
            case string denied when denied.Equals("denied", StringComparison.OrdinalIgnoreCase):
                decision = GateDecision.Denied;
                return true;
            case string reopened when reopened.Equals("reopened", StringComparison.OrdinalIgnoreCase):
                decision = GateDecision.Reopened;
                return true;
            default:
                return false;
        }
    }

    /// <summary>The YAML shape, kept separate from the model so the model has no settable state.</summary>
    // CA1812 does not fire: it is in NoWarn repository-wide, so the attribute that stood
    // here suppressed nothing and its justification -- "scoped here rather than suppressed
    // repository-wide" -- asserted the inverse of what the build does. Kept as a comment,
    // which reads the same and claims nothing: this shape is constructed by YamlDotNet
    // through reflection, which is why it looks dead. F-18.
    private sealed class LedgerShape
    {
        public string? Cycle { get; set; }

        public string? FrameworkVersion { get; set; }

        public List<EntryShape>? Decisions { get; set; }
    }

    // CA1812 does not fire: it is in NoWarn repository-wide, so the attribute that stood
    // here suppressed nothing and its justification -- "scoped here rather than suppressed
    // repository-wide" -- asserted the inverse of what the build does. Kept as a comment,
    // which reads the same and claims nothing: this shape is constructed by YamlDotNet
    // through reflection, which is why it looks dead. F-18.
    private sealed class EntryShape
    {
        public string? Decision { get; set; }

        public string? By { get; set; }

        public DateTimeOffset? At { get; set; }

        public Dictionary<string, string>? Hashes { get; set; }

        public string? Reason { get; set; }

        public Dictionary<string, AnchorShape>? Rounds { get; set; }
    }

    // Constructed by YamlDotNet through reflection, like the two shapes above.
    private sealed class AnchorShape
    {
        public int? Round { get; set; }

        public string? Sha256 { get; set; }
    }
}
