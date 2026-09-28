<!--
covers:
  - "skills/**/SKILL.md"
  - "hooks/hooks.json"
-->

# Running your first cycle

Eighteen steps, in order, from an empty directory to a pull request. One command per block, so each can
be copied on its own.

**Verified as of `0.2.0`:** steps 4 to 13. Step 3 and steps 14 onward use skills that have never been run
end to end, and each says so where it appears. Expect friction there and note where it happens — those
notes are worth more than the artifacts.

---

## Read this first

Two cycles in one empty repository. Together they cover both paths anyone will actually take — adopting
into nothing, and changing something that exists — and neither covers the other.

**Cycle 1, greenfield.** *"There is no way to see a die roll; I want a web page that shows one number from
1 to 6."* The only thing that exercises `init` from empty, the `solution` intent type, and a plan whose
files are all new.

**Cycle 2, a change to what cycle 1 built.** *"One die is not enough; the page should show two."* Now there
is existing code with a rendered output, which is what brings the rest alive: CON_07, the declared-file
hook, a plan trying to prove existing behaviour did not move, a tier that is not automatically `local`,
and escalation.

Cycle 2 is the more informative of the two, and it needs cycle 1 to have happened. It also tests something
no fixture can: cycle 2's substrate was produced *by the process*, so a vague spec in cycle 1 will be felt
in cycle 2.

### Why a die roll is a good subject

**Randomness resists a falsifiable criterion.** "Shows a random number" cannot fail, and CON_03 forbids a
criterion that cannot. A sound spec has to land on something checkable — the value is within 1 to 6, every
face appears across N rolls, or the roll comes from a seam a test can seed. Which one it picks is the most
interesting thing cycle 1 will tell you.

**A page is harder to prove than a class.** The honest shape is the roll living in a testable unit with the
page as a thin caller. A plan that puts the logic in the page and then claims a unit test proves it has
written an inadequate proof, which is CON_06's to catch. Expect real effort on test infrastructure; that is
the framework forcing a testable design, not getting in the way.

**Scope cycle 1 hard.** The interview will cheerfully help you design a board-game platform. Keep saying
"not in this cycle." If the spec comes back with eight criteria, that is worth reporting.

---

## 1. Make the repository

Already done at `C:\Repos\dice-trial` — skip to step 2. These are here so the sequence is repeatable.

`init` refuses a directory that is not a repository, and not out of bureaucracy: `build` records the diff
that is the only evidence `verify` derives its claims from, and every gate keys on a `cycle/<id>` branch
that cannot otherwise exist. A governed directory without git permits everything while still producing
artifacts that look governed.

```pwsh
mkdir C:\Repos\dice-trial
```

```pwsh
cd C:\Repos\dice-trial
```

```pwsh
git init
```

An empty repository is not enough either — there has to be a commit for a sha to point at.

```pwsh
git commit --allow-empty -m "before adoption"
```

## 2. Confirm the plugin is installed

```pwsh
claude plugin details sdlc@ai-sdlc
```

Expect a version, `Skills (12)` and `Hooks (1) PreToolUse`. **If the hook count is zero, stop** — the
deterministic layer is not loaded, nothing is enforced, and every step below will appear to work anyway.
If the plugin is missing entirely, **Troubleshooting** at the end has the command.

## 3. Adopt the framework

```pwsh
cd C:\Repos\dice-trial
```

```pwsh
claude
```

Then, inside that session:

```
/sdlc:init
```

*Never run end to end.* It should create `.sdlc/solution.md` with your stack and origin, and a
`.sdlc/README.md` explaining the terminology, the flow and the commands. It creates no empty
directories — a cycle's directory is made by the skill that writes the first artifact into it. It
should leave any existing `AGENTS.md` alone.

Adoption is free. Your existing code is not judged — constraints govern each change as it is made, never
code that predates them, so there is nothing to baseline.

## 4. Write the intent

```
/sdlc:intent We have no way to see a die roll. I want a web page that shows one number from 1 to 6, rerolled each time the page is asked for.
```

Describe **the problem**, not the fix. If you name a fix, the skill will ask what its absence causes — that
is the conversion working, not the skill being difficult. A decision you have already taken is welcome; it
gets recorded as a fixed decision with your name on it.

