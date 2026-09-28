---
name: verify
description: Adjudicate a built cycle against its spec and write the verify document. Use after build, after every remediation pass, and whenever a person asks whether a cycle is landable.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Bash(dotnet *)
---

# Verify cycle `$0`

Write `.sdlc/cycles/$0/verify.md`. You are the double and triple check: the diff against the spec,
the spec against what was claimed, and the evidence against every word you write.

## You write no code

Not one line. Not a test, not a fix, not a formatting change. If something is wrong you report it;
remediation fixes it.

An auditor that produces its own evidence is not an auditor, and this document's entire value is
being derived. That is also why `verify.md` is never hand-edited — a hook refuses it — and why a
correction is made by changing an input and re-deriving, never by editing the record.

## Everything derives from evidence that exists

CON_08. Read, from `.sdlc/cycles/$0/evidence/`: `build.log`, `tests.trx`, `diff.patch`. Read the
spec and the plan.

Every statement you write cites one of those. Two failures to avoid:

- **A claim with no evidence.** "The tests pass" citing nothing is a guess wearing a fact's
  clothes
- **A claim reaching past its evidence.** A green test run proves those tests passed, not that a
  criterion is met — say which test establishes which criterion, or report that none does

If an evidence file is missing, that is a finding, not a gap to fill by running the build
yourself. Running it would make you the author of what you are auditing.

## The file set is checked mechanically, and you cite the result

Run this before judging anything, from the solution's root:

```
powershell.exe -NoProfile -File "${CLAUDE_PLUGIN_ROOT}/tools/Compare-DiffToPlan.ps1" -CycleId $0
```

It reads git and the plan's `## Files` table and prints what changed but was not declared, and what was
declared but did not change. **This is the complete check on the file set**, and the reason it exists
here rather than only in a hook: a hook inspects an action, so it can only recognise ways of writing a
file that it knows about, and a heredoc that writes one inside a script is opaque to it. This reads the
result, so nothing routes around it.

Treat its output as evidence and cite it:

- **UNDECLARED** — a blocking finding citing CON_07. The change reached a file no reviewer approved
- **UNDELIVERED** — worth a finding only if a criterion needed that file. Often the plan over-promised,
  which is a suggestion, not a breach

Do not run it and then paraphrase. Quote the lines, as with every other piece of evidence.

## Judge every criterion

For each numbered criterion in the spec, decide: met, unmet, or unprovable from the evidence.
Report **every** unmet or unprovable criterion as a finding — none is too small to mention, and a
verify document that reports the worst problem and stops has hidden the rest.

Then interrogate the diff itself against the constraint set, exactly as `/sdlc:interrogate` would.
CON_07 is the one machines miss: a change inside a declared file that the plan never described.

Severity: a criterion the spec asked for and the change does not meet is `blocking`. Something
that would be better and was not asked for is a `suggestion`. Suggestions never block and are
never remediated.

## Decide the outcome

Read `.sdlc/cycles/$0/waivers.md` if it exists; a finding it covers gets `disposition: waived`.

| Outcome | When |
|---|---|
| `pass` | no blocking finding is still open |
| `blocked` | at least one open blocking finding, and fewer than three remediation passes spent |
| `escalated` | at least one open blocking finding, and three passes spent |

`passes_spent` counts consecutive remediation passes **with no change to your inputs**. If a
person changed the code, the spec, or the waivers since the last verify, reset it to zero and
record `reset_by`. In an unattended run nothing human changes, so the ceiling holds absolutely.

The frontmatter is validated against the verify schema, which enforces these combinations — a `pass` over an open blocker, or an `escalated` with budget left,
will be rejected as the contradiction it is.

## Write it

Frontmatter carries the machine-readable record; the prose below is the human-readable report, derived
from the same evidence and adding nothing.

**Copy the shape below exactly.** Every key name and nesting level is checked, and a document that
reads correctly while using the wrong names is worse than one that is obviously broken — nothing
downstream can read it, and the state machine it encodes stops being enforced. In particular: the cycle
key is `cycle`, not `id`; `passes_spent` lives inside `remediation`, not at the top level; each evidence
entry is an object with a `path`, not a bare string; and there is no `criteria` key — a criterion's
verdict belongs in the prose table, and anything it needs to block on is a finding.

```markdown
---
framework_version: 0.1.0
cycle: 20260928_8d33
spec: .sdlc/cycles/20260928_8d33/spec.md
plan: .sdlc/cycles/20260928_8d33/plan.md
verified_at: 2026-09-28T04:54:38Z
outcome: blocked
remediation:
  passes_spent: 0
evidence:
  build:
    path: .sdlc/cycles/20260928_8d33/evidence/build.log
    summary: 0 warnings, 0 errors
  tests:
    path: .sdlc/cycles/20260928_8d33/evidence/tests.trx
    summary: 14 passed, 0 failed
  diff:
    path: .sdlc/cycles/20260928_8d33/evidence/diff.patch
    sha: f060736
findings:
  - id: F_01
    severity: blocking
    disposition: open
    cites: CON_07
    statement: <one sentence saying what is wrong>
    locus: <where in the diff or the artifact>
    remedy: <what would resolve it; required on every blocking finding>
---
```

Add `waivers: <path>` only when a finding's disposition is `waived`.

```markdown
# Verify - cycle $0

## Outcome
<one line, and what it means for landing>

## Criteria
| # | Verdict | Evidence |

## Findings
<each, with its constraint, locus and remedy>

## Suggestions
<non-blocking, for a person to read>
```

## Then stop

Report the outcome and the counts, then each blocking finding in one line: the constraint **and a
short description of what it forbids**, not the bare id. A reader should not have to look up `CON_07`
to know what went wrong.

On `blocked`, say that `/sdlc:remediate $0` is next. On `escalated`, say plainly that landing will
refuse this cycle and a person owns it now.

If a finding's cause lies in the framework rather than in this cycle — adoption left something
uncommitted, a skill wrote a malformed artifact — say so in the report. None of the four exits fits
that case, and quietly proposing a plan amendment for it sends a person to fix the wrong thing.

---

# Reference

The framework version, the current UTC time, and the global constraints, injected at load time. They
are here so that what you write is written to them. **Do not try to read them from disk** — a session
working in a governed solution is sandboxed to that solution and the attempt is denied, which wastes a
turn and looks like a failure to anyone watching.

Interrogation is still the gate. Writing to the constraints does not mean judging your own work
against them.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`
