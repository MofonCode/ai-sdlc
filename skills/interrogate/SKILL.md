---
name: interrogate
description: Judge one artifact against the constraints in force, in a fresh process, and write a findings round. Never edits. Use to challenge an intent, spec, plan or verify document.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> <intent|spec|plan|verify>"
allowed-tools: Bash(sdlc *), Bash(claude *), Bash(git *), Read, Write, Glob, Grep
---

# Interrogate an artifact

This skill **judges and never edits**. If you find yourself about to improve the artifact, stop: that
is `refine`, and the separation is the point. An interrogation that could also fix things would grade
its own corrections.

## The fresh process is the whole mechanism

The reader must not have seen the artifact being made. Run the judgement in a **new process**, not in
this session and not as a subagent. **The binary writes every reader's prompt** (D-117); you start the
readers and nothing else:

```bash
sdlc round prompt <id> <artifact> --dir <scratch>/round
# one fresh process per <scratch>/round/<reader>.prompt.md, concurrently, each answer to <reader>.json
SDLC_READER=1 claude -p --output-format text < <scratch>/round/<reader>.prompt.md > <scratch>/round/<reader>.json
```

`<scratch>` is any directory outside the repository that your host gives you to write in. What the
command prints names every reader the round runs — the rules reader, then one per active perspective — and
the path each answer goes to.

**Start every fresh process with `SDLC_READER=1`** — this one, each perspective's, the reconciler's. In
PowerShell that is `$env:SDLC_READER = '1'` before the call. The reader inherits the cycle's branch, and
without the mark a red build's turn-end hook answers the reader's last turn instead of letting it end, so
the verdict you were waiting for never arrives. The mark stands down only that hook; the reader still may
not write a protected file. **Never switch the hooks off to get a reader through** — that is the
safeguard the mark exists so nobody has to remove.

**Pipe the prompt in; never pass it as an argument.** A plan with its upstream and stack runs to tens
of kilobytes, and `claude -p "$(…)"` fails on Windows with `Argument list too long` long before that.

**Never edit a prompt, and never add to it.** Each is what the round is judged from, the same bytes in
every session, and the material a reader may read is decided by the binary rather than by what this
session thinks would help: the constraints, the document above, the stack file on a plan, the spec and the
evidence on a verify (D-58). Nothing about the conversation, the drafts or what the author meant — every
one of those is the bias you just paid to remove.

Any non-interactive invocation of the host will do; what matters is that the model reading the
artifact is not the one that wrote it.

**What this buys and what it does not.** It removes *investment bias* — the reader never saw the
interview, the drafts, or the author's reasoning, so it has nothing to defend. It does **not** remove
*model bias*: a borderline judgement flips the same coin twice. Do not describe the round as
independent verification. It is a second reading by a reader with no stake.

**A tool call is not a substitute.** A tool runs in its own process, but the model calling it is this
session — so either the tool judges, which needs its own model and credentials, or this session
judges, which is the author grading their own homework. A tool call hands data to the reader you
already have. A new process creates a new reader. `sdlc round prompt` is a tool call of exactly that
kind: it assembles, and judges nothing.

## The readers

**The rules reader** judges the document against every rule in force, and answers with findings in JSON,
each citing one rule by its id and the line its heading states, copied rather than summarised.

**A reader per perspective** brings a way of looking — security traces trust boundaries, operability asks
what a person on call could tell at 3 a.m. — each in a fresh process of its own (`CONCEPT.md` §6.5, D-90).
Its prompt carries its method, its questions for this artifact and the rules it may block on, and after
the first round the sections changed since, with the instruction to raise suggestions about those alone.
**Set `"perspective": "<name>"` on every finding it raised when you write the round.** The field is what
`sdlc propose constraints` counts a perspective's suggestions by across cycles; the prefix is only for
reading, and a round naming a perspective the binary does not carry is rejected when it is read back. The
round's own reader leaves the field out.

> **A perspective's finding blocks only when it cites a rule the perspective lists.** Everything else its
> method finds is a `suggestion`. A suggestion that keeps recurring is what `sdlc propose constraints`
> turns into a rule; a perspective reader inventing a blocker is the waiver of D-27 run backwards.

Then **one reconciling reader**, in a fresh process of its own, once every other reader has answered:

```bash
sdlc round prompt <id> <artifact> --dir <scratch>/round --reader reconcile
```

Its prompt carries the artifact, every finding this round's readers raised, the findings still open on
this artifact and **the cycle's open suggestions from every document** — and nothing else. It is asked two
things, and nothing about whether any finding is right: which of this round's findings **restate one still
open**, and which pairs of findings **cannot both be answered**. It decides nothing about the document.

