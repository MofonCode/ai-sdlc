# Adopting the framework

Putting a repository under the framework: the command that writes what governs code, the interview that
says what matters here, and the drafts a person fills in, renames and commits before the first cycle can
open.

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

**The order is load-bearing, not politeness.** The command writes what it is told into the draft of
`solution.md`, and once that is placed it is protected, so an answer given after the fact costs a draft
and a rename rather than a sentence. Two options, and the first is the one that bites:

| | |
|---|---|
| `--platform` | `github`, `azure-devops`, or `local` for a repository with no remote yet. `local` writes no pipeline, so **no pull-request check governs a merge** and nothing mechanical decides what reaches the default branch |
| `--integration-branch` | where approved plans wait and built cycles land. **Leave it out** for `development`, which is what nearly everybody wants; name one only if your team already integrates somewhere else |

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
| **update** | `.sdlc/` is already there | nothing new — it refreshes the hook configs, the pipeline and `.sdlc/README.md`, stamps the README with the release that wrote them, names what a newer release ships that the declared stack never mentions, and never writes the seven files only a person writes. Run it after every upgrade of the binary; `sdlc doctor` says when it is due |

It writes, **commits** what it wrote, and then **creates the integration branch** from that commit and
switches to it, when the branch does not exist yet. The commit is deliberate: an untracked file is
untracked when the first cycle takes its diff, so the diff would record the whole file as new and a plan
saying it changes that file could never match. The branch is where you will commit what you place, where
a cycle's plan branch starts from, and where its pull request goes.

What lands, committed:

| | |
|---|---|
| `.sdlc/README.md` | the whole flow in one page, inside the solution |
| `.sdlc/.gitignore` | ignores the drafts below, so what is left of one never reaches a commit |
| `.claude/settings.json` | the `PreToolUse` and `Stop` hooks that call the gate, **merged** into whatever is already there |
| `.github/hooks/sdlc.json` | the same two moments, in Copilot's shape: `preToolUse` and `agentStop` |
| `.agents/hooks.json` | the tool-call moment in Antigravity's shape, in its `sdlc` section. Its turn end is deliberately not governed — `installing-the-plugin.md` has why |
| `AGENTS.md` | how to work in this repository, written for an agent. Created if absent, printed for merging if you already had one |
| `.gitattributes` | `* text=auto`: every text file stored with LF and checked out with your platform's endings, so a commit from another machine never shows a whole file changed. Created if absent; if yours lacks the line, it is printed for you to add |
| `.github/workflows/sdlc-verify.yml` | the chain check. **The only guarantee** — and written only where a platform was recorded. `--platform local` writes no pipeline at all |

What lands beside it, **not committed**, for you to fill in and rename:

| | |
|---|---|
| `.sdlc/solution.draft.md` | becomes `solution.md`: the keys adoption observed, and a prompt where your paragraph goes |
| `.sdlc/techstack.draft.md` | becomes `techstack.md`: the framework's default stack, for you to cut down. The options menu to read beside it is under `.sdlc/reports/` |
| `.sdlc/constraints.draft.md` | becomes `constraints.md`: the header and no rule. Optional |

**Each draft says at the top that only a person changes the real file**, because an agent's write to it
is denied — `../mechanisms/protected-files.md` says how that works and which files it covers.

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

**Where there was code first, adding the line-endings rule shows files as changed** that were committed
with CRLF endings. Commit `git add --renormalize .` once, on its own, so the change that follows does not
carry a diff of every file. The approval is not affected either way: it hashes text with its line endings
canonicalised.

**What does not land: a placed solution document, a declared stack, the build settings, and any code.**
The first two are yours to place from the drafts. The build settings belong to a stack nobody has
declared yet. The solution file, the projects, the architecture tests and the reference
slice arrive in your first cycle, behind an intent, a spec, a plan and an approval like everything else —
otherwise the first code in a governed repository would be the only thing in it with no chain behind it.