It will interview you. Answer what you know and say so when you do not: unknowns become **Open** questions
rather than invented answers. An intent with three open questions is sound; three invented ones are a
liability, because every later phase treats an accepted intent as settled.

**A deliberate choice:** saying "1 to 6" hands the spec a fact. Leaving it out is the sharper test — if the
spec invents a range, CON_02 should have caught it here. Pick knowingly.

It writes `.sdlc/cycles/<id>/intent.md`. **Note that id** — every command after this takes it.

## 5. Put the cycle on a branch

Substitute your id for `20260928_a1b2` here and in every later step.

```pwsh
git checkout -b cycle/20260928_a1b2
```

The name matters: the gates key on `cycle/<id>` and stay silent off it, so a mistyped branch means nothing
is enforced and everything *appears* to work. Branching now means the whole chain travels with the change.
If the intent is rejected, delete the branch.

## 6. Interrogate the intent

```
/sdlc:interrogate 20260928_a1b2 intent
```

The second argument names which artifact to judge — `intent`, `spec` or `plan`. Leave it off and it
takes the furthest-advanced one that exists, saying which it chose.

Writes `.sdlc/cycles/20260928_a1b2/findings/intent.r1.json`. Read the findings, not just the count.

Blocking findings mean the intent must change first. Fix it and run this again — it writes `r2` and keeps
`r1`, because the sequence is the record of whether your repair worked.

## 7. Accept the intent

Nothing accepts an artifact for you. Edit the intent's frontmatter yourself and add both fields:

```yaml
accepted_by: Peter Almanzar
accepted_at: 2026-09-28T09:00:00Z
```

Both, or neither — a name with no date says nothing about which version you accepted. This is the cheapest
of the three gates: you are agreeing this is the right problem, before anything else exists.

## 8. Write the spec

```
/sdlc:spec 20260928_a1b2
```

Turns the intent into numbered acceptance criteria, each stating an observable outcome and how it is
checked. It refuses if the intent is not accepted.

## 9. Interrogate the spec

```
/sdlc:interrogate 20260928_a1b2 spec
```

## 10. Accept the spec

Read the criteria properly first. This is what everything downstream is measured against, and the numbering
is an interface — the plan and the verify document cite criteria by number, so a criterion must never be
renumbered once accepted.

```yaml
accepted_by: Peter Almanzar
accepted_at: 2026-09-28T09:15:00Z
```

## 11. Write the plan

```
/sdlc:plan 20260928_a1b2
```

Declares every file it will touch, a blast-radius tier, and how each criterion will be proven.

## 12. Interrogate the plan

```
/sdlc:interrogate 20260928_a1b2 plan
```

## 13. Approve the plan

Read it as the thing you would read to decide whether to let the change happen, because that is what it is.
Two parts repay real attention:

- **The file list.** A hook refuses any edit to a file the plan did not declare, so an incomplete list stops
  the build rather than surprising you later
- **The proofs.** Ask of each: *could this check pass while its criterion is unmet?* If yes it is
  inadequate, and an inadequate proof is worse than none because it produces a green verification. This is
  the question the process is least able to answer for you

```yaml
approved_by: Peter Almanzar
approved_at: 2026-09-28T09:30:00Z
```

**This is the gate that matters.** Everything after it is machine-decided until landing, and a hook refuses
every source edit until those two fields exist.

## 14. Build

```
/sdlc:build 20260928_a1b2
```

*Never run end to end.* It implements the plan and heals its own build and test failures — nothing should
leave this step that does not compile and pass.

It must also write three files into `.sdlc/cycles/<id>/evidence/`: `build.log`, `tests.trx` and
`diff.patch`. If any is missing, the next step will report the matching claims as unprovable. That is the
design working, and it will look like a failure.

## 15. Verify

```
/sdlc:verify 20260928_a1b2
```

*Never run end to end.* Writes `.sdlc/cycles/<id>/verify.md` and decides an outcome. It writes no code — it
reads the diff against the spec and reports every criterion that is unmet or unprovable.

