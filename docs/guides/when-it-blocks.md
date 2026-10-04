# When something refuses

Every refusal the framework produces, what it means, and what to do next.

**A refusal is the gate working. A failure is the gate broken.** Those are different exit codes on
purpose, so a hook never reports its own bug as a governed stop:

| Exit | Means | |
|---|---|---|
| `0` | the command did what was asked | |
| `1` | **a rule refused it** | the gate working as intended |
| `2` | the command line could not be understood | a typo, or a missing argument |
| `3` | **the gate itself failed** | never a governed stop. Read the message on standard error |

A refusal goes to standard error and a pass to standard output, so a host that forwards only one stream
still shows you why you were stopped.

**One refusal has no recovery, and it is not a bug.** A blocking finding cannot be waived, accepted or
overridden by any mechanism — not at the gate, not with a flag, not with a recorded override. There is no
field for it in an approval entry and no refusal code that acceptance would clear. It is answered, or the
cycle does not advance.

---

## `sdlc gate` — the eight ways a build is refused

`sdlc gate <id>` exits 1 and says which of these it is. In rough order of how early you hit them:

### no cycle

The cycle has no directory. Nothing has been written yet.

**Next:** `sdlc new`, which allocates the id and creates the directory. If you expected a cycle to be
there, you have the wrong id — `sdlc landable --all` lists every cycle this solution holds.

### abandoned

The cycle was stopped, which is terminal.

**Next:** open a new cycle. An abandoned cycle does not reopen, and the record says why it stopped —
which is the point of it being a file a person wrote rather than an exit an agent took.

### a planning document is missing

One of `intent.md`, `spec.md`, `plan.md` is not there. All three are required, always.

**Next:** `sdlc status <id>` names which, and ends with the literal command to produce it.

### not approved

There is no approval ledger. Nobody has been to the gate.

**Next:** `sdlc draft approval <id>` and paste what it prints into `.sdlc/cycles/<id>/approval.md`.
**Asking an agent to paste it will be denied** — see the hook section below. That denial is the
mechanism, not a configuration problem.

### the standing decision refuses it

Somebody was at the gate and the decision was `changes-requested`, `denied` or `reopened` rather than an
approval.

**Next:** read the reason in `approval.md`. A refusal is rejected without one, so there is always
something there. Then `/sdlc:refine` or `/sdlc:steer` as the reason calls for, and back to the gate.

### a document changed since it was approved

The approval recorded a hash of each of the three documents, and one of them no longer matches.

**Next:** either put the document back as approved, or approve again — `sdlc draft approval <id>` prints
a fresh entry with the new hashes. Approving again is usually right: the edit happened for a reason, and
the gate's job is to make sure somebody saw it.

This is also what you hit after editing a planning document post-approval to "just fix a typo". There is
no size threshold, deliberately.

### a blocking finding is open

The one with no way out but to answer it.

**Next:** `/sdlc:refine <id> <artifact>` applies the findings to the artifact. Every finding ends in
exactly one of three exits and there is no fourth — **healed** (the artifact changes), **assumed**
(recorded in the decisions region, marked unconfirmed), or **asked** (recorded in `## Open`, and the
skill stops and asks you).

What refine may *not* do is answer a finding by making the artifact vaguer. "Fourteen seconds" becoming
"slow" removes the problem from the page while leaving the belief in it, and that is the single move the
exit rules exist to forbid.

Reaching this refusal at all means the blocker was raised after the approval entry was printed, or the
entry was written by hand — the printer refuses to produce one while a blocker is open. The check lives
in both places because the printer is advice a person can decline to take and the gate is the layer that
does not negotiate.

### a findings round is stale

A round judged its artifact against an upstream document that has since changed. The three approval
hashes prove the three documents were approved; they do not prove the spec was derived from *this*
intent. An intent edited after a clean spec leaves a spec derived from an intent that no longer exists,
with every approval hash matching. Only the round knows what it read, which is why it records it.

**Next:** re-interrogate the downstream artifact — `/sdlc:interrogate <id> spec`, and the plan after it
if the spec changed. `/sdlc:steer` does this for you, which is the reason to use it rather than editing
documents by hand.

