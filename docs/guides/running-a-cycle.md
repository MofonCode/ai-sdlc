# Running a cycle

One governed change, from why it exists to a pull request: every stage, and what each one refuses if
you arrive at it early.

**This assumes the repository is adopted, and the drafts adoption left are placed and committed** —
`adopting-the-framework.md` and `declaring-the-stack.md` are the steps before, and `sdlc doctor` exits 0
when they are done. Nothing here is specific to what the change is.

**This is the happy path.** When an interrogation raises a blocking finding or a verification records
`blocked`, `handling-blockers.md` is the walk back. **If you have done this before**, the short path at
the top of `../cheat-sheets/commands.md` is this same walk in numbered lines.

## 1. Read the rules before writing anything

```pwsh
sdlc constraints
```

The global `CON_` set, the `PRO_` stack rules, then this solution's own `LOC_` — in that order, so a
solution's rules are the last word. **Constraints live in the framework and are never copied into a
solution**, so this command is how they reach a session. Every stage skill runs it first, and one that
did not is working from recollection.

The middle tier is the one your declared stack decides: the framework ships three rules that have to
name a framework, and a solution is held to exactly the ones its stack file marks `true`. A stack that
lists none has no such tier in this output. `declaring-the-stack.md` has why.

## 2. Open the cycle — the intent skill does it

```
/sdlc:intent The dashboard takes fourteen seconds and people have stopped opening it
```

**You do not open the cycle or switch branches yourself.** The skill runs `sdlc new`, which allocates the
id and claims it, then `sdlc branch <id> plan` and the `git switch` it prints, and says the id and the
branch it is now on in one line. Then it interviews you — section 3.

**It refuses while anything is uncommitted**, and names the files: a switch carries uncommitted changes
with it, so they would reach the cycle's diff as work no plan declared. Commit, stash or discard them,
and ask again. The skill asks you which rather than choosing.

**What the skill runs, for when you need it by hand.** Opening a batch of cycles before interviewing any
of them is the one ordinary reason:

```pwsh
sdlc new
sdlc branch 20260929_1456_32 plan
```

`sdlc new` prints the id. **Do not type it and then invoke the intent skill** — the skill opens a cycle
of its own, so that allocates a second id and a second directory, and the claim succeeds both times. The
work goes into one of them and the id you wrote down names the other. `sdlc branch` prints the one
command that switches — `git switch --no-track -c cycle/20260929_1456_32/plan origin/development` when
the integration branch is on the remote, from the local one when it is not — and run twice it prints the
plain switch to a branch that already exists.

**A cycle's branches sit under `cycle/<id>/`, one per kind of work**: `plan` for the planning documents,
`build` for a build you run by hand, `build-unattended` for one a batch runs, and `forward` for a release's
fix carried into the integration branch. The skill for each stage asks `sdlc branch` for its own; it
knows which base each one starts from.

**The names are not cosmetic.** On a branch that is not a cycle branch the turn-end hook goes silent —
that is the half with no message: it reads the cycle out of the branch you are on, so you keep building and
lose the two send-backs it exists for, a recorded suite with a failure in it and code that has moved since
the evidence was recorded. And the pull-request check reports that there is no chain at all, because it
reads the cycle out of the pull request's branch the same way.

## 3. Intent, spec, plan

Three artifacts, all required, always. Each one is written, challenged in a **fresh process** against the
rules in force, and revised until a round raises nothing blocking. Each stage skill runs that loop itself;
`handling-blockers.md` is what it is doing and what to do when it asks you something.

**The intent** is the only artifact written by interview. **Nothing happens until you answer the
questions** — the intent is written from your answers, so there is nothing to interrogate before then.
Expect to be asked questions rather than handed a draft, and
expect a request shaped as a solution to be turned back into a problem: "add a cache" becomes "the
dashboard takes fourteen seconds and people stop opening it". If you have already decided something, say
so — it is recorded under `Fixed decisions` with your name on it rather than argued with.

The skill **hands back rather than continuing into the spec.** That hand-back is the human-in-the-loop
point; a skill that chained on would remove it while appearing helpful.

```
/sdlc:spec 20260929_1456_32
```

Reads `intent.md` and nothing else — not the conversation, not the request that started the cycle, not
the codebase. Every criterion has to be able to fail, and its check has to be one this solution's testing
layers can actually run.

```
/sdlc:plan 20260929_1456_32
```

Unlike the spec, this one **must** read the codebase: a file table invented from the spec alone is the
fastest way to a plan that cannot be built. It also runs `sdlc stack`, and compares what the solution
declares it is built from with what is there. A build settings file the stack takes and the repository
lacks is closed by this plan only if the intent asked for it; otherwise the plan names it and leaves
it.

