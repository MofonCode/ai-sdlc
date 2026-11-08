---
name: mine
description: Mine the solution's cycles -- read what every merged cycle argued, settled and raised, and propose the rules, decisions and stack rows it is missing, answering every item. Proposes and never places. Use once a cycle's verify passes, or when somebody asks to bring the rules, decisions and stack up to what the cycles settled.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *), Bash(claude *), Bash(git show *)
---

# Mine the cycles

```
sdlc mine
sdlc draft mining <the proposal file>
```

**Mining is reading the record across cycles; converging is a document's rounds settling.** The two
were once both called convergence, and this skill was `/sdlc:propose convergence` (D-148).

**Once a cycle's verify passes, this is the one to run** — on the cycle's branch before its pull request,
as a commit of its own that merges with the code, or on the integration branch after the merge (D-111).
It brings the rules, the decisions and the declared stack up to what the cycles have settled, in one
sitting, and it is not a cycle and not part of one: the person decides what binds, so nothing about it
needs a plan or an approval (D-104). What mining places binds the **next** cycle, never one in flight —
rules placed mid-cycle re-judge documents already argued, which in one real cycle cost about twenty
blockers. That is why **never before the passing verify** — and this skill is what keeps it: the
pull-request check only reports a change committed ahead of the verify, since git cannot tell mining
from the person's own edit in the cycle (D-113). If you are on a cycle branch and `sdlc status <id>` does not
show a passing verify, stop and say so.

## 1. Read the report, then the rounds

`sdlc mine` prints, ranked: rules cited across cycles; the rules **argued** longest inside
a cycle, with how long their longest finding stood and whether the rule is the framework's; findings that
rested on no rule, grouped by who raised them and at which stage, each sample with its cycle, its id and
how many rounds it stood; which readers raised what a later one withdrew; and, per cycle, **the rounds
each document took, every reopen with the reason the person wrote, and every verdict the verify
reached** — then what approved cycles stated, and what the declared stack never mentions. It ends with
the path of the proposal file.

**The report is what to read, not what to propose.** Its samples are pointers, and a history runs to
hundreds of findings, which no single reading holds. So the reading is split:

1. **One fresh process per cycle and document with findings**, concurrently, started as
   `SDLC_READER=1 claude -p --output-format text --tools= --strict-mcp-config --no-session-persistence`
   with the prompt piped in — no tools, because everything it reads is in its prompt (D-118). Hand each `sdlc findings <id> --stage <artifact>` and nothing else,
   and ask: *group these findings by the problem underneath, not by their wording — one plan raised the
   same missing log level twenty times in twenty wordings. For each group: the finding ids, one sentence
   naming the problem, how many rounds its longest finding stood, and the remedy the readers agreed
   on, if they did. Return the groups and nothing else.*
