# Rolling a d6: a worked example

One cycle with one small solution in it, command by command. A six-sided die, rolled from a page in a
browser, in a repository with no remote.

**The guides are the stage reference** — `../guides/adopting-the-framework.md` and
`../guides/running-a-cycle.md` say what each stage is, what it refuses if you arrive early, and why the
flow is shaped this way. This is the same walk with something to type at every prompt and something that
runs at the end. Where another guide covers a step, this one points at it rather than restating:
`../guides/installing-the-plugin.md` for the two halves, `../guides/handling-refusals.md` for a refusal,
`../cheat-sheet.md` for the cycle on one page. **Nothing here states a rule**; `sdlc constraints` renders
those.

In this tutorial, you:

- make a repository and adopt it
- place the solution document and the declared stack
- write the intent, spec and plan, and approve them
- build the cycle and verify it
- roll the die in a browser

## What this is, and what it is not

**This solution is local.** No remote, no pipeline, no pull request. That keeps the walk to one machine
and one terminal, and it costs the guarantee: with no pull-request check, **nothing mechanical decides
what reaches the default branch**. Everything else is identical — cycles are
planned, interrogated, approved, built and verified exactly the same way. The last section has the two
commands that give the solution a remote when you want one.

**It is a page over an api rather than a console app**, because that is what the `dotnet-default`
profile declares: an api, Blazor for web, and MudBlazor for the components. A console roller would leave
the scaffolded projects unused and make the plan argue for the departure under `CON_15`.

**It has a front end on purpose.** A die somebody asked to *see* is the work the adoption interview's
second question, `CON_16` and the component layer each exist for, and an endpoint returning JSON walks
past all three. The cost is that the skeleton writes no front end — a UI in the default would commit
every solution to having one — so the two projects that hold it arrive in this cycle, declared by its
plan like any other file.

**Every criterion about the page is held at the component layer.** bUnit renders a component and drives
it with no browser. A criterion about what a real browser engine does needs Playwright in Chromium and
WebKit, which is two engines to install before the first cycle can verify, and nothing in this work asks
for it: each outcome is about what a component renders and how it reacts.

**It is the happy path.** A blocking finding and a blocked verification both have a remedy here, and
neither is walked.

## What you end with

```
the page   ->  a die and a Roll button; press it and see a face of one to six pips
GET /roll  ->  that face, in the solution's response envelope
```

The envelope rather than a bare number, and not a choice this cycle makes: every endpoint the skeleton
admits returns `ApiResult<T>`, carrying whether the operation succeeded, the value, any notices, and the
trace id. An architecture test fails the build on an endpoint that returns anything else.

The two project names are fixed, because the architecture suite reads a project's role off its name.
The file names inside them are the plan's to choose, so expect this shape rather than these words:

| | |
|---|---|
| `src/DiceTrial.Contracts/Dice/RollResponse.cs` | the shape that crosses the wire |
| `src/DiceTrial.Application/Dice/Roll.cs` | the operation: a query, a handler, its registration |
| `src/DiceTrial.Application/Dice/DieSource.cs` | where a face comes from, `internal` so a test can substitute it |
| `src/DiceTrial.Api/Dice/RollEndpoints.cs` | `MapDice()` |
| `src/DiceTrial.Ui/` | the Razor class library, and the only project MudBlazor may be referenced from: the die face drawn as pips, the panel holding the button, the face and the failure message, and the client the panel asks for a roll through |
| `src/DiceTrial.Web/` | the Blazor WebAssembly host. It references `Ui` and `Contracts`, and holds no component of its own |
| `tests/DiceTrial.Tests/Dice/RollTests.cs` | the unit layer, where the three criteria about the roll itself are proved |
| `tests/DiceTrial.Ui.Tests/` | the component layer, where the three criteria about the page are proved |

Plus changes to three files the scaffold already wrote: `Services.cs` registers the handler, `Program.cs`
maps the endpoint and admits the page's origin, and the solution file gains the three new projects.

## Prerequisites

- the .NET 10 SDK
- git
- both halves of the framework — the `sdlc` binary and the plugin — installed as
  [Installing the plugin](../guides/installing-the-plugin.md) describes. Step 1 checks them
- a browser, to see it roll

---

## 1. Check both halves

```pwsh
sdlc version
```

```pwsh
sdlc constraints
```

The second is the smoke test rather than `sdlc help`, and it should print the rules and end with
`global  embedded in sdlc <version>`. If either is missing, `../guides/installing-the-plugin.md` is the whole of it. You also
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
| what the solution is for | a six-sided die somebody rolls from a page in a browser |
| what every screen should look and behave like | the component library as it comes: MudBlazor, its own default look, no brand. A die is drawn as a face with pips, never as a bare number, and when something fails the page says so in words |

