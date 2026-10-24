using System.Globalization;

namespace Sdlc.Core.Solutions;

/// <summary>
/// The menu a person edits a declared stack beside: every concern the shipped profiles know about,
/// and the answers each one declares.
/// </summary>
/// <remarks>
/// <para><strong>A separate file from the draft, and that is the whole of the design.</strong> The
/// draft is a byte copy of the declared stack, so renaming it applies what somebody changed and
/// nothing else (D-67). Putting candidates inside it would break that twice over: the rename would no
/// longer be a copy, and a placed file carrying answers the solution has <em>not</em> declared would
/// let a plan reach for one and truthfully not be departing from anything — which is the deadlock
/// protecting the file closed in the first place. So the menu is never renamed into place and nothing
/// reads it back.</para>
///
/// <para><strong>And it is written as a report, not as a file in <c>.sdlc/</c> itself.</strong> The
/// menu outlives the rename it was written for — nothing deletes it — so an untracked copy would sit
/// there indefinitely, and <c>evidence</c> stages untracked paths before diffing. It would reach every
/// later cycle's diff as a file no plan declared, which is what <c>CON_07.2</c> fires on.
/// <see cref="Reports.ReportFile"/> carries that argument already, along with the directory that
/// ignores itself (D-75).</para>
///
/// <para><strong>Derived from the shipped profiles rather than authored.</strong> Every answer here is
/// a row some profile actually declares, so the menu cannot come to disagree with the tables it was
/// built from — the failure mode of a second catalogue. The two sections that are authored are the
/// ones the framework has genuinely argued and no profile declares: an alternative it weighed, and a
/// choice it refused. Those carry their reasoning rather than only a name, because a candidate with no
/// argument behind it is a tool making a judgement while looking like bookkeeping (D-67).</para>
///
/// <para><strong>It proposes nothing.</strong> Listing what exists is not choosing: the menu offers
/// both ML.NET and TensorFlow for machine learning and says which profile declares each, and the
/// person decides. Open question 8 of the concept document makes the same argument for a profile's
/// rules as a menu — every item was already argued by somebody, which beats a blank page and costs
/// nothing.</para>
/// </remarks>
public static class StackOptions
{
    /// <summary>
    /// Marks a document as this menu, for a check that the menu did not end up in the declared stack.
    /// </summary>
    /// <remarks>
    /// An HTML comment, so it is invisible wherever the file is rendered and survives a copy and paste
    /// into the real file — which is the only way the menu reaches somewhere it should not be, and the
    /// case <c>sdlc doctor</c> reports. A sentinel rather than matching on a heading: a heading is the
    /// part somebody would reword.
    /// </remarks>
    public const string Marker = "<!-- sdlc:stack-options -->";

    /// <summary>One concern, an answer, and every profile declaring it.</summary>
    /// <remarks>
    /// Plural deliberately. The platform rows — where data lives, how infrastructure is described — are
    /// declared by every profile from one shared set, and crediting the first profile to mention one
    /// would read as the others having no answer for it, which is the opposite of what those rows mean.
    /// </remarks>
    /// <param name="Choice">What is declared.</param>
    /// <param name="Profiles">The profiles declaring it, in the order they are listed.</param>
    private sealed record Answer(string Choice, List<string> Profiles);

    /// <summary>
    /// An alternative the framework weighed and did not take as its default.
    /// </summary>
    /// <param name="Concern">The concern it answers, which a profile already declares.</param>
    /// <param name="Candidate">The alternative.</param>
    /// <param name="Turns">What the choice between them turns on.</param>
    private sealed record Alternative(string Concern, string Candidate, string Turns);

    /// <summary>A choice the framework considered and refused, with the reason.</summary>
    /// <param name="Concern">What it would have answered.</param>
    /// <param name="Candidate">The thing refused.</param>
    /// <param name="Why">Why it is not here.</param>
    private sealed record Refused(string Concern, string Candidate, string Why);

