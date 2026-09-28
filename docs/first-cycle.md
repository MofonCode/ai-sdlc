---
covers:
  - "skills/**/SKILL.md"
  - "hooks/hooks.json"
---

# Running your first cycle

A walkthrough for taking one real change from idea to pull request in a repository that has adopted
the framework. Follow it in order; every step names what it produces and who has to act.

**Verified as of `0.1.0`:** `interrogate`, `intent`, `spec`, `plan`, `constraints` — steps 3 to 8.
Step 2 and steps 9 onward use skills that have not been run end to end yet, and each is marked where
it appears. Expect friction there, and note where it happens: those notes are worth more than the
artifacts.

---

## What to run it on

Two cycles in one empty repository. Together they cover both paths anyone will actually take — adopting
into nothing, and changing something that exists — and neither covers the other.

**Cycle 1, greenfield.** `type: solution`. *"There is no way to see a die roll; we want a web page that
shows one number from 1 to 6."* This is the only thing that exercises `init` from empty, the `solution`
intent type, and a plan whose files are all new.

**Cycle 2, a change to what cycle 1 built.** *"One die is not enough; the page should show two."* Now
there is existing code with a caller and a rendered output, which is what brings the rest alive: CON-07,
the declared-file hook, a plan trying to prove existing behaviour did not move, a tier that is not
automatically `local`, and escalation.

Cycle 2 is the more informative of the two, and it needs cycle 1 to have happened. It also tests
something no fixture can: cycle 2's substrate was produced *by the process*, so a vague spec in cycle 1
will be felt in cycle 2. That is the only honest test of whether the chain compounds.

**Scope cycle 1 hard.** The natural failure of a greenfield intent is sprawl — the interview will
cheerfully help you design a board-game platform. Keep saying "not in this cycle." If the spec comes
back with eight criteria, that is worth reporting.

### Why a die roll is a good first subject

It puts pressure on exactly the two places this process is supposed to earn its keep.

**Randomness resists a falsifiable criterion.** "Shows a random number" cannot fail, and CON-03 forbids
a criterion that cannot. A sound spec has to land on something checkable — the value is within 1 to 6,
every face appears across N rolls, or the roll comes from a seam a test can seed. Which one it picks is
the most interesting thing cycle 1 will tell you. If it writes "displays a random number" and
interrogation passes it, that is a finding about CON-03 rather than about you.

**A page is harder to prove than a class.** Asserting on a rendered component needs a component test
harness, and the honest shape is the roll living in a testable unit with the page as a thin caller. A
plan that puts the logic in the page and then claims a unit test proves it has written an inadequate
proof, which is CON-06's to catch. Expect cycle 1 to spend real effort on test infrastructure; that is
the framework forcing a testable design rather than getting in the way.

**State the range in the intent.** If you say "1 to 6", the spec inherits a fact. If you leave it out
and the spec invents it, CON-02 should have caught it at the intent — so mentioning it is the kinder
test, and omitting it deliberately is the sharper one. Pick which experiment you want.

**Cycle 2 has a scope question worth leaving open.** Two numbers *instead of* one, or a second added
alongside? And is the page's existing output a must-not-change? Do not resolve either in the opening
sentence. Watch whether the interview asks, and whether the spec writes a criterion for the part you
did not mention.

## Before you start: the repository

The framework needs one, and `init` refuses without it. Not bureaucracy — `build` records the diff that
is the only evidence `verify` derives its claims from, and every gate keys on a `cycle/<id>` branch that
cannot exist otherwise. A governed directory with no repository permits everything while still producing
artifacts that look governed.

An empty repository is not enough either; there has to be a commit for a sha to point at.

```pwsh
mkdir C:\Repos\dice-trial; cd C:\Repos\dice-trial
git init
git commit --allow-empty -m "before adoption"
```

## 1. Install the plugin