**A suggestion is the cycle's, not the document's** (D-115). The plan's reader raising what the intent's
raised is the same concern found again, so it is carried under the intent's id and recorded against the
plan. That is why the reconciler sees every document's suggestions: one that sees only this document's is
how one cycle came to 1,489 open suggestions, mostly a handful of concerns under fresh ids.

**A restatement is carried under the id that stands** (D-108). The readers have no history, so after a
section moves they raise what is already open there again, under ids of their own. Write each finding the
reconciler names as a restatement with the standing id, at the disposition it stands at, in the words the
ledger holds — which records nothing, because it moved nothing. Leave the reader's new wording out; a
second id for the same objection is how one cycle reached seven duplicate suggestions in a single round.
**Where the reconciler is unsure, the finding is new.** A duplicate is noise a person can see; a distinct
objection merged into an old one is lost.

**Every id that is not a restatement is new, and numbered past what the ledgers hold.** A reader's ids
are its own, so `maintainability.F_01` from this round's reader is not the `maintainability.F_01` the
ledger already has. **A new blocker is `F_nn`, past this document's ledger; a new suggestion is `S_nn`,
past the cycle's** — `maintainability.S_04`. `sdlc round` refuses a known id in other words, and a new
suggestion whose id is taken or not `S_nn`, and names the next free one for that prefix.

**A suggestion raised again as a blocker** keeps neither id: the blocker takes a new `F_nn`, and the
suggestion is handed back `withdrawn` in the same round.

For each pair that cannot both be answered, it names:

| The pair | becomes |
|---|---|
| two findings neither citing a rule | **both suggestions**, neither applied: each keeps its statement and its `perspective`, and gains "contradicts `<the other id>`", so a person reviewing the round sees the contradiction and decides what becomes a future intent |
| a finding citing a rule, and one that does not | the rule's finding stands; the other becomes a suggestion naming the rule it ran into |
| two findings citing two rules | a `conflict` between the two rules, as the first reader would report one (D-64) |

**Why one reconciler after, and not readers that see each other.** A reader shown earlier findings is
anchored by them, order would decide whose finding survives, and the readers would run one at a time. So
they run blind and in parallel, and one reader reads the round whole. That is also why the reconciler, and
not the readers, is shown what stands: it judges no document, so there is nothing for the history to
anchor. And it is why you do not decide restatements yourself — you may have written the document, and
"the same objection" is a judgement in words.

**A perspective listed as not written yet has nothing to run.** Say so in one line of the report; it is a
gap, not a pass.

## Write the round

**One round, whatever ran**: the rules reader's findings, every perspective's, and the reconciler's
changes to them. A round is clean when every reader was silent.

**The binary writes it** (D-101). Put the findings in a file in `<scratch>` — not in the repository —
and hand it over:

```json
{ "findings": [ ... ], "conflicts": [ ... ] }
```

```bash
sdlc round <id> <artifact> --from <scratch>/round.json
```

It numbers the round, stamps when it ran, and records the hash of the document judged, of the one above
it, of each section, and on a verify of the evidence. **It refuses while anything above the artifact is
unsettled** — no round yet, a round that judged a different version, a round judged against an upstream
that has since moved, or a blocker still open — and the refusal names which. That refusal is the order:
the chain is interrogated top down, **one document at a time**, never in parallel, because a round about
a spec judged while the intent is still moving is a clean reading of a document about to stop existing.

**The round lands in one file per artifact, `findings/<artifact>.json`** — the findings ledger (D-107).
The binary merges what you hand it: an id it has not seen is a new finding, a known id at a different
disposition is a move, and a known id at the disposition it already stands at adds nothing. It chains the
round to the ones before it by hash. The hook denies writing the ledger any other way, and the binary
refuses a ledger whose chain has been edited. A cycle still holding round files from before the ledger,
`findings/<artifact>.r<n>.json`, is converted by its next round.

**The round's suggestions land in `findings/suggestions.json`** — one per cycle, each concern once, with a
line per document and round where it was raised, answered or withdrawn (D-115). You hand them over in the
same round file; the binary splits them out. A restatement where it already stands on this document
records nothing. A suggestion that was already in a document's ledger before that file existed stays
there.

Two things are still yours to get right:

**A finding keeps its id across rounds.** A blocker raised in round one and still standing in round
three carries `F_01` in both. Without that, nothing can tell a carried finding from a new one, and the
status panel would report three problems where there is one.