---

## `sdlc landable` — the four ways a landing is refused

### not verified

There is no cycle, or no `verify.md`.

**Next:** `/sdlc:verify <id>`, which needs recorded evidence — so `sdlc evidence <id>` before it if the
build has not recorded any.

### the verification did not pass

The verdict is `blocked`. Not "no failures recorded" — an actual pass is required, because the verdict is
the only thing standing between unattended building and unreviewed work reaching a pull request.

**Next:** `/sdlc:remediate <id>`.

### escalated

Remediation ran out of its three passes with blockers still open. **An escalated cycle never lands**, and
that stopping is the only reason an unattended batch is safe to run at all.

**Next:** a person decides. Either the criterion is wrong, in which case
`sdlc draft approval <id> --reopen` withdraws the approval with a recorded reason and the documents can be
rewritten, or the work is harder than the plan thought and the cycle is abandoned with
`sdlc draft abandon <id>`.

**Do not raise the budget.** Three passes then stop is the bound everything unattended rests on.

### the chain does not hold

The pull-request check would refuse it anyway, so pushing would cost a round trip through CI to learn what
you were just told.

**Next:** `sdlc verify-chain <id>`, which prints every check — passing and failing both, because a reader
looking at a failed check needs to know what held. The checks are: the cycle exists, no shape problems,
the chain is complete, the approval, the gate is satisfied, every criterion has a proof, every named test
passed, the change was verified.

**"every named test passed" is the one that surprises people.** Each proof in the plan names its test in
backticks, and this reads those names out of the plan and confirms each one ran and passed according to
the normalised summary — not according to anything the build said about itself.

---

## The pre-tool hook — the five things an agent is denied

These are denied at the tool call, by file path and by shell command alike. The rules are absolute rather
than conditional, and that is the point: there is no mode, no flag, and no check on which skill is
running, so a rule with no exception has nowhere for a bypass to live.

| What | Why it is a person's |
|---|---|
| writing `approval.md` | an approval is a person's act, and a pasted one means something because no tool could have produced it |
| writing `abandoned.md` | an agent stuck on a blocking finding has an obvious escape — abandon and start clean. A hand-written reason keeps the objections that killed it on the record |
| writing `constraints.md` | a cycle judged by the constraints it is changing is the same deadlock as the framework governing itself |
| writing `solution.md` | the origin is a historical fact that must never change. Per-key protection would be the first conditional rule, which is where a bypass would live |
| running `sdlc init` | adoption writes the hook configuration that constrains the agent, the pipeline that gates the merge, and the file saying where this code came from |

**If your agent reports one of these as a broken tool, it is reading the mechanism as a malfunction.** The
refusal message names the drafter to run instead. Run it yourself and paste the output.

**Reading these files is allowed.** Refusing `cat approval.md` would make the gate obstructive without
making it safer.

The shell rule is deliberately broad — it looks for the file names near a write indicator rather than
parsing a command line. A false refusal on a command that merely mentions one of these files costs you a
rephrasing; a miss costs the guarantee.

**Five is the whole list, and the limit is structural.** The hook sees a tool call and not which skill
made it, so it cannot deny an edit that is wrong only because of where it came from — `remediate`
touching `spec.md`, or `refine` touching source. Those rules are real and the skills state them, but
they are the skill's own discipline rather than something denied at the call. What catches a breach is
slower and certain: an approved cycle's three documents are hash-locked, so a planning document edited
after approval makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the merge, each naming
which document moved. **You cannot edit your way past a criterion; you can only turn a cycle that would
have landed into one that cannot.**

---

## The turn-end hook — the three ways a turn is sent back

Asked before an agent finishes, on a `cycle/<id>` branch only. It is what makes a warnings-as-errors
build and an architecture test enforcement rather than something a reviewer finds afterwards: nothing can
report done over a red suite.

**It never runs a build.** It reads what `sdlc evidence` recorded, which every stack normalises to the
same shape — so the gate stays out of the business of knowing what a test platform is.

### the recorded suite has a failure

