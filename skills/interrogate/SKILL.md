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
this session and not as a subagent:

```bash
sdlc constraints --for <artifact> > /tmp/sdlc-constraints.txt
# a plan round reads one file more -- see the prompt below
cat .sdlc/techstack.md > /tmp/sdlc-techstack.txt
claude -p "$(cat <<'PROMPT'
<the judging prompt below>
PROMPT
)" --output-format text
```

Any non-interactive invocation of the host will do; what matters is that the model reading the
artifact is not the one that wrote it.

**What this buys and what it does not.** It removes *investment bias* — the reader never saw the
interview, the drafts, or the author's reasoning, so it has nothing to defend. It does **not** remove
*model bias*: a borderline judgement flips the same coin twice. Do not describe the round as
independent verification. It is a second reading by a reader with no stake.

**A tool call is not a substitute.** A tool runs in its own process, but the model calling it is this
session — so either the tool judges, which needs its own model and credentials, or this session
judges, which is the author grading their own homework. A tool call hands data to the reader you
already have. A new process creates a new reader.

## The prompt to give the fresh reader

Give it the constraints in force, the artifact, and the artifact above it in the chain — and, **when
the artifact is a plan, the solution's `techstack.md`**. Nothing else — no conversation, no request
history, no explanation of what the author was trying to do. Every one of those is the bias you just
paid to remove.

**Why the stack file is a fourth input and not a fourth bias.** `CON_15` is a rule entirely about
conformance to `techstack.md`, and while the reader was not given that file the rule could not be
decided. A reader could only object to a departure the plan confessed to — so the rule caught the
honest plan and missed the careless one, which is the inversion of what a rule is for. It is not
hypothetical: round one of a real plan said in its own findings that it could not tell whether the
component-test package it was reading about was the declared default, an undeclared addition, or a
replacement for something the stack named, and declined to raise the point for exactly that reason.

What the fresh process withholds is the **cycle's own history** — the drafts, the interview, the
earlier findings with their ids and statements (D-49). `techstack.md` is none of those. It is a rule
source, the same category as the constraint set already being handed over, and it existed before this
cycle did. Handing it over removes nothing the new process was opened to protect.

**A plan only.** `CON_15` applies to a plan and to nothing else, and an intent or a spec that reached
for a package would be breaking a different rule. On those rounds the file would be context with no
rule to serve, which is the definition of what does not belong in this prompt.

**An empty stack is pasted as an empty stack, not left out.** An adopted solution's table starts
empty, and a reader handed no block cannot tell "this solution declares nothing" from "nobody showed
me". The first means every choice in the plan is unconstrained by a declared default and `CON_15` has
nothing to bite on; the second means the reader should not have judged the rule at all. Paste the file
as it is, and if it is absent say so in the block in those words.

> You are judging one document against a fixed set of rules. You did not write it and you have no
> stake in it.
>
> Here are the rules in force. Each has an `Applies to`, a `Forbids` and a `Why`:
>
> <the output of sdlc constraints --for \<artifact\>>
>
> Here is what this solution declares it is built from:
>
> <the contents of .sdlc/techstack.md — this block on a plan round only, and omitted whole for an
> intent or a spec>
>
> Here is the document above it in the chain, which it is meant to be derived from:
>
> <the upstream artifact, or "none -- this is the first artifact" for an intent>
>
> Here is the document to judge:
>
> <the artifact>
>
> For each rule, decide whether this document breaks it. Report only what you can point at in the
> document. Do not report style, wording you would have chosen differently, or anything the rules do
> not forbid.
>
> For every finding, give: the single rule it cites, by id and revision **and by the one line its
> heading states** -- copied from the rules above exactly as written, never summarised in your own
> words; what is wrong, in full; where in the document; and what would fix it.
>
> A finding is `blocking` when the rule it cites forbids what the document does. It is a `suggestion`
> when it would improve the document and no rule forbids the current state. A blocking finding must
> cite exactly one rule. A suggestion may cite none.
>
> The document's `## Open` section is not judged. It is where the document records what it
> deliberately does not assert, and recording something there is a legitimate answer to a rule rather
> than a breach of it. What *is* judged about it is whether each entry is genuinely unsettleable —
> see the rule about open items.
>
> If two rules' remedies exclude each other, report it as a conflict rather than choosing one. A
> conflict is not an objection to this document: it is the claim that **no** version of this document
> satisfies both rules, so every edit that answers one breaks the other. Name both rules, the same way
> a finding names its one. Before you report one, check that neither rule's own exit applies -- a
> remainder named in `Out of scope`, or an item that genuinely belongs under `## Open` -- because a
> conflict with an exit available is a finding.
>
> Answer as JSON only, in this shape:
> ```json
> { "findings": [ { "id": "F_01", "severity": "blocking",
>   "cites": { "id": "CON_16", "heading": "A spec covers every outcome its intent asks for, and all of each one" },
>   "statement": "...", "locus": "...", "remedy": "..." } ],
>   "conflicts": [ { "between": [
>       { "id": "CON_05", "heading": "A spec introduces no scope its intent did not ask for" },
>       { "id": "CON_16", "heading": "A spec covers every outcome its intent asks for, and all of each one" } ],
>     "statement": "why each remedy excludes the other", "locus": "..." } ] }
> ```
> An empty `findings` array means the document breaks nothing. Omit `conflicts` when there are none,
> which is the normal case.

