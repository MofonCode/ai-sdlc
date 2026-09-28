---
name: interrogate
description: Challenge one artifact against the effective constraint set and write the findings document. Use before an intent, spec or plan advances, and whenever a person asks whether an artifact would survive review.
argument-hint: "[cycle-id] [intent|spec|plan, optional]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Write
---

# Interrogate `$0`

Challenge one artifact against every constraint that applies to it, and **write a findings document to
disk**. You are an adversary, not an editor: you do not fix the artifact, and you do not merely describe
what you found.

## 0. Work out which artifact you were given

Three forms, and the first argument is a cycle id in the two that matter — the same as every other
command in this process.

**`$0` is a cycle id and `$1` names a kind.** `intent`, `spec` or `plan`. Interrogate
`.sdlc/cycles/$0/$1.md`. Nothing is inferred, so nothing can be inferred wrongly. Prefer this form.

**`$0` is a cycle id and `$1` is empty.** Interrogate the furthest-advanced artifact that exists in
`.sdlc/cycles/$0/`, in flow order: `plan.md` if it is there, otherwise `spec.md`, otherwise `intent.md`.
Convenient while a cycle moves forward one artifact at a time.

Say which artifact you chose, in the first line of your report — *"interrogating plan.md, the
furthest-advanced artifact in this cycle"* — and name the explicit form once:

> To judge a different one, pass its kind: `/sdlc:interrogate $0 spec`.

That matters because flow order picks wrongly in one real case. Amending an accepted spec after a
verification failure is a legitimate way out of an escalated cycle, and it leaves a plan on disk, so the
rule lands on the plan when the spec was meant. Naming your choice is the only thing standing between
that and a confident verdict on the wrong document.

**`$0` contains a `/` or ends in `.md`.** A path. Interrogate that file and nothing else. This is the
escape hatch for an artifact that does not live in a cycle directory.

Anything else — an unrecognisable argument, or none — stop and say what you expected. Do not scan for a
likely cycle.

If the cycle directory does not exist, or holds none of the three, say so and name `/sdlc:intent` as the
step that starts one. If `$1` is `verify`, say that a verify document is not interrogated: it is itself
the adjudication of a diff, and `/sdlc:verify` is what produces it.

## 0b. Refuse an artifact that is already settled

Read the artifact's frontmatter before judging it. If it carries `accepted_by` and `accepted_at`, or
`approved_by` and `approved_at`, **stop.**

A person has passed that gate. A finding raised now has no consumer: the artifact it would change is
already the basis of everything downstream, and the phase that would have acted on the finding is behind
you. Producing one anyway invites exactly the loop this process exists to avoid — an artifact that comes
back sound, an observation that reads as work, a round that changes nothing.

Say which gate was passed and by whom, then where the cycle actually is: after an accepted spec the step
is `/sdlc:plan`, after an approved plan it is `/sdlc:build`. If every artifact in the cycle is settled,
say so plainly — that is a useful answer, not a failure.

**The one legitimate reason to interrogate a settled artifact is that it changed.** Amending an accepted
spec is a way out of an escalated cycle, and an amendment makes the old acceptance stale. The order is:
amend it, remove the acceptance fields, interrogate, then have a person accept it again. Say that, so the
route is obvious. Never clear those fields yourself — they are the gate, and clearing one is a person's
act.

## The constraint set

Injected below at load time, and the only copy. Do not read it from disk and do not invoke another
skill to fetch it: a session working in a governed solution is sandboxed to that solution and cannot
reach the plugin root. If you find yourself asking for directory access you have taken a path that
does not exist.