The evidence for this cycle carries a failing test. Fix the code, run the suite, record it again. **Do not
adjust the test**: that is the inversion the whole design exists to prevent, and it is also the easier
fix, which is why the hook says so rather than trusting anyone to remember.

### there are changes and no recorded evidence

Code was written on this branch and nothing recorded what the build did with it. Verify reads only what
the build records, so a change nothing recorded is a change nothing can judge.

```
sdlc evidence <id> --tests <results file> --build-log <log>
```

### the working tree has moved since the evidence

The recorded diff no longer matches what is on disk, so the evidence describes something other than the
code. Run the build and the suite again over what is there now, and record it.

### when it says nothing

Deliberately most of the time. It is silent off a cycle branch, on a cycle with no directory, on an
abandoned cycle, before the cycle has a plan, when nothing has changed, and when the host is already
continuing because it was sent back once already. A governance hook that met a person finishing a thought
would be switched off within a day, and then the one case it catches would go uncaught too.

**It is bypassable.** Running `sdlc evidence` against a stale results file satisfies it. That is the local
layer behaving as designed — `sdlc verify-chain` in the pipeline re-reads the evidence and checks that
every test the plan named actually passed, and that is the guarantee.

**Two of the three hosts.** Claude Code calls the moment `Stop` and Copilot `agentStop`, and adoption
configures whichever name the host uses. Antigravity has such an event and is deliberately left
ungoverned — see the paragraph below. Copilot was left out of this for a
release on the belief that its hook surface had no such event; it has had one all along, and
`CONCEPT.md` D-46 records how the claim survived. A host that genuinely has no turn end is still refused
by name — `sdlc hook --event turn-end` says so and configures nothing, because a plausible-looking
configuration for an event that cannot fire is a check that appears to enforce and does not.

**The send-back needs the host to say it is already retrying**, or a refusal is an endless turn rather
than a governed stop. Claude Code and Copilot both carry that flag and both spell it `stop_hook_active`.
**Antigravity does not**, and that is why its turn end is not governed at all:
`sdlc hook --event turn-end --host antigravity` refuses by name rather than installing a check that
could loop. Its tool calls are governed normally.

---

## When nothing refuses and it should have

The dangerous direction. Four ways the gate goes quiet — and **`sdlc doctor` is the command for this
section**: it checks every host's hook configuration, adoption, and whether a pipeline here runs the one
check that is the guarantee, and exits 1 if any of it is wrong. The two causes it cannot see are marked
`elsewhere` rather than passed, and they are the first two below.

**A hook is not loaded.** `claude plugin details sdlc@ai-sdlc` must say `Hooks (2) PreToolUse, Stop`. **A
count below two is a failed install, not a detail** — and the two fail differently. Without `PreToolUse`
nothing is denied; without `Stop` a turn can end on a red build. Either way every cycle still appears to
work. No binary can check this for you, which is why `doctor` names the command instead of answering it.

**The workspace is not trusted.** Project hooks do not run until it is. Same silence, different cause.

**The session predates the install.** Skills and hooks load at startup. Reinstall, then start a new
session.

**The check is not required.** `sdlc verify-chain` in the pipeline is the only non-bypassable layer, and
it is only a guarantee while branch policy makes it required. A pipeline that runs it and a merge that
does not wait for it is a directory that appears enforced and is not — which is worse than one that never
pretended. Iteration 3 shipped a nightly job that needed an API key nobody had set, and warned and exited
cleanly for weeks. That is the failure mode this design is written against.

**One more, on the generated pipeline:** its first real step installs the gate, pinned to the release
that generated it, and **that install does not resolve yet** because the package is not on a public feed.
So a first pull request fails at `install the gate` rather than at `verify the artifact chain`. That is
the intended shape — the failure names the missing thing, before a build is spent, in a step whose
comment says so (D-37). Name a feed holding the package in `nuget.config`, or wait for it to be
published.

---

## Where to go next

| | |
|---|---|
| a cycle walked end to end | `first-cycle.md` |
| every command for one cycle, on one screen | `cycle-commands.md` |
| what each skill is and who starts it | `../skills.md` |
| installing, updating, removing | `install.md` |
| the fourteen global constraints, each with what it forbids | `../../CONSTRAINTS.md` |
