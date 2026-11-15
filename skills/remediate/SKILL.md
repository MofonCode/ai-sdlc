---
name: remediate
description: Fix it -- take a blocked verification, an escalated cycle with a person's instruction, a change a person asks of a built cycle, or a pull request that conflicts with the integration branch, and go round implement and verify while each pass converges, deviating within limits before it escalates, then update the pull request and report. A playbook: resolve-conflict brings the integration branch in, implement changes the code, verify judges it, pr updates the pull request. Use after verify recorded a blocked outcome, when a person says what to do with an escalated cycle, when a person reviewing the pull request says what to change, or when that pull request conflicts or went red after another merge.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[<cycle id>]"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Bash(gh *), Bash(az *), Read, Write, Edit, Glob, Grep
---

# Fix it

**A playbook** (D-186, D-187): it decides what `implement` is handed and runs the loop, and it changes no code
itself. Four ways in (D-185, D-186):

| Way in | Where | What `implement` is handed |
|---|---|---|
| **a verification found a criterion unmet** — `blocked` | usually inside the build's own process, where the `build` playbook runs this with nobody watching | the unmet criteria and open findings |
| **the build escalated**, and the person has said what to do — the panel offered *say what to do, then fix it* | a person's session | their instruction |
| **the cycle passed, its pull request is open, and a person reviewing it says what to change** — *change deviation 2: use X instead*, *revert that choice* | a person's session | their instruction, on the same pull request (D-181) |
| **its pull request conflicts with the integration branch, or went red after another merge** | a person's session | nothing at first: **`resolve-conflict`** brings the integration branch in and records fresh evidence, and the loop starts at step 2. A conflict it hands back as a change of plan, or two plans disagreeing about one setting, stops here: say what it said |

## Which cycle

**Given no cycle id, ask the binary which one is meant** (D-142):

```
sdlc cycles --here --for remediate
```

It finds a cycle that is `blocked`, `escalated`, or passed with its verdict not yet on the integration branch,
and exits 0 printing the one this working copy carries. Otherwise it lists the candidates, or says none is ready
and why: ask the person which, in one question, and **never pick one yourself**.

## Where you work

**Inside the build's own process**, you are already on the cycle's build branch, in the worktree the binary
started you in. Stay there.

**In a person's session**, the cycle's branch is checked out in the build's worktree, not in their checkout:

```
sdlc branch <id> remediate
```

It prints the path of a worktree on the branch the pull request is from, creating it if absent. Every step below
runs there; leave the person's own checkout alone.

## A person's instruction: never act on a "no" without an alternative

On the second and third ways in, the person's words are the instruction. **Never act on a "no" that does not say
what to do instead** — *not like this* leaves `implement` guessing, and a guess is a new choice nobody approved.
Ask, in one question, what they want in its place, and ask again until you know. Then hand it to `implement` in
their words. A person's words never reach the intent, the spec or the plan, and never widen the cycle: an
instruction that would is a reopen or a successor, and you say so rather than act.

## The loop

1. **`implement`**, handed the findings or the instruction. On a cycle that passed, it records the evidence with
   `--retire-pass`. On a conflict, the first pass is **`resolve-conflict`** instead, and later passes are
   `implement` with the findings.
2. **`verify`**, in a fresh process that never saw the change. **You run the loop; you never write the verdict**
   (D-159): you do not adjudicate, edit `verify.md`, or argue a Not met back to Met.
3. **Read where the loop stands:**

```
sdlc pr-ready <id>
```

| What it prints | What you do |
|---|---|
| *N findings open, N passes spent and making progress* | go round: back to step 1 with the new findings. **There is no pass limit to run out of** |
| *… and the passes stopped converging: the next may take a different approach, recorded as a deviation, and must reduce what is open or the cycle escalates* | go round once more, telling `implement` to take a different approach than the approved text describes, recorded as a deviation |
| *escalated: the passes stopped converging again after a different approach, …* | stop: the different approach did not work either |
| *escalated: DEV_nn, DEV_nn, DEV_nn bend the same text in …* | stop: a third attempt at the same approved text |
| the outcome is `pass` | stop, and go on to the pull request |

**Deviate once before you escalate** (D-179). A stall — a pass that undoes earlier work, or two that reduce
nothing — may mean the approach the plan described is what is failing. Three limits, and none bends: never
change what *done* means (a criterion, or a test weakened so it passes); never widen the cycle; and never go on
without end — it escalates when the passes after the deviation stall or regress too, or when a third deviation
would bend the same approved text.

**There used to be a hard three and there is deliberately no ceiling now (D-66).** A blind three failed an honest
cycle that needed a fourth pass and let one making no progress spend all three. **Nothing stops you at pass
nine**: if the passes are getting smaller and you can say what the next one will fix, that is the rule working;
if you are repeating yourself, say so and stop.

## The pull request

On `pass`, or on `escalated`, **`pr`** updates the pull request: the push to the branch it was opened
from, and its body's report refreshed from `sdlc report <id>`. Inside the build's own process, the `build`
playbook opens the first one; here you only update one that exists.

## Report and hand back

**Name every finding, deviation, criterion and rule by what it says, with its id trailing at most.** The person
has not read the round: *"the die has no label (S_04)"*, never *"S_04"* alone.

**One line per pass**, and in it one line per unmet criterion and finding it worked: what was wrong, what
changed — or the deviation recorded — and whether `implement` was confident that was the cause. Then:

```
sdlc status <id>
```

Show it verbatim and stop. **Close with one plain offer**: on `pass`, hand back `sdlc report <id>` and say
*review the pull request — merging accepts it*; on `escalated`, what the passes did is the hand-back, and *say
what to do, then fix it* starts this playbook again from the person's instruction. A reopen, when the deviations
show the plan is wrong in substance, is the person's — on the plan branch once the plan is on the integration
branch (D-123).

## What you may not do

- **Change code, tests or evidence yourself.** That is `implement`'s.
- **Write the verdict.** That is `verify`'s.
- **Edit `intent.md`, `spec.md`, `plan.md` or `approval.md`.**
