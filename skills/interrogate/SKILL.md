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

Give it exactly three things: the constraints in force, the artifact, and the artifact above it in
the chain. Nothing else — no conversation, no request history, no explanation of what the author was
trying to do. Every one of those is the bias you just paid to remove.

> You are judging one document against a fixed set of rules. You did not write it and you have no
> stake in it.
>
> Here are the rules in force. Each has an `Applies to`, a `Forbids` and a `Why`:
>
> <the output of sdlc constraints --for \<artifact\>>
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
> For every finding, give: the single rule it cites, by id and revision; what is wrong, in full;
> where in the document; and what would fix it.
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
> If two rules' remedies exclude each other, say so plainly instead of choosing one.
>
> Answer as JSON only, in this shape:
> ```json
> { "findings": [ { "id": "F_01", "severity": "blocking", "cites": "CON_04",
>   "statement": "...", "locus": "...", "remedy": "..." } ] }
> ```
> An empty array means the document breaks nothing.

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
carries it forward unchanged, which is correct.

**`upstream_sha256` is the canonical hash of the artifact above, as you just read it.** Omit it only
for an intent, which has no upstream. This is what makes a stale judgement detectable: the approval's
three hashes prove three documents were approved, not that the spec was derived from *this* intent, so
an upstream edited afterwards leaves a round that is clean about a document that no longer exists. A
round with no upstream recorded counts as stale, so leaving it out does not skip the check.

The canonical hash is CRLF normalised to LF, trailing whitespace stripped per line, one closing
newline. `sdlc status <id>` prints the first twelve characters of each document's hash if you need to
check your arithmetic.

## Then report, and stop

```
sdlc status <id> --stage <artifact>
```

Show it verbatim. Do not apply anything. Do not recommend whether to run another round — the panel
withholds that on purpose, because the judgement has been wrong before and the owner decides.

An artifact that ran eleven rounds is not a failure of the process. On one real intent, round seven
found that the headline change was unreachable, and round eleven found that no success criterion
failed if it were skipped entirely.
