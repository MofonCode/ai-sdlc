# Rolling a d6: a worked example

One cycle with one small solution in it, command by command. A six-sided die, rolled over HTTP, in a
repository with no remote.

**`first-cycle.md` is the stage reference** — what each stage is, what it refuses if you arrive early,
and why the flow is shaped this way. This is the same walk with something to type at every prompt and
something that runs at the end. Where another guide covers a step, this one points at it rather than
restating: `install.md` for the two halves, `when-it-blocks.md` for a refusal, `cycle-commands.md` for
the sheet. **Nothing here states a rule**; `sdlc constraints` renders those.

## What this is, and what it is not

**This solution is local.** No remote, no pipeline, no pull request. That keeps the walk to one machine
and one terminal, and it costs the guarantee: with no pull-request check, **the pre-push hook is the
only mechanical layer and `--no-verify` walks past it**. Everything else is identical — cycles are
planned, interrogated, approved, built and verified exactly the same way. The last section has the two
commands that give the solution a remote when you want one.

**It is a d6 over HTTP rather than a console app**, because the `dotnet-default` profile's skeleton is
an API. A console roller would leave the scaffolded projects unused and make the plan argue for the
departure under `CON_15`; rolling over HTTP departs from nothing.

**It is the happy path.** A blocking finding and a blocked verification both have a remedy here, and
neither is walked.

## What you end with

```
GET /roll  ->  a face from 1 to 6, in the solution's response envelope
```

The envelope rather than a bare number, and not a choice this cycle makes: every endpoint the skeleton
admits returns `ApiResult<T>`, carrying whether the operation succeeded, the value, any notices, and the
trace id. An architecture test fails the build on an endpoint that returns anything else.

| | |
|---|---|
| `src/DiceTrial.Contracts/Dice/RollResponse.cs` | the shape that crosses the wire |
| `src/DiceTrial.Application/Dice/Roll.cs` | the operation: a query, a handler, its registration |
| `src/DiceTrial.Application/Dice/DieSource.cs` | where a face comes from, `internal` so a test can substitute it |
| `src/DiceTrial.Api/Dice/RollEndpoints.cs` | `MapDice()` |
| `tests/DiceTrial.Tests/Dice/RollTests.cs` | the unit layer, where three of the four criteria are proved |

Plus one line each in two files the scaffold already wrote: `Services.cs` registers the handler,
`Program.cs` maps the endpoint.

---

## 1. Check both halves

```pwsh
sdlc version
```

```pwsh
sdlc constraints
```

The second is the smoke test rather than `sdlc help`, and it should print the rules and end with
`global  embedded in sdlc <version>`. If either is missing, `install.md` is the whole of it. You also
want a new agent session, because skills load at startup.

## 2. Make the repository

```pwsh
mkdir dice-trial
```

```pwsh
cd dice-trial
```

```pwsh
git init -b main
```

```pwsh
dotnet new gitignore
```

```pwsh
git add .gitignore
```

```pwsh
git commit -m "Ignore build output"
```

**The ignore file goes in before anything builds, and it is not housekeeping.** `sdlc evidence` stages
untracked paths before it diffs — a new file an agent wrote and never staged is part of the change — so
without it, `bin/` and `obj/` land in `diff.patch` as hundreds of files the plan never named, and the
interrogation of that diff has plenty to say about it.

**It does not make this an onboard.** Adoption counts an ignore file, a readme, a licence and an
`AGENTS.md` as furniture rather than code, so the repository is still greenfield and still gets the
skeleton.

**The directory name becomes the solution name.** `dice-trial` gives `DiceTrial.Api`,
`DiceTrial.Application` and the rest — separators become word boundaries. Nothing renames it later.

## 3. Adopt the repository

```
/sdlc:init
```

Start with the skill, not the command: it asks the things the command needs told and cannot guess, then
hands you the line to run. Answer it like this:

