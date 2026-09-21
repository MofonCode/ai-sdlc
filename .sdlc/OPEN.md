# Open items that live in no other file

**What this file is for.** A few things are outstanding that no document in this repository
records. A session starting fresh reads the charter, the chain directories and the code, and
learns none of them — so each one survives only as long as someone remembers to say it out loud.
This is where they are written down instead.

**What this file is deliberately not.** It is **not a list of open chains.** Every chain under
`.sdlc/` already carries its own stage in its `status:` line and, where it is parked or blocked,
a banner at the top of its intent saying so, with the date and the reasoning. Copying that here
would make two lists with nothing comparing them, which is the exact failure this repository has
now been bitten by four times — the pack's file table beside its manifest, the verification
schema beside the agent that writes it, the pack's charter line beside the layout it scaffolds,
and the silent-failure register beside the test suite. Each drifted; each needed a check built to
hold it. **To see what is in flight, read the chain documents.**

**How an item leaves.** By being done, or by becoming a chain of its own — at which point the
chain's documents are the record and the line here is deleted. Not by being forgotten.

Last reviewed: 2026-09-21. Seven items open. Three closed the same day: the missing git remote;
the register row that registered nothing — by charter version 8, which rewrote it to name its
test class and proved the row live by injecting a fault and watching the check fail; and **the
parallel build that had never run**, by `every-workflow-body-runs`, which fixed the loop and
built the harness the item said was the larger half. **Three opened since:** nothing writes a
*task-tier* plan's finished state once the collision check depends on one; an approved plan was
produced from a draft intent, which the collision change is about to erase the evidence of; and
fan-out has still never dispatched a real agent, which is what survives of the item that closed.

---

## 1. `/sdlc-core:init` has never been hand-run

Seven clauses of the shipped `init-scaffolds-solution` spec describe what the command *asks a
person*, and nothing in the test suite can observe a prompt. They shipped verified by reading,
which is stated in the spec and is not the same as tested:

| Clause | What it requires |
|---|---|
| R15, kind half | Stop when the interview has not settled the solution kind, rather than choosing one |
| R16 | In a repo that already has an approved charter, offer amendment and gap-filling as the two things it can do |
| R17, confirmation half | Present the derived solution name and take back a yes or a different string |
| R20 | If the executor fails, relay its report verbatim and do not claim the repo is scaffolded |
| R60 | Invoke the executor with the manifest path, the settled kind, the target directory and the confirmed identifier |
| S10, residual | Call the executor only after the charter is approved, at the right point in the flow |
| S11, asking half | Actually conduct the interview |

**What closes it:** running `/sdlc-core:init` by hand in an empty directory, and recording what
happened. `pack-promises-what-it-scaffolds` changed the R15 clause, so that run is now also the
only evidence the change reads correctly to someone who did not write it.

**Why it is not a chain:** it is an act, not a change. There is nothing to plan.

**Where to do it, as of 2026-09-20:** `MofonCode/MofonCode.Core` was created empty and needs
initialising anyway. Running `init` there does the hand-run against a repository that genuinely
needs the output, rather than against a directory created to be thrown away — which also
exercises the interview honestly, because the answers matter.

## 2. Five task-tier changes shipped with no fresh-context verification

The framework runs adversarial verification in a clean context for **feature**-tier work only.
Everything shipped since the scaffold was task tier and got none:

- `verification-reports-options` — verification records lead with a summary and end with options
- `register-names-every-check` — the silent-failure register and the test suite name each other
- `stack-dotnet-house-stack`
- `pack-promises-what-it-scaffolds`
- `every-workflow-body-runs` — the wave loop repaired, and the harness that executes a workflow
  body rather than parsing it. Added on the day it shipped rather than noticed later, which is
  the only difference between it and the four above.

The last of those has the strongest case for one it never got: it superseded half of a
requirement in an approved, shipped spec, it amended the charter, and two of its four
requirements ship on a reading of prose with no instrument behind them.

**The open question is not whether to verify that one change.** It is whether "task tier" should
mean "no verification" or "a shorter verification", given that a task-tier change can supersede a
shipped requirement and amend the charter — which is what happened. Answering it is a change to
the framework's own gates, and it belongs with `findings-leave-only-by-decision`, whose subject
is the same shape: a thing that should have been decided instead quietly not happening.

**What closes it:** a decision on that question, and a verification pass on any change still
standing unverified when it is made.

## 3. The export-and-return half of the self-improvement request

The request had two halves. **Shipped:** verification records now open with a plain-language
summary of what was found and close with the owner's options. **Not drafted:** running this
framework against a solution in another repository, exporting something from that run, and
bringing it back here so the engine improves from its own use.

