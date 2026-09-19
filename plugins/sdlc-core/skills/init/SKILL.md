---
name: init
description: Stage 0 setup of a repository for the SDLC artifact chain - creates .sdlc/, interviews the owner for CHARTER.md (what the system is, its architectural invariants, its permanent non-goals), checks CLAUDE.md exists, and then runs the chosen stack pack's scaffold manifest to generate the solution. Run before the first /sdlc-core:intent; run again later to amend the charter or fill scaffold gaps.
argument-hint: Optional one-line description of what this solution is
disable-model-invocation: true
---

# /sdlc-core:init — Stage 0, set the repo up

It produces two things: `.sdlc/CHARTER.md`, the repo-scope invariants and permanent non-goals
that every future `intent.md` is checked against, and — by handing the chosen stack pack's
manifest to the generator — a solution on disk that builds.

This runs in the main loop because it has to interview the owner.

## 1. Check what is already here

```bash
ls -d .sdlc/ 2>/dev/null; ls */CHARTER.md */*/CHARTER.md 2>/dev/null
ls CLAUDE.md 2>/dev/null; git branch --show-current
```

The second command is the one that matters: **a charter anywhere means this repo already has a root.** Use it. Creating a second root alongside an existing one splits the chain in half, and the gate hook will then follow only one of them.

- **`.sdlc/CHARTER.md` already exists** — this repo is initialised, and there are exactly two
  things this command can still do for it. Read the charter, put both to the owner, and do the
  one they pick:
  - **amend the charter** — a deliberate act with its own approval. Do not silently rewrite a
    charter; existing intents were approved against it.
  - **fill scaffold gaps** — run the generator again for the stack pack the charter already
    commits to. It creates only what is missing and leaves every file that is already there
    untouched, so it is safe on a repo that has been worked in for months.
- **`.sdlc/` exists without a charter** — someone ran `/sdlc-core:intent` first. Fine. Carry on
  and write the charter now; then re-check the existing intents against it and report conflicts.
- **Nothing** — normal first run.

**Choose the artifact root.** It defaults to `.sdlc/`, but a repo that already keeps a visible
backlog (`intent/`, `specs/`, `docs/sdlc/`) has a convention worth keeping — ask rather than
impose one, and record the answer in the charter's `artifact_root`. Every later skill reads it
from there.

Also check the branch. If this is `main` or another protected branch, say so and ask whether to
branch first; the charter is a commit like any other.

## 2. Interview the owner

Read the codebase first if there is one — an existing README, project layout, and dependencies
answer several of these questions without asking. Ask only what the code cannot tell you, and
say what you inferred so it can be corrected.

### Establish the stack first

The stack is the one commitment every later spec depends on, so settle it before the open
questions. This framework is stack-agnostic and ships no defaults of its own — it consumes
**stack packs**.

A stack pack is a skill named `stack-<something>` that describes one opinionated stack and
declares, in a manifest it ships, everything it generates. **Read the available-skills list
already in your context** and take every skill whose name starts with `stack-`. Do not shell out
to the plugin cache: it misses packs loaded with `--plugin-dir`, assumes a cache layout that is
not a contract, and answers a question you can already see the answer to.

Then:

- **An existing stack in the repo wins outright.** Project files, a lockfile, a manifest — never
  propose something that contradicts code already there. Name the stack pack that matches it, if
  one exists.
- **Exactly one stack pack installed** → propose it. Read its skill, apply its own rules for
  choosing between any variants it offers, and say which signal you used.
- **Several installed** → ask which, with AskUserQuestion, listing what each is for.
- **None installed** → ask the owner for the stack directly: language and version, framework,
  test runner, persistence. Record the answers in the charter. Do not invent a stack, and do not
  generate a layout you have no manifest for.
- **No pack applies** → some repos are not built with a stack: a prompt library, a docs site, a
  plugin marketplace. Proposing one anyway is how a documentation repo acquires a pinned build toolchain. Say
  so, write the charter's Technology commitments by hand naming what the repo actually is, and
  generate nothing. A pack being installed is not a reason to use it.

