# A first cycle

From an empty directory to a pull request. One cycle, every stage, and what each one refuses if you
arrive at it early.

**This assumes both halves are installed** — `install.md` covers that, and `sdlc version` is how you
know. Everything below is one cycle in one repository; nothing here is specific to what the change is.

**If you have done this before, the short path at the top of `cycle-commands.md` is this same walk in
fourteen numbered lines** — the commands with none of the reasoning, which is what you want on the second
cycle rather than the first.

## 1. Adopt the repository

Adoption is a decision about how a team works, so **you run the command and an agent cannot.** The
pre-tool hook denies it to an agent by command rather than by file path, which is why asking your agent
to run it produces a refusal rather than a cycle.

```pwsh
git init
```

**Start with the skill rather than the command.**

```
/sdlc:init
```

It asks the one thing the command needs told and cannot guess — where the code will live — and then
hands you the line to run, `!` and all. On Claude Code that prefix runs
the rest of the line as your own command rather than as a tool call of the agent's, so the gate never
sees it and what it prints lands in the conversation where the agent can read the mode and the file list
back without you pasting them.

**The order is load-bearing, not politeness.** `solution.md` is created by the command and protected
the moment it exists, so an answer given after the fact costs a draft and a rename rather than a
sentence. The one that bites:

| | |
|---|---|
| `--platform` | `github`, `azure-devops`, or `local` for a repository with no remote yet. `local` writes no pipeline, so **no pull-request check governs a merge** and nothing mechanical decides what reaches the default branch |

**Adoption is not told what the solution is built from, and decides nothing about it.** There is no
profile to choose. The stack is the next step, below, and it is yours to declare.

**If the code will live on a remote, create the remote before adopting.** Re-running adoption once one
exists writes the pipeline, but `solution.md` is protected by then, so the platform keys are printed for
you to place by hand — and until they are placed the record still says `local`, which is what `doctor`
and the `pr` skill both read.

The command picks its own mode from what it finds: **greenfield** for a repository holding no code, **onboard** for
existing code with no `.sdlc/`, **update** when `.sdlc/` is already there. Update refreshes only what the
framework owns — the hook configs, the pipeline, `.sdlc/README.md` — and never the six files only a
person writes.

It writes and then **commits** what it wrote. That is deliberate: an untracked file is untracked when the
first cycle takes its diff, so the diff would record the whole file as new and a plan saying it changes
that file could never match. The first cycle would fail for something adoption did.

What lands:

| | |
|---|---|
| `.sdlc/solution.md` | what this solution is, where its code came from, and which conventions new work follows. **A person's file** |
| `.sdlc/README.md` | the whole flow in one page, inside the solution |
| `.claude/settings.json` | the `PreToolUse` and `Stop` hooks that call the gate, **merged** into whatever is already there |
| `.github/hooks/sdlc.json` | the same two moments, in Copilot's shape: `preToolUse` and `agentStop` |
| `.agents/hooks.json` | the tool-call moment in Antigravity's shape, in its `sdlc` section. Its turn end is deliberately not governed — `install.md` has why |
| `AGENTS.md` | how to work in this repository, written for an agent. Created if absent, printed for merging if you already had one |
| `.github/workflows/sdlc-verify.yml` | the chain check. **The only guarantee** — and written only where a platform was recorded. `--platform local` writes no pipeline at all |

**Every host's configuration is written whichever host you use.** A file is inert on a machine without
that host, and a teammate who opens the repository in a different agent is then governed without having
to adopt again.

Adoption rewrites a file only where the framework's own name is in the path, so `.sdlc/**` and the two
`sdlc`-named files above are refreshed on every run. `.claude/settings.json` is shared with you and with
the host: `init` adds or updates its two hook entries and keeps every other key, including your
permissions and your own hooks. If that file is not valid JSON — a comment, a trailing comma — nothing
is written to it and the entries are printed for you to place. On Azure DevOps the pipeline is
`azure-pipelines.yml`, which is the path that platform looks for rather than a name the framework owns,
so an existing one is printed rather than replaced. Nothing verifies a chain until it is in a pipeline
that runs, and `init` says so.

**What does not land: the declared stack, and the build settings.** Both belong to a stack nobody has
declared yet.

### Declare the stack

```pwsh
sdlc draft techstack
```

It writes `.sdlc/techstack.draft.md`: the framework's default, whole — every row it would pin, under
`## Rules` every stack rule it ships, each marked `true`, and under `## Build settings` the five files
below. **What ends up in the file is exactly what binds**, so cut it down to what is true of this
solution and rename it into place:

```pwsh
mv -f .sdlc/techstack.draft.md .sdlc/techstack.md
```

