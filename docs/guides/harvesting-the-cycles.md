# Harvesting the cycles

What to do once a cycle is completed: take back what it settled — rules, decisions, successors, stack rows,
areas — one aspect at a time, then close it and clear its folder out.

**This used to be called mining**, and before that convergence. *Converge* means only a document's rounds settling
inside a cycle (D-148). A harvest is not a cycle and not part of one: nothing in it needs a plan or an approval,
because you decide what binds, and what you place binds the **next** cycle, never one in flight.

## Which cycles it reads

**Completed cycles only** (D-190): a cycle whose pull request merged after a passing verify, or one a person
abandoned. The pull request is where you accept a build (D-181), so a cycle that passed verify and whose pull request
is still open, closed or sent back has not been accepted yet. `sdlc harvest` lists it as waiting. A cycle in flight is
read by nothing here.

**An abandoned cycle gives some things and not others.** Your reason for abandoning it can become a decision, its
findings still count as evidence for rules, and its suggestions can become new intents. Its plan's approach is never
offered as a decision, because that approach is what you abandoned.

**An abandoned cycle needs no merge.** If you stopped it before any of it reached the integration branch, it lives
only on its own `cycle/<id>/...` branch. `sdlc harvest` finds it there, among the branches this clone knows (yours
before the remote's), and brings its folder into your working tree, untracked, so every aspect reads it like the rest.
What you keep from it reaches the integration branch through the harvest's own pull request; its folder never does. A
branch that only exists on somebody else's machine is not seen: push it if it should be harvested.

**A rejected series is abandoned in one step.** Place the record `sdlc draft abandon --series <id>` prints beside the
series' own, on `series/<id>`, and push it: every cycle the series ran reads as abandoned with your one reason, and
`sdlc harvest` brings the series' folder and theirs in from that branch the same way.

You do not have to harvest after every cycle. The three-cycle bar — one cycle is an instance, two a coincidence,
three a pattern — is easier to see with several cycles in front of you.

**A long backlog is harvested in batches.** Name the cycles for a sitting and each step reads those alone
(`--cycles <id>,<id>`). The rule counts still cover every completed cycle, and the rest wait for a later sitting
(D-166).

## A harvest, step by step

Say `/sdlc:harvest`. It is a playbook: it runs five skills, **one at a time**, and then closes (D-191).

| Step | Skill | What it proposes | Placed in |
|---|---|---|---|
| 1 | `/sdlc:harvest-constraints` | rules the findings kept arguing | `constraints.md` |
| 2 | `/sdlc:harvest-decisions` | choices the cycles settled | `decisions.md` |
| 3 | `/sdlc:harvest-suggestions` | each open suggestion: taken further, declined, covered or ignored | `.sdlc/successors/`, and the ledger |
| 4 | `/sdlc:harvest-stack` | rows the declared stack never mentions | `techstack.md` |
| 5 | `/sdlc:harvest-areas` | areas the plans named | `areas.md` |
| 6 | `/sdlc:close-cycle` | — it closes the cycles every aspect has answered | a pull request you merge |

`sdlc harvest` shows which aspects each completed cycle still needs, and the playbook runs only those. Each step is
also a skill you can say on its own — *just harvest the decisions* — and nothing is lost if you stop after one: the
ledger remembers which aspects each cycle has answered.

**Every aspect runs the same way:**

1. The skill writes a proposal file under `.sdlc/reports/` and hands back what `sdlc harvest summary <file>` prints:
   each proposal numbered, with its mark, the other answers counted by theme, and every rule, decision and successor the file
   names with what it says — then what it needs from you. The skill chooses the themes; the binary counts and quotes,
   so a group cannot claim more than it holds. It places nothing.
2. You open the file, mark `[x]` on each proposal you accept, and edit its wording to yours — or answer by number
   (*accept all but 11*) and the skill marks the file for you. A successor you want and not now is marked `[h]`:
   it is drafted held, so no wave or series starts it until you do (D-220).
3. The skill runs `sdlc draft harvest <file>`, which writes the draft beside the real file — a copy with only your
   accepted lines applied — and two fresh processes review it: whether what you approved still serves its evidence
   and `solution.md`, and whether any item was left without an answer. A gap goes back to the skill first.
4. You rename the draft over its file, or delete it. The rename is yours, because these are files only a person
   writes.
5. The skill runs `sdlc harvest record <aspect> <file>`, which records in `.sdlc/harvested.json` that the cycles in
   the file's `## Cycles` have answered that aspect, and keeps its notes for the framework. Recording closes nothing.

Every proposal arrives with its evidence beneath it on `>` lines. Refuse one on that evidence as freely as you accept
one. **There is no remove** for a rule or a decision: one out of force is superseded and keeps its place.

## What makes a decision

**A decision is a choice that had a real alternative, that future cycles will meet again, whose reason is not in the
code, and that steers without forbidding** (D-189). Departing from it is declared, not blocked. Where a choice forbids
something, that half is a rule instead.

**Who wrote it decides how much evidence it needs:**

| Source | Qualifies |
|---|---|
| your own words: a fixed decision in an intent, your reason for abandoning or reopening, your note on a declined deviation | once |
| a plan's approach, which an agent wrote | only when the same choice appears in two or more cycles |
| a departure from a standing decision | once, as a supersede of that decision |
| a fixed decision marked unconfirmed | never: it is an assumption, not a choice |

**A declined suggestion is not a decision.** It stays in the sitting with its reason and never reaches
`decisions.md`.

## Answering the suggestions

Every open suggestion of a completed cycle gets one of four answers (D-153):

| Answer | Means | Afterwards |
|---|---|---|
| **taken further** | it is worth doing | a successor, its text copied in, which `/sdlc:wave` or `/sdlc:series` picks up |
| **declined** | you decided against it, and someone who raises it again deserves the reason | the reason, kept in the sitting in the ledger |
| **covered** | a rule, a decision, a successor or a file here already handles it | what covers it, checked and kept in the sitting in the ledger |
| **ignored** | nothing to do, nothing to decide | a tally of the concern and the cycles it was ignored in |

**Covered is checked.** `sdlc harvest record` refuses the sitting when a `## Covered` line names a rule not in force,
a decision not standing, a successor that does not exist and was never started, or a file that is not here — every
such line at once, and nothing is recorded. Covered keeps *declined* for real refusals: nobody decided against a
concern a rule already handles.

**Ignoring is counted, by concern.** One ignored in three cycles is at the bar: that sitting takes it further,
declines it or covers it, rather than ignoring it a fourth time. A covered line's `Concern:` names the concern it
closes, and that concern leaves the tally.

**Every counted concern carries its reason.** A concern watched below the bar, or ignored, is kept with a line
saying what it prevents and what it promotes, so when it reaches the bar you can weigh it without digging the
deleted cycles back out of git. `sdlc harvest --history` prints it under each one. Concerns are matched by their
wording, so check the sitting's file before it is recorded: two wordings of one concern count apart.

## What a harvest finds about the framework

**A sitting has two outputs** (D-204). What it finds about your solution goes into your records. What it finds about
the framework — a rule or a reader that cost more than it caught, a rule any solution would need, a step that did
something wrong — goes under `## For the framework`, because nothing you place here could change it.

Each note is written for somebody who has never seen your solution: its heading names the part of the framework it is
about and what fell short, a `Kind:` line says `cost`, `missing` or `defect`, and the evidence — your cycles, finding
ids and rounds — goes beneath it on `>` lines. `sdlc harvest record` keeps the notes in `.sdlc/framework-harvest.json`
with the release each one's cycles ran on, so a cost the framework has since fixed reads as history.

`sdlc harvest framework` prints everything that could leave your solution: each note **without** its evidence, and
each closed cycle's release, rounds, verdicts, what each reader raised and what it cost, **without** its id, files,
commits or your own rules. Read it, and if you choose to, write it with `--out <file>` and hand that file to the
framework's maintainers. Nothing sends it for you.

## Closing

Once every aspect is answered for a cycle and every draft is placed or deleted, `/sdlc:close-cycle` runs
`sdlc harvest close` on the integration branch (D-192). It records each closed cycle in the ledger, commits the
removal of their folders with the ledger and the records you placed on a `harvest/<date>` branch, pushes it, and
opens the pull request. **You merge it**; nothing reaches the integration branch unreviewed. The check admits it as a
harvest when it carries only the records, the ledger, successors and the removed folders.

**Nothing is lost when a folder goes.** It is in git at the merge commit the ledger names, and what later harvests
need — the rules it cited, the files it touched, the areas its plan proposed, the releases it ran on, what each
reader raised and what each document cost — is kept in the ledger.
`sdlc harvest --history` shows it. A cycle read from its own branch is at that branch's last commit instead, and the
folder brought into your working tree is deleted again. Once the pull request merges, `sdlc tidy` lists that branch
for deletion; until then it keeps it, so nothing is deleted before it is harvested.

## When it refuses

| Refusal | What to do |
|---|---|
| `sdlc draft harvest` refuses an entry or a row that would go missing | supersede it or set it to `none` instead |
| `sdlc draft harvest` refuses an entry over its word budget | a title takes at most fifteen words and a clause sixty: shorten it and draft again |
| `sdlc draft harvest` refuses an entry naming an area nobody defines | add the area in the same file, or take the tag off |
| `sdlc harvest record` refuses while the aspect's draft is beside its file | rename it into place or delete it, then record |
| `sdlc harvest record` refuses a note for the framework | give its heading a surface — `CON_06`, `stage:verify`, `perspective:security` — and a `Kind:` line beneath it |
| `sdlc harvest close` refuses while a cycle still needs an aspect | run that aspect's skill first; the refusal names it |
| `sdlc harvest close` refuses anywhere but the integration branch | switch to it, where the cycles merged and the drafts were placed |

[`handling-refusals.md`](handling-refusals.md) has every refusal in full.

## Where to go next

| | |
|---|---|
| the suggestions a solution has, and what became of each | `sdlc suggestions --all` |
| the skills in depth, with everything they read | [`../../skills/README.md`](../../skills/README.md#harvest) |
| the files a harvest drafts, and why only you place them | [`../mechanisms/protected-files.md`](../mechanisms/protected-files.md) |
| where the ledger lives and what shows it | [`../reference/data-views.md`](../reference/data-views.md) |
| the cycle a harvest reads | [`running-a-cycle.md`](running-a-cycle.md) |