Nothing about the second half exists in any file — not an intent, not a non-goal, not a note.

**What closes it:** an intent, if it is still wanted. The nearest thing already drafted is
`findings-leave-only-by-decision`, which is about findings not escaping the record; this is about
findings *arriving* from outside. They are related and they are not the same chain.

## 4. Nothing writes a task-tier plan's finished state, so live plans accumulate

Once `no-two-plans-claim-one-file` ships, a plan that is not live claims no files — which is
what makes cross-chain collision reporting usable. **Nothing moves a plan into that state after
the one-time marking that change performs — *unless the chain is feature tier*.** Charter
version 9 settled that `/sdlc-core:verify` writes the field on the SHIP branch of its gate, so
feature chains are covered. **Task tier is the hole**, and it is the larger half: task tier
requires only a plan and passes through no stage after approval, so nothing writes the field
and nobody is prompted to. Of the five plans that have actually shipped here, four shipped that
way — the majority case rather than an edge. A sixth plan carries `approved` and was superseded
without ever being built.

An earlier version of this item said *nothing* writes it, which was true when it was written
and stopped being true when the amendment landed.

**The other way a plan stops being live is written by nobody at all.** The shipped artifact
schema says `/sdlc-core:plan` writes `superseded` when it replaces a plan. The plan skill has
never contained the word, and nothing under `plugins/` writes that value anywhere — so a
replaced plan reads `approved` forever, which is how `engine-substitutes-declared-tokens` came
to carry an approved plan for work that was abandoned. `no-two-plans-claim-one-file` corrects
the schema to say supersession is a person's act; it does not build the tool, and the closing
step below is the fix for both routes. Found on 2026-09-21 by a fresh-context interrogation of
that chain's intent.

**The consequence is slow and quiet.** Every new *task-tier* plan stays live after its work
ships, so the collision report fills with chains that are finished, and the check drifts toward
the state its own intent says gets a check switched off — reporting collisions that are all
false. Slower than it would have been before the amendment, because feature chains retire
themselves; not stopped.

**What closes it:** a closing step — one gate every tier passes through at ship time, which
moves the plan to finished and commits it. It binds the tier contract, needs its own approval
question and status transition, and is impossible at trivial tier, which has no plan to mark.
So it is a change of its own, not a detail of collision checking.

**It converges with item 2 above**, which asks whether task tier should mean no verification or
a shorter one. Same question from the other side: task-tier work passes through nothing after
approval, and both items are about that.

**Recorded rather than fixed on 2026-09-20**, deliberately: making it a success criterion of
the collision change produced a deadlock, since that step is placed first with nothing else
starting until it lands, and the criterion blocked it on a chain that does not exist.

## 5. An approved plan was produced from a draft intent, and the evidence is about to be erased

`engine-substitutes-declared-tokens/plan.md` carries `status: approved` and `spec: none` over an
`intent.md` that reads `status: draft`. That is a direct violation of the invariant that nothing
downstream is produced from a draft — the plan was approved before its intent ever was.

**It is recorded here because it is about to become invisible.** `no-two-plans-claim-one-file`
marks that plan superseded, which is correct and which erases the only machine-readable trace:
afterwards the frontmatter no longer says `approved`, and a fresh session reading the artifacts
alone — the premise the whole design rests on — would never learn it happened.

**What closes it:** nothing to fix in the artifact; the plan is superseded and stays so. What is
open is whether the gate that let it through needs an instrument. The invariant's checkable
clause is *"every path that consumes an artifact reads `status:` from the file and stops"*, and
the plan skill's own gate is prose. Found on 2026-09-21 by the twenty-ninth interrogation of
that chain's intent.

## 6. The framework cannot be installed anywhere but this machine

`.claude-plugin/marketplace.json` is a valid Claude Code marketplace declaring two plugins —
`sdlc-core` at 0.9.1 and `stack-dotnet` at 0.6.0. **Both sources are relative paths** into this
working tree, and the documented install is a local filesystem path:

    /plugin marketplace add C:\Repos\ai-sdlc

So it installs on a machine that has this repository checked out at that path, and nowhere else.

**Three things are missing, and none of them is a bug — they were simply never built:**

- **No remote install path.** The repository had no remote at all until 2026-09-20. It is now at
  `MofonCode/ai-sdlc` and **private**, which was the deliberate reversible choice; private means
  only accounts with access could install it even once the marketplace points at the remote.