| It asks | Answer |
|---|---|
| where the code lives | `local`. There is no remote |
| what the solution is for | a six-sided die, rolled over HTTP |
| what every screen should look and behave like | nothing is on a screen here — it is an endpoint returning JSON. Say that, because *no screens* is an answer and differs from nobody having asked |

Those are the two interview questions on a greenfield repository, and there is no third. It does not ask
what *done to a good standard* means: `CON_16`, `CON_17` and `CON_05` already put that to every spec,
one change at a time. If it asks you anything beyond these — what a reviewer would send back, what this
will be judged on — it has gone past its own skill.

The line it hands back is the adoption command with `--platform local`, and it comes with a leading
`!`. It does not ask what the die is built from: adoption decides nothing about the stack. **Run it yourself** — the hook denies adoption to an agent, and on Claude Code
that prefix runs the rest of the line as your own command, so what it prints lands in the conversation
where the agent can read the file list back.

It picks greenfield mode, writes what governs code, and commits it. `first-cycle.md` has the table of
what each file is. Then install the hook it wrote but deliberately did not install:

```pwsh
git config core.hooksPath .sdlc/hooks
```

## 4. Place the two protected files

Adoption writes `.sdlc/solution.md` with a placeholder and does not write `.sdlc/techstack.md` at all.
Both are protected, so nothing can place them for you. **Do this before the cycle, not after** — the
cycle will not open without the second.
`solution.md` is the only file in the solution that says what good looks like, and a plan that reads
*Replace this with a paragraph a newcomer could read* has been told nothing.

```pwsh
sdlc draft solution
```

It writes the whole file beside the real one, carries the three keys through as recorded, and prints the
rename that places it:

```pwsh
Move-Item .sdlc/solution.draft.md .sdlc/solution.md
```

The paragraph for this solution is about a line:

> A six-sided die, rolled over HTTP. One endpoint, one face, no history and no storage. Responses are
> the solution's envelope and nothing else; there is no user interface and none is wanted.

Then the stack:

```pwsh
sdlc draft techstack
```

The draft is the framework's default, and for a d6 over HTTP it needs nothing changed: the rows that
matter here are MSTest on Microsoft.Testing.Platform for the unit layer and ASP.NET Core for the api,
the three stack rules under `## Rules` are all marked `true`, and so are the five files under
`## Build settings` — this repository held no code, so nothing predates them. Rename it as it is:

```pwsh
Move-Item .sdlc/techstack.draft.md .sdlc/techstack.md
```

What is in that file is now exactly what binds. A solution that was not .NET would have cut the draft
down first, or drafted an empty one with `--empty`.

```pwsh
sdlc propose constraints
```

With no history to read, this prints the next free `LOC_` id empty beside the three admission tests.
**Propose none.** An empty solution-constraint set is the right starting point, and this work is too
small to be what earns a rule.

```pwsh
sdlc doctor
```

Expect everything `ok`, the platform reported as a recorded choice rather than a broken install, and two
marked `elsewhere` — whether the host loaded the skills, and whether the pre-push hook is installed in
this clone. You did the second in step 3.

## 5. The intent

```
/sdlc:intent
```

**Pass no sentence.** The value of the stage is the questions, and a one-line request arrives already
shaped as a solution. The skill opens the cycle itself — it runs `sdlc new`, so do not run that first or
you will allocate a second id — and prints the branch to create:

```pwsh
git switch -c cycle/<id>
```

**The prefix is `cycle/` and nothing else works.** The turn-end hook reads the cycle out of the current
branch and silently governs nothing when it cannot parse one.

Nothing exists yet, so it asks in that tense:

| It asks | Answer |
|---|---|
| what can nobody do today that they need to | nobody on this machine can ask for a die roll and get one back. There is no die |
| who needs it, and how often | whatever is driving the board game, every turn that calls for a roll |
| what would you be able to see that you cannot today | a request for a roll comes back with a number from 1 to 6 |
| what is out of bounds | one die only, no history, nothing stored, no user interface |