**Every finding gets a `disposition`.** `open` when outstanding. A round that mentions nothing about a
finding has not answered it — the ledger holds each finding at its last move, so silence carries it
forward unchanged, which is correct. **Which means a finding that has been dealt with has to be
mentioned, and said to be dealt with.** The next section is how. **There is no need to restate the
rest.** A finding handed back at the disposition it already stands at records nothing, so a round only
needs what the reader raised and what moved.

**A steer carrying a change down adds a `note`** — one or two sentences saying what moved above this
document since its last round and what this document did about it, including "nothing, because …".

## How a finding stops blocking

**This is the only way out of a blocking finding, and the round you are writing is the only place it
can happen.** A blocker cannot be waived at the gate, with a flag, or by a person deciding it is
fine — `CONCEPT.md` D-27. So a round that is written after the document was fixed, and says nothing
about the finding that prompted the fix, leaves the cycle unable to pass its own gate with nothing
explaining why. That has happened: it is why the reference cycle in this repository carries three
blocking findings that were answered in the document and never recorded as answered.

The fresh reader does not decide this and is not asked to. It judges the document in front of it and
reports what it objects to; it has no history and no ids from earlier rounds. **You hold the history,
so you record the transitions** — and the reader's silence is the evidence:

| An earlier finding the reader | becomes | because |
|---|---|---|
| did **not** raise, and the document changed to address it | `answered` | a reader with no stake read the new text and did not object |
| did **not** raise, and the document did not change for it | `withdrawn` | it was dropped rather than answered — the objection was wrong |
| raised again | stays `open` | whatever anyone says it was, it still stands |
| did not raise, and you cannot tell which of the first two | stays `open` | a finding you are unsure about is not one you may clear |

**The last row is the rule, not a fallback.** You are the session that may have written the document.
Marking a finding answered on your own reading of it is the one inversion this whole skill exists to
prevent, so the only thing that clears a blocker is a fresh reader not raising it.

Which of the first two applies comes from `refine`'s report: it says, per finding, whether it changed
the document (`healed` or `assumed`) or disagreed and left the artifact alone. If you are running the
loop you have that report. If you are not, the diff since the previous round answers it.

A round that answers something therefore carries both: what the reader just found, and the earlier
findings whose disposition moved. **A suggestion moves only on the documents that raised it**: a spec
round may answer what was raised against the spec, and not what was raised against the intent only.

```json
{
  "round": 2,
  "findings": [
    { "id": "F_01", "severity": "blocking", "disposition": "answered",
      "cites": { "id": "CON_02", "heading": "<as round 1 copied it, unchanged>" },
      "statement": "<as round 1 stated it, unchanged>" },
    { "id": "S_03", "severity": "suggestion", "disposition": "open",
      "statement": "<what this reader found>" }
  ]
}
```

**Carry the severity, the statement and the citation across unchanged.** The id, the heading and the
statement are what round one said; this round is saying where that objection has got to, not restating it
in your own words. **`sdlc round` refuses a known id in other words**, because a round whose `F_01` says
something different from round one's `F_01` is one id meaning two objections -- and that includes the
heading, which is round one's record of the wording it judged against, not a field to refresh from
today's library. A new objection gets a new id.

**An empty `findings` array still means a clean reading, and it clears nothing.** If round one raised
a blocker and round two is empty, that blocker is still open and still refuses the build — which is
what makes the distinction above worth getting right rather than a formality.

**A conflict is written as the reader gave it, and never carried forward.** Unlike a finding it has no
id and no disposition, because it is this round's observation rather than a tracked objection. The
conflicts that stand are the latest round's: one declared in round two and not declared in round three
is one the refiner found an exit for, and the next reader's silence is what says so — the same rule
that moves a finding to `answered`. So do not copy a previous round's conflicts into this one. If it
still holds, this round's reader will have said so independently, and that independence is the whole
value of the declaration.

**Omit `conflicts` entirely when there are none.** An absent array and an empty one mean the same
thing here, unlike `findings`. A reader that found no conflict is the overwhelming default, and a key
that is empty on almost every round is the decoration `CON_11` exists to refuse.

## Then report, and stop

```
sdlc status <id> --stage <artifact>
```

Show it verbatim, then **at most two lines of your own**. Do not apply anything. Do not recommend
whether to run another round — the panel withholds that on purpose, because the judgement has been wrong
before and the owner decides.

**What the round found is in the findings document and on the panel, so do not narrate it again.** A
round's own prose is the one thing here nothing downstream reads: the next round reads the JSON, the
gate reads the fold, and the person reads the panel.

An artifact that ran eleven rounds is not a failure of the process. On one real intent, round seven
found that the headline change was unreachable, and round eleven found that no success criterion
failed if it were skipped entirely.