A row pins a choice; no row leaves it to each plan; a rule applies only where its row says `true`. To
pin nothing at all — when the stack is part of what the first cycles are working out — run it with
`--empty` and rename that. **No cycle opens until the file is placed**, and from then on it is a
person's file: `the-stack.md` is what it is and how to change it.

### The build settings

The files that decide how .NET code is written — yours to amend. They are rows in the declared stack,
one per file, and **a cycle brings them**; adoption writes none:

| | |
|---|---|
| `.editorconfig` | the conventions, including the ones only an IDE enforces |
| `Directory.Build.props` | warnings as errors, the analyser level, the banned-API list |
| `Directory.Packages.props` | central package versions, so a project cannot pick its own |
| `global.json` | the SDK, and the test runner without which the suite silently does not run |
| `tests/Directory.Build.props` | the six rules relaxed for test code, each with its reason |

**In a new repository** the rows start `true`, and all five arrive with the solution skeleton, from
`sdlc scaffold` in the first cycle.

**In an existing codebase** the rows start `false`: the framework proposes a shape for new work and
does not rewrite the conventions of a codebase that predates it. Taking one is a decision rather than a
default — `Directory.Build.props` makes warnings errors, so a build can fail on files nobody has
touched. To take one:

1. Set its row to `true` in the draft, and rename the draft into place. `sdlc stack` now shows it as
   declared and missing.
2. Run a cycle whose intent is to adopt it. Its plan declares the file, and after you approve,
   `sdlc scaffold <id>` writes it — and only the files that plan named.

The fallout lands inside that cycle, where it is planned for. Narrow a severity or scope a rule to a
directory when a build fails; each of these is created once and never rewritten, so your amendments
survive every later run. A file you already had is kept rather than replaced.

**Which conventions new work follows is a separate choice**, recorded as `standard` in `solution.md`.
Every plan reads it to know whether to match the surrounding code or the profile. An existing codebase
starts on `existing`; `sdlc draft solution --standard profile` writes the change for you to rename into
place. The `origin` stays `adopted` either way — where this code came from is a fact, not a preference.

**No code lands, and no projects.** Adoption writes what governs code and stops there. The solution file,
the projects, the architecture tests and the reference slice arrive in your first cycle, behind an intent,
a spec, a plan and an approval like everything else — otherwise the first code in a governed repository
would be the only thing in it with no chain behind it.

**Do the one thing adoption cannot do for you: make the check required.** The pipeline step is the
whole promise — nothing merges unless the chain is complete, approved and hash-matched — and it is a
promise only while branch policy requires it. Until then the file is advisory and the directory looks
enforced without being enforced, which is the failure mode this design is written against.

**Then back to the skill, which picks up where the command stopped.** It asks what matters in this
solution and proposes its first `LOC_` constraints — asking you rather than reading the codebase on
purpose, because reading an existing repository to decide what to forbid would make adoption a judgement
on code nobody is defending, and the first cycle would spend itself on findings about work that predates
the framework. A greenfield repository has no history to ask about, so it asks a shorter set — two
questions: what the solution is for, and what every surface should look and behave like. It does not ask
what *done to a good standard* means, because `CON_16`, `CON_17` and `CON_05` already put that question
to every spec, one change at a time, where there is a change to ask it about.

**Place both protected files before the first cycle, not after it.** `solution.md` holds a
placeholder and `techstack.md` does not exist yet, and nothing can place either for you — the skill runs
the drafters and prints the rename. **Adoption's report ends by naming the first of them and only the
first**: what the solution is comes before what it is built from. A rename runs nothing, so after each
one `sdlc doctor` is what names the next:

```pwsh
sdlc draft solution
```

```pwsh
sdlc draft techstack
```

This is the step with the highest cost for being skipped, and it is not hypothetical. A dice roller
adopted with the placeholder left in place, built a cycle later, came out as a numeral in a bare `<p>`
with every gate green. Nobody had been asked what good looked like, so nobody had been told, and no
rule was broken by any of it. `solution.md` is the only file in the solution that says what good looks
like, and a plan that reads *Replace this with a paragraph a newcomer could read* has been told nothing.

## 2. Read the rules before writing anything

```pwsh
sdlc constraints
```

The global `CON_` set, the `PRO_` stack rules, then this solution's own `LOC_` — in that order, so a
solution's rules are the last word. **Constraints live in the framework and are never copied into a
solution**, so this command is how they reach a session. Every stage skill runs it first, and one that
did not is working from recollection.

The middle tier is the one your declared stack decides: the framework ships three rules that have to
name a framework, and a solution is held to exactly the ones its stack file marks `true`. A stack that
lists none has no such tier in this output. `the-stack.md` has why.

