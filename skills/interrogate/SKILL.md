---
name: interrogate
description: Challenge one artifact against the effective constraint set and write the findings document. Use before an intent, spec or plan advances, and whenever a person asks whether an artifact would survive review.
argument-hint: "[path to the artifact]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Write
---

# Interrogate `$0`

Challenge the artifact at **$0** against every constraint that applies to it, and **write a findings
document to disk**. You are an adversary, not an editor: you do not fix the artifact, and you do not
merely describe what you found.

## The constraint set

Injected below at load time, and the only copy. Do not read it from disk and do not invoke another
skill to fetch it: a session working in a governed solution is sandboxed to that solution and cannot
reach the plugin root. If you find yourself asking for directory access you have taken a path that
does not exist.

Also read `.sdlc/constraints.md` for this solution's own `LOC-nn`. It is often absent; that is normal
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
  the document and mention it in your closing summary as an observation. Do not manufacture an ID
- **Report nothing when nothing is wrong.** An empty findings array is legitimate and common.
  Over-reporting is as damaging as under-reporting: it teaches everyone to skim
- **Severity is not a feeling.** `blocking` means the artifact must not advance as written.
  `suggestion` means it may advance and would be better changed

## 3. Work out the path, then write the file

This step is the deliverable. **Printing the document into your reply is not doing it** — nothing
downstream reads your reply, and remediation, verify and landing all read the file.

| Artifact | Path |
|---|---|
| `.sdlc/intents/<id>.md` | `.sdlc/intents/findings/<id>.r<round>.json` |
| `.sdlc/cycles/<id>/spec.md` | `.sdlc/cycles/<id>/findings/spec.r<round>.json` |
| `.sdlc/cycles/<id>/plan.md` | `.sdlc/cycles/<id>/findings/plan.r<round>.json` |

`<round>` starts at 1 and increments: list the target directory first and take one higher than the
highest already there. Never overwrite an earlier round — the sequence is the record of whether a
repair worked. Create the directory if it does not exist.

So interrogating `.sdlc/intents/20260927-0002.md` with no earlier rounds writes
`.sdlc/intents/findings/20260927-0002.r1.json`.

Every field is required for a reason:

- `framework_version` — the `Framework version` stated with the injected constraints above
- `cycle` — the artifact's id, `YYYYMMDD-xxxx`
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
  "cycle": "20260927-0002",
  "artifact": ".sdlc/intents/20260927-0002.md",
  "interrogated_at": "2026-09-27T22:40:00Z",
  "round": 1,
  "findings": [
    {
      "id": "F-01",
      "severity": "blocking",
      "disposition": "open",
      "cites": "CON-01",
      "statement": "...",
      "locus": "...",
      "remedy": "..."
    }
  ]
}
```

## 4. Report

Confirm the path you wrote, then the count at each severity, then each blocking finding as one line
naming its constraint and its locus. Do not restate the document in prose — it is the record, and
your summary is a pointer to it.

If the artifact is sound, say so plainly in one line, having still written the document with an empty
findings array. Resist the urge to add advice.
