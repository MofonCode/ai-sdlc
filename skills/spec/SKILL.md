---
name: spec
description: Turn an approved intent into a spec with verifiable acceptance criteria, interrogate it, and resolve what only the user can resolve. Use after an intent exists and before planning.
argument-hint: [cycle-id]
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Write the spec for cycle $0

Turn `.sdlc/intents/$0.md` into `.sdlc/cycles/$0/spec.md`.

A spec's whole job is to state, in criteria a test could check, what the intent asked for.
Generation is yours. **Resolving ambiguity is not** — see below.

## Read first

Read the intent, then `.sdlc/CHARTER.md`. Then gather facts, because a spec that asserts
something about the repository which no check verified is an unfounded claim (INV-07):

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact ".sdlc/intents/$0.md" -Charter ".sdlc/CHARTER.md"
```

## Acceptance criteria — the format is parsed, not decorative

The fact gatherer reads this table, so the shape matters:

```markdown
## Acceptance criteria

| ID | Verifiable | Criterion |
|---|---|---|
| AC-$0-1 | yes | Stated so that a test could prove it. |
| AC-$0-2 | no | Stated so that only a person could judge it. |
```

- **Ids are `AC-<cycle-id>-<n>` and must be unique.** Two criteria sharing an id make
  traceability report one as covered when the other is not, which is why the gatherer checks
  for duplicates.
- **`Verifiable` is `yes` or `no`, and it is a commitment.** Every `yes` obliges a test at
  build time, and verification blocks on a `yes` with no test proving it. Marking something
  `yes` that no test could check is the `untestable` finding.
- Prefer `no` over a dishonest `yes`. A criterion a person must judge is a legitimate
  criterion; pretending it is mechanical is not.

Also state what is **out of scope**, carried from the intent. A stated exclusion prevents
scope drift far more cheaply than a finding does.

## Frontmatter

Must validate against `schemas/spec.schema.json` — read it rather than trusting this:

```yaml
---
cycle-id: $0
intent: $0
charter-version: <from the charter frontmatter>
---
```

## Interrogate it, then stop where you must

```
/sdlc:interrogate .sdlc/cycles/$0/spec.md
```

Then act on what comes back, by state:

- **`suggested`** — accumulates as evidence. Do not halt for it.
- **`blocking` with a repair you can apply** — apply it, then re-interrogate. That is
  `/sdlc:repair`'s job if there are several.
- **`blocking` or `open` that only the user can settle — ASK THE USER.**

That last case is the reason this stage is a skill and not a workflow. An `ambiguity` finding
says a statement admits two readings leading to materially different work. When the two
readings differ in what the *user wanted*, no amount of reasoning settles it — only they
know. A `missing-case` finding about a failure mode nobody has decided on is the same.

**Do not pick a reading to keep things moving.** An ambiguity resolved silently by an agent
is a decision nobody made, and it propagates into the plan, the code and the tests. Ask,
name both readings, and say what each would cost.

## When you are done

Show the user the criteria and the interrogation result. Then:

> The next step is `/sdlc:plan $0`, which you drive.

Do not plan it yourself. The gate between spec and plan is a person.