**It will ask one more and the answer matters.** Say *keep it simple* and it asks which you mean — small
in scope, or plain in appearance. Here the answer is **scope**: one die, one endpoint. The word reaches
every stage afterwards and no gate can tell the two readings apart.

Expect fixed decisions roughly like *a single six-sided die*, *nothing is stored* and *no user
interface*, each with your name and the date on it. Those are what keep the spec from growing a page.

Then the loop, until a round raises nothing blocking:

```
/sdlc:interrogate <id> intent
```

```
/sdlc:refine <id> intent
```

```pwsh
sdlc status <id> --brief
```

**How many rounds is deliberately not a question the tool will answer.** The panel's `Loop` line warns
when a rule has been answered and raised again — going backwards, not going slowly — and there is no cap
to run out of.

## 6. The spec

```
/sdlc:spec <id>
```

It reads `intent.md` and nothing else — not the conversation, not the code. Four criteria is what this
work comes to, and three land at the unit layer:

1. **A roll is an integer from 1 to 6, and every face is reachable.** Drawing 100,000 rolls from the
   component with the real source, the set of distinct values observed is exactly 1, 2, 3, 4, 5 and 6,
   and nothing outside it appeared.
2. **The response carries the face that was rolled, unchanged.** With a substituted source presenting a
   recorded sequence, a source yielding 4 produces a response whose face is 4, and the component consumes
   exactly one draw per roll.
3. **The endpoint answers on the solution's envelope.** Checked by the architecture suite the skeleton
   already holds — `EndpointTests.Every_endpoint_returns_the_envelope` fails the build if any mapped
   endpoint returns something else.
4. **A caller whose roll cannot be produced is told so.** With a substituted source that throws, the
   operation returns a failure result carrying a message rather than letting an exception leave the
   handler.

**Criterion 4 is the one this work nearly skips, and `CON_17` is why it cannot.** A spec carrying
criteria for an action somebody initiates and none for what they are told when it does not succeed is
refused. The roll takes no input, so there is no validation failure to report; the failure that does
exist is the source, and criterion 4 makes handling it something the build is required to do rather than
something the implementer happened to think of.

**Criterion 3 is worth reading twice.** Its proof is a test that already exists, and the plan names it
without writing it. That is legitimate — what is not is a criterion whose named test could pass while
the criterion is unmet.

```
/sdlc:interrogate <id> spec
```

```
/sdlc:refine <id> spec
```

## 7. The plan

```
/sdlc:plan <id>
```

Unlike the spec, this one **must** read the code — and there is almost none yet, so what it reads is the
skeleton it is about to ask for. It runs `sdlc stack` first, which shows the five build settings files
as declared and missing. Expect it to declare `sdlc scaffold`: the solution file, the five projects, the
reference slice and those build settings arrive in this cycle behind an approval like everything else.

**It is the last document before you approve, so read it for proof adequacy.** Ask of each check: *what
would have to be true for this to pass while the criterion is still unmet?* On this work the answer to
watch for is a proof that rests entirely on a substituted source — a test that only ever drives the roll
through a fake has proved the plumbing and said nothing about the die. That is why criterion 1 draws
from the real source and only criterion 2 substitutes.

The file table should name the two shared files as **changed, not new**: `Services.cs` gains
`.AddRollRequestHandler()` and `Program.cs` gains `app.MapDice()`. A plan that forgets them produces a
build that compiles and an endpoint nothing routes to.

```
/sdlc:interrogate <id> plan
```

```
/sdlc:refine <id> plan
```

## 8. Approve

```pwsh
sdlc draft approval <id>
```

It writes the complete file beside the ledger and prints the rename that records it:

```pwsh
Move-Item .sdlc/cycles/<id>/approval.approved.draft.md .sdlc/cycles/<id>/approval.md
```