**Why the citation carries the rule's own line.** An id on its own is an index entry. It tells a reader
that something was breached without telling them what, so the only way to read the finding is to go and
resolve the id somewhere else -- which nobody does, and which nothing downstream makes them do. The line
travels with the citation instead, into the status panel, the gate refusal and the approval draft, so a
person meeting the finding meets the rule with it.

**Copy it, do not write it.** The heading is in the block of rules you were handed, above its
`Applies to`. A summary of a rule is a second version of that rule, and the round is a historical record
of what was actually judged -- so the line has to be the one that was read. A citation with no heading is
rejected when the round is read back.

## Write the round

`.sdlc/cycles/<id>/findings/<artifact>.r<n>.json`:

```json
{
  "framework_version": "1.0.0",
  "cycle": "<id>",
  "artifact": "<artifact>.md",
  "upstream_sha256": "<the upstream's canonical hash>",
  "interrogated_at": "<now, UTC, ISO 8601>",
  "round": 2,
  "findings": [ ... ]
}
```

Four things to get right:

**The round number is one past the highest already there.** Rounds accumulate and are never
rewritten. The history is the record of what was challenged and what became of it.

**A finding keeps its id across rounds.** A blocker raised in round one and still standing in round
three carries `F_01` in both. Without that, nothing can tell a carried finding from a new one, and the
status panel would report three problems where there is one.

**Every finding gets a `disposition`.** `open` when outstanding. A round that mentions nothing about a
finding has not answered it — the fold reads the latest round that *mentions* an id, so silence
carries it forward unchanged, which is correct. **Which means a finding that has been dealt with has
to be mentioned, and said to be dealt with.** The next section is how.

**`upstream_sha256` is the canonical hash of the artifact above, as you just read it.** Omit it only
for an intent, which has no upstream. This is what makes a stale judgement detectable: the approval's
three hashes prove three documents were approved, not that the spec was derived from *this* intent, so
an upstream edited afterwards leaves a round that is clean about a document that no longer exists. A
round with no upstream recorded counts as stale, so leaving it out does not skip the check.

The canonical hash is CRLF normalised to LF, trailing whitespace stripped per line, one closing
newline. `sdlc status <id>` prints the first twelve characters of each document's hash if you need to
check your arithmetic.

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
findings whose disposition moved.

```json
{
  "round": 2,
  "findings": [
    { "id": "F_01", "severity": "blocking", "disposition": "answered",
      "cites": { "id": "CON_02", "heading": "<as round 1 copied it, unchanged>" },
      "statement": "<as round 1 stated it, unchanged>" },
    { "id": "F_03", "severity": "suggestion", "disposition": "open",
      "statement": "<what this reader found>" }
  ]
}
```

**Carry the statement and the citation across unchanged.** The id, the heading and the statement are
what round one said; this round is saying where that objection has got to, not restating it in your own
words. A round whose `F_01` says something different from round one's `F_01` makes the history
unreadable -- and that includes the heading, which is round one's record of the wording it judged
against, not a field to refresh from today's library.

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
