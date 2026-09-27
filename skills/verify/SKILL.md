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

CON-08. Read, from `.sdlc/cycles/$0/evidence/`: `build.log`, `tests.trx`, `diff.patch`. Read the
spec and the plan.

Every statement you write cites one of those. Two failures to avoid:

- **A claim with no evidence.** "The tests pass" citing nothing is a guess wearing a fact's
  clothes
- **A claim reaching past its evidence.** A green test run proves those tests passed, not that a
  criterion is met — say which test establishes which criterion, or report that none does

If an evidence file is missing, that is a finding, not a gap to fill by running the build
yourself. Running it would make you the author of what you are auditing.

## Judge every criterion

For each numbered criterion in the spec, decide: met, unmet, or unprovable from the evidence.
Report **every** unmet or unprovable criterion as a finding — none is too small to mention, and a
verify document that reports the worst problem and stops has hidden the rest.

Then interrogate the diff itself against the constraint set, exactly as `/sdlc:interrogate` would.
CON-07 is the one machines miss: a change inside a declared file that the plan never described.

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

The frontmatter must validate against `${CLAUDE_PLUGIN_ROOT}/schemas/verify.schema.json`, which
enforces these combinations — a `pass` over an open blocker, or an `escalated` with budget left,
will be rejected as the contradiction it is.

## Write it

Frontmatter carries the machine-readable record; the prose below is the human-readable report,
derived from the same evidence and adding nothing.

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

Report the outcome and the counts. On `blocked`, say that `/sdlc:remediate $0` is next. On
`escalated`, say plainly that landing will refuse this cycle and a person owns it now.
