# The seventeen skills

Every skill in the plugin: what it is, how it is invoked, when it runs, and who decides it should.

**A command is deterministic and the binary owns it; a skill needs language and an agent runs it.**
That split is why this document exists separately from `sdlc help` — the binary's own help lists what a
file listing and a hash can decide, and everything here is what cannot be decided that way. A skill
calls commands and shows what they print verbatim.

**How a skill starts depends on the host, and this is the one place the two differ.** In Claude Code you
name it. On a client that loads the skills through [Agent Plugins](https://agent-plugins.org/specification)
— Copilot, VS Code, Cursor, Codex, Gemini — there is no slash form: the client matches your request
against each skill's `description` and asks before activating one. So `/sdlc:spec 20261002_1414_12` there
is "write the spec for cycle 20261002_1414_12", and the cycle id still has to be in the sentence because
nothing else carries it.

In Claude Code, `/sdlc:<name>`:

```
/sdlc:intent We have no way to see a die roll
/sdlc:interrogate 20261002_1414_12 spec
```

Typing a stage name at the binary is answered rather than refused — `sdlc spec <id>` says it is a skill
and prints the invocation — because a tool must never send somebody somewhere it does not go.

## The whole set

| Skill | What it is | Takes | When | Who starts it |
|---|---|---|---|---|
| `init` | adopts the framework into a repository | `[--platform github\|azure-devops]` | once per repository | **a person**, and the hook denies an agent |
| `intent` | interviews a person into the problem the change exists to solve | `[a sentence]` | opening a cycle | a person |
| `spec` | derives testable acceptance criteria from the intent | `<id>` | the intent has a clean round | a person |
| `plan` | decides how, and what will prove each criterion | `<id>` | the spec has a clean round | a person |
| `interrogate` | judges one artifact against the rules, in a fresh process. Never edits | `<id> <artifact>` | an artifact has just been written or changed | the stage skill's loop, or a person |
| `refine` | applies an artifact's findings to that artifact. Never judges | `<id> <artifact>` | a round raised blocking findings | the stage skill's loop, or a person |
| `steer` | carries a mid-cycle correction down the chain | `<id> "what to change"` | a person changes their mind before the gate | a person |
| `draft` | prints what belongs in a file only a person writes | `approval <id>`, and three more | at the gate, to stop a cycle, or to add a rule | a person |
| `build` | implements the plan, builds, tests, records the evidence | `<id>` | the gate is satisfied | a person, or `batch-build` |
| `verify` | adjudicates the build against the spec, from the evidence alone | `<id>` | the build recorded evidence | a person, or `batch-build` |
| `remediate` | applies a blocked verification's findings to source and tests | `<id>` | verify recorded `blocked` | a person, or `batch-build` |
| `land` | pushes, opens the pull request, links the work item | `<id> \| --all` | a verification passed | **a person**, asked before every push |
| `batch-build` | completes every approved cycle, concurrently where the claims allow | `[--land]` | several cycles are approved and waiting | a person |
| `resolve-conflict` | resolves a collision on a file no cycle can claim exclusively | `<file path>` | a batch run reports one | a person, or `batch-build` |
| `status` | shows where a cycle has got to | `<id> [artifact]` | after any stage, or when somebody asks | anyone, constantly |
| `constraints` | shows the rules in force, all tiers resolved | `[artifact]` | before writing or judging anything | every stage skill, as its first step |
| `suggestions` | lists the open non-blocking findings | `[id]` | looking for what was noticed and not acted on | a person |

**"Who starts it" is not "who runs it".** Every one of these is executed by an agent — that is what a
skill is. What differs is who decides it should happen, and two of them are a person's decision in a way
the framework enforces rather than requests: `init` writes the hook configuration that constrains the
agent, so the hook denies an agent the command; `land` is the only stage that reaches outside the
repository, so it asks before every push.

**All seventeen are model-invocable.** Nothing in the manifest stops an agent reaching for one on its
own, and that is deliberate — what protects the decisions is the hook denying the four protected files
and `sdlc init`, plus the pull-request check, rather than a flag in a skill's frontmatter. A guard that
lives in the manifest is a guard the manifest can lose. `guides/when-it-blocks.md` has what the hook
actually denies today, and what it does not.

---

## Adoption

### `init`

**Adopts the framework into a repository**: runs the scaffolding, interviews the team about what matters
in this solution, and proposes its first constraints.

Three modes, decided by what it finds: **greenfield** for a repository holding no code, **onboard** for
existing code with no `.sdlc/`, **update** when `.sdlc/` already exists. Update refreshes only what the
framework owns, which is everything not protected — hook configs, the pipeline, `.sdlc/README.md`.

**A person runs it, and the hook denies an agent the command.** The skill's part is the two halves a
command cannot do: asking what matters here, and proposing the `LOC_` rules that follow from the answers.
It asks the person rather than reading the codebase, because reading an existing repository to decide
what to forbid would make adoption a judgement on code that predates the framework — and the first cycle
would spend itself on findings about work nobody is defending.

Refuses outside a git repository. Without git, a build cannot record the diff a verification derives
from, and the gates key on a branch that cannot exist — so every hook would permit everything while the
artifacts still looked governed.

---

## The planning chain

Three artifacts, all required, always. Each is written, challenged in a fresh process against the rules
in force, and revised until a round raises nothing blocking. A stage will not run until the artifact
before it has such a round.

### `intent`

**Interviews a person into a governed intent** — why the work exists. The only artifact written by
interview, and the skill's job is to elicit rather than to draft.

It opens the cycle with `sdlc new`, which allocates the id and creates the directory — creating it *is*
the collision check. It converts a request that arrived shaped as a solution ("add a cache") into a
problem ("the dashboard takes fourteen seconds and people stop opening it"), and records a decision
already taken under `Fixed decisions`, with attribution, rather than discarding it.

Then it runs the interrogate-refine loop itself and hands back. It does not chain into the spec: that
hand-back is the human-in-the-loop point, and a skill that continued would remove it while appearing
helpful.

### `spec`

**Derives testable acceptance criteria from the intent** — what done means.

It reads `intent.md` as its only input: not the request that started the cycle, not the conversation, not
the codebase for requirements. Every criterion has to be able to fail, and its check has to be one this
solution's testing layers can actually run. Every entry under the intent's `## Open` must end up in
exactly one of three places — a criterion, `Out of scope` with a reason, or the spec's own `## Open`
citing the intent item.

Refuses when the intent is missing or not clean, and when a spec already exists.

### `plan`

**Decides how the spec will be met and what will prove it.** The last document written before a person
approves, so it is the one a reviewer reads most closely.

Unlike the spec, this stage must read the codebase — a file table invented from the spec alone is the
fastest way to a plan that cannot be built. It holds two kinds of claim and only one is judged: the
approach in `Shape and departures` is a decision, and the file table is an estimate that nothing gates.

The constraint to watch is proof adequacy. *What would have to be true for this check to pass while the
criterion is still unmet?* If that question has an answer, the proof is not adequate yet — and an
inadequate proof is worse than none, because it produces a green verification instead of an obvious gap.
Each proof entry names its test in backticks, and the pull-request check reads the names out and confirms
each one ran and passed.

---

## The loop

### `interrogate`

**Judges one artifact against the constraints in force and writes a findings round. Never edits.**

**The fresh process is the whole mechanism.** The reader must not have seen the artifact being made, so
the judgement runs in a new process — not in the authoring session, and not as a subagent. A tool call
runs in its own process while the model calling it is still the author; a new process creates a new
reader rather than handing data to the one you already have.

What that buys is the removal of *investment bias*: the reader never saw the interview, the drafts or the
author's reasoning, so it has nothing to defend. What it does not buy is independence from *model bias* —
a borderline judgement flips the same coin twice, so a round is a second reading by a reader with no
stake, never independent verification.

Rounds accumulate and are never rewritten. A finding keeps its id across rounds, and every round records
`upstream_sha256` — the hash of the artifact above it, as the reader saw it. That is what makes a stale
judgement detectable when an upstream document is edited afterwards.

### `refine`

**Applies an artifact's findings to that artifact. Edits one planning document and nothing else, and
never judges.**

The separation from `interrogate` is the point: an interrogation that could also fix things would grade
its own corrections.

It may disagree with a finding — not every finding is right. What it may not do is answer one by making
the artifact vaguer, which is the single move that removes a problem from the page while leaving the
belief in it. "Fourteen seconds" becoming "slow" is not a fix.

Every finding ends in exactly one of three exits, and there is no fourth: **healed** (the artifact
changes), **assumed** (recorded in the decisions region, marked unconfirmed), **asked** (recorded in
`## Open`, and the skill stops and asks). Never assume silently — a report is read once, and a section is
read every time the artifact is read.

Refuses once the cycle is approved: the plan is hash-locked at that point, so editing it would break the
gate rather than satisfy it. The recovery is a recorded reopen.

---

## Correction

### `steer`

**Carries a person's mid-cycle correction down the artifact chain**, editing the intent, spec and plan
rather than regenerating them.

It exists because of cost. *"Use the existing cache abstraction rather than a new one"* arrives mid-cycle,
and doing that by hand means editing and re-interrogating three documents. Nobody pays it, so the
correction ends up in conversation where it is lost — and a path so costly that it pushes information out
of the system is worse than no path.

**Steering changes how, not what.** Which approach, which abstraction, which order, what to avoid. A
comment that changes the problem is a different intent, and the skill says so and offers a new cycle.

It records the correction as a fixed decision with attribution, edits rather than regenerates — a
regenerated artifact has a findings history about a document that no longer exists — and re-interrogates
everything downstream of what it touched. That last part is checked rather than trusted: a round records
the hash of the artifact above it, so editing the intent makes the spec's latest round stale.

---

## The gate

### `draft`

**Prints what belongs in one of the four files only a person writes.** One rule: a tool never writes
them, a tool prints what to put in them.

| Drafter | What it is for |
|---|---|
| `draft approval <id>` | the gate decision, covering all three planning documents, with a hash of each |
| `draft abandon <id>` | why a cycle stopped. Terminal, and the reason is left empty because the file is rejected without one |
| `draft constraint` | the next free `LOC_` id with its four parts empty, and the three admission tests |
| `draft profile <name>` | both `solution.md` keys, not only the one that changes |

Every agent write to `approval.md`, `abandoned.md`, `constraints.md` and `solution.md` is denied at the
tool call — by file path and by shell command alike. There is no flag, no mode, and no approve command for
an exception to be carved out of, which is why the rule states in one sentence and has nowhere for a
bypass to live. So the skill does not offer to paste it and does not read the refusal as a configuration
problem: **a pasted approval means something precisely because no tool could have produced it.**

A refusal is drafted on the same terms as an approval — `--changes-requested`, `--deny`, `--reopen` —
because rejecting is what a person does when they are least inclined to look a format up, and helping
with the easy case while withholding help from the hard one is the asymmetry people resent.

`draft approval` refuses to print while a blocking finding is open. **A blocking finding cannot be waived
by any mechanism**: there is no field for it in the entry and no refusal code at the gate that acceptance
would clear. It is answered, or the cycle does not advance.

---

## Build and verify

### `build`

**Implements an approved plan, builds, tests, and records the evidence a verification will read.** The
first stage that writes code, and the first a person is not expected to watch.

It asks `sdlc gate <id>` and stops if the gate refuses — there are eight ways it can, and each has a
different next step. It implements what the plan decided and only that: creep inside an approved file is
what reaches review dressed as approved work, which is where it is least likely to be questioned.
Something worth fixing that the plan did not describe gets left alone and reported as a suggestion.

Warnings are errors, enforced by the build props rather than by instruction. And it checks that the suite
actually ran: a trial cycle lost its whole verification to `dotnet test` exiting zero without running
anything, because the runner was not configured.

**The turn end is checked as well**, on both hosts. A hook asks the gate before the agent finishes
and sends the turn back on three things: recorded evidence showing a failing suite, code written with no
evidence recorded at all, and code that has moved since the evidence was recorded. It is not a rule the
skill has to remember — it is why there is no point reporting done over a red build.
`guides/when-it-blocks.md` has the three send-backs and the holes the gate states rather than hides.

Then `sdlc evidence <id>` records the diff, the commit it is measured from, the runner's own output, and
one normalised test summary. **Verify reads only these files**, so a verdict derived from the builder's
recollection is a verdict nobody can check.

### `verify`

**Adjudicates the build against its spec, from the recorded evidence alone. Never edits code.**

The only artifact whose entire value is being derived. It reads `tests.json`, `diff.patch` and `base.sha`
rather than running the tests — running them would produce new evidence instead of adjudicating the
build's, and the next reader could not tell which run the verdict describes. It does not read the source
to decide whether a criterion is met: reading the implementation and concluding it looks correct is the
most tempting way to write a claim with no evidence behind it.

Per criterion: which test did the plan name, does the summary say it ran and passed, and does that test
actually establish the criterion? The third question is where judgement lives, and where the verification
may disagree with the plan — a test that passed but does not reach its criterion is **not met**.

It writes `pass` or `blocked`. **`escalated` is computed, not declared**: it is what the remediation
budget running out makes of a blocked cycle.

### `remediate`

**Applies a blocked verification's findings to source and tests. Edits code, never a planning document,
and gets at most three passes.**

`refine` and `remediate` are two editors with opposite guards, and the asymmetry is the whole design: if a
failing test could cause an agent to adjust the spec, the criteria become whatever the code happens to do.
So when the honest conclusion is "the criterion is wrong", the skill says it and stops. The recovery is
`sdlc draft approval <id> --reopen`, which withdraws the approval with a recorded reason.

**Three passes, then the cycle stops.** Not three then try harder. A pass is a verify findings round after
the first, so the count is read from what happened rather than from a counter anybody could edit. That
stopping is the only reason an unattended batch is safe to run at all.

It does not verify its own remediation. A verification by the session that wrote the fix is the investment
bias the whole separation exists to remove.

---

## Landing and unattended runs

### `land`

**Pushes a verified cycle and opens its pull request.** The only stage that reaches outside the
repository.

Everything before this is local and reversible; a pull request notifies people, appears in a queue, and
withdrawing it is something somebody has to notice happened. So it asks `sdlc landable` first, every
time, and does not land past a refusal — the pull-request check would refuse it anyway, so pushing would
cost a round trip through CI to learn what it was just told.

The body is the cycle rather than a summary somebody invented: the intent's problem in the intent's words,
the criteria with the verification's verdict on each, the tier, the risk section, and anything in an
`## Open` region, because a reviewer should meet the limits before they approve.

**It is not approving the merge.** A code owner does that, and never the agent that wrote the change. The
local approval defended against an agent that forgets; the review is what defends against a human who did
not look, and they are different jobs.

With `--all` it lists every cycle and what each one is, then asks once before the first push. The question
is about volume rather than permission: every cycle that ever passed is eligible, including one from last
week that somebody has forgotten, and "land 8 cycles?" is a question nobody can answer honestly without
the list.

### `batch-build`

**Completes every approved cycle in this working copy** — builds and verifies each, concurrently where
their declared files do not collide. A local skill, executed rather than subscribed to: there is no shared
queue and no daemon.

**Everything here rests on the remediation bound.** A cycle that cannot be made to pass stops at three
passes and escalates, and that stopping is the only reason this is safe to run without watching.

It reads the claim graph from `sdlc claims` rather than working it out: the graph is a set intersection
over a file table, which the binary does the same way every time, and a graph derived by reading plans
would come out slightly differently each run while deciding what runs concurrently. Groups run in
parallel, each cycle in its own worktree; cycles in different groups collide, so they serialise in
cycle-id order.

**It schedules around collisions rather than resolving them.** A merge you did not have to do is worth
more than a merge you did well. A sibling's failure does not discard the cycles that completed — each can
be landed individually.

Plain `batch-build` never pushes. `--land` composes the two and asks once before the run rather than at
the end, because the question is about volume and a person should answer it before waiting rather than
after.

### `resolve-conflict`

**Resolves a collision on one of the few files no cycle can claim exclusively**: project files and
`Directory.Packages.props`, dependency-injection registration, `appsettings`, EF Core migrations,
Terraform near state.

**Enumerated rather than discovered.** A skill that resolved whatever happened to conflict would quietly
become the thing that merges real code, and the moment it does, scheduling around collisions stops being
the strategy. A conflict on a file not in the list is a scheduling failure to report, not a merge to
perform.

These files are additive by nature, which is why they can be resolved at all: two cycles each add a
package, a registration, a setting, and the resolution is to keep both. The same key with two different
values is not additive and not the skill's to settle — that is two plans disagreeing about the solution.

Migrations do not come here. Two branches that each add a migration snapshot the same parent model, and
the merged chain compiles and then fails at deploy, which is the worst shape a failure takes. The
resolution is to rebase and regenerate, and a batch run should have serialised them.

---

## Reading and deciding

### `status`

**Shows where a cycle has got to** — the stage panel, its checks, the gate, and the literal next command.

The binary prints the panel, not the model. The format is a contract, and a skill that reformatted it
would make the thing people actually read drift between hosts. Without a stage argument the panel is for
whichever artifact the cycle is waiting on, which is what a person wants after any stage finishes.

**What it will not tell you, on purpose: whether to keep interrogating.** That judgement has been wrong
before — on one real intent, round seven found the headline change was unreachable and round eleven found
that no success criterion failed without it. A tool that suggested stopping at round four would have been
confidently wrong.

### `constraints`

**Shows the rules in force for this solution, all tiers resolved** — the global `CON_` set, the profile's
`PRO_`, and the solution's own `LOC_`, in that order, so a solution's own rules are the last word.

Running this is the first step of every skill that writes or judges an artifact, and it is not a
convenience: the constraints live in the framework and are never copied into a solution, so a session that
did not run it is working from recollection.

### `suggestions`

**Lists the open non-blocking findings a solution has accumulated.**

Non-blocking is not trivial. Two real ones from a single trial cycle: `dotnet test` did not actually run
the suite because the runner was not configured, and a criterion's proof rested entirely on a substituted
source. Both were substantive; neither cited a rule that forbade the current state, which is the only
thing that made them suggestions.

No ranking and no ages, because a suggestion's age says nothing about its worth and a tool that sorted by
age would quietly argue that the oldest matters most.

**This is the whole of the learning loop today.** Iteration 2 built a corpus with computed signals across
cycles; iteration 3 removed them and recorded the cost plainly. Nothing aggregates automatically, and that
is a deliberate deferral rather than an oversight.

---

## Where to go next

| | |
|---|---|
| installing the plugin and the binary | `guides/install.md` |
| a cycle from an empty directory to a pull request | `guides/first-cycle.md` |
| every command for one cycle, on one screen | `guides/cycle-commands.md` |
| what to do when something refuses | `guides/when-it-blocks.md` |
| the design, and every decision behind it | `../CONCEPT.md` §15 |
| a real cycle's artifacts, to calibrate against | `../reference/iteration-3-cycle/` |
