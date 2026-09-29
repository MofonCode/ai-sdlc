---
name: build
description: Implement an approved plan and heal what does not build or pass. Use after a person has approved a plan, including inside an unattended batch.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *) Bash(dotnet *)
---

# Build cycle `$0`

Implement `.sdlc/cycles/$0/plan.md`. Nothing leaves this phase that does not compile and pass its
tests.

## Refuse unless this cycle is ready to build

Run this first, from the solution's root, and do not proceed on a `NOT READY`:

```
powershell.exe -NoProfile -File "${CLAUDE_PLUGIN_ROOT}/tools/Assert-BuildReady.ps1" -CycleId $0
```

It checks the three things that have to hold before a single line is written: you are on `cycle/$0`,
a person has approved the plan, and nothing is uncommitted outside `.sdlc/`. Quote its output and stop
if it refuses — every item it names is a decision for a person, not something to work around.

Read the plan too, and stop if its interrogation left blocking findings. That one is a judgement the
script cannot make.

## Pin the base before you touch source

Everything verify later claims is measured from one commit. Commit the cycle's record first, then write
that commit's sha where verify will read it:

```
git add .sdlc
```

```
git commit -m "record artifacts for cycle $0"
```

```
powershell.exe -NoProfile -File "${CLAUDE_PLUGIN_ROOT}/tools/Set-CycleBase.ps1" -CycleId $0
```

The intent, spec, plan and findings are the cycle's record, not part of the change under review, which
is why they go in before the base rather than after.

**After this, commit your source changes as often as you like.** The base is pinned, so the comparison
spans every commit you make. It was not always so: the comparison used to take its base as `HEAD` at the
moment it ran, and one ordinary `git commit` during a build moved the base on top of the change, emptied
the diff, and reported success with an undeclared file sitting in it. A recorded base cannot do that.

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

Verify derives every claim from evidence that already exists, so this phase produces it. Write into
`.sdlc/cycles/$0/evidence/`:

| File | Content |
|---|---|
| `build.log` | the full output of the final, clean build |
| `tests.trx` | the results of the final, passing test run |
| `diff.patch` | the change, taken against the base pinned above |
| `base.sha` | written already, when you pinned the base |

Take the patch against the base that `Set-CycleBase.ps1` reported, so it covers every commit you made:

```
git diff <base>
```

**The diff must say what it is a diff against.** Put that base in the patch as a first-line comment.
Verify reports a claim as unprovable when it cannot tell whether content came from this cycle or
predates it, and a patch with no stated base is exactly that situation.

These are the inputs to verify. A missing file is not a formatting problem — it makes the corresponding
verify claim unprovable, and verify will report that rather than assume.

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
