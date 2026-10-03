# A first cycle

From an empty directory to a pull request. One cycle, every stage, and what each one refuses if you
arrive at it early.

**This assumes both halves are installed** — `install.md` covers that, and `sdlc version` is how you
know. Everything below is one cycle in one repository; nothing here is specific to what the change is.

## 1. Adopt the repository

Adoption is a decision about how a team works, so **you run this and an agent cannot.** The pre-tool hook
denies `sdlc init` to an agent by command rather than by file path, which is why asking your agent to run
it produces a refusal rather than a cycle.

```pwsh
git init
```

```pwsh
sdlc init
```

It picks its own mode from what it finds: **scaffold** for a repository holding no code, **onboard** for
existing code with no `.sdlc/`, **update** when `.sdlc/` is already there. Update refreshes only what the
framework owns — the hook configs, the pipeline, `.sdlc/README.md` — and never the four files only a
person writes.

It writes and then **commits** what it wrote. That is deliberate: an untracked file is untracked when the
first cycle takes its diff, so the diff would record the whole file as new and a plan saying it changes
that file could never match. The first cycle would fail for something adoption did.

What lands:

| | |
|---|---|
| `.sdlc/solution.md` | what this solution is and which profile governs it. **A person's file** |
| `.sdlc/techstack.md` | the stack, for a session that has not read the code |
| `.sdlc/README.md` | the whole flow in one page, inside the solution |
| `.sdlc/hooks/pre-push` | written, **not installed**. See below |
| `.claude/settings.json` | the `PreToolUse` hook that calls the gate |
| `.github/hooks/sdlc.json` | the same policy, in Copilot's shape |
| `.github/workflows/pull-request.yml` | `sdlc verify-chain`. **The only guarantee** |

And, in **scaffold** mode only, the files that decide how code is written here — the profile's defaults,
yours to amend:

| | |
|---|---|
| `.editorconfig` | the conventions, including the ones only an IDE enforces |
| `Directory.Build.props` | warnings as errors, the analyser level, the banned-API list |
| `Directory.Packages.props` | central package versions, so a project cannot pick its own |
| `global.json` | the SDK, and the test runner without which the suite silently does not run |
| `tests/Directory.Build.props` | the six rules relaxed for test code, each with its reason |

An **onboard** gets none of them: the profile proposes a shape for new work and never rewrites the
conventions of a codebase that predates it.

**No code lands, and no projects.** Adoption writes what governs code and stops there. The solution file,
the projects, the architecture tests and the reference slice arrive in your first cycle, behind an intent,
a spec, a plan and an approval like everything else — otherwise the first code in a governed repository
would be the only thing in it with no chain behind it.

**The pre-push hook is written and not installed**, because a hook under `.git` would be a file the
framework placed somewhere git does not track and nobody would find later. Install it yourself:

```pwsh
git config core.hooksPath .sdlc/hooks
```

**Then do the one thing adoption cannot do for you: make the check required.** The pipeline step is the
whole promise — nothing merges unless the chain is complete, approved and hash-matched — and it is a
promise only while branch policy requires it. Until then the file is advisory and the directory looks
enforced without being enforced, which is the failure mode this design is written against.

Finally, the interview:

```
/sdlc:init
```

The command did the mechanical half. The skill asks what matters in this solution and proposes its first
`LOC_` constraints. It asks you rather than reading the codebase on purpose: reading an existing
repository to decide what to forbid would make adoption a judgement on code nobody is defending, and the
first cycle would spend itself on findings about work that predates the framework.

## 2. Read the rules before writing anything

```pwsh
sdlc constraints
```

The global `CON_` set, the profile's `PRO_`, then this solution's own `LOC_` — in that order, so a
solution's rules are the last word. **Constraints live in the framework and are never copied into a
solution**, so this command is how they reach a session. Every stage skill runs it first, and one that
did not is working from recollection.

## 3. Open the cycle

```pwsh
sdlc new
```

It prints the id on standard output, so a skill can capture it, and everything you need on standard error
beside it. Creating the directory *is* the collision check: at allocation there is no directory to be
already there, so a look-then-create would leave the race it was written to close.

Switch to the branch it names:

```pwsh
git switch -c sdlc/20260929_1456_32
```

## 4. Intent, spec, plan

Three artifacts, all required, always. Each one is written, challenged in a **fresh process** against the
rules in force, and revised until a round raises nothing blocking.

```
/sdlc:intent The dashboard takes fourteen seconds and people have stopped opening it
```

The only artifact written by interview. Expect to be asked questions rather than handed a draft, and
expect a request shaped as a solution to be turned back into a problem: "add a cache" becomes "the
dashboard takes fourteen seconds and people stop opening it". If you have already decided something, say
so — it is recorded under `Fixed decisions` with your name on it rather than argued with.

The skill runs the interrogate-refine loop itself and **hands back rather than continuing into the
spec.** That hand-back is the human-in-the-loop point; a skill that chained on would remove it while
appearing helpful.

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
fastest way to a plan that cannot be built. It is the last document before you approve, so it is the one
to read closely — and the thing to read for is proof adequacy. Ask of each check: *what would have to be
true for this to pass while the criterion is still unmet?* If that question has an answer, the proof is
not adequate, and an inadequate proof is worse than none because it produces a green verification instead
of an obvious gap.