| Outcome | Go to |
|---|---|
| `pass` | step 18 |
| `blocked` | step 16 |
| `escalated` | three passes are spent; the cycle is yours now |

**Never hand-edit `verify.md`.** A hook refuses a targeted edit, and the reason is the whole audit story:
the document's value is that it was derived. To change it, change an input — the code, the spec, or a waiver
— and run verify again so a fresh document supersedes it.

## 16. Remediate

```
/sdlc:remediate 20260928_a1b2
```

*Never run end to end.* Works the open blocking findings and nothing else — suggestions are yours to read,
not its to act on.

## 17. Verify again

```
/sdlc:verify 20260928_a1b2
```

Nothing but a fresh verification can tell whether a fix worked. Three passes, then `escalated`. The counter
resets only when **you** change an input, so an unattended run cannot talk its way into a fourth attempt.

Return to step 16 while the outcome is `blocked`. If you land on `escalated`, there are four ways out and
all of them are yours: fix it by hand, amend the spec and re-approve it, waive the finding with a recorded
reason, or abandon the cycle.

## 18. Land

```
/sdlc:land 20260928_a1b2
```

*Never run end to end.* Needs `gh` authenticated and a GitHub remote. Opens a pull request whose body is
assembled from the artifacts, and refuses anything but `pass` — a hook refuses it too.

It does not merge. A code owner does, and never the agent that wrote the change.

---

## Forcing the unhappy path

Run this as cycle 2. It is the more valuable of the two, because escalation is what makes unattended runs
trustworthy and it is the half nobody demonstrates.

The route in is to **approve a plan that interrogation flagged.** Write a spec with a criterion the plan
does not cover — for two dice, a criterion about the page's existing single-die output that the plan leaves
alone — then let the plan declare only the file it wants. Interrogation should raise CON_06, because no
proof covers that criterion. Approve it anyway.

What should follow: build satisfies what it can, verify finds the uncovered criterion unmet, and remediation
cannot fix it because the hook refuses to touch a file the plan never declared. Three passes, then
`escalated`, and landing refuses with the count attached.

That chain exercises the declared-file hook, the remediation ceiling, the terminal state and the landing gate
in one run. If any link behaves differently, that is the most useful thing you will learn.

## Troubleshooting

### The plugin is missing

Install it from GitHub. This clones the repository itself, so it works from anywhere and you do not need
a copy of the framework.

```pwsh
claude plugin marketplace add MofonCode/ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

### You changed the framework and want the change picked up

Only relevant if you have a clone and installed from it. An install caches by version, and
`claude plugin update` does nothing when the version has not moved, so a refresh is a full reinstall:

```pwsh
claude plugin uninstall sdlc@ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

The two are a pair — running only the first leaves you with no plugin. `install.md` covers both sources
and when to use which.

### Nothing is refused and everything works first time

Check the branch. The gates key on `cycle/<id>` exactly, and off that branch they are silent by design.

```pwsh
git rev-parse --abbrev-ref HEAD
```

### A skill says it cannot read something in the plugin directory

It should not need to. The constraints and the stack defaults are injected at load time, precisely because
a session working in your solution cannot reach wherever the plugin is installed. If you see a skill
asking for access to a plugin path, an injection failed — worth reporting verbatim.

## What to bring back

The artifacts are on disk and can be read directly — the whole `.sdlc/` tree, every findings round included.
The session transcript can be read too, from `~/.claude/projects/<slugged-repo-path>/`.

What is in neither is where you felt friction: a refusal you wanted to override, a question the interview
should have asked, a step that felt like ceremony. Note those as they happen, in as few words as you like.
Every defect worth fixing so far came from watching the flow run, not from reading what it produced.

---

---

---

---

---

---

---

---

## Constraints cited here

Short descriptions, so nothing here sends you to look one up. `constraints.md` carries the full
statement, its `Forbids:` clause and the reasoning; `/sdlc:constraints` renders the effective set for
a solution, this list plus any `LOC_` of its own.

| | |
|---|---|
| `CON_02` | an intent's unknowns stay open questions |
| `CON_03` | a criterion can fail |
| `CON_06` | a plan's proof is adequate to its criterion |
| `CON_07` | a change does only what its plan declared |