**The second answer is the one a new repository most needs, and it is why the question is asked.** A
solution with no history has nothing else recording what it should look like when it is finished, and
the gates cannot supply it: a numeral in a bare paragraph tag breaks no rule.

**None of that answer becomes a constraint, and most of it is already placed.** The component library
is a row in the declared stack you place in the next step, and the architecture suite already admits it
in `Ui` and nowhere else. A brand would go in a theme, which `sdlc propose theme` drafts as roles for a
person to fill; this solution has no brand, so it has no theme, and that is a complete answer rather
than a gap. What is left — pips rather than a number, failure said in words — is about this one piece of
work, and the intent is where it is recorded.

Those are the two interview questions on a greenfield repository, and there is no third. It does not ask
what *done to a good standard* means: `CON_16`, `CON_17` and `CON_05` already put that to every spec,
one change at a time. If it asks you anything beyond these — what a reviewer would send back, what this
will be judged on — it has gone past its own skill.

The line it hands back is the adoption command with `--platform local`, and it comes with a leading
`!`. It does not ask what the die is built from: adoption decides nothing about the stack. **Run it yourself** — the hook denies adoption to an agent, and on Claude Code
that prefix runs the rest of the line as your own command, so what it prints lands in the conversation
where the agent can read the file list back.

It picks greenfield mode, writes what governs code, and commits it to `main`. Then it creates
`development` from that commit and switches to it — that is where you will commit what you place next,
and where the cycle's branch starts from. `../guides/adopting-the-framework.md` has the table of what
each file is.

## 4. Fill in the drafts, place them, commit them

Adoption places none of the files only a person writes. It drafts three of them beside where each goes,
each with a note at the top saying an agent cannot change the real file, and its report ends with what to
fill in and the rename for each. **Do this before the cycle, not after** — the cycle will not open
without the stack, or over anything uncommitted.

**`.sdlc/solution.draft.md`.** Replace the comment under the heading with the paragraph. For this
solution it is about a line:

> A six-sided die, rolled from a page in a browser. One page, one die, no history and no storage. The
> page is built from the solution's component library as it comes, and a face is drawn as pips.

`solution.md` is the only file in the solution that says what good looks like, and every plan reads it.
`sdlc doctor` fails while it still carries the comment.

**`.sdlc/techstack.draft.md`.** The draft is the framework's default, and for a die on a page it needs
nothing changed. The rows that matter here are MSTest on Microsoft.Testing.Platform for the unit layer,
bUnit for the component layer, ASP.NET Core for the api, Blazor for web and MudBlazor for the components.
The three stack rules under `## Rules` are all marked `true` — `PRO_01` is the one this work will meet,
at the plan — and so are the five files under `## Build settings`, because this repository held no code
and nothing predates them. Read `## Notes worth keeping` before you rename it: its first four paragraphs
are about exactly the two projects this cycle adds. What is in that file once it is placed is exactly
what binds; a solution that was not .NET would cut it down first.

**`.sdlc/constraints.draft.md`.** Leave its contents as they are. The interview produced nothing that is
a constraint, and an empty solution-constraint set is the right starting point — this work is too small
to be what earns a rule. Place it anyway: renamed with no entry, it says in the record that this
solution has no rules of its own, rather than leaving a draft git ignores.

Now place the three. On Claude Code a `!` line runs in bash, so use that form there:

```sh
mv -f .sdlc/solution.draft.md .sdlc/solution.md
mv -f .sdlc/techstack.draft.md .sdlc/techstack.md
mv -f .sdlc/constraints.draft.md .sdlc/constraints.md
```

In a PowerShell terminal, where `mv -f` is ambiguous:

```pwsh
Move-Item -Force .sdlc/solution.draft.md .sdlc/solution.md
Move-Item -Force .sdlc/techstack.draft.md .sdlc/techstack.md
Move-Item -Force .sdlc/constraints.draft.md .sdlc/constraints.md
```

Then commit them, on `development`:

```pwsh
git add .sdlc
```

```pwsh
git commit -m "Place the solution and its stack"
```

```pwsh
sdlc doctor
```

Expect everything `ok` — `development` as the integration branch included — the platform reported as a
recorded choice rather than a broken install, and whether the host loaded the skills marked `elsewhere`,
because a binary cannot ask a host what it loaded. It exits 0 and ends by naming the first cycle.

## 5. The intent

```
/sdlc:intent
```

