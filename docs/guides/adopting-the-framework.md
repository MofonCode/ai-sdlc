# Adopting the framework

Putting a repository under the framework: the command that writes what governs code, the interview that
says what matters here, and the two files a person places before the first cycle can open.

**This assumes both halves are installed** — `installing-the-plugin.md` covers that, and `sdlc version`
is how you know. Nothing here is specific to what the solution is.

## 1. Start with a repository

```pwsh
git init
```

**Adoption refuses outside a git repository**, and that is not fussiness. A build records the diff a
verification derives from, and every gate keys on a branch — without git, every hook would permit
everything while the documents still looked governed.

## 2. Start with the skill, not the command

```
/sdlc:init
```

**Two things share the name and do opposite halves of the job.** `/sdlc:init` is the skill: it asks
what the command needs told, interviews you about what matters here, and writes nothing. `sdlc init` is
the binary, which does the writing — and **you run it, because an agent cannot.** Adoption is a decision
about how a team works, so the pre-tool hook denies the command to an agent by its text rather than by
file path, and asking your agent to run it produces a refusal rather than a repository.

The skill asks the one thing the command cannot guess — where the code will live — and then hands you the
line to run, `!` and all. On Claude Code that prefix runs the rest of the line as your own command rather
than as a tool call of the agent's, so the gate never sees it, and what it prints lands in the
conversation where the agent can read the mode and the file list back without you pasting them.

**The order is load-bearing, not politeness.** `solution.md` is created by the command and protected
the moment it exists, so an answer given after the fact costs a draft and a rename rather than a
sentence. The one that bites:

| | |
|---|---|
| `--platform` | `github`, `azure-devops`, or `local` for a repository with no remote yet. `local` writes no pipeline, so **no pull-request check governs a merge** and nothing mechanical decides what reaches the default branch |

**If the code will live on a remote, create the remote before adopting.** Re-running adoption once one
exists writes the pipeline, but `solution.md` is protected by then, so the platform keys are printed for
you to place by hand — and until they are placed the record still says `local`, which is what `doctor`
and the `pr` skill both read.

**Adoption is not told what the solution is built from, and decides nothing about it.** There is no
profile to choose. The stack is a later step, and it is yours to declare.

## 3. What the command does

It picks its own mode from what it finds:

| Mode | When | Records |
|---|---|---|
| **greenfield** | a repository holding no code. An ignore file, a readme, a licence and an `AGENTS.md` are furniture, not code | `origin: scaffolded` |
| **onboard** | existing code with no `.sdlc/` | `origin: adopted`, `standard: existing` |
| **update** | `.sdlc/` is already there | nothing new — it refreshes the hook configs, the pipeline and `.sdlc/README.md`, and never the six files only a person writes |

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
| `.agents/hooks.json` | the tool-call moment in Antigravity's shape, in its `sdlc` section. Its turn end is deliberately not governed — `installing-the-plugin.md` has why |
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

**What does not land: the declared stack, the build settings, and any code.** The first two belong to a
stack nobody has declared yet. The solution file, the projects, the architecture tests and the reference
slice arrive in your first cycle, behind an intent, a spec, a plan and an approval like everything else —
otherwise the first code in a governed repository would be the only thing in it with no chain behind it.

## 4. Make the check required

**This is the one thing adoption cannot do for you.** The pipeline step is the whole promise — nothing
merges unless the chain is complete, approved and hash-matched — and it is a promise only while branch
policy requires it. Until then the file is advisory and the directory looks enforced without being
enforced, which is the failure mode this design is written against. A `local` solution has no pipeline
to require, and says so.

| Platform | What to set |
|---|---|
| GitHub | a branch protection rule on the default branch that requires the `verify` status check |
| Azure DevOps | **Repos > Branches > the default branch > Branch policies > Build validation**: add the `azure-pipelines.yml` pipeline, make it **Required**, and set **Build expiration** to **Immediately when the branch is updated** |

**How often an open pull request is checked again.** A pull request is checked against the branch as it
was when the check ran. Two pull requests can each pass and merge without a conflict and still break
together — one renames a method the other calls — and only re-checking after the first merges catches
that before the second does. What to set is a trade between CI minutes and how early you find out:

| Setting | Re-checks | Finds the break |
|---|---|---|
| **Immediately when the branch is updated** (recommended) | every open pull request, each time anything merges | before the second pull request can merge |
| **After n hours if the branch has been updated** | a pull request whose passing check is older than n hours | before merging, unless both merge inside the window |
| **Never** | nothing | after both have merged, at the next build that includes them |

On GitHub the nearest equivalents are **Require branches to be up to date before merging** on the
protection rule, which makes a pull request's author bring it up to date and re-run the check, and a
**merge queue**, which checks each pull request against everything queued ahead of it. Azure DevOps has
no merge queue.

**On Azure DevOps this is not only what makes the check required — it is what runs it.** Azure Repos
ignores a `pr:` trigger written in YAML, so the pipeline has no trigger of its own, and the file says so
at the top. Until the policy exists, no pull request is built, tested or verified, and nothing fails to
tell you. `sdlc doctor` names this step under `branch policy`, marked `elsewhere`, because a binary
cannot read the platform's policies.

## 5. The interview

**Then back to the skill, which picks up where the command stopped.** It asks what matters in this
solution and proposes its first constraints — asking you rather than reading the codebase on purpose,
because reading an existing repository to decide what to forbid would make adoption a judgement on code
nobody is defending.

**A greenfield repository has no history to ask about, so it asks two questions:**

1. **What is this solution for**, in a sentence a newcomer could read?
2. **What should every screen look and behave like?** A component library, a design somebody is working
   to, a product it has to sit beside — or plain and unstyled, which is a legitimate answer and a
   different one from nobody having asked.