Also read `.sdlc/CONSTRAINTS.md` for this solution's own `LOC_nn`. It is often absent; that is normal
and not a problem to report.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`

---

# What to do

## 1. Resolve what the artifact answers to

A spec is judged against its intent and a plan against its spec, so several constraints are
relational. Read the artifact's frontmatter and open the predecessor it names.

**If you cannot resolve it, stop and report that as the blocking problem.** Do not judge the
constraints you can reach and stay quiet about the rest: that produces a document saying the artifact
is sound when the relational rules were never applied, which is the most expensive kind of wrong
answer this process can give. A bare filename where a repository-relative path belongs is the common
cause.

## 2. Judge

Each constraint above states what it **Applies to**. Check only the ones that apply to this
artifact's kind — a spec is not judged by a rule about waivers.

- **Judge against the `Forbids:` clause, never the heading.** The heading names a concern; the
  clause is what makes a breach, and it is deliberately narrower. A spec with a criterion for every
  outcome does not breach a clause about outcomes left "with no criterion at all", however vague
  those criteria are — that is a different constraint's breach. If the clause does not cover what
  you are looking at, there is no finding, however wrong it seems
- **Cite exactly one constraint per finding.** A problem breaching two constraints is two findings
- **Never invent a constraint.** If something is wrong and no constraint forbids it, keep it out of
  the document, and do not manufacture an ID. You may raise it afterwards as a single observation,
  under the cap in step 4 — which exists because an uncapped list of them has no stopping point
- **Report nothing when nothing is wrong.** An empty findings array is legitimate and common.
  Over-reporting is as damaging as under-reporting: it teaches everyone to skim
- **Severity is not a feeling.** `blocking` means the artifact must not advance as written.
  `suggestion` means it may advance and would be better changed

## 3. Work out the path, then write the file

This step is the deliverable. **Printing the document into your reply is not doing it** — nothing
downstream reads your reply, and remediation, verify and landing all read the file.

Everything for one cycle lives in `.sdlc/cycles/<id>/`, and every round of every interrogation for
that cycle lives in its `findings/` folder, named after the artifact it judged:

| Artifact | Findings |
|---|---|
| `.sdlc/cycles/<id>/intent.md` | `.sdlc/cycles/<id>/findings/intent.r<round>.json` |
| `.sdlc/cycles/<id>/spec.md` | `.sdlc/cycles/<id>/findings/spec.r<round>.json` |
| `.sdlc/cycles/<id>/plan.md` | `.sdlc/cycles/<id>/findings/plan.r<round>.json` |

`<round>` starts at 1 and increments: list the target directory first and take one higher than the
highest already there. Never overwrite an earlier round — the sequence is the record of whether a
repair worked. Create the directory if it does not exist.

So interrogating `.sdlc/cycles/20260927_0002/intent.md` with no earlier rounds writes
`.sdlc/cycles/20260927_0002/findings/intent.r1.json`.

Every field is required for a reason:

- `framework_version` — the `Framework version` stated with the injected constraints above
- `cycle` — the artifact's id, `YYYYMMDD_xxxx`
- `artifact` — the path you were given, repository-relative
- `interrogated_at` — the `Now (UTC)` value stated with the injected constraints above, verbatim.
  You cannot read a clock, and a guessed timestamp orders the rounds wrongly
- `round` — as derived above
- `locus` — a quoted phrase, a heading, or a path and line. A finding nobody can locate cannot be
  acted on
- `remedy` — required on every `blocking` finding. Remediation cannot work a finding that does not
  say what would resolve it
- `disposition` — always `open` here. Only a waiver makes it `waived`, and waivers are not yours to
  write

```json
{
  "framework_version": "0.0.1",
  "cycle": "20260927_0002",
  "artifact": ".sdlc/cycles/20260927_0002/intent.md",
  "interrogated_at": "2026-09-27T22:40:00Z",
  "round": 1,
  "findings": [
    {
      "id": "F_01",
      "severity": "blocking",
      "disposition": "open",
      "cites": "CON_01",
      "statement": "...",
      "locus": "...",
      "remedy": "..."
    }
  ]
}
```

## 4. Report, in this shape every time

The counts come first, then the findings, then one command to copy. Same order, same shape, every
interrogation — a reader should not have to work out where the answer is, and the shape is what lets them
skim a stack of these and see only what changed.

### When something is blocking

```
blocking: 1 · suggestions: 1

**blocking**

- `F_01` · **CON_01** — the problem, not the solution
  at `## What to build` — prescribes the expression to write and states no problem at all
  remedy: state what is wrong with Totals.Sum today; move the chosen fix into fixed decisions

**suggestions**

- `F_02` · **CON_03** — a criterion can fail
  at criterion 2 — "renders correctly" names no check

findings → `.sdlc/cycles/20260928_a43a/findings/intent.r1.json`

next → fix the blocking findings, then interrogate again:

/sdlc:interrogate 20260928_a43a intent
```

### When nothing is blocking

```
blocking: 0 · suggestions: 0

**No blocking findings — the artifact is ready to advance.**

findings → `.sdlc/cycles/20260928_a43a/findings/intent.r1.json`

next → a person accepts it by adding `accepted_by` and `accepted_at` to its frontmatter, then:

/sdlc:spec 20260928_a43a
```

### The rules that shape it

**Counts on the first line**, as `blocking: n · suggestions: n`, before anything else. Even when both are
zero — a reader scanning three of these in a row is reading that line and nothing more.

**Name the constraint and what it forbids**, never the bare id. `CON_01` alone sends someone to look it
up; `CON_01 — the problem, not the solution` does not. Keep the description to a handful of words.

**One finding, three lines**: the id and constraint, then where it is, then what would fix it. A remedy on
every blocking finding, because remediation cannot work one that does not say what would resolve it.
Suggestions get two lines — they have no remedy to act on.

**The command on its own line, alone in a fence**, so it can be copied without trimming anything. Never
wrap it in prose.

**Where the next command comes from:**

| This artifact | Nothing blocking, so next is | Something blocking, so next is |
|---|---|---|
| intent | a person accepts, then `/sdlc:spec <id>` | `/sdlc:interrogate <id> intent` |
| spec | a person accepts, then `/sdlc:plan <id>` | `/sdlc:interrogate <id> spec` |
| plan | a person approves, then `/sdlc:build <id>` | `/sdlc:interrogate <id> plan` |

Say who acts, not only what runs. "A person accepts it" is the gate; omitting it implies the next command
is yours to run, and it is not.

**Never restate the findings document in prose.** It is the record; this is a pointer to it, and the
summary exists so nobody has to open the JSON to know whether they are blocked.

### Observations are capped at one, and never read as work

Raise at most **one** thing no constraint forbids, and only if it would change what a person actually
does. Put it after the command, under a line saying plainly that it is not required and not a reason to
delay.

The cap is the point. There is always one more thing a careful reader could notice, so an uncapped list
has no terminal state — the artifact comes back sound, the observations read as a queue, the queue gets
worked, and the next run produces a fresh queue. Constraints converge, because there is a fixed number of
them and an artifact either breaches one or does not. Taste does not converge. If you have several
observations, keep the one that matters and drop the rest.

