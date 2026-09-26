---
name: plan
description: Turn a spec into a plan that declares the files it will touch, carries a blast-radius tier, and is approved by a person before any build. Use after a spec exists.
argument-hint: "[cycle-id]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Write the plan for cycle $0

Turn `.sdlc/cycles/$0/spec.md` into `.sdlc/cycles/$0/plan.md`.

This is the **last artifact before anything becomes irreversible**, and the only one a person
approves in writing. Everything downstream is checked against it.

## Declare the files — this is the invariant, not a nicety

```markdown
## Files

| Path | Change |
|---|---|
| src/Thing.cs | modify |
| tests/ThingTests.cs | new |
```

`Change` is `new`, `modify` or `delete`. The gatherer parses this table.

INV-05 forbids a plan that does not enumerate the files it will touch. The declaration does
two jobs: it makes scope drift mechanically detectable instead of a judgment, and it is what
the blast-radius tier is computed from. The same declaration serves both, which is why one
invariant covers it.

Declare what you genuinely expect to touch. Under-declaring produces a scope-drift finding at
build time; over-declaring inflates the tier and may cost an approval you did not need.

## The tier is computed, never chosen

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact ".sdlc/cycles/$0/plan.md" -Charter ".sdlc/CHARTER.md"
```

Read `blast-radius` and take `predicted-tier` from it verbatim.

**Do not compute or adjust the tier yourself.** It is derived from the declared paths by the
same function that will later compute the *actual* tier from the real diff — that is what
makes the two comparable, and comparing them is how a build that turned out more dangerous
than approved gets caught.

The selection rule behind it, so you understand what it is measuring: **a change whose
failure cannot be corrected by reverting the commit.** Migrations, auth, published contracts.
Not "how big is this."

If the tier surprises you, say so to the user rather than reshaping the plan to lower it.
Gaming the tier is a gate bypass.

## Frontmatter

Must validate against `schemas/plan.schema.json` — read it rather than trusting this:

```yaml
---
cycle-id: $0
spec: $0
charter-version: <from the charter frontmatter>
predicted-tier: <from the gatherer>
---
```

Do **not** write `approved:` yourself. That field is the gate, it is the user's to give, and
writing it on their behalf is the one bypass that matters here.

## Interrogate it, then stop where you must

```
/sdlc:interrogate .sdlc/cycles/$0/plan.md
```

**File what comes back**, or nothing downstream can see it. Write the returned JSON to
`.sdlc/cycles/$0/interrogation.json` — gitignored and regenerable — and file it:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Add-SdlcFindings.ps1 -CycleId $0 -Path ".sdlc/cycles/$0/interrogation.json"
```

It validates the document first and refuses an invalid one rather than filing it with a warning.
A refusal is the guard working; fix what produced the document, not the document.

Apply repairs you can. **Where a finding turns on something only the user can settle — which
approach, which trade-off, whether a file really needs touching — ask them.** Do not pick to
keep moving; a silently resolved ambiguity becomes code nobody chose.

Record what was done about each one, because verification will not pass a cycle carrying a
blocking finding with no disposition:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Set-SdlcDisposition.ps1 -CycleId $0 -Finding <id> -Disposition fixed
```

## Then hand it over and stop

Show the user the declared files, the tier, and the interrogation result. Then ask for
approval explicitly:

> This plan declares N files at tier T. Approving it adds `approved: <date>` to the
> frontmatter, which makes the cycle eligible for an unattended build.

**You do not approve it.** When the user approves, add the `approved:` field and confirm the
cycle is now `planned`. Until then it stays `queued` and no batch will touch it.

**Above tier 1, say plainly what the approval is covering.** The tier is the answer to "how much
damage can this do if it is wrong", and at tier 3 it means a change no revert undoes — a
migration, an auth path, a published contract. Approving the plan **is** the explicit human
sign-off that tier asks for, so nothing downstream will ask again: the build proceeds unattended
on the strength of this decision.

What still halts without you is the one thing this approval cannot cover — the finished diff
turning out to be a higher tier than the plan declared. That escalates rather than merging.
