using System.Globalization;
using System.Text;
using Sdlc.Core.Artifacts;
using Sdlc.Core.Constraints;
using Sdlc.Core.Cycles;

namespace Sdlc.Core.Findings;

/// <summary>
/// The items a rule judged one at a time is answered over, and the rows that answer it (D-119).
/// </summary>
/// <remarks>
/// One plan took thirty-seven rounds, each finding one more inadequate proof under the same rule, because
/// "look for the same defect elsewhere" was advice. A row per item is countable, so a missing one is the
/// binary's to refuse. The prompt and the check read the items from here, so they cannot disagree about
/// how many there are.
/// </remarks>
internal static class JudgedItems
{
    /// <summary>The items, numbered as a reader cites them.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="per">What the rule is judged over.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>
    /// Each item's number and text: a criterion keeps the number the spec gives it, and an outcome is
    /// numbered in document order — wanted outcomes first, then what must not change.
    /// </returns>
    public static async Task<IReadOnlyDictionary<int, string>> Of(Cycle cycle, JudgedPer per, CancellationToken cancellationToken)
    {
        if (per == JudgedPer.Criterion)
        {
            string spec = cycle.PathOf(ArtifactKind.Spec);

            return File.Exists(spec)
                ? ArtifactSections.NumberedItems(await File.ReadAllTextAsync(spec, cancellationToken).ConfigureAwait(false), "Acceptance criteria")
                : new Dictionary<int, string>();
        }

        string intent = cycle.PathOf(ArtifactKind.Intent);
        Dictionary<int, string> outcomes = [];

        if (!File.Exists(intent))
        {
            return outcomes;
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> sections = ArtifactSections.Read(
            await File.ReadAllTextAsync(intent, cancellationToken).ConfigureAwait(false));

        // A top-level bullet is an item; an indented line continues the one above it.
        foreach (string section in new[] { "Wanted outcomes", "Must not change" })
        {
            foreach (string line in sections.GetValueOrDefault(section) ?? [])
            {
                if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                {
                    outcomes[outcomes.Count + 1] = line[2..].Trim();
                }
            }
        }

        return outcomes;
    }

    /// <summary>Every rule in a set that is judged item by item, with its items.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="set">The rules in force on the artifact.</param>
    /// <param name="cancellationToken">Token observed while reading.</param>
    /// <returns>The rules and their items, in the set's order.</returns>
    public static async Task<IReadOnlyList<(Constraint Rule, IReadOnlyDictionary<int, string> Items)>> In(
        Cycle cycle,
        EffectiveConstraints.Response set,
        CancellationToken cancellationToken)
    {
        List<(Constraint, IReadOnlyDictionary<int, string>)> judged = [];

        foreach (Constraint rule in set.Constraints.Where(rule => rule.Judged is not null))
        {
            judged.Add((rule, await Of(cycle, rule.Judged!.Value, cancellationToken).ConfigureAwait(false)));
        }

        return judged;
    }

    /// <summary>
    /// What the rules reader is told about each rule in force that is judged item by item, or nothing
    /// when none is.
    /// </summary>
    /// <param name="judged">Each such rule, with the items it is judged over.</param>
    /// <returns>The instruction, and the line to add to the answer's shape.</returns>
    public static (string Instruction, string Shape) Prompt(IReadOnlyList<(Constraint Rule, IReadOnlyDictionary<int, string> Items)> judged)
    {
        if (judged.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        StringBuilder text = new();
        text.Append("\n**Some rules are judged one item at a time.** For each of them, answer every item below with a row in\n");
        text.Append("`rows` — the rule's id, the item's number, whether the document meets the rule for that item, and why —\n");
        text.Append("before you write any finding. A row judged unmet is a blocking finding citing that rule, and one finding\n");
        text.Append("per rule: its `locus` names every item that fails. The answer is refused with a row missing.\n\n");

        foreach ((Constraint rule, IReadOnlyDictionary<int, string> items) in judged)
        {
            text.Append(CultureInfo.InvariantCulture, $"{rule.Id} -- {rule.Statement}, judged {(rule.Judged == JudgedPer.Criterion ? "per acceptance criterion of the spec" : "per outcome of the intent")}:\n\n");

            foreach ((int number, string item) in items.OrderBy(pair => pair.Key))
            {
                text.Append(CultureInfo.InvariantCulture, $"{number}. {item}\n");
            }

            text.Append('\n');
        }

        return (text.ToString(), ",\n  \"rows\": [ { \"rule\": \"CON_21\", \"item\": 1, \"met\": true, \"why\": \"...\" } ]");
    }

    /// <summary>Why a rules reader's rows do not answer every judged item, or null when they do.</summary>
    /// <param name="judged">Each rule judged item by item, with its items.</param>
    /// <param name="rows">The rows the reader returned.</param>
    /// <param name="findings">The findings it raised.</param>
    /// <returns>The refusal, or null.</returns>
    public static string? Unanswered(
        IReadOnlyList<(Constraint Rule, IReadOnlyDictionary<int, string> Items)> judged,
        IReadOnlyList<ReaderRow> rows,
        IReadOnlyList<Finding> findings)
    {
        List<string> missing = [];
        List<string> unaccounted = [];

        foreach ((Constraint rule, IReadOnlyDictionary<int, string> items) in judged)
        {
            int number = rule.Id.Number;
            List<ReaderRow> mine = [.. rows.Where(row => ConstraintId.TryParse(row.Rule, out ConstraintId? id) && id.Value.Number == number && id.Value.Tier == rule.Id.Tier)];

            missing.AddRange(items.Keys
                .Where(item => !mine.Any(row => row.Item == item && row.Met is not null))
                .Select(item => string.Create(CultureInfo.InvariantCulture, $"{rule.Id} item {item}")));

            bool blocked = findings.Any(finding => finding.Severity == Severity.Blocking
                && ConstraintId.TryParse(finding.Cites?.Id, out ConstraintId? cited)
                && cited.Value.Number == number
                && cited.Value.Tier == rule.Id.Tier);

            if (!blocked && mine.Any(row => row.Met == false))
            {
                unaccounted.Add(rule.Id.ToString());
            }
        }

        if (missing.Count > 0)
        {
            return "The rules reader's answer has no row for " + string.Join(", ", missing) + ". Every item of a rule judged one at a time is answered.";
        }

        return unaccounted.Count > 0
            ? "The rules reader judged an item unmet under " + string.Join(", ", unaccounted) + " and raised no blocking finding citing it."
            : null;
    }
}