    /// <summary>
    /// The alternatives the framework has argued in writing and no profile declares.
    /// </summary>
    /// <remarks>
    /// Each one is in the concept document with its reasoning, and each answers a concern that is
    /// already a row — so a person taking one is replacing a declared answer rather than inventing a
    /// concern. The list is short on purpose: an alternative nobody has argued is a name, and a name
    /// in a menu reads as an endorsement.
    /// </remarks>
    private static readonly Alternative[] _alternatives =
    [
        new(
            "ui components",
            "Microsoft.FluentUI.AspNetCore.Components",
            "the breadth of the component set an application shell needs, rather than who maintains it. "
            + "MudBlazor is the default and is the profile stepping off its own Microsoft-first habit."),
    ];

    /// <summary>
    /// Choices the framework looked at and refused, so that reaching for one is a decision rather
    /// than an oversight.
    /// </summary>
    /// <remarks>
    /// These answer concerns no profile declares a row for, which is why they are not in the menu
    /// above: a row would make the absence look like a gap somebody forgot to fill. Both are
    /// deliberate, both are argued in the concept document, and both are things a session reaches for
    /// out of habit — which is the reason to name them at all.
    /// </remarks>
    private static readonly Refused[] _refused =
    [
        new(
            "mediator",
            "MediatR",
            "the kernel this profile references is a mediator already, so a second one is two ways to "
            + "dispatch an operation. Declared against rather than left unmentioned, because it is what "
            + "a session reaches for out of habit."),
        new(
            "device end-to-end testing",
            "Appium",
            "a flaky proof gets rerun until green, which teaches a team to ignore red; its prerequisite "
            + "lands on every machine and in CI. A device-only outcome belongs in an intent as a bound, "
            + "verified by a person outside the cycle, where it is honest about not being governed."),
    ];