**No `--type`.** This is a scaffolded solution with no cycle yet approved, so the skill recognises the
first piece of work in a new solution and says so in a line. **The skill opens the cycle and switches to
its branch itself**, and says the id
and the branch in one line — run neither `sdlc new` nor `sdlc branch` yourself. The branch is
`cycle/<id>/plan`, and the name matters: the turn-end hook reads the cycle out of the current branch and
silently governs nothing when it cannot parse one.

**Then it waits for your answers.** The intent is written from them, so until you reply there is no
intent, and an interrogation asked for now finds nothing to read. It asks **one question at a time**,
each with a few example answers under it, and skips what your solution paragraph already answered — so
expect its wording to differ from this table and some rows to be skipped:

| It asks | Answer |
|---|---|
| what should someone be able to do first | open a page with a die on it and a button. Press the button and see a die face showing one to six pips. If the roll fails, the page says so |
| who is it for, and what can't they do today | whoever is playing the board game, every turn that calls for a roll. Nobody here can roll a die |
| what do they use instead | nothing. There is no die |
| what is it definitely not | one die only, no history, nothing stored, no sign-in, no animation |
| has anything already been decided | a single six-sided die; nothing is stored; a face is drawn as pips, not as a numeral |
| what does it touch, beyond the basics | it is a page someone uses, so ui-ux and accessibility. Nothing else: no public API, no performance target, no infrastructure |

**It will ask one more and the answer matters.** Say *keep it simple* and it asks which you mean — small
in scope, or plain in appearance. Here the answer is **scope**: one die, one page. It is not appearance,
and the difference is the whole of this example: *plain* is the reading under which a die becomes a
number in a paragraph tag. The word reaches every stage afterwards and no gate can tell the two readings
apart.

What is already decided becomes the intent's fixed decisions, each with your name and the date on it,
and what it touches its `perspectives: ui-ux, accessibility`. The first two decisions keep the spec from
growing a second page; the third keeps it from shrinking the one it has.

**Once you answer, the intent is interrogated for you.** The skill writes it, challenges it, fixes what
comes back, then shows the cycle's status and stops. If it stops on a blocker, see
[handling blockers](../guides/handling-blockers.md).

## 6. The spec

```
/sdlc:spec <id>
```

It reads `intent.md` and nothing else — not the conversation, not the code. Seven criteria is what this
work comes to: three about the roll at the unit layer, one the skeleton already proves, and three about
the page at the component layer.

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
5. **The page displays a die face showing that many pips, the number being an integer from 1 to 6.**
   Rendering the die face component for each face from 1 to 6, the count of pips in the markup equals
   the face, and no face renders as a numeral alone.
6. **Pressing Roll shows the face that came back.** With a substituted client answering 4, one press
   renders a die face of four pips and makes exactly one request.
7. **A person whose roll fails is told so on the page.** With a substituted client answering a failed
   envelope carrying a notice, the press renders that notice's text, and no face is presented as the
   result of that press.

**Criterion 5 is the one that gets narrowed, and `CON_16` is why it cannot be.** The intent said *a die
face showing one to six pips*. A criterion reading *displays a face value that is an integer from 1 to
6* still traces to that outcome and still satisfies a coverage check — it is the same sentence with the
thing you asked to look at removed. Nothing downstream compares a spec to its intent again, so read
criterion 5 against your own words before you approve it. The intent's words should still be in it.

**Criteria 4 and 7 are the ones this work nearly skips, and `CON_17` is why it cannot.** A spec carrying
criteria for an action somebody initiates and none for what they are told when it does not succeed is
refused. The roll takes no input, so there is no validation failure to report; the failure that does
exist is the source. There are two of them because a failure here has two readers: criterion 4 is what
the caller of the endpoint is told, and criterion 7 is what the person who pressed the button is told.
One without the other is a failure that is reported correctly and shown to nobody.

**Criterion 3 is worth reading twice.** Its proof is a test that already exists, and the plan names it
without writing it. That is legitimate — what is not is a criterion whose named test could pass while
the criterion is unmet.

**The spec is interrogated for you**, the same way. If it stops on a blocker, see
[handling blockers](../guides/handling-blockers.md).

## 7. The plan

```
/sdlc:plan <id>
```

Unlike the spec, this one **must** read the code — and there is almost none yet, so what it reads is the
skeleton it is about to ask for. It runs `sdlc stack` first, which shows the five build settings files
as declared and missing. Expect it to declare `sdlc scaffold`: the solution file, the skeleton's
projects, the reference slice and those build settings arrive in this cycle behind an approval like
everything else. It runs `sdlc theme` too, which reports no theme; that is the answer you gave at
adoption, read back.