**On the loop.** Rounds accumulate and are never rewritten. A finding keeps its id across rounds, and
every round records the hash of the artifact above it as the reader saw it — which is what makes a stale
judgement detectable when you edit an upstream document afterwards.

**How many rounds is deliberately not a question the tool will answer.** On one real intent, round seven
found the headline change was unreachable and round eleven found that no success criterion failed without
it. A tool that suggested stopping at round four would have been confidently wrong.

**If you change your mind mid-cycle**, do not edit the documents by hand:

```
/sdlc:steer 20260929_1456_32 "use the existing cache abstraction rather than a new one"
```

That edits the intent, spec and plan rather than regenerating them, records the correction with your name
on it, and re-interrogates everything downstream of what it touched. Steering changes *how*, not *what* —
a correction that changes the problem is a different cycle, and the skill says so and offers one.

## 5. The gate

One gate, covering all three documents, immediately before the build.

```pwsh
sdlc draft approval 20260929_1456_32
```

It **prints** the entry. **You paste it** into `.sdlc/cycles/<id>/approval.md`. No tool writes that file:
the hook denies every agent write to it by path and by shell command alike, there is no flag and no
approve command for an exception to live in, and that is precisely what makes a pasted approval mean
something.

The entry records a hash of each of the three documents. **If any of them changes afterwards the gate
refuses** until it is approved again.

It refuses to print at all while a blocking finding is open. There is no field for waiving one and no
refusal code at the gate that acceptance would clear: **a blocking finding cannot be waived by any
mechanism.** It is answered, or the cycle does not advance.

Refusing is drafted on the same terms as approving — `--changes-requested`, `--deny`, `--reopen` — and
leaves the reason line empty for you, because the gate rejects a refusal that says nothing about why.
Helping with the easy case while withholding help from the hard one is the asymmetry people resent.

Then check it took:

```pwsh
sdlc gate 20260929_1456_32
```

Eight things can refuse here, each with a different next step. `when-it-blocks.md` has all eight.

## 6. Build and verify

```
/sdlc:build 20260929_1456_32
```

The first stage that writes code and the first you are not expected to watch. It asks the gate and stops
if the gate refuses. It implements what the plan decided **and only that** — something worth fixing that
the plan did not describe gets left alone and reported as a suggestion, because creep inside an approved
file is what reaches review dressed as approved work, where it is least likely to be questioned.

It checks the suite actually ran rather than that the runner exited zero. A trial cycle lost its whole
verification to `dotnet test` exiting zero without running anything, because the runner was not
configured.

On a new solution's first cycle, this is also where the skeleton arrives, because the plan declared it:

```pwsh
sdlc scaffold 20260929_1456_32
```

A solution file, a core project, an HTTP project, two test projects, and a reference slice written in
the shape every operation should have -- including the one conversion every response leaves through,
which is what makes the banned response factories a rule that can fire rather than a file that looks
like one. It refuses unless this cycle is approved, so the skeleton is a change like any
other, and it writes only what is absent — a second run keeps what is there rather than overwriting it.
Every cycle after this one writes its own code.

```pwsh
sdlc evidence 20260929_1456_32
```

Records the diff, the commit it is measured from, the runner's own output, and one normalised test summary
into `evidence/`. This exits zero even when tests failed: recording what happened succeeded, and what to
make of it belongs to verify. A non-zero exit here would make the build step look like the thing that
refused.

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

It writes `pass` or `blocked`. On `blocked`:

```
/sdlc:remediate 20260929_1456_32
```

Edits source and tests, never a planning document, and gets **at most three passes**. Not three then try
harder. If the honest conclusion is that a criterion is wrong, it says so and stops rather than adjusting
the spec to match the code — if a failing test could cause an agent to edit the spec, the criteria become
whatever the code happens to do. The recovery is `sdlc draft approval <id> --reopen`, which withdraws the
approval with a recorded reason. It does not verify its own remediation.

`escalated` is computed rather than declared: it is what running out of budget makes of a blocked cycle.
An escalated cycle never lands.

## 7. Land

```pwsh
sdlc landable 20260929_1456_32
```

```
/sdlc:land 20260929_1456_32
```

The only stage that reaches outside the repository, and it asks before every push. Everything before this
is local and reversible; a pull request notifies people, appears in a queue, and withdrawing it is
something somebody has to notice happened.

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

With several cycles approved and waiting, `/sdlc:batch-build` completes them all, concurrently where
their declared files do not collide. Everything unattended rests on the three-pass remediation bound, so
the first batch runs are worth watching.

## Where to go next

| | |
|---|---|
| every command for one cycle, on one screen | `cycle-commands.md` |
| what to do when something refuses | `when-it-blocks.md` |
| what each skill is and who starts it | `../skills.md` |
| installing, updating, removing | `install.md` |
| a real cycle's artifacts, to calibrate against | `../../reference/iteration-3-cycle/` |