**The rename is the decision and it is yours.** No tool places that file, there is no flag for an
exception, and a draft nobody renamed records nothing. It refuses to print at all while a blocking
finding is open: a blocking finding cannot be waived by any mechanism.

```pwsh
sdlc gate <id>
```

Eleven things can refuse here, each with a different next step. `when-it-blocks.md` lists them all
with the remedy for each.

## 9. Build

```
/sdlc:build <id>
```

The first stage that writes code and the first you are not expected to watch. It asks the gate, stops if
the gate refuses, and implements what the plan decided and only that.

**It runs these itself.** They are written out because what each records is what verify and the chain
check later read:

```pwsh
sdlc scaffold <id>
```

The skeleton, because the plan declared it — a solution file, five projects, the architecture suite and
a reference slice written in the shape every operation should have — and the five build settings files
it compiles under. It refuses unless this cycle is approved, and writes only what is absent.

```pwsh
dotnet test
```

Then the code is committed, and the evidence recorded from the commit:

```pwsh
sdlc evidence <id> --tests <the directory holding the results files> --build-log <path>
```

**Point `--tests` at the directory, not at one file.** Each test project writes its own results, and a
summary built from one of them is a verification reading part of the suite.

```pwsh
git add .sdlc/cycles/<id>/evidence
```

```pwsh
git commit -m "Record the build evidence for cycle <id>"
```

**That second commit is not an afterthought.** Verify and the chain check both read the evidence out of
the repository, so evidence that exists only in a working tree is evidence the chain cannot see.

One thing to expect: the scaffold writes a `Greet` reference slice that exists to be copied. The dice
operation takes its shape, and the slice stays until there is enough real work to delete it — which is
not this cycle.

## 10. Verify

```
/sdlc:verify <id>
```

A fresh process, reading `tests.json`, `diff.patch` and `base.sha` and **not** running the tests. Per
criterion: which test did the plan name, does the summary say it ran and passed, and does that test
actually establish the criterion? The third is where judgement lives — a test that passed but does not
reach its criterion is *not met*.

It writes `pass` or `blocked`. On `blocked`:

```
/sdlc:remediate <id>
```

Edits source and tests, never a planning document, and runs while each pass reduces what is open.
`when-it-blocks.md` has the rest.

```pwsh
sdlc status <id> --brief
```

## 11. See it roll

```pwsh
dotnet run --project src/DiceTrial.Api
```

```pwsh
curl http://localhost:<port>/roll
```

A face between 1 and 6 inside the envelope, and a different one next time. `/swagger/index.html` has the
surface — the api sends `/` there, so a browser is not met with a bodiless 404 — and `/health` answers
because an architecture test fails the build if it stops answering.

```pwsh
sdlc suggestions
```

What the verification noticed and did not block on. **Non-blocking is not trivial**; it means nothing
cited a rule that forbade the current state.

## If you want to open a pull request

A local solution has nowhere to push, and `/sdlc:pr` stops there rather than pretending — the one
refusal that is not about the cycle, because the work may be perfectly ready and have no destination.
Two commands give it a destination:

```pwsh
gh repo create dice-trial --private --source . --remote origin
```

```pwsh
git push -u origin main
```

**A remote alone installs no guarantee.** Adoption wrote no pipeline for a local solution, so re-run it
with a platform to get one — the skill walks through it — and then require that check in branch policy.
The pipeline step is the whole promise, and it is a promise only while a policy requires it.

Then the cycle goes out like any other:

```pwsh
sdlc pr-ready <id>
```

```
/sdlc:pr <id>
```

It asks before the push. **It is not approving the merge** — a code owner does that, and never the agent
that wrote the change.

## Where to go next

| | |
|---|---|
| the same walk with no solution in it, stage by stage | `first-cycle.md` |
| every command for one cycle, on one screen | `cycle-commands.md` |
| what to do when something refuses | `when-it-blocks.md` |
| installing both halves | `install.md` |
| what each skill is and who starts it | `../../skills/README.md` |
