---
name: build
description: Implement an approved plan and heal what does not build or pass. Use after a person has approved a plan, including inside an unattended batch.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Bash(dotnet *)
---

# Build cycle `$0`

Implement `.sdlc/cycles/$0/plan.md`. Nothing leaves this phase that does not compile and pass its
tests.

## Refuse unless the plan is approved

Read the plan. Stop if it does not exist, if its interrogation left blocking findings, or if no
person has approved it. A hook also refuses source edits without an approved plan, so proceeding
without one wastes the attempt.

## Build only what the plan declared

Implement the declared changes in the declared order. Two rules, and CON_07 covers the second:

- **A file the plan did not name is not yours to touch.** If the work genuinely needs one, stop,
  say which file and why, and let the plan be amended and re-approved. That is a short interruption;
  an undeclared file is a blocked build
- **Inside a declared file, change only what the plan described.** No unrelated refactor, no
  drive-by rename, no fix to something else you noticed. The file set is mechanical and a hook
  catches new files; creep inside an approved file passes that hook and reaches review dressed as
  approved work, which is where nobody questions it. Note what you noticed and leave it

## Heal your own failures

You own making the tree work. If it does not compile, or a test fails, fix it — that is this
phase's job, not verify's, and verify exists to audit evidence rather than produce it.

Two things the healing may not do:

- **Never weaken a test to make it pass.** If a test is genuinely wrong, that is a finding for a
  person, not an edit. A hook refuses test edits during a build for this reason
- **Never suppress a warning to clear the build.** The repository treats warnings as errors
  deliberately. A suppression needs a comment saying why, and that is a plan-level decision

Keep going until the build is clean and every test passes, or until you are certain you cannot get
there — in which case stop and say exactly what fails and what you tried.

## Record the evidence

Verify derives every claim from evidence that already exists, so this phase produces it. Write
into `.sdlc/cycles/$0/evidence/`:

| File | Content |
|---|---|
| `build.log` | the full build output of the final, clean build |
| `tests.trx` | the test run results of the final, passing run |
| `diff.patch` | `git diff <base>` for the cycle's branch |

These are the inputs to verify. A missing file is not a formatting problem — it makes the
corresponding verify claim unprovable, and verify will report that rather than assume.

## Then stop

**Do not invoke another skill.** A skill cannot reliably call another one, and a chain that only
sometimes fires is worse than none: it produces artifacts nobody challenged while appearing to have
challenged them. Report what you produced and name the next command for whoever is driving — a person
in an interactive session, or the batch runner in an unattended one.

Report: what you changed, what the final build and test run said, and anything you
noticed and deliberately left alone. Then name `/sdlc:verify $0` as the next step. Do not open a pull
request, and do not verify your own work — an auditor that produced the evidence is not an auditor.

---

# Reference

The framework version, the current UTC time, and the global constraints, injected at load time. They
are here so that what you write is written to them. **Do not try to read them from disk** — a session
working in a governed solution is sandboxed to that solution and the attempt is denied, which wastes a
turn and looks like a failure to anyone watching.

Interrogation is still the gate. Writing to the constraints does not mean judging your own work
against them.

!`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "${CLAUDE_PLUGIN_ROOT}/tools/Get-Constraints.ps1"`