An existing codebase gets the first question and three about its history instead of the second. Neither
asks what *done to a good standard* means: `CON_16`, `CON_17` and `CON_05` already put that to every
spec, one change at a time, where there is a change to ask it about.

**Most answers are not constraints, and the skill sorts them before proposing anything:**

| An answer about | Goes in |
|---|---|
| what the solution is and who it is for | the paragraph in `solution.md`, below |
| which component library, which packages, which project may reference what | the declared stack, and an architecture test |
| the names a visual treatment is expressed in | a theme, which `sdlc propose theme` drafts as roles for you to fill |
| whether a judgement was made and recorded | a `LOC_` constraint, below |

## 6. Place `solution.md`

`solution.md` holds four keys and a placeholder paragraph. **The keys record facts; the paragraph is
yours to write.**

| Key | Values | What it decides |
|---|---|---|
| `origin` | `scaffolded`, `adopted` | where the code came from. **A historical fact that never changes** |
| `standard` | `profile`, `existing` | whether new work follows the profile's patterns or the surrounding code. Every plan reads it |
| `platform` | `github`, `azure-devops`, `local` | whether a pipeline is written, and so whether a check governs a merge |
| `integration_branch` | a branch name; `development` when absent | where approved plans wait and built cycles land. The factory builds from it, and a cycle's diff is measured from where it diverged from it — or from the default branch while it does not exist |

```pwsh
sdlc draft solution
```

It writes the whole file beside the real one, keeps the four keys as recorded, and prints the one rename
that places it:

```pwsh
Move-Item .sdlc/solution.draft.md .sdlc/solution.md
```

**No stage reads the paragraph, and it is still the step with the highest cost for being skipped.** A
dice roller adopted with the placeholder left in place, built a cycle later, came out as a numeral in a
bare `<p>` with every gate green. Nobody had been asked what good looked like, so nobody had been told,
and no rule was broken by any of it. It is the one place a person joining this repository learns what it
is for, and *Replace this with a paragraph a newcomer could read* tells them nothing.

**`standard` is the one key you may move.** An existing codebase starts on `existing`;
`sdlc draft solution --standard profile` writes the change for you to rename into place. The `origin`
stays as it is either way — where this code came from is a fact, not a preference.

## 7. Declare the stack

**No cycle opens until `.sdlc/techstack.md` is placed**, and adoption does not write it. It is the
second rule source a plan is read against, so it has a guide of its own: `declaring-the-stack.md`.

```pwsh
sdlc draft techstack
```

**Adoption's report names the next step, and only one.** What the solution is comes before what it is
built from. A rename runs nothing, so after each one `sdlc doctor` is what names the next — and once both
are placed, it names the first cycle.

## 8. Propose the first constraints, or none

```pwsh
sdlc propose constraints
```

A repository being adopted has no cycles, so this prints the next free `LOC_` id with its four parts
empty, beside the three admission tests. **All three must pass:**

1. its `Forbids` clause names something that becomes illegal
2. **no analyzer, schema or hook could decide it**
3. it is incompatible with at least one plausible implementation

The second rejects most candidates — "every public method has a doc comment" is an analyzer, not a
constraint. **The skill proposes and you place**: every agent write to `.sdlc/constraints.md` is denied.

**Proposing none is the right answer for most solutions.** An empty solution-constraint set is the
correct starting point, and three constraints nobody believes in cost attention at every interrogation
for the life of the repository.

## What your solution owns

**Six files only a person writes**, and every tool that could write one prints what belongs in it
instead. The pre-tool hook denies every agent write to all six, by file path and by shell command alike.

| File | What it holds | Changes when | How it gets written |
|---|---|---|---|
| `.sdlc/solution.md` | the four keys and the paragraph | the paragraph whenever it stops being true; `standard` when you choose | `sdlc draft solution`, then your rename |
| `.sdlc/techstack.md` | what the solution is built from, the stack rules it is held to, the build settings it takes | a default or a rule turns out wrong for this solution | `sdlc draft techstack`, then your rename — `declaring-the-stack.md` |
| `.sdlc/constraints.md` | this solution's own `LOC_` rules, the last word over every other tier | a rule keeps being cited across cycles, or one stops earning its place | `sdlc propose constraints` proposes; you paste |
| `.sdlc/decisions.md` | choices this solution already settled, so a cycle reads what was argued rather than arguing it again. Binds nothing | an approved cycle settles something worth not re-deciding | `sdlc propose decisions` reads approved cycles; you paste |
| `.sdlc/cycles/<id>/approval.md` | the decision on one cycle's three planning documents | at the gate, and again if a document changes | `sdlc draft approval <id>`, then your rename |
| `.sdlc/cycles/<id>/abandoned.md` | why one cycle stopped for good | once, and it is terminal | `sdlc draft abandon <id>`, then your rename |

**One solution file is deliberately not on that list.** `.sdlc/theme.json` holds the roles this
solution's surfaces use and the values that fill them. Nobody is judged against a colour, so an ordinary
cycle may declare it and change a value like any other file. `sdlc propose theme` prints its roles for
you to fill, and never a colour.

**The rules themselves are not in your repository.** `sdlc constraints` renders the global set, the
stack rules your declared stack switches on, and your own `LOC_` rules, in that order — a copy in the
repository would be a second place the truth lives.

## Where to go next

| | |
|---|---|
| what the solution is built from, and how to change it | `declaring-the-stack.md` |
| the first cycle, stage by stage | `running-a-cycle.md` |
| this whole walk with a real solution in it | `../samples/rolling-a-die.md` |
| what to do when something refuses | `handling-refusals.md` |
| every command on one screen | `../cheat-sheets/commands.md` |