**It is the last document before you approve, so it is the one to read closely** — and the thing to
read for is proof adequacy. Ask of each check: *what would have to be true for this to pass while the
criterion is still unmet?* If that question has an answer, the proof is not adequate, and an inadequate
proof is worse than none because it produces a green verification instead of an obvious gap.

**If you change your mind mid-cycle**, do not edit the documents by hand:

```
/sdlc:steer 20260929_1456_32 "use the existing cache abstraction rather than a new one"
```

That edits the intent, spec and plan rather than regenerating them, records the correction with your name
on it, and re-interrogates everything downstream of what it touched. Steering changes *how*, not *what* —
a correction that changes the problem is a different cycle, and the skill says so and offers one.

## 4. The gate

One gate, covering all three documents, immediately before the build.

```pwsh
sdlc draft approval 20260929_1456_32
```

It writes the complete file — not a fragment — to
`.sdlc/cycles/<id>/approval.approved.draft.md`, and prints it with the rename that records it:

```pwsh
Move-Item approval.approved.draft.md approval.md
```

**The rename is the decision, and it is yours.** No tool places that file: the hook denies every agent
write to the ledger by path and by shell command alike, including every spelling of a move onto it,
there is no flag and no approve command for an exception to live in, and that is precisely what makes a
recorded decision mean something. A draft nobody renamed records nothing.

Where a decision already stands, the draft reproduces it above the new entry, so the rename replaces the
ledger without losing what it held. The printed instructions say how many it carries.

The entry records a hash of each of the three documents. **If any of them changes afterwards the gate
refuses** until it is approved again.

It refuses to print at all while a blocking finding is open. **A blocking finding cannot be waived by
any mechanism** — it is answered, or the cycle does not advance. `handling-blockers.md` is how.

Refusing is drafted on the same terms as approving — `--changes-requested`, `--deny`, `--reopen` — and
leaves the reason line empty for you, because the gate rejects a refusal that says nothing about why.
Helping with the easy case while withholding help from the hard one is the asymmetry people resent.

Then check it took:

```pwsh
sdlc gate 20260929_1456_32
```

Eleven things can refuse here, each with a different next step. `handling-refusals.md` lists them all
with the remedy for each.

### Admit the plan to the integration branch

When the solution has an integration branch — `development`, unless `solution.md` names another — the
approved plan goes there by its own pull request before anything is built:

```
/sdlc:pr 20260929_1456_32 --plan
```

It asks the gate first, pushes `cycle/<id>/plan`, and opens the pull request so that merging it deletes the
branch. **It is admission, not a second approval**: the rename above was the approval. Its check reads what
the pull request changes, and a pull request carrying only the cycle's planning documents gets the
admission check — the documents, the approval and its hashes, no build and no verify. One source file in
it and the full chain runs instead.

Once it merges, the plan is queued: `sdlc cycles --queued` lists it, and a batch run or the factory picks it
up. With no integration branch, skip this — the build starts from the plan branch, and one pull request
carries the plan and the code together.

## 5. Build

```
/sdlc:build 20260929_1456_32
```

The first stage that writes code and the first you are not expected to watch. It asks the gate and stops
if the gate refuses, then puts itself on `cycle/<id>/build` with `sdlc branch` — from the integration
branch once the plan is admitted there, from the plan branch before that. It implements what the plan decided **and only that** — something worth fixing that
the plan did not describe gets left alone and reported as a suggestion, because creep inside an approved
file is what reaches review dressed as approved work, where it is least likely to be questioned.

It checks the suite actually ran rather than that the runner exited zero. A trial cycle lost its whole
verification to `dotnet test` exiting zero without running anything, because the runner was not
configured.

**The two binary commands in this stage are the skill's, not yours.** It runs both itself; they are
written out here because what each records is what verify and the pull-request check later read, and a
cycle that lost one of them fails at the gate rather than here.

On a new solution's first cycle, the skeleton arrives first, because the plan declared it. In an
existing codebase the same command writes no skeleton — only the build settings files the plan declared,
when it declared any:

```pwsh
sdlc scaffold 20260929_1456_32
```

The five build settings files, a solution file, six projects — four under `src/` and two test projects,
one of them the architecture suite — and a reference slice written in the shape every operation should
have, including the one conversion every response leaves through, which is what makes the banned
response factories a rule that can fire rather than a file that looks like one. It refuses unless this
cycle is approved, so the skeleton is a change like any other, and it writes only what is absent — a
second run keeps what is there rather than overwriting it. Every cycle after this one writes its own code.

Then the code is committed, and the evidence recorded from it:

```pwsh
sdlc evidence 20260929_1456_32 --tests <the directory holding the results files> --build-log <path>
```

Records the diff, the commit it is measured from, the runner's own output, and one normalised test summary
into `evidence/`. This exits zero even when tests failed: recording what happened succeeded, and what to
make of it belongs to verify. A non-zero exit here would make the build step look like the thing that
refused.