```pwsh
cd C:\Repos\ai-sdlc
claude plugin marketplace add ./          # note the ./ — a bare . is rejected
claude plugin install sdlc@ai-sdlc
claude plugin details sdlc@ai-sdlc        # expect: Skills (12), Hooks (1) PreToolUse
```

The marketplace is a **directory** source pointing at `C:\Repos\ai-sdlc`, and an install caches by
version. So while you are testing, changes there will not reach you unless you reinstall:

```pwsh
claude plugin uninstall sdlc@ai-sdlc; claude plugin install sdlc@ai-sdlc
```

`claude plugin update` will tell you it is already at the latest version and do nothing. That is the
caching, not a bug.

## 2. Adopt the framework

In your trial repository, in an interactive session:

```
/sdlc:init
```

*Not yet verified.* It should create `.sdlc/solution.md` with your stack and origin, plus empty
`intents/` and `cycles/` directories, and leave an existing `AGENTS.md` alone.

Adoption is free — your existing code is not judged. Constraints govern each change as it is made,
never code that predates them, so there is nothing to baseline and nothing to clean up first.

## 3. Write the intent

```
/sdlc:intent We have no way to see a die roll. I want a web page that shows one number from 1 to 6, rerolled each time the page is asked for.
```

Describe **the problem**, not the fix. If you name a fix, the skill will ask what its absence causes
— that is the conversion working, not the skill being difficult. A decision you have already taken
is welcome; it gets recorded as a fixed decision with your name on it, which keeps the intent a
problem statement while letting the spec honour the choice.

In an interactive session it will interview you. Answer what you know and say so when you do not:
unknowns become **Open** questions rather than invented answers, and an intent with three open
questions is sound. Three invented ones are a liability, because every later phase treats an
accepted intent as settled.

It writes `.sdlc/intents/<id>.md`, where `<id>` is `YYYYMMDD-xxxx`. **Note that id — every command
after this takes it.**

## 4. Put the cycle on a branch

```pwsh
git checkout -b cycle/20260928-a1b2      # your id
```

The branch name matters: the hooks key on `cycle/<id>` and stay silent off it. Branching now means
the intent, spec, plan and verify document all travel with the change, so the eventual pull request
carries the whole chain. If the intent is rejected, delete the branch.

## 5. Interrogate the intent

```
/sdlc:interrogate .sdlc/intents/20260928-a1b2.md
```

This writes `.sdlc/intents/findings/20260928-a1b2.r1.json` and reports what it found. Read the
findings, not just the count.

Blocking findings mean the intent must change before it advances. Fix the intent and run
interrogation again — it writes `r2`, and keeps `r1`, because the sequence is the record of whether
your repair worked.

## 6. Accept the intent

Nothing accepts an artifact on your behalf. Add both fields to the intent's frontmatter yourself:

```yaml
---
id: 20260928-a1b2
type: bug
accepted_by: Your Name
accepted_at: 2026-09-28T09:00:00Z
---
```

Both, or neither: a name with no date says nothing about which version you accepted. This is the
first of three gates, and the cheapest — you are agreeing that this is the right problem, before a
line of anything else exists.

## 7. Spec, interrogate, accept

```
/sdlc:spec 20260928-a1b2
/sdlc:interrogate .sdlc/cycles/20260928-a1b2/spec.md
```

The spec turns the intent into numbered acceptance criteria, each stating an observable outcome and
how it is checked. It will refuse if the intent is not accepted.

Read the criteria properly before accepting. This is the artifact everything downstream is measured
against, and its numbering is an interface — the plan and the verify document both cite criteria by
number, so a criterion must never be renumbered once accepted.

Accept it the same way, with `accepted_by` and `accepted_at` in its frontmatter.

## 8. Plan, interrogate, approve

```
/sdlc:plan 20260928-a1b2
/sdlc:interrogate .sdlc/cycles/20260928-a1b2/plan.md
```

The plan declares every file it will touch, a blast-radius tier, and how each criterion will be
proven. Read it as the thing you would read to decide whether to let the change happen — because
that is what it is.

Two parts repay real attention:

- **The file list.** A hook refuses any edit to a file the plan did not declare, so an incomplete
  list stops the build rather than causing a surprise later
- **The proofs.** Ask of each one: *could this check pass while its criterion is unmet?* If yes, it
  is inadequate, and an inadequate proof is worse than none because it produces a green
  verification. This is the question the process is least able to answer for you

Then approve it:

```yaml
---
id: 20260928-a1b2
spec: .sdlc/cycles/20260928-a1b2/spec.md
tier: local
approved_by: Your Name
approved_at: 2026-09-28T09:30:00Z
---
```

**This is the gate that matters.** Everything after it is machine-decided until landing, and a hook
refuses every source edit until those two fields exist.

## 9. Build

```
/sdlc:build 20260928-a1b2
```

*Not yet verified.* It implements the plan and heals its own build and test failures — nothing
should leave this step that does not compile and pass.

It must also write three files to `.sdlc/cycles/<id>/evidence/`: `build.log`, `tests.trx` and
`diff.patch`. If any is missing, the next step will report the matching claims as unprovable. That
is the design working, and it will look like a failure.

## 10. Verify

```
/sdlc:verify 20260928-a1b2
```

*Not yet verified.* It writes `.sdlc/cycles/<id>/verify.md` and decides an outcome. It writes no
code — it reads the diff against the spec and reports every criterion that is unmet or unprovable.

| Outcome | What happens next |
|---|---|
| `pass` | step 12 |
| `blocked` | step 11 |
| `escalated` | three remediation passes are spent; it is yours now |

**Never hand-edit `verify.md`.** A hook refuses a targeted edit, and the reason is the whole audit
story: the document's value is that it was derived. To change it, change an input — the code, the
spec, or a waiver — and run verify again so a fresh document supersedes it.

## 11. Remediate, then verify again

```
/sdlc:remediate 20260928-a1b2
/sdlc:verify 20260928-a1b2
```

*Not yet verified.* Remediation works the open blocking findings and nothing else — suggestions are
yours to read, not its to act on. Then verify runs again, because nothing but a fresh verification
can tell whether a fix worked.

Three passes, then `escalated`. The counter resets only when **you** change an input, so an
unattended run cannot talk its way into a fourth attempt.

If you land on `escalated`, there are four ways out and all of them are yours: fix it by hand, amend
the spec and re-approve it, waive the finding with a recorded reason, or abandon the cycle.

## 12. Land

```
/sdlc:land 20260928-a1b2
```

*Not yet verified.* Needs `gh` authenticated and a GitHub remote. It opens a pull request whose body
is assembled from the artifacts, and it refuses anything but `pass` — a hook refuses it too.

It does not merge. A code owner does, and never the agent that wrote the change.

---

## Forcing the unhappy path

Do this as a second cycle. It is the more valuable of the two, because escalation is what makes
unattended runs trustworthy and it is the half nobody demonstrates.

The cleanest way in is to **approve a plan that interrogation flagged**. Write a spec whose criteria
include something the plan does not cover — say a criterion about removing the duplicated guards at
the call sites — then let the plan declare only the central file. Interrogation will raise CON-06,
because no proof covers that criterion. Approve it anyway.

What should follow: build satisfies what it can, verify finds the uncovered criterion unmet, and
remediation cannot fix it because the hook refuses to touch a file the plan never declared. Three
passes, then `escalated`, and landing refuses with the count attached.

That chain exercises the declared-file hook, the remediation ceiling, the terminal state and the
landing gate in one run. If any link behaves differently, that is the most useful thing you will
learn today.

## What to bring back

The artifacts are on disk and can be read directly — the whole `.sdlc/` tree, every findings round
included. The session transcript can be read too, from `~/.claude/projects/<slugged-repo-path>/`.

What is in neither is where you felt friction: a refusal you wanted to override, a question the
interview should have asked, a step that felt like ceremony. Note those as they happen, in as few
words as you like. Every defect worth fixing so far came from watching the flow run, not from
reading what it produced.
