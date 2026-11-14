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

    /// <summary>
    /// A person's answer to each deviation a build or a remediation recorded, accepted or declined one by one.
    /// </summary>
    /// <remarks>
    /// <strong>Not a decision about the cycle, so it never stands.</strong> It sits beside the approval it
    /// answers for and leaves it in force: <see cref="ApprovalLedger.Current"/> passes over it, so the gate
    /// keeps reading the hashes and anchors of the approval itself. A deviation replaces approved text the
    /// build could not follow, and a person signs the replacement, so nothing is waived (D-27).
    /// </remarks>
    Deviations,
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
/// <param name="Answers">
/// On a <see cref="GateDecision.Deviations"/> entry, the person's answer to each deviation; empty on any other.
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
    IReadOnlyDictionary<string, RoundAnchor>? Rounds = null,
    IReadOnlyList<DeviationAnswer>? Answers = null);

/// <summary>A person's answer to one deviation, as a deviations entry records it.</summary>
/// <param name="Id">The deviation, as <c>DEV_nn</c>.</param>
/// <param name="Sha256">The hash of the deviation's text as it was answered; an edit since leaves it unanswered.</param>
/// <param name="Accepted">True when the person accepted it. False is the default the drafter writes.</param>
/// <param name="Note">Why, when the person declined it. False with no note is a deviation nobody has reviewed.</param>
public sealed record DeviationAnswer(string Id, string Sha256, bool Accepted, string? Note)
{
    /// <summary>Whether the person declined it: false, with the reason written.</summary>
    public bool Declined => !Accepted && !string.IsNullOrWhiteSpace(Note);

    /// <summary>Whether the person answered it at all: accepted, or declined with a reason.</summary>
    public bool Answered => Accepted || Declined;
}

/// <summary>Where one document's findings ledger stood when a decision was taken (D-107).</summary>
/// <param name="Round">The latest round it had.</param>
/// <param name="Sha256">That round's entry hash.</param>
public sealed record RoundAnchor(int Round, string Sha256);

/// <summary>
/// A cycle's gate history: an append-only ledger of decisions, read top to bottom.
/// </summary>
/// <param name="Cycle">The cycle the ledger belongs to.</param>
/// <param name="Decisions">Every decision, in the order they were taken.</param>
public sealed record ApprovalLedger(
    string Cycle,
    IReadOnlyList<GateEntry> Decisions)
{
    /// <summary>
    /// The decision that stands: the last one recorded that decides the cycle.
    /// </summary>
    /// <remarks>
    /// A later approval supersedes an earlier refusal and a later reopen supersedes an approval, so
    /// order decides rather than presence. The file is append-only, which is what makes the last entry
    /// meaningful. <strong>A deviations entry is passed over</strong>: it answers for the approval before it
    /// and decides nothing about the cycle.
    /// </remarks>
    public GateEntry? Current => StandingIndex is int index ? Decisions[index] : null;

    /// <summary>Where the decision that stands is in <see cref="Decisions"/>, or null when there is none.</summary>
    public int? StandingIndex
    {
        get
        {
            for (int index = Decisions.Count - 1; index >= 0; index--)
            {
                if (Decisions[index].Decision != GateDecision.Deviations)
                {
                    return index;
                }
            }

            return null;
        }
    }

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
                    $"{position} has decision '{entry.Decision}'. Expected approved, changes_requested, denied, reopened or deviations.")));
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

            if (decision is not GateDecision.Approved and not GateDecision.Deviations && string.IsNullOrWhiteSpace(entry.Reason))
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

            List<DeviationAnswer> answers = [];

            foreach (AnswerShape answer in entry.Deviations ?? [])
            {
                if (string.IsNullOrWhiteSpace(answer?.Id) || string.IsNullOrWhiteSpace(answer.Sha256))
                {
                    errors.Add(new Error(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{position} answers a deviation without its id and hash. 'sdlc draft approval' writes both; change only 'accepted' and 'note'.")));
                    malformed = true;
                    continue;
                }

                answers.Add(new DeviationAnswer(
                    answer.Id.Trim(),
                    answer.Sha256.Trim(),
                    answer.Accepted ?? false,
                    string.IsNullOrWhiteSpace(answer.Note) ? null : answer.Note.Trim()));
            }

            if (decision == GateDecision.Deviations && answers.Count == 0 && !malformed)
            {
                errors.Add(new Error($"{position} answers no deviation. A deviations entry lists each one the person answers."));
                continue;
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
                anchors,
                answers));
        }

        return errors.Count > 0
            ? Result.Fail<ApprovalLedger>(errors)
            : Result.Ok(new ApprovalLedger(shape.Cycle ?? string.Empty, entries));
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
        GateDecision.Deviations => "deviations",
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
            case string deviations when deviations.Equals("deviations", StringComparison.OrdinalIgnoreCase):
                decision = GateDecision.Deviations;
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

        public List<AnswerShape>? Deviations { get; set; }
    }

    // Constructed by YamlDotNet through reflection, like the shapes above.
    private sealed class AnswerShape
    {
        public string? Id { get; set; }

        public string? Sha256 { get; set; }

        public bool? Accepted { get; set; }

        public string? Note { get; set; }
    }

    // Constructed by YamlDotNet through reflection, like the two shapes above.
    private sealed class AnchorShape
    {
        public int? Round { get; set; }

        public string? Sha256 { get; set; }
    }
}