Proposing beats interviewing when a pack exists: confirming a default costs the owner one word,
while composing a stack from nothing costs a conversation and usually lands in the same place.

### Settle the solution kind and its module boundaries

A pack's manifest declares one or more **kinds** — named shapes of repository the pack knows how
to generate, each with a description. Read the kind names and descriptions out of the manifest
(§3 says where it lives), put them to the owner with AskUserQuestion, and ask the question no
kind answers on its own: **what the modules are, and where the boundaries between them fall.**

Both answers are required and neither has a default. **If the interview ends without them, stop
and say which answer is missing** — that the kind is unsettled, or the module boundaries are, or
both. Do not pick a kind yourself to keep the command moving. The kind decides what exists on
disk and which layout the repository is stuck with afterwards, and a layout nobody chose is one
nobody will own.

### Confirm the solution identifier

One short string is repeated through everything generated here, and the owner confirms it before
any of it is written.

The generator derives a candidate from the repository directory's own name: it splits the name
on `-`, `_`, `.` and spaces, drops every character that is not a letter or a digit, capitalises
each piece, and joins them. **Present that candidate and get back a yes or a different string.**
It is worth one question because it becomes every project and assembly name in the repository,
and because renaming what is already in a repo is a permanent non-goal of this process — once
written, this string is out of reach.

If the derivation yields nothing usable — nothing left after the filtering, or a leading digit —
the generator reports it as underivable and substitutes nothing. **Ask for an explicit answer.**
Do not offer a repaired guess. A name the engine patched into validity is a name nobody chose,
sitting in every project in the repository.

### The rest of the interview

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

- *Ordered data, event streams, time series:* can any computation read a value that did not exist
  at its own timestamp? Are stored values ever restated after the fact? Is a result reproducible
  from persisted inputs alone?
- *Money, ledgers, orders:* what must never be lost, double-counted, or silently reordered?
- *Anything multi-user:* what must one user never be able to see or change?
- *Anything scheduled or retried:* what breaks if an operation runs twice?

Write each as something a reviewer could catch a violation of. "Be careful with timestamps" is
not an invariant; "no computation reads data unavailable at its own timestamp" is.

## 3. Write the charter — and nothing else

Write `<root>/CHARTER.md` per the schema in `references/artifact-schemas.md` (the `sdlc-router`
skill), `status: draft`, `version: 1`. Technology commitments records **the profile name and any
overrides**, not the style rules themselves.

**The charter is the only file this skill writes.** Every file a stack pack puts in the
repository is written by the generator in §7, from the pack's own manifest — its configuration
files included. Do not copy a pack asset by hand, do not follow a pack's prose to produce one,
and do not write a file a pack names for any reason. Two writers of one file set drift apart
with nothing to notice; worse, a file written here appears in no report, so the owner never sees
it go in.

**Where the pack's manifest lives.** Beside the pack's `SKILL.md`, at
`<plugin>/skills/<pack-name>/scaffold.json`. That is a convention every pack fills in, the same
way `SKILL.md` itself is, so finding it needs no per-pack knowledge. Keep the path — §7 hands it
to the generator.

If no stack pack applies, there is no manifest and nothing is generated. Write the charter and
stop there rather than improvising a layout.

### Do not interrogate code style

Style is enforced by whatever style and formatter config the chosen stack pack ships:
deterministic, checkable in CI, and free of any dependence on an agent remembering it. Asking the owner about brace placement spends a
gate on something a file settles, and the whole point of these gates is to spend the owner's
attention on intent instead.

If the owner raises style unprompted, translate it into a rule in that config rather than
recording it as prose anywhere.

## 4. Attack it

Dispatch **intent-interrogator** against the draft charter. It arrives with no memory of the
interview, so it catches invariants stated too vaguely to enforce and non-goals that are really
just "not this quarter".

