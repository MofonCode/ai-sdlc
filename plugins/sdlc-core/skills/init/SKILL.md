---
name: init
description: One-time setup of a repository for the SDLC artifact chain - creates .sdlc/, interviews the owner for CHARTER.md (what the system is, its architectural invariants, its permanent non-goals), and checks CLAUDE.md exists. Run once per repo, before the first /sdlc-core:intent. Stage 0.
argument-hint: Optional one-line description of what this solution is
disable-model-invocation: true
---

# /sdlc-core:init — Stage 0, set the repo up

Run **once per repository**, before the first intent. It produces `.sdlc/CHARTER.md`: the
repo-scope invariants and permanent non-goals that every future `intent.md` is checked against.

This runs in the main loop because it has to interview the owner.

## 1. Check what is already here

```bash
ls -la .sdlc/ 2>/dev/null; ls CLAUDE.md 2>/dev/null; git branch --show-current
```

- **`.sdlc/CHARTER.md` already exists** — this repo is initialised. Read it and offer to
  *amend* it instead, which is a deliberate act with its own approval. Do not silently rewrite
  a charter; existing intents were approved against it.
- **`.sdlc/` exists without a charter** — someone ran `/sdlc-core:intent` first. Fine. Carry on
  and write the charter now; then re-check the existing intents against it and report conflicts.
- **Nothing** — normal first run.

Also check the branch. If this is `main` or another protected branch, say so and ask whether to
branch first; the charter is a commit like any other.

## 2. Interview the owner

Read the codebase first if there is one — an existing README, project layout, and dependencies
answer several of these questions without asking. Ask only what the code cannot tell you, and
say what you inferred so it can be corrected.

### Propose a stack profile first

Read `references/stack-profiles.md` and propose one before asking anything open-ended. An
existing project file in the repo wins outright; competition markers mean `python-ml`;
otherwise `dotnet`. Say which signal you used, and take an override without argument.

Proposing beats interviewing here: confirming a default costs the owner one word, while
composing a stack from nothing costs a conversation and usually lands in the same place.

Use AskUserQuestion for choices, prose for open questions, and batch related ones. What the
charter cannot ship without:

- **What this is** — three sentences, for someone who has never seen it.
- **Who it is for** — the actual users and the job they hire it for.
- **Architectural invariants** — the properties that must hold across *every* change. This is
  the load-bearing section and the one owners under-specify, so push hardest here. Ask what
  would make a change obviously wrong regardless of what it was trying to do.
- **Permanent non-goals** — what this system will never be. A later intent landing here is
  blocked outright, not negotiated, so be sure the owner means *never* and not *not yet*.
- **Technology commitments** — decisions already made and not up for relitigation per feature,
  each with a one-line reason. Capture language and version, framework, test runner, and
  persistence at minimum: `spec-writer` reads this section before the codebase, so whatever is
  missing here is something every future spec has to re-decide or ask about. Where conventions
  span several of the owner's projects, name the skill that carries them rather than restating
  them.

  On a **greenfield** repo this section is the whole point of running init early — there is no
  codebase to infer a stack from, so ask directly rather than letting the first spec guess.

### Drawing out invariants

Owners answer "what are your invariants?" with silence and "no lookahead bias in any signal
path" with certainty — so ask the concrete version. Useful probes, chosen by domain:

- *Ordered data, market data, backtests:* can any computation read a value that did not exist
  at its own timestamp? Are stored values ever restated after the fact? Is a result reproducible
  from persisted inputs alone?
- *Money, ledgers, orders:* what must never be lost, double-counted, or silently reordered?
- *Anything multi-user:* what must one user never be able to see or change?
- *Anything scheduled or retried:* what breaks if an operation runs twice?

Write each as something a reviewer could catch a violation of. "Be careful with timestamps" is
not an invariant; "no computation reads data unavailable at its own timestamp" is.

## 3. Write it

Write `.sdlc/CHARTER.md` per the schema in `references/artifact-schemas.md` (the `sdlc-router`
skill), `status: draft`, `version: 1`. Technology commitments records **the profile name and any
overrides**, not the style rules themselves.

Then write the profile's `.editorconfig` from `assets/` if the repo does not already have one:

```bash
[ -f .editorconfig ] || cp "${CLAUDE_SKILL_DIR}/assets/dotnet.editorconfig" .editorconfig
```

**Never overwrite an existing `.editorconfig`** — a repo that has one has already made this
decision, and silently replacing it changes every file on the next format run. If one exists,
say so and leave it.

### Do not interrogate code style

Style is enforced by `.editorconfig` and the formatter: deterministic, checkable in CI, and free
of any dependence on an agent remembering it. Asking the owner about brace placement spends a
gate on something a file settles, and the whole point of these gates is to spend the owner's
attention on intent instead.

If the owner raises style unprompted, translate it into an `.editorconfig` rule rather than
recording it as prose anywhere.

## 4. Attack it

Dispatch **intent-interrogator** against the draft charter. It arrives with no memory of the
interview, so it catches invariants stated too vaguely to enforce and non-goals that are really
just "not this quarter". Relay the blocking findings in your own words — its report is not shown
to the user.

## 5. Check CLAUDE.md exists

Different artifact, different job: `CHARTER.md` is product intent for humans and future intents;
`CLAUDE.md` is instructions for the agent. If there is no `CLAUDE.md`, say so and suggest `/init`
— do not write one here, and do not merge the two.

## 6. Gate

Get explicit approval. On approval set `status: approved` and commit:

```
git add .sdlc/CHARTER.md && git commit -m "charter: <solution name> v1"
```

Then tell them the chain is open and the next command is:

```
/sdlc-core:intent <the first thing you want to build>
```

Say plainly that `init` is never run again for this repo — from here on every change starts at
`intent`, and the charter only changes through a deliberate amendment.
