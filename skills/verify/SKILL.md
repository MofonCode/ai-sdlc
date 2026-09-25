---
name: verify
description: Assemble verify.md from generated evidence and decide the cycle's outcome. Use after build, including inside an unattended batch run.
argument-hint: [cycle-id]
allowed-tools: Bash(powershell.exe *) Bash(dotnet *) Bash(git *)
---

# Verify cycle $0

Assemble `.sdlc/cycles/$0/verify.md` and decide the outcome.

**Everything in a verify document is derived.** Traceability comes from test annotations,
adherence from the diff against the plan's declaration, gate results from the build and the
suppression scan. Nothing here is written by hand — a hand-maintained traceability table
beside the real tests drifts the moment anything is refactored, and nothing detects it
(INV-03).

So: **do not compute any of these numbers yourself, and do not estimate one you could not
obtain.** A number you invented is an unfounded claim (INV-07), and it is worse than a
missing one because it looks like evidence.

## Gather the evidence

Run the gatherer twice — once against the spec, which carries traceability and
suppressions, once against the plan, which carries adherence and both tiers:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact ".sdlc/cycles/$0/spec.md" -Charter ".sdlc/CHARTER.md"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact ".sdlc/cycles/$0/plan.md" -Charter ".sdlc/CHARTER.md"
```

Then run the gates: `dotnet build` and `dotnet test`.

From that output take the criteria counts, which criteria have no test and which
annotations point at no criterion, the suppression count and any unjustified ones, the
undeclared files, and both tiers.

## Decide the outcome — by rule, not by judgment

Apply these in order. The first that matches wins:

| Condition | Outcome |
|---|---|
| The actual tier exceeds the predicted tier | `halted-tier-escalated` |
| The build fails, or any test fails | `blocked` |
| Any verifiable criterion has no test proving it | `blocked` |
| Any suppression lacks a written justification | `blocked` |
| Any finding is still `open` or `blocking` and undispositioned | `blocked` |
| Otherwise | `passed` |

**`passed` requires zero undispositioned findings.** That is not a convention — the schema
enforces it, so a verify document claiming otherwise will not validate.

A gap between a derived artifact and its source is **corrected, never signed off**.

## Write it

Write `.sdlc/cycles/$0/verify.md`. The frontmatter must validate against
`schemas/verify.schema.json` — read it rather than trusting this list:

```yaml
---
cycle-id: $0
plan: $0
charter-version: <from the charter frontmatter>
outcome: <from the table above>
predicted-tier: <n>
actual-tier: <n>
suppression-count: <n>
criteria-total: <n>
criteria-verifiable: <n>
criteria-covered: <n>
undispositioned: <n>
---
```

In the body put the traceability table (criterion → the test proving it), the adherence
result, the gate results, and every finding with its disposition. Each of those is
transcribed from the evidence above, not composed.

## Report

State the outcome and what drove it. Then stop:

> This cycle is now `awaiting-landing`. Landing is serialized and human-gated —
> `tools/Complete-SdlcCycle.ps1` rebases, re-verifies and squash-merges.

**Do not land it.** Do not merge, do not move any ref, and do not write a retro. The retro
is written at landing, and it is the completion certificate — writing one for a cycle that
has not landed would make a dead cycle read as done.