**Pass it the artifact path and nothing else.** No summary of the conversation, no note about
which parts you think are solid, no account of why a choice was made. Its whole value is that
it was not in the room; every line of context you add hands back the reasoning it exists to be
free of, and a reader who has heard the argument has already been argued with. Relay the blocking findings in your own words — its report is not shown
to the user.

Name each invariant and non-goal by its label **and** what it forbids, and assume the
owner has never used this process. See `references/plain-language.md` (the `sdlc-router`
skill).

## 5. Check CLAUDE.md exists

Different artifact, different job: `CHARTER.md` is product intent for humans and future intents;
`CLAUDE.md` is instructions for the agent. If there is no `CLAUDE.md`, say so and suggest
Claude Code's own built-in `/init` command — not this skill — and do not write one here or
merge the two.

## 6. Gate

Get explicit approval. On approval set `status: approved` and commit:

```
git add <root>/CHARTER.md && git commit -m "charter: <solution name> v1"
```

**Nothing is generated before this point.** While the charter is still `status: draft` this
skill runs no generator action at all — not a probe, not a directory, not one file. The owner
approving the charter is what makes the rest of this command legitimate, so running it after the
approval rather than before is not a formality.

It is also not the only thing holding that ordering. **The generator reads `status:` out of the
charter file itself and refuses to run on a draft**, whatever it was told and by whom — so a
mistake in this skill's ordering fails loudly instead of quietly building out a repo whose owner
has agreed to nothing.

## 7. Generate the solution

With the charter approved and a stack pack chosen, run the generator once. It is shipped engine
tooling rather than a skill, and it is the only thing that writes the pack's files:

```
powershell -NoProfile -ExecutionPolicy Bypass -File "<sdlc-core>/scripts/Invoke-ScaffoldManifest.ps1" -Manifest <path to the pack's manifest> -Kind <kind name> -Target <repository directory> [-Solution <Identifier>]
```

Supply all four: `-Manifest` is the `scaffold.json` located in §3, `-Kind` is the kind the
interview settled, `-Target` is the repository directory being initialised, and `-Solution` is
the identifier the owner confirmed in §2. `<sdlc-core>` is this skill's own plugin root —
`${CLAUDE_PLUGIN_ROOT}`.

The generator writes its report to stdout and everything meant for a human to stderr. Read the
report: it is the record of what happened to this repository, and the owner sees it through you.

### Relay the report in full

Show the owner, in plain words and assuming no prior exposure to any of this — see
`references/plain-language.md` (the `sdlc-router` skill):

- **every entry the report marks `failed`**, each with the reason recorded beside it;
- **every path marked `mutated`** — changed in place rather than created — one line each;
- **every path marked `removed`**, one line each;
- what was `created`, and what was `skipped` because it was already there. Grouping these by
  directory is fine as long as every path is still on the screen.

**Never turn the changed and removed paths into a count.** "Four files changed" hands the owner
nothing they can act on; four paths they can open is the entire point of keeping the record at
all. Spell each path the way the report spells it.

**If the generator exits non-zero, the run halted.** Say so plainly, show the failed entry and
its reason, show what had already been written before the halt — and **do not say the repository
is scaffolded.** It is not. A halt leaves the repository part-built on purpose: once the failure
is dealt with, running this command again fills in what is missing and touches nothing that is
already there.

A refusal before any action ran is the same shape with a better outcome: nothing at all was
written, and the pack supplies the sentence saying what is missing and how to get it. Relay that
sentence as it stands, and do not install anything on the owner's machine on their behalf.

Then run the pack's own build and test commands and show the real output. A solution nobody has
built is a claim; the point of this command is to leave the owner with one they have watched
compile and pass its own tests.

## 8. Hand over

Tell them the chain is open and the next command is:

```
/sdlc-core:intent <the first thing you want to build>
```

From here on every change starts at `intent`. This command stays available and does two things
on a repo that already has a charter: amend the charter, which is a deliberate change carrying
its own approval, and fill scaffold gaps, which adds what is missing and leaves what is there
alone.