**The skeleton stops at the api, so the plan declares the front end itself.** Two projects under
`src/`, and their names are not the plan's to choose: `DiceTrial.Ui` and `DiceTrial.Web`. The
architecture suite the scaffold writes already carries a row for each — what it may reference, what
packages it may take — and matches them by role, so a library called `DiceTrial.Components` builds
nothing and fails asking for a row. Nothing in that suite changes in this cycle, which is the point of
the rows having been there waiting.

**Three things about the page the plan has to say, because nothing else will.** A plan silent on any of
them still produces a build that compiles:

- **How the page reads the envelope.** The record that declares it lives in the api project, and no
  front end may reference that project — what the page references is what a browser downloads. So the
  page needs a read-side shape of its own, and the plan says where it lives.
- **How the page reaches the api.** They are two origins: an address the page is configured with, and
  the api admitting that origin. Forget either and both halves run while the button fails.
- **Whether it adds an Aspire AppHost.** The declared stack says Aspire where there is more than one
  process, and there are now two to start. The plan either adds one or says under `Shape and departures`
  what it would have cost here, which is what `CON_15` asks of a default left unapplied.

**It is the last document before you approve, so read it for proof adequacy.** Ask of each check: *what
would have to be true for this to pass while the criterion is still unmet?* Three answers to watch for
on this work:

- **A proof that rests entirely on a substituted source.** A test that only ever drives the roll through
  a fake has proved the plumbing and said nothing about the die. That is why criterion 1 draws from the
  real source and only criterion 2 substitutes.
- **A proof of the page that never renders the page.** Requesting the api and asserting over the
  response body passes with a component that draws nothing. `PRO_01` refuses that plan, and criteria 5,
  6 and 7 should each name a bUnit test.
- **A component test that looks for the number.** A test asserting the markup contains `4` passes over
  a numeral in a paragraph tag, which is the exact failure criterion 5 exists to stop. Its proof counts
  pips.

The component tests need two lines of setup the plan should name rather than leave to the build:
MudBlazor's services registered, and bUnit's JavaScript interop set to loose. Without them the first
test throws on a call nobody planned, which reads as a broken component rather than a missing
registration.

The file table should name the shared files as **changed, not new**: `Services.cs` gains
`.AddRollRequestHandler()`, `Program.cs` gains `app.MapDice()` and the page's origin, and the solution
file gains three projects. A plan that forgets the first two produces a build that compiles and an
endpoint nothing routes to; one that forgets the third produces projects `dotnet test` never builds.

**The plan is interrogated for you**, the same way. If it stops on a blocker, see
[handling blockers](../guides/handling-blockers.md).

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

The skeleton, because the plan declared it — a solution file, its projects, the architecture suite and
a reference slice written in the shape every operation should have — and the five build settings files
it compiles under. It refuses unless this cycle is approved, and writes only what is absent. The two
front-end projects and the component tests are not part of it: the build writes those by hand, from the
plan's file table.

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
`../guides/handling-blockers.md` has the rest.

```pwsh
sdlc status <id> --brief
```

## 11. See it roll

```pwsh
dotnet run --project src/DiceTrial.Api
```

And in a second terminal, the page:

```pwsh
dotnet run --project src/DiceTrial.Web
```

Open the address the second one prints and press Roll. A die face of one to six pips, and a different
one next time. If the plan added an AppHost, that is the one project to run instead of these two.

**Stop the api and press it again.** The page should tell you the roll failed, in words, and leave no
face standing as though it were the result. That is criterion 7 in front of you rather than in a test
summary, and it is the one worth seeing once.

```pwsh
curl http://localhost:<port>/roll
```

The same roll without the page: a face between 1 and 6 inside the envelope. `/swagger/index.html` has
the surface — the api sends `/` there, so a browser is not met with a bodiless 404 — and `/health`
answers because an architecture test fails the build if it stops answering.

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

## Next steps

- [Running a cycle](../guides/running-a-cycle.md) — the same walk with no solution in it, stage by stage, for your own work

## See also

- [Handling blockers](../guides/handling-blockers.md) — a blocking finding, or a blocked verification
- [Handling refusals](../guides/handling-refusals.md) — what to do when something refuses
- [Installing the plugin](../guides/installing-the-plugin.md) — installing both halves
- [The cheat sheet](../cheat-sheet.md) — the cycle, one line per step
- [Commands](../reference/commands.md) — every command, on one screen
- [The skills reference](../../skills/README.md) — what each skill is and who starts it