## 4. The interview

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
| whether a judgement was made and recorded | a `LOC_` constraint |

**For each answer that might be a constraint, the skill runs `sdlc propose constraints` itself** and fills
in the entry it prints. With no history to read, that is the next free `LOC_` id with its four parts
empty, beside the three admission tests — and **all three must pass:**

1. its `Forbids` clause names something that becomes illegal
2. **no analyzer, schema or hook could decide it**
3. it is incompatible with at least one plausible implementation

The second rejects most candidates — "every public method has a doc comment" is an analyzer, not a
constraint. **Proposing none is the right answer for most solutions**: three constraints nobody believes
in cost attention at every interrogation for the life of the repository.

## 5. Fill in the drafts

Adoption's report ends with what is left and how to place it, and `sdlc doctor` prints the same block
whenever you ask — it reads disk, so it is right after a rename it did not see. **Your agent cannot do
this part for you**: it may edit a draft, but placing one is yours.

**`.sdlc/solution.draft.md` — the paragraph.** It holds its keys and a prompt where the paragraph goes.
**The keys record facts; the paragraph is yours to write**, in place of the comment.

| Key | Values | What it decides |
|---|---|---|
| `origin` | `scaffolded`, `adopted` | where the code came from. **A historical fact that never changes** |
| `standard` | `profile`, `existing` | whether new work follows the profile's patterns or the surrounding code. Every plan reads it. **The one key you may change**, here before you place it or later with `sdlc draft solution --standard profile` |
| `platform` | `github`, `azure-devops`, `local` | whether a pipeline is written, and so whether a check governs a merge |
| `integration_branch` | a branch name; `development` when absent | where approved plans wait and built cycles land. The factory builds from it, and a cycle's diff is measured from where it diverged from it |
| `approval` | `required`, `none`; `required` when absent | whether a person approves each cycle's intent, spec and plan before it is built. Leave it `required` to start; `running-a-cycle.md`, under *Running without approvals*, says what `none` keeps and what it gives up |
| `coverage_floor` | a whole percentage; `80%` when absent | the whole-codebase coverage at or above which a cycle need not hold the coverage baseline; under it, the codebase may not fall. An adopted codebase starting low is kept from falling while it climbs; one already high is not refused for drifting a point. `handling-refusals.md` says when to lower it |
| `telemetry` | `appinsights:<application id>`; absent for most | where Operate reads production's errors from, to file them as leads on the platform's tracker. Optional, and written only when set |

Once it is placed
, `sdlc settings` shows each key, whether it was written or defaulted, and how to change it.

**Every plan reads the paragraph for what good looks like here, and `sdlc doctor` fails while it is the
template's text.** A dice roller adopted with the placeholder left in place, built a cycle later, came out
as a numeral in a bare `<p>` with every gate green: nobody had been asked what good looked like, so
nobody had been told.

**`.sdlc/techstack.draft.md` — the stack.** No cycle opens until it is placed. It is the second rule
source a plan is read against, so it has a guide of its own: `declaring-the-stack.md`. Write `none` in a
row that is not true here rather than deleting it.

**`.sdlc/constraints.draft.md` — this solution's own rules, if it has any.** Add at its end each entry the
interview produced, as the skill showed it — there is no command to run here. **Rename it even with no
entry**: it places a file with no rule in force, which says in the record that this solution has none
of its own. It is the one draft you may leave unplaced — a solution with no rules of its own is sound
either way — but a draft is ignored by git, so it reaches nobody else.

**The drafts are ignored by git, so a clone does not have them.** Somebody who clones before you have
placed the files, or who deletes a draft, gets it back by running adoption again: it drafts whichever
file is neither placed nor drafted and leaves the rest alone. `.sdlc/README.md`, in the repository, says
the same where a teammate will find it.

## 6. Rename them into place

From the repository root. In bash or Git Bash:

```sh
mv -f .sdlc/solution.draft.md .sdlc/solution.md
mv -f .sdlc/techstack.draft.md .sdlc/techstack.md
mv -f .sdlc/constraints.draft.md .sdlc/constraints.md
```

In PowerShell, where `mv -f` is ambiguous:

```pwsh
Move-Item -Force .sdlc/solution.draft.md .sdlc/solution.md
Move-Item -Force .sdlc/techstack.draft.md .sdlc/techstack.md
Move-Item -Force .sdlc/constraints.draft.md .sdlc/constraints.md
```

**On Claude Code, a `!` line runs in bash, whatever your terminal is** — so it is the first form there.
The last line is the optional one.

## 7. Commit them

```pwsh
git add .sdlc
git commit -m "Place the solution, its stack and its constraints"
```

**Commit them before the first cycle.** The intent skill plans in a sandbox, a worktree that starts from the
integration branch as committed, so a placed file left uncommitted is one the sandbox — and every cycle in it —
cannot see. You are on the integration branch adoption created, which is where these belong.

```pwsh
sdlc doctor
```

**It exits 0 when everything is in place**, and ends by naming the first cycle.

## 8. Set the branch policy, before the first pull request

**On a `local` solution, skip this: there is no pipeline to require.** Everywhere else it is the one
thing adoption cannot do for you, and it has to be in place before the first pull request is opened.
The pipeline step is the whole promise — nothing merges unless the chain is complete, approved and
hash-matched — and it is a promise only while branch policy requires it. Until then the file is advisory and the directory looks enforced without being
enforced, which is the failure mode this design is written against.

**Push the integration branch first**, because a policy needs a branch on the remote to name. Then:

```
sdlc branch-policy
```

It prints the commands that set the default (D-136), for you to run signed in to `gh` or `az` as an
administrator. It applies nothing itself.

| | The integration branch | The default branch |
|---|---|---|
| **Way in** | pull requests only | pull requests only |
| **Merged by** | squash, so a cycle is one commit | merge commit, so the two branches keep sharing history |
| **Required check** | the pipeline (`verify` on GitHub) | the same: on it, the pipeline runs `sdlc promotion`, which admits a pull request only from the integration branch or a `release/*` branch |
| **Approvals** | none required, unless you ask | the same |
| **Bypass** | nobody | nobody |

On GitHub that is two rulesets, written as JSON under `.sdlc/reports/branch-policy/`. On Azure DevOps it is a
merge strategy policy and a build validation policy on each branch, and one screen the CLI cannot set:
in **Project settings > Repositories > Security**, no group may hold **Bypass policies when completing pull
requests** or **Bypass policies when pushing**.

**It is a default, and any of it may be changed.** None is required because nobody can approve their own
pull request. To ask for reviewers, give the same command options, which apply to both branches:

| Option | What it asks | On GitHub | On Azure DevOps |
|---|---|---|---|
| `--approvals <n>` | how many approvals a pull request needs | the rulesets' required approvals | a minimum reviewers policy |
| `--owner <who>[,<who>]` | who must give one, at least one approval | code owner review, and a `CODEOWNERS` line for you to commit | a required reviewer policy; name a group to let any one member approve |
| `--self-approve` | an author may stand in for an approval | never counted, so `--self-approve <team>` lets that team bypass the rules on a pull request, which also lets it merge past the check | the author's own vote counts |

`CODEOWNERS` is GitHub's, not git's: a file GitHub reads from the branch a pull request merges into, so until
it has merged any approval counts, and afterwards changing who must approve is a reviewed pull request too. **An administrator can still edit or
switch off a policy** on either platform, and nothing a repository holds prevents that.

**How often an open pull request is checked again.** A pull request is checked against the branch as it
was when the check ran. Two pull requests can each pass and merge without a conflict and still break
together — one renames a method the other calls — and only re-checking after the first merges catches
that before the second does. What to set is a trade between CI minutes and how early you find out:

| Setting | Re-checks | Finds the break |
|---|---|---|
| **Immediately when the branch is updated** (the default `sdlc branch-policy` sets) | every open pull request, each time anything merges | before the second pull request can merge |
| **After n hours if the branch has been updated** | a pull request whose passing check is older than n hours | before merging, unless both merge inside the window |
| **Never** | nothing | after both have merged, at the next build that includes them |

On GitHub the equivalent is **Require branches to be up to date before merging**, which the rulesets set and
which makes a pull request's author bring it up to date and re-run the check; a **merge queue** checks each pull
request against everything queued ahead of it. Azure DevOps has no merge queue.

**On Azure DevOps the build validation policy is not only what makes the check required — it is what runs
it.** Azure Repos ignores a `pr:` trigger written in YAML, so the pipeline has no trigger of its own, and the
file says so at the top. Until the policy exists, no pull request is built, tested or verified, and nothing
fails to tell you.

**`sdlc doctor` reads it back**, through `gh` or `az` signed in as you. A branch whose policy does not require
the check is a problem, on the integration branch and on the default branch alike; how each branch merges is
reported against the default and never a problem. When the CLI is not signed in it names this step instead,
marked `elsewhere`.

## 9. Open the first cycle

```
/sdlc:intent
```

**The skill opens the cycle and switches to its branch itself** — you do not run `sdlc new` or `sdlc
branch`. Then it interviews you, and **nothing more happens until you answer**: the intent is written
from your answers, so there is nothing for an interrogation to read before then. `running-a-cycle.md`
takes it from there.

## What your solution owns

**Seven files only a person writes**, and every tool that could write one prints or drafts what belongs in
it instead. The pre-tool hook denies every agent write to all seven, by file path and by shell command
alike — `../mechanisms/protected-files.md` is the whole of how.

| File | What it holds | Changes when | How it gets written |
|---|---|---|---|
| `.sdlc/solution.md` | its keys and the paragraph | the paragraph whenever it stops being true; `standard` or `approval` when you choose | adoption drafts it first; after that `sdlc draft solution`. Then your rename |
| `.sdlc/techstack.md` | what the solution is built from, the stack rules it is held to, the build settings it takes | a default or a rule turns out wrong for this solution | adoption drafts it first; after that `sdlc draft techstack`. Then your rename — `declaring-the-stack.md` |
| `.sdlc/constraints.md` | this solution's own `LOC_` rules, the last word over every other tier | a rule keeps being cited across cycles, or one stops earning its place | adoption drafts it empty; `sdlc propose constraints` proposes entries; you paste and rename |
| `.sdlc/decisions.md` | choices this solution already settled, so a cycle reads what was argued rather than arguing it again. Binds nothing | an approved cycle settles something worth not re-deciding | `sdlc propose decisions` reads approved cycles; you paste |
| `.sdlc/areas.md` | the parts of the code, each a name for a set of files, so a reader gets in full only the rules and decisions its change reaches. Absent until three cycles' plans name an area | mining proposes an area, a rename, a merge, or globs for files that moved | `/sdlc:mine` proposes; `sdlc draft mining` drafts; you rename |
| `.sdlc/cycles/<id>/approval.md` | the decision on one cycle's three planning documents | at the gate, and again if a document changes | `sdlc draft approval <id>`, then your rename |
| `.sdlc/cycles/<id>/abandoned.md` | why one cycle stopped for good | once, and it is terminal | `sdlc draft abandon <id>`, then your rename |

**Your agent can do everything up to the last step.** It can run the drafter, edit the draft to what
you asked for, and show you the line that places it. Then it stops, and you run that line — which on
Claude Code is the same line with a `!` in front.

**Commit what you place.** A sandbox starts from what the integration branch committed, so a file you placed
and did not commit is missing from every cycle you open next.

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
| why an agent cannot write the files above | `../mechanisms/protected-files.md` |
| every command on one screen | `../reference/commands.md` |