2. **Then read, yourself, what no reader is handed:** each reopen's reason, and each criterion that
   failed more than one verdict, with the verify documents of those runs (`git show <commit>:<path>`,
   commits from the report's cycle section). A reopen is a person who already decided something and
   usually named what they refused — the closest thing in the record to a finished decision. A criterion
   failing verdict after verdict is an argument about what the criterion asks.
3. **For each argued rule**, read what the cycle finally did about it in the plan: that choice is the
   decision to record.

### Account for every item

Before writing a single proposal, list what the record raised, and give each one an answer. Every
**argued rule**, every **reopen**, every **criterion that failed more than one verdict**, every
**perspective at the bar**, every **group a reader returned whose findings stood three rounds or
more**, and every entry the report lists **over the word budget** gets one of:

- a proposal, by the heading it will have in the file
- *already recorded*, naming the entry — `DEC_04`, `LOC_02`, a stack row
- *a note for the framework* — a framework rule's cost, which no local entry can change; written under
  `## For the framework` in the proposal file, with its evidence, for the person to carry to ai-sdlc
- *no proposal, because* — one line, and a real reason: a one-off, a mistake the rule caught correctly,
  a choice already in the code that nothing will revisit

This list goes in the proposal file under `## Not proposed`, after the three sections the drafter reads;
it ignores any other heading. **An item with no answer is a hole in the review**, and step 4 is told to
look for exactly that.

## 2. Write the proposal file

At the path the report names, one heading per proposal, in exactly this shape — `sdlc draft mining`
parses the first three sections and ignores the rest:

```markdown
# Mining proposals — <date>

Mark each proposal you accept by changing [ ] to [x], and edit its wording as you want it.
Nothing is written until you run the draft, and nothing binds until you rename the drafts.

## Rules

### [ ] add — <the rule's one line>
<its three clauses -- which artifact and when, what becomes illegal, what goes wrong without it --
each a bold label and a colon, exactly as constraints.md writes them, so the draft pastes as it is>
> <the cycles and finding ids behind it, each id with one line of what it objected to>

### [ ] supersede LOC_02 — <the replacing rule's one line>
<its three clauses, as above>
> <why the old one is out of force>

### [ ] reword LOC_01 — <the same rule, better words>
<its three clauses: the same Forbids, or it is a supersede>

## Decisions

### [ ] add — <the decision's one line>
**Decided:** <what>

**Because:** <why>

**Instead of:** <what was weighed and refused>

**From:** <the cycle, and where it was settled>

### [ ] supersede DEC_02 — <the reversal's one line>
<its four clauses>

## Stack

### [ ] set <concern> — <the choice, or none>
> <what in the record asks for it>

## For the framework

### <the framework rule> — <what it cost here, in one line>
> <the cycles, finding ids and rounds behind it, each id with one line of what it objected to>

## Not proposed

- <the item> — <already recorded as DEC_04 | no proposal, because ...>
```

**There is no remove.** A rule out of force is superseded and keeps its place; a reversed decision takes a
new id; a stack concern no longer used is set to `none`. A `reword` keeps the `Forbids` — if what is
forbidden changed, it is a `supersede`. Label nothing with an id you invent: the drafter numbers what is
accepted, so a declined proposal leaves no gap.

Every proposal arrives with its evidence on `>` lines. A proposal you cannot evidence from the record is
one the person cannot refuse on the record, so leave it out.

**Short enough to be read as a headline** (D-149). Every reader is handed every entry's title, and the full
body only of the entries in scope, so a title takes at most **15 words** — what was decided, not why — and
each clause at most **60**. `sdlc draft mining` refuses a new entry over either and names it. An entry the
report lists over budget is proposed for a `reword` when it is a rule, and a `supersede` with the shorter
wording when it is a decision, which takes a new id rather than being reworded in place (D-70).

## 3. Hand it to the person, then draft

Tell them where the file is and stop. They mark `[x]` on what they accept and edit the wording. Then:

```
sdlc draft mining <the proposal file>
```

It writes `constraints.draft.md`, `decisions.draft.md` and `techstack.draft.md` beside the real ones —
each a copy of the file as it stands with only the accepted lines applied — and refuses if any entry or
row would go missing. Show what it prints verbatim.

## 4. Review what was approved, in a fresh process

Before the person renames anything, open one fresh process, started as in step 1 of reading the
report, and hand it the marked
proposal file, the three drafts, and `.sdlc/solution.md` — nothing else. Ask:

> For each proposal: was it accepted as proposed, edited, or declined? For each edit and each decline,
> does what was approved still serve what the evidence lines describe? And does the approved set, taken
> together, move the solution toward what `solution.md` says it is, or away from it? Answer **in
> alignment**, or **deviates**, with each place it deviates and why. Do not argue for the proposals.

Then a **second** fresh process, handed the report `sdlc mine` printed and the proposal
file — nothing else — and asked the question the first cannot, because it sees only what was proposed:

> Here is everything the record raised, and what was made of it. Name each argued rule, reopen, criterion
> that failed more than one verdict, and perspective at the bar that has **no answer** in the proposal file
> — neither a proposal nor a line under `## Not proposed` or `## For the framework`. Then name each
> `no proposal, because` whose reason the report contradicts. Answer **complete**, or list the gaps. Do
> not propose anything yourself.

Show both answers verbatim, each under its own heading. A gap goes back to step 1 for that item before the
person marks anything; it is not theirs to find.

Show the answer verbatim under its own heading. **It is advisory**: the person chose knowingly, and the
review makes the choice visible rather than overruling it. Then the renames are theirs.

---
