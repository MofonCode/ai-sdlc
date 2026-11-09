# Mining the cycles

What to do once a cycle is finished: read what it and the cycles before it argued, settled and raised, decide
what the solution keeps from them — rules, decisions, stack rows, areas, successors — answer every suggestion
left open, and clear the finished cycles' folders out.

**This is what used to be called convergence.** It is mining now, and *converge* means only a document's rounds
settling inside a cycle (D-148). Mining is not a cycle and not part of one: nothing in it needs a plan or an
approval, because you decide what binds, and what you place binds the **next** cycle, never one in flight.

## When to run it

**Once a cycle's verify has passed, and never before.** Either on the cycle's branch, before its pull request —
mining then rides in as a commit of its own and merges with the code — or on the integration branch after the
merge. `/sdlc:pr` asks once which you want. A rule placed while a cycle is still being argued re-judges
documents that were already settled, which in one real cycle cost about twenty blockers.

You do not have to mine after every cycle. A sitting reads every finished cycle not mined yet, so mining after
three cycles is one sitting rather than three. The three-cycle bar — one cycle is an instance, two a
coincidence, three a pattern — is easier to see with several cycles in front of you.

## A sitting, step by step

| Step | You run | What happens |
|---|---|---|
| 1 | `/sdlc:mine` | the skill runs `sdlc mine`, shows the report, reads the rounds in fresh processes, and writes a proposal file under `.sdlc/reports/`. It places nothing |
| 2 | — | you open the proposal file, mark `[x]` on each proposal you accept, edit its wording to yours, and change any answer to a suggestion you disagree with |
| 3 | `sdlc draft mining <file>` | the skill runs it: it writes `constraints.draft.md`, `decisions.draft.md`, `techstack.draft.md` and `areas.draft.md` beside the real files, each only where something you accepted reaches it, and any successors you accepted |
| 4 | — | two fresh processes review what you approved: whether it still serves the evidence and `solution.md`, and whether anything the record raised was left without an answer. Both are advice; a gap goes back to step 1 for that item |
| 5 | rename each draft over its file | the rename is yours, because these are files only a person writes. Delete a draft you decided against |
| 6 | `sdlc mine record <file>` | records the sitting in `.sdlc/mined.json`. Every finished cycle on the integration branch becomes a **husk**, and the deletion of the husks' folders is printed |
| 7 | the printed deletion, then a pull request | a `mining/<date>` branch, `git rm -r` per husk, the ledger added, committed with `git commit -F`. The check admits it as mining when it carries only the records, the ledger, successors and the deletions |

**Nothing is lost when a husk goes.** Its folder is in git at the commit the ledger names, and what later mining
needs from it — the rules it cited, the files it touched, the areas its plan proposed, its rounds and verdicts —
is kept in the ledger. `sdlc mine --history` shows the ledger.

**A cycle mined on its own branch before its pull request is not a husk yet**: it is not on the integration
branch. The sitting says so, and the next one picks it up.

## What the proposal file holds

| Section | What goes in it | Placed by |
|---|---|---|
| `## Rules` | `add`, `supersede` or `reword` one of this solution's own rules | the constraints draft |
| `## Decisions` | `add` or `supersede` a recorded decision | the decisions draft |
| `## Stack` | `set` a concern of the declared stack, or set it to `none` | the stack draft |
| `## Areas` | `add`, `rename`, `merge`, `remove`, re-glob or `tag` an area | the areas draft |
| `## Successors` | a suggestion worth doing, as a successor of the cycle it came from | a file under `.sdlc/successors/` |
| `## Watch` | a concern seen in fewer than three cycles, with the cycles | the ledger, at the record |
| `## Declined` | a suggestion decided against, with why | the ledger, at the record |
| `## Ignored` | a suggestion let go, as its concern and the cycles | the ledger's tally, at the record |
| `## For the framework` | a framework rule's cost here, for somebody to carry to ai-sdlc | nothing: it is a note |
| `## Not proposed` | every other item the record raised, with why it gets no proposal | nothing: it shows the reading was complete |

**There is no remove** for a rule or a decision. One out of force is superseded and keeps its place; a reversed
decision takes a new id. An area can be removed, because nothing cites an area's name.

Every proposal arrives with its evidence beneath it on `>` lines — the cycles and findings behind it. Refuse one
on that evidence as freely as you accept one.

## Answering the suggestions

The report lists every open suggestion of the finished cycles not yet mined, as `<cycle>/<id>`, and the skill
proposes one of three answers for each. **Read these answers as carefully as the proposals**: they are where
the difference between letting something go and deciding against it is kept.

| Answer | Means | Afterwards |
|---|---|---|
| **taken further** | it is worth doing | a successor, its text copied in, which `/sdlc:next` picks up once it is ready |
| **declined** | you decided against it, and someone who raises it again deserves the reason | the reason, kept in the sitting in the ledger |
| **ignored** | nothing to do, nothing to decide | a tally of the concern and the cycles it was ignored in — no reason, and no decision |

**Decline is a decision; ignore is not.** Change an ignore to a decline when you would want the reason on record,
and a decline to an ignore when there is no reason worth keeping.

**Ignoring is counted, by concern.** The same concern raised in a later cycle has a new id, so the tally is by
its gist, and the report lists the earlier ignored concerns with how many cycles each was ignored in. **One
ignored in three cycles is at the bar**: the report says so, and that sitting proposes it as a rule, takes it
further, or declines it, rather than ignoring it a fourth time. That is how a concern nobody decided anything
about still gets seen when it keeps coming back.

## When it refuses

| Refusal | What to do |
|---|---|
| the skill stops on a cycle branch whose verify has not passed | finish the cycle first, or mine on the integration branch after it merges |
| `sdlc draft mining` refuses an entry or a row that would go missing | the proposal would drop something the file holds; supersede it or set it to `none` instead |
| `sdlc draft mining` refuses an entry over its word budget | a title takes at most fifteen words and a clause sixty: shorten it in the proposal file and draft again |
| `sdlc draft mining` refuses an entry naming an area nobody defines | add the area in the same file, or take the tag off |
| `sdlc mine record` refuses while a draft is beside its file | rename it into place or delete it, then record |

`handling-refusals.md` has every refusal the framework produces.

## Where to go next

| | |
|---|---|
| the suggestions a solution has, and what became of each | `sdlc suggestions --all` |
| the skill in depth, with everything it reads | [`../../skills/README.md`](../../skills/README.md#mine) |
| the files mining drafts, and why only you place them | [`../mechanisms/protected-files.md`](../mechanisms/protected-files.md) |
| where the ledger lives and what shows it | [`../reference/data-views.md`](../reference/data-views.md) |
| the cycle mining reads | [`running-a-cycle.md`](running-a-cycle.md) |