**Point `--tests` at the directory, not at one file.** Each test project writes its own results, and a
summary built from one of them is a verification reading part of the suite.

**Then the evidence directory is committed too, and that is a second commit rather than an afterthought.**
Verify and the pull-request check both read the evidence out of the repository, so evidence that exists
only in a working tree is evidence the chain cannot see — and the failure surfaces two stages later as a
chain that does not hold.

## 6. Verify

```
/sdlc:verify 20260929_1456_32
```

A fresh process again, and the only artifact whose entire value is being derived. **It reads
`tests.json`, `diff.patch` and `base.sha`, and does not run the tests** — running them would produce new
evidence instead of adjudicating the build's, and the next reader could not tell which run the verdict
describes. It does not read the source to decide whether a criterion is met either: reading the
implementation and concluding it looks correct is the most tempting way to write a claim with no evidence
behind it.

Per criterion: which test did the plan name, does the summary say it ran and passed, and **does that test
actually establish the criterion?** The third is where judgement lives, and where a verification may
disagree with its own plan — a test that passed but does not reach its criterion is *not met*.

It writes `pass` or `blocked`. On `pass`, go on to the pull request. On `blocked`,
`handling-blockers.md` is the next step: remediation, and what happens when it runs out of progress.

## 7. Pull request

```pwsh
sdlc pr-ready 20260929_1456_32
```

```
/sdlc:pr 20260929_1456_32
```

The only stage that reaches outside the repository, and it asks before every push. Everything before this
is local and reversible; a pull request notifies people, appears in a queue, and withdrawing it is
something somebody has to notice happened.

It pushes the cycle's own branch and targets the integration branch, or the default branch while there is
none. **A person merges it**, squashed, one pull request at a time. When the next one conflicts, or goes red
because an earlier merge broke it, `/sdlc:resolve-conflict <id>` brings it up to date inside the cycle's
declared files and verifies it again.

**It checks first whether there is anywhere to push.** A solution recorded as `local`, or one whose
`git remote -v` prints nothing, stops here — the one refusal that is not about the cycle, because the
work may be perfectly ready and have no destination. The skill walks through creating the remote
rather than creating it for you, and says the part that is easy to miss: a remote alone installs no
guarantee, so adoption is re-run afterwards to get the pipeline. This is the cost of adopting before the
remote existed, and `adopting-the-framework.md` is where it is avoided.

The body is the cycle rather than a summary somebody invented: the problem in the intent's own words, the
criteria with the verification's verdict on each, the tier, the risk section, and anything still in an
`## Open` region — because a reviewer should meet the limits before they approve.

**It is not approving the merge.** A code owner does that, and never the agent that wrote the change. The
local approval defended against an agent that forgets; the review is what defends against a human who did
not look, and they are different jobs.

## Afterwards

```pwsh
sdlc suggestions
```

What was noticed and not acted on. Non-blocking is not trivial — two real ones from a single trial cycle
were "the suite did not actually run" and "this criterion's proof rests entirely on a substituted
source". Both substantive; neither cited a rule that forbade the current state, which is the only thing
that made them suggestions.

Once a few cycles have run, the other half of the same question:

```pwsh
sdlc propose constraints
```

Not what is outstanding, but what keeps happening. It reads every cycle's findings — including the
blockers that were answered, which `suggestions` deliberately never shows — and reports the rules cited
in three or more **distinct** cycles, with the cycles and finding ids behind each. Two is coincidence;
three is a pattern. On a first cycle it has nothing to say and prints the next free `LOC_` id with its
four parts empty instead, which is also how a rule gets added by hand before any history exists.

It proposes and never places. Pasting an accepted entry into `.sdlc/constraints.md` is a person's job,
because a cycle judged by the constraints it is changing is the same deadlock as the framework governing
itself.

With several cycles queued, `/sdlc:batch-build --pr` completes them all, each on its own
`cycle/<id>/build-unattended` branch and independently, opens a pull request for each — a draft for one
that did not pass — and ends with the sitrep:

```pwsh
sdlc sitrep
```

It is the morning's page, and worth running on its own: what is ready to merge and in what order, which
pull requests are expected to conflict, what needs a person, what was held back and why, and what is
waiting and going stale. Everything unattended rests on the remediation bound — a cycle that stops making
progress stops rather than reaching a ready pull request — so the first batch runs are worth watching.

## Where to go next

| | |
|---|---|
| this same walk with one solution in it, end to end | `../samples/rolling-a-die.md` |
| a blocking finding, or a blocked verification | `handling-blockers.md` |
| what to do when something refuses | `handling-refusals.md` |
| this walk as fourteen numbered lines, then every command on one screen | `../cheat-sheets/commands.md` |
| what this solution is built from, and how to change it | `declaring-the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