## 3. Open the cycle

```pwsh
sdlc new
```

It prints the id on standard output, so a skill can capture it, and everything you need on standard error
beside it. Creating the directory *is* the collision check: at allocation there is no directory to be
already there, so a look-then-create would leave the race it was written to close.

**`/sdlc:intent` runs this itself, so on the ordinary path you do not.** The command is here because it
is the stage, and because a batch of cycles is opened before any of them is interviewed — but typing it
and *then* invoking the intent skill allocates a second id and a second directory, and the claim
succeeds both times, so nothing complains. The work goes into one of them and the id you wrote down
names the other.

Switch to the branch it names:

```pwsh
git switch -c cycle/20260929_1456_32
```

**The prefix is `cycle/` and it is not cosmetic.** Get it wrong and two different things happen, which
is why it is worth knowing which.

**The turn-end hook goes silent, and that is the half with no message.** It reads the cycle out of the
branch you are on, and a branch that is not a cycle branch is not governed work — a spike, a document,
a configuration change — so it allows the turn rather than firing where it would be obstructive for no
gain. You keep building and lose the two send-backs it exists for: a recorded suite with a failure in
it, and code that has moved since the evidence was recorded.

**`sdlc evidence` then refuses, and it refuses loudly.** It derives the branch from the cycle id rather
than from where you happen to be, so it looks for `cycle/<id>`, finds nothing to measure from, and says
git could not say where that branch diverged from the default branch. **The message names a branch you
do not have**, which is the one confusing part of this — the remedy is to rename the branch you are on,
not to go looking for why git lost one.

The pull-request check would fail too, for the same reason and just as loudly, but you do not get that
far: the recording stops first.

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
fastest way to a plan that cannot be built. It also runs `sdlc stack`, and compares what the solution
declares it is built from with what is there. A build settings file the stack takes and the repository
lacks is closed by this plan only if the intent asked for it; otherwise the plan names it and leaves
it. It is the last document before you approve, so it is the one
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

Eleven things can refuse here, each with a different next step. `when-it-blocks.md` lists them all
with the remedy for each.

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

**The two binary commands in this stage are the skill's, not yours.** It runs both itself; they are
written out here because what each records is what verify and the pull-request check later read, and a
cycle that lost one of them fails at the gate rather than here.

On a new solution's first cycle, the skeleton arrives first, because the plan declared it. In an
existing codebase the same command writes no skeleton — only the build settings files the plan declared,
when it declared any:

```pwsh
sdlc scaffold 20260929_1456_32
```

The five build settings files, a solution file, a core project, an HTTP project, two test projects, and
a reference slice written in the shape every operation should have -- including the one conversion every response leaves through,
which is what makes the banned response factories a rule that can fire rather than a file that looks
like one. It refuses unless this cycle is approved, so the skeleton is a change like any
other, and it writes only what is absent — a second run keeps what is there rather than overwriting it.
Every cycle after this one writes its own code.

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

Edits source and tests, never a planning document, and runs **while each pass reduces what is open**.
It stops when a pass undoes what an earlier one answered, or when two in a row reduce nothing — so there
is no number of tries to run out of, and no ceiling either (D-66).
If the honest conclusion is that a criterion is wrong, it says so and stops rather than adjusting
the spec to match the code — if a failing test could cause an agent to edit the spec, the criteria become
whatever the code happens to do. The recovery is `sdlc draft approval <id> --reopen`, which withdraws the
approval with a recorded reason. It does not verify its own remediation.

`escalated` is computed rather than declared: it is what running out of budget makes of a blocked cycle.
An escalated cycle never opens a pull request.

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

**It checks first whether there is anywhere to push.** A solution recorded as `local`, or one whose
`git remote -v` prints nothing, stops here — the one refusal that is not about the cycle, because the
work may be perfectly ready and have no destination. The skill walks through creating the remote
rather than creating it for you, and says the part that is easy to miss: a remote alone installs no
guarantee, so adoption is re-run afterwards to get the pipeline. This is the cost of adopting before the
remote existed, and step 1 is where it is avoided.

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

With several cycles approved and waiting, `/sdlc:batch-build` completes them all, concurrently where
their declared files do not collide. Everything unattended rests on the remediation bound — a cycle that
stops making progress stops rather than opening one — so the first batch runs are worth watching.

## Where to go next

| | |
|---|---|
| this same walk with one solution in it, end to end | `dice-worked-example.md` |
| this walk as fourteen numbered lines, then every command for one cycle on one screen | `cycle-commands.md` |
| what to do when something refuses | `when-it-blocks.md` |
| what this solution is built from, and how to change it | `the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
| installing, updating, removing | `install.md` |