- **No releases.** `git tag` returns nothing. Plugin versions are bumped per change, but nothing
  consumes them, because nothing is ever packaged. The charter already records the consequence
  under **Known and unchecked**: *"a plugin version that is valid semver but was never bumped —
  the format is checked, the increment is not, so an update can still be a silent no-op."*
  Untagged, the bump is a no-op regardless of whether it happened.
- **No build or publish pipeline.** `.github/workflows/checks.yml` is the only workflow and it
  runs the test suite. No artifact, no package, no release job.

**Not a defect in the framework's design.** A plugin is installed once per machine and is inert
in repositories with no chain directory, so "install it into a repo" is not a step that exists —
running `/sdlc-core:init` there is. What is missing is a way for a *second machine* to get it.

**Closing this reopens a decision `no-two-plans-claim-one-file` made on the strength of it.**
That change rewrites how a claimed path is normalised, which changes which paths two plans are
judged to collide on, and plugins are cached per version — so an older engine and a newer one
would disagree with no version gate between them. It ships no migration, and the reason it can
is written down as *"free only while the installed population is one"*. The moment a second
machine can install this, that sentence stops being true. **Whoever points the marketplace at
the remote owes that change a migration question**, and this paragraph exists so that the
dependency is readable from both ends rather than only from the chain that noticed it.

**What closes it:** pointing the marketplace at the git remote rather than a local path, and
tagging releases so a version bump means something. A publish job is the larger question, and it
is worth deciding deliberately rather than drifting into — permanent non-goal N3 says this
framework *"never becomes a CI/CD or project-management system"*, which governs what the
framework does rather than whether this repository may release itself, but a marketplace that
publishes itself is exactly the shape that argument gets made about.

**Unverified, and stated as such:** the exact syntax and behaviour for adding a marketplace from
a private git remote is a Claude Code mechanic, not something this repository documents. It was
not confirmed before writing this.

## 7. Fan-out has never dispatched a real agent

**This is what is left of item 6 after it closed, not a new discovery.** That item said the
parallel build had never run and that nothing would have noticed. `every-workflow-body-runs`
fixed the loop and built the harness, so the body now runs end to end — the gate reads the plan
and refuses a draft, waves dispatch in dependency order, a red build halts before the next wave,
a do-not-ship verdict comes back blocked. **All of it against doubles.** Not one real
implementer has ever been dispatched by that script, and every fan-out this framework has done
was arranged by hand.

**Why it is not covered by what shipped.** The harness proves the body's control flow. It says
nothing about whether an implementer agent respects the file boundary in its prompt, whether
two of them racing over shared build output behave the way the prompt warns they will, or
whether the verification stage produces a usable record when the thing it verifies is real.
Those are the questions a first live run answers and nothing else does.

**What closes it:** running one plan through `workflows/parallel-build.js` for real, on work
small enough that a bad outcome is cheap, and recording what happened. Two independent slices is
the honest minimum, because a single-slice plan exercises no fan-out at all.

**Why it is not a chain:** it is an act, not a change — the same shape as the `init` hand-run
above, and for the same reason. There is nothing to plan until it has been tried once.

**Recorded on 2026-09-21**, the day the harness shipped, so that a green suite is not read as
evidence of something it does not show. The charter's amendment log and the new check's own
remarks say the same thing; this is where it lives as outstanding work rather than as a caveat.

---

## Deliberately not listed here, and where each actually lives

- **Chain state** — parked, blocked, drafted, shipped. In each chain's own `status:` line and
  banner under `.sdlc/<slug>/`. See the rule at the top of this file.
- **The encapsulation rules** — private handlers, an internal `DbContext`, non-public EF
  configurations. Recorded as an explicit non-goal in both `scaffold-emits-a-module` and
  `scaffold-emits-an-application-layer`. Poorly placed, since both of those are themselves parked
  or blocked, but genuinely written down; it becomes its own chain next.
- **The charter's contradiction between amending and registering** — that amending the charter was
  forbidden as a side effect of shipping while its silent-failure register cannot stay complete
  unless it moves while something ships. Recorded unresolved at versions 4, 5, 6 and 7, and
  **settled at version 8** by splitting the rule: moving a constraint takes its own approval
  conversation, adding a register row rides the gate of the change that adds the check. In the
  charter's amendment log, and in the charter schema the engine ships.
- **The two live defects in `SharedKernel`** — thread-pool-sized concurrency limits, and
  `Expression.Invoke` in the specification combinators. Named as a dependency in
  `scaffold-emits-an-application-layer`. **MarketSense was sunsetted on 2026-09-20**, so these
  stop being another repository's problem and become part of whatever is carried into
  `MofonCode.Core`: code lifted out of a dead codebase arrives with its defects, and nothing
  will fix them there.