    /// <summary>
    /// Renders the menu.
    /// </summary>
    /// <remarks>
    /// The same for every solution. It used to end with an empty table of the concerns the solution's
    /// recorded profile declared, and no solution records a profile any more -- so that table is the
    /// default profile's, which is the one a first draft is written from (D-76).
    /// </remarks>
    /// <returns>The document's text.</returns>
    public static string Render()
    {
        List<string> lines =
        [
            Marker,
            "# Stack options",
            string.Empty,
            "Every concern the profiles this framework ships know about, and what each one declares for",
            "it. Written every time `sdlc draft techstack` runs, as a report -- so this copy is safe to",
            "delete, an edit to it is lost on the next run, nothing reads it back, and nothing is judged",
            "against it. Open it beside the draft and copy across what you want.",
            string.Empty,
            "**This file is never renamed into place.** The declared stack is the one a plan is read",
            "against, and a file carrying answers the solution has not chosen would let a plan reach for",
            "one and truthfully not be departing from anything. Copy what you want into the draft; leave",
            "this where it is.",
            string.Empty,
            "**Nothing here is a recommendation.** Two profiles disagree about several of these rows, and",
            "both answers are on the page with the profile that declares each. What a solution is built",
            "from is a judgement about how its team works, which is why no tool fills the table in.",
            string.Empty,
            "## How to use it",
            string.Empty,
            "1. Read the concerns below and decide what this solution actually uses.",
            "2. Edit `techstack.draft.md` -- the byte copy of the real file -- and change only the rows",
            "   you mean to change.",
            "3. Rename the draft over the real file. That is the act; nothing else places it.",
            string.Empty,
            "A row this solution has no use for says `none` rather than being left empty or deleted: an",
            "empty row declares nothing and reads as something nobody got round to, and a deleted one",
            "reads as never asked, so the next release offers it again.",
            string.Empty,
            "## Known answers, by concern",
            string.Empty,
            "| Concern | Known answers |",
            "|---|---|",
        ];

        foreach ((string concern, List<Answer> answers) in Menu())
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"| {concern} | {Describe(answers)} |"));
        }

        lines.AddRange(
            string.Empty,
            "## Alternatives this framework has argued",
            string.Empty,
            "Answers to a concern above that no profile declares as its default, each with what the",
            "choice turns on. Taking one is a departure from a declared default, which `CON_15` asks a",
            "plan to argue for -- or a change to this solution's declaration, which is this draft.",
            string.Empty,
            "| Concern | Alternative | What the choice turns on |",
            "|---|---|---|");

        foreach (Alternative alternative in _alternatives)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"| {alternative.Concern} | {alternative.Candidate} | {alternative.Turns} |"));
        }

        lines.AddRange(
            string.Empty,
            "## Argued and refused",
            string.Empty,
            "Not absences. Each of these was looked at and left out, and each is a thing a session",
            "reaches for by habit -- so a solution adopting one is making a decision rather than filling",
            "a gap.",
            string.Empty,
            "| Concern | Refused | Why |",
            "|---|---|---|");

        foreach (Refused refused in _refused)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"| {refused.Concern} | {refused.Candidate} | {refused.Why} |"));
        }

        lines.AddRange(
            string.Empty,
            "## Stack rules this framework ships",
            string.Empty,
            "The rules that may name a language, framework or package. A declared stack lists the ones",
            "it is held to under `" + StackRules.Heading + "`, each with `true` or `false`; a rule with no row",
            "there does not apply. Shown switched off here because listing one is not choosing it --",
            "copy a row into the draft and set it to `true` to be held to it. `sdlc constraints` prints",
            "the ones in force in full.",
            string.Empty);

        lines.AddRange(StackRules.Table(
            Constraints.ShippedProfileLibrary.Rules().Select(rule =>
                (Constraints.ShippedProfileLibrary.RowId(rule), rule.Statement)),
            on: false));

        lines.Add(string.Empty);
        lines.AddRange(Settings());
        lines.AddRange(Skeleton());

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    /// <summary>
    /// Every concern across the shipped profiles, in the order they are first declared, with each
    /// profile's answer.
    /// </summary>
    /// <remarks>
    /// Keyed on the concern exactly as a profile writes it, so two profiles that word one differently
    /// -- "language, framework" and "language" -- appear as two concerns. That is the truthful reading:
    /// the menu's job is to show what is declared, and collapsing them would mean deciding they are the
    /// same question, which is the judgement this file does not make.
    /// </remarks>
    /// <returns>The concerns, each with its answers in profile order.</returns>
    private static IEnumerable<KeyValuePair<string, List<Answer>>> Menu()
    {
        Dictionary<string, List<Answer>> byConcern = new(StringComparer.OrdinalIgnoreCase);
        List<string> order = [];

        foreach (StackProfile profile in StackProfiles.All)
        {
            foreach (StackDefault row in profile.Defaults)
            {
                if (!byConcern.TryGetValue(row.Concern, out List<Answer>? answers))
                {
                    answers = [];
                    byConcern[row.Concern] = answers;
                    order.Add(row.Concern);
                }

                Answer? already = answers.Find(answer => string.Equals(
                    answer.Choice,
                    row.Choice,
                    StringComparison.OrdinalIgnoreCase));

                if (already is null)
                {
                    answers.Add(new Answer(row.Choice, [profile.Name]));
                }
                else if (!already.Profiles.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
                {
                    already.Profiles.Add(profile.Name);
                }
            }
        }

        foreach (string concern in order)
        {
            yield return new KeyValuePair<string, List<Answer>>(concern, byConcern[concern]);
        }
    }

    /// <summary>
    /// One concern's answers as a cell.
    /// </summary>
    /// <remarks>
    /// Each answer carries the profile that declares it, because an answer with no provenance is one a
    /// reader has to take on trust -- and where every profile declares the same thing, saying so is
    /// what tells somebody the row is a platform decision rather than a language one.
    /// </remarks>
    /// <param name="answers">The answers for one concern.</param>
    /// <returns>The cell's text.</returns>
    private static string Describe(List<Answer> answers) => string.Join("; ", answers.Select(Attribute));

    /// <summary>
    /// One answer with who declares it.
    /// </summary>
    /// <remarks>
    /// An answer every profile declares says so rather than listing them: those are the platform rows,
    /// declared once in the framework so two profiles cannot come to disagree about Terraform by being
    /// edited separately (D-68), and naming that is what tells a reader the row is a decision about the
    /// platform rather than about the language.
    /// </remarks>
    /// <param name="answer">The answer.</param>
    /// <returns>The choice, and the profile or profiles behind it.</returns>
    private static string Attribute(Answer answer) => answer.Profiles.Count == StackProfiles.All.Count
        ? string.Create(CultureInfo.InvariantCulture, $"{answer.Choice} — every profile")
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{answer.Choice} — {string.Join(", ", answer.Profiles.Select(name => "`" + name + "`"))}");

    /// <summary>
    /// The build-settings files the framework ships: a row each, then what each one is.
    /// </summary>
    /// <remarks>
    /// <para><strong>A row says what a file does; the paragraph under it says what it costs.</strong>
    /// The row is what gets copied into the draft, so it has to fit a cell. What a person needs before
    /// switching one on does not: what is in the file, what it does to a codebase that predates it, and
    /// which other file it is useless without. A draft that started every row at <c>false</c> on an
    /// adopted repository would otherwise be five names and no way to choose between them (D-77).</para>
    ///
    /// <para>Shown switched off for the reason the rules are: listing one is not choosing it.</para>
    /// </remarks>
    /// <returns>The section's lines.</returns>
    private static IEnumerable<string> Settings() =>
    [
        "## Build settings this framework ships",
        string.Empty,
        "The files that decide how .NET code is built. A declared stack lists the ones it takes under",
        "`" + BuildSettings.Heading + "`, each with `true` or `false`; a file with no row there is not declared.",
        "Shown switched off here because listing one is not choosing it -- copy a row into the draft",
        "and set it to `true` to take the file.",
        string.Empty,
        "**A row writes nothing.** A cycle whose approved plan declares the file brings it, with",
        "`sdlc scaffold`, so the cost of switching one on lands inside a cycle somebody planned and",
        "approved. `sdlc stack` shows which are declared and not there yet. Each is created once, never",
        "rewritten, and a person's to amend -- and one this repository already has is kept as it is.",
        string.Empty,
        .. BuildSettings.Table(on: false),
        string.Empty,
        .. BuildSettings.All.SelectMany(setting => (string[])
        [
            "### `" + setting.Path + "`",
            string.Empty,
            setting.Summary + ".",
            string.Empty,
            setting.Detail,
            string.Empty,
        ]),
    ];

    /// <summary>
    /// An empty table of the default profile's concerns, to copy wholesale.
    /// </summary>
    /// <remarks>
    /// For the solution that wants to start over rather than amend -- one whose stack shares the
    /// default's concerns and none of its answers. It is here rather than in the draft on purpose: a
    /// draft of an existing file is a byte copy, and replacing it with a blank form would drop every
    /// row somebody did not retype from the one file every plan is judged against, silently. Copied
    /// into the draft it is a deliberate act sitting in front of the person before they place it.
    /// </remarks>
    /// <returns>The section's lines.</returns>
    private static IEnumerable<string> Skeleton()
    {
        StackProfile profile = StackProfiles.Default;

        return
        [
            "## An empty table to start from",
            string.Empty,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The concerns `{profile.Name}` declares, with the answers taken out. For a solution"),
            "starting over rather than amending a row -- copy it into the draft, fill it in, and write",
            "`none` in the rows this solution has no use for. Copying it in is a deliberate act; it is here rather",
            "than in the draft because a blank draft would drop every row nobody retyped.",
            string.Empty,
            "```markdown",
            "| Concern | Choice |",
            "|---|---|",
            .. profile.Defaults.Select(row => string.Create(
                CultureInfo.InvariantCulture,
                $"| {row.Concern} | |")),
            "```",
            string.Empty,
        ];
    }
}
