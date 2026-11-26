# Handling refusals

Every refusal the framework produces, what it means, and what to do next. `../reference/refusals.md`
is the same list as one line each.

**A refusal is a command or hook declining to go on.** It is not the same thing as a blocker — a
blocking finding on a document, or a `blocked` verification — though an open blocker is one of the
things that causes refusals. Where the remedy for a refusal is answering a blocker, the entry below says
so and `handling-blockers.md` has the walk.

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

## Opening a cycle — `sdlc new`, and `sdlc branch <id> plan`

**`sdlc new` refuses one way, and before an id is spent**, so nothing is left behind to clean up. The second
refusal here is `sdlc branch <id> plan`'s, met only when you ask to plan in this checkout rather than a
sandbox.

### no declared stack

`.sdlc/techstack.md` is not there. Adoption drafts it and a person places it, so this is where every
solution stands between adopting and renaming the draft, and where a deleted file leaves one. A cycle
opened now would reach its plan with nothing to be read against and no stack rule in force: every gate
green, and the argument the stack file exists to force never made.

**Next:** fill in `.sdlc/techstack.draft.md` — the framework's default, cut down to what is true of this
solution — and rename it into place yourself, then commit it. If the draft is gone, `sdlc draft
techstack` writes it again, or `sdlc draft techstack --empty` writes one that pins nothing. An empty file
opens a cycle; a missing one does not, because one is a decision and the other is nobody having made it.

### uncommitted work

`sdlc branch <id> plan` was asked to switch this checkout, and something in the working tree is not committed;
the refusal names the files. A switch carries uncommitted changes with it — so they would ride into the cycle
and reach its diff as work no plan declared. Opening a cycle no longer refuses: the intent skill plans in a
sandbox, a worktree of its own, which switches nothing (D-144). This meets you only when you asked for one
working copy.

**Next:** decide what each file is — commit it, stash it, or discard it — and ask again; or plan in a
sandbox instead, `sdlc branch <id> sandbox`, which the refusal names. The
intent skill asks you rather than choosing, because work moved out of sight is work you have to find. The
framework's own ignore files under `.sdlc/` never count, and neither does anything git ignores, which
includes the drafts.

## `sdlc gate` — every way a build is refused

`sdlc gate <id>` exits 1 and says which of these it is. In rough order of how early you hit them, and
this is the whole list:

| | |
|---|---|
| no cycle · abandoned · a planning document is missing | nothing to gate |
| not approved · the ledger cannot be read · the standing decision refuses it | the gate was not passed |
| a findings round cannot be read · a document was never interrogated | **the loop cannot be shown to have run** |
| a document changed since it was approved · a findings round it anchored has changed · a blocking finding is open · a findings round is stale | the approval no longer covers what is there |

### no cycle

The cycle has no directory. Nothing has been written yet.

**Next:** `sdlc new`, which allocates the id and creates the directory. If you expected a cycle to be
there, you have the wrong id — `sdlc cycles` lists every cycle this solution holds.

### abandoned

The cycle was stopped, which is terminal: by its own `abandoned.md`, or by the one beside its series' record
when the whole series was rejected.

**Next:** open a new cycle. An abandoned cycle does not reopen, and the record says why it stopped —
which is the point of it being a file a person wrote rather than an exit an agent took.

### a planning document is missing

One of `intent.md`, `spec.md`, `plan.md` is not there. All three are required, always.

**Next:** `sdlc status <id>` names which, and ends with the literal command to produce it.

### not approved

There is no approval ledger. Nobody has been to the gate. Never raised in a solution that declares
`approval: none`, where the gate asks for no ledger and keeps its other refusals (`running-a-cycle.md`,
under *Running without approvals*); `sdlc settings` says which the solution declares.

**Next:** `sdlc draft approval <id>`, then rename the draft it writes onto the ledger beside it.
**Asking an agent to rename it will be denied** — see the hook section below. That denial is the
mechanism, not a configuration problem.

### the ledger cannot be read

There is an `approval.md` and it is not valid YAML — almost always a decision placed by hand, or a
half-finished edit.

**Next:** `sdlc draft approval <id>`. It still runs, and the draft it writes is a complete replacement.
Nothing is carried forward from a file that will not parse, because that is a failed placement rather
than a decision somebody took — so read the draft, and if the broken file did hold a decision worth
keeping, recover it from git rather than from the file.

This refuses rather than failing, which is the point: the one file a person writes is the one most
likely to be malformed, so `status` and the drafter both have to survive one. A malformed ledger never
reads as approved.

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

### a findings round cannot be read

A findings ledger, `findings/<artifact>.json`, will not parse; or its chain does not verify, because a
round, a finding it raised or a disposition it moved was changed after `sdlc round` wrote it; or round
files from before the ledger sit beside it. A round file from before the ledger that will not parse is
this refusal too, and so is a suggestions ledger, `findings/suggestions.json`, that will not parse, names
a round its document's ledger does not hold, or records two `raised` in a row against one document — a
line no round writes (D-115).

**Next:** put the ledger back as `sdlc round` wrote it — `git restore` it, or remove the stray round files
beside it — and interrogate again if a round is genuinely lost. What it will not do is skip the
artifact — **"could not tell" must never read as "clean".** That is not hypothetical: overwriting one
round file with `{}` once made every blocker for that document disappear from the gate and from
`verify-chain`, which is a one-command waiver for the one thing that cannot be waived by any mechanism.

### a document was never interrogated

A planning document has no findings round at all — most often because `findings/` was deleted.

**Next:** `/sdlc:interrogate <id> <artifact>` for whichever the message names. An artifact nobody
challenged is not a clean artifact, and without this refusal deleting the directory would leave the gate,
`verify-chain` and `pr-ready` all passing with no round having ever existed. The drafter refuses this too, but that is
advice a person can decline to take; this is the layer that does not negotiate.

### a findings round it anchored has changed

The approval records, for each of the three documents, the round its findings ledger had reached and that
round's hash (D-107). One of those rounds is missing, or no longer hashes to what was approved. The
ledger's own chain cannot catch a rewrite on its own, because anyone who can edit the file can recompute
every hash in it. The anchor can, because it lives in `approval.md`, which no agent can write.

Rounds added after the anchored one are not this refusal. A blocker raised in one of them is
*a blocking finding is open*.

**Next:** restore the ledger from git if it was changed by mistake. Otherwise the argument behind the
approval is not the one on disk: interrogate again and take the approval again with
`sdlc draft approval <id>`.

### a blocking finding is open

The one with no way out but to answer it.

**Next:** `/sdlc:refine <id> <artifact>`, then a fresh interrogation. `handling-blockers.md` is the
whole walk: the three exits a finding can leave by, what refine may not do, and what to do when the loop
hands back.

Reaching this refusal at all means the blocking finding was raised after the approval entry was printed,
or the entry was written by hand — the printer refuses to produce one while a blocking finding is open. The check lives
in both places because the printer is advice a person can decline to take and the gate is the layer that
does not negotiate.

### a findings round is stale

A round judged its artifact against an upstream document that has since changed — or judged a version
of the artifact itself that has since been edited. The three approval hashes prove the three documents
were approved; they do not prove the spec was derived from *this* intent, or that the plan approved is
the plan a reader cleared. Only the round knows what it read, which is why it records both.

Under `approval: none` the rounds are the only record of what was judged, so a round written before rounds
recorded their subject is refused this way too: no person read the bytes instead.

**Next:** re-interrogate the downstream artifact — `/sdlc:interrogate <id> spec`, and the plan after it
if the spec changed. *Take it to plan* (`/sdlc:to-plan`) does this for you, after `steer` when you said what
should change, which is the reason to use it rather than editing documents by hand.

## `sdlc round` — the twelve ways a round is refused

`sdlc round` writes every findings round (D-101): blockers into the artifact's findings ledger (D-107),
suggestions into the cycle's suggestions ledger (D-115). `/sdlc:interrogate` runs it; you meet the first
when the order of the chain was skipped, the second when a finding was carried forward in different
words, the third when a reader raised a blocker without saying in a sentence what set it off, the
fourth when a blocker cites a rule its reader was not handed (D-174), the fifth when a new finding quotes its
rule in words other than the rule's own (D-177), the sixth when a suggestion's id or its move does not fit
the cycle's suggestions, and the seventh when a suggestion has nothing to read or says what another already
says (D-223). The next is a document citing a decision or a criterion that does not exist (D-223). The next two
are about areas (D-150): a plan that leaves a declared file without one, and an area whose files have moved. Then
a verify round that could not change where the cycle goes (D-165), and last a verdict whose Not met or open entry
does not say what it is about (D-222).

### something above it is unsettled

A round about a document is only worth recording once every document above it was read as it stands and
nothing there still blocks. The refusal names, for each document above, what is wrong: it has no round,
it changed after its latest round, that round judged it against an upstream that has since moved, or a
blocking finding is still open. A spec judged while its intent is still moving is a clean reading of a
document that is about to stop existing — in one real cycle, seven plan rounds were spent that way.

**Next:** settle the highest document the refusal names — `/sdlc:interrogate <id> <artifact>`, and
`/sdlc:refine` until its round is clean — then come back down, one document at a time.

### a known finding comes back in other words

The round hands back an id the ledger already holds, with a different severity, citation or statement. A
finding is stored once, so one id meaning two objections would make the history unreadable. Handing back
the same finding where it already stands is fine, and records nothing.

**Next:** carry the earlier finding's severity, citation and statement exactly as the ledger holds them —
`sdlc findings <id> --stage <artifact>` prints them — and give a new objection a new id. The refusal names
the next free one under that prefix. The usual cause is a perspective reader, which numbers from `F_01`
every round because it has no history (D-108).

### a new blocker has no gist

The round raises a blocking finding the ledger has not seen, and its `gist` is missing, empty, longer
than 240 characters or over more than one line. The gist is the one plain sentence a panel shows first,
so a blocker without one reaches the person at the gate as a rule id and a paragraph (D-110). A finding
carried forward from before gists needs none.

**Next:** ask the reader that raised it for the gist — start that fresh process again with the same
input — rather than writing one in this session, which would be reinterpreting a finding it did not
raise. The refusal names each finding that wants one.

### a new blocker cites a rule not in force for it

The round raises a blocking finding the ledger has not seen, and the rule it cites is not one its reader was
handed: superseded, written for another document — a spec's rule raised against a plan — not issued at all, or,
for a perspective's finding, not one the perspective lists as a rule it blocks on (D-174, D-175). A blocker
stops a cycle on the rule it names, so it has to name one that binds here. `sdlc round collect` already records
such a blocker as a suggestion saying so; this refusal meets a draft edited after that. A blocker the ledger
already holds is never judged again.

**Next:** make the finding a suggestion, or cite the rule in force it actually breaks —
`sdlc constraints --for <artifact>` lists them, and `sdlc perspective show <name>` a perspective's.

### a new finding quotes its rule in other words

The round carries a finding the ledger has not seen, and the line beside its citation is not the line of the rule
it cites. A finding carries its rule's own words so a cycle read later shows what it was judged under, and a
paraphrase there is a second version of the rule shown to the person at the gate as the rule (D-63, D-177).
`sdlc round collect` already copies the rule's line in; this refusal meets a draft edited after that. Whitespace,
quotes and dashes are evened out before comparing, and nothing else. A finding the ledger already holds keeps the
line it was recorded with.

**Next:** put the rule's line back as it is — the refusal prints it, and `sdlc constraints --for <artifact>` prints
every one — or run `sdlc round collect` again, which copies it.

### a suggestion does not fit the cycle's suggestions

A suggestion is the cycle's, not the document's, so `findings/suggestions.json` holds each concern once
(D-115). The round is refused when a new suggestion's id is taken anywhere in the cycle or does not end
`S_` and a number; when it answers or withdraws a suggestion against a document that never raised it; or
when it calls a suggestion blocking under the suggestion's own id. A reader's ids are its own, so the
usual cause is a perspective reader numbering from `F_01`.

**Next:** number a new suggestion past the cycle's — the refusal names the next free one, as in
`accessibility.S_04` — and carry a restated one under its standing id, which the reconciler finds among
every document's open suggestions. Answer a suggestion only on the documents that raised it. A suggestion
that has become a blocker is handed back `withdrawn`, and the blocker takes a new `F_` id of its own.

### a suggestion with nothing to read, or in another's words

A new suggestion is refused when its statement is empty, when it carries a gist that is blank, or when its
statement is word for word — past spacing and case — what another suggestion of the round or of the cycle's
ledger says. Each would stand as an item somebody must answer: one with nothing in it, or one answered twice
(D-223). `sdlc round collect` already sends back a reader that raised a finding with no statement, drops a blank
gist, and carries a copy under the first one's id; this refusal meets a draft edited after that.

**Next:** carry the copy under the id the refusal names, and leave out a gist you have no sentence for. A finding
with no statement is its reader's to write: start that reader again rather than writing it in this session.

### a document cites what does not exist

Writing a round's prompts, and recording the round, are refused when the document cites a number that points at
nothing: an intent, spec or plan naming a `DEC_nn` the solution's `decisions.md` does not hold, or a spec, plan or
verify naming a criterion the spec does not have — a proof row, an adjudication, *criterion 4* in prose — or a spec
whose criteria are out of order (D-223). A reader would spend the round on the number. The usual causes are a
decision proposed by a harvest and not yet placed, a decision placed in another checkout and not committed to the
integration branch — a sandbox starts from what that branch committed — and a spec renumbered under its references.
A cycle opened before 0.58 is not held to it.

**Next:** place the decision and bring it onto this branch, or cite what `sdlc decisions` lists; give a criterion
reference the number the spec has now. A reference to another cycle's criterion names that cycle beside it. On a
verify, run the adjudicator again: it wrote the number.

### a declared file with no area

A plan round is refused when the plan declares a file that no area in `.sdlc/areas.md` covers and its
`## Areas` section does not account for either. Every declared file has an answer: an area the solution
defines, a name the plan proposes for the part of the code it belongs to, or `none` for a one-off. Which
files are left over is a listing; what to call them is the plan's to say (D-150). A plan of a cycle opened
before areas is not held to it.

**Next:** add the files the refusal names to the plan's `## Areas` section — `- new <name>: <files>`,
with the name the team would use, or `- none: <files>` — and record the round again.

### an area covers no file

Writing a round's prompts is refused while an area in `.sdlc/areas.md` has globs that match no file in
the solution. Its rules and decisions would reach no reader in full, which is the one thing areas must
never do. The files usually moved in a refactor (D-150).

**Next:** `sdlc areas` shows what each area covers. `/sdlc:harvest-areas` proposes the new globs as a `files`
line, `sdlc draft harvest` writes the draft and you rename it — or correct the `Files:` line by hand, since
the file is yours.

### a blocked verdict on evidence a round already judged

Writing a verify round's prompts is refused when `verify.md` reads `blocked` and the evidence is what the last
verify round judged. It happens after a new adjudication concedes a blocker: the outcome sends the cycle to
remediate whatever the readers say, so a round there pays for every reader to confirm it. One verify spent
1.1M tokens doing exactly that, four minutes after the round before (D-165). A verdict that still claims a
pass on that evidence is judged as before, and the run remediate records gets a full round.

**Next:** `/sdlc:remediate <id>`.

### a verdict that does not say why, or bounds nothing

`sdlc round verdict` refuses the adjudicator's answer when a criterion is marked Not met without saying which kind
— `Not met (contradicted).` when the evidence shows it failing, `Not met (unproven).` when it does not show it
either way — or when an entry under `## Open` names no criterion, one the verdict does not adjudicate, or one it
marks Not met. Both kinds block alike, but they tell remediate different things: the code to fix, or the proof to
strengthen. An open entry bounds a criterion that was met; one bounding nothing stood nine rounds in one cycle
(D-222).

**Next:** `sdlc round run <id> verify` again for a new adjudication. Never repair the answer in the session:
the repair is this session writing the verdict.

## `sdlc scaffold` — the two ways it is refused

`sdlc scaffold <id>` writes the solution skeleton on a solution that held no code, and on one adopted
into existing code only the build settings files its plan declares.

### the gate refuses the cycle

It asks the gate the same question the build stage asks, so the message is the gate's own and the
remedy is whichever one above matches it. Nothing is written for a cycle that is not approved.

### an adopted solution whose plan declares no build settings

The solution's origin is `adopted`, so it already has a shape and the skeleton would lay a second one
over it. What scaffold can write for it is the build settings, and only the ones the approved plan
names in its `Files` section. This plan named none.

**It is not refused for the row being `false`, and a row being `true` is not enough.** The plan is what
a person approved for this cycle. A file that makes warnings errors does not arrive on the strength of
a row, in a cycle about something else.

**Next:** `sdlc stack` shows which files the declared stack takes and which are missing. If this cycle
is meant to bring one, the plan declares it — `sdlc draft approval <id> --reopen`, add the file, and
approve again. If it is not, there is nothing for scaffold to do here: build without it.

---

## `sdlc build` — the two ways its own process is refused

`sdlc build <id>` starts the build in a process of its own, in a worktree beside the repository. That process is
headless: it asks nobody anything, so whatever it is not allowed is refused and stays refused.

### a solution document the build cannot read

The worktree's `.sdlc/solution.md` would not read, so the build was never started. Its own message says which key,
and the usual one is `build_tools` naming something that is not one command: `docker *`, a path in place of the
command, a quote, a variable, `;` or `&&`. Each entry is allowed as `Bash(<entry> *)`, so any of those would allow
more than the command you meant. A command line is fine: `docker build` grants that line and never `docker run`
(D-224).

**Next:** `sdlc draft solution`, correct the line to commands separated by commas — `build_tools: pwsh, docker
build` — rename the draft into place, commit it, and build again.

### a tool the build's process was not given

The build stopped as not finished, and `sdlc build` printed *The build's process was refused … no test failed for
it*, with each refused command under it, read from the host's own record of what it refused (D-224). The process
is allowed the skills its playbook runs, `sdlc`, `git`, `dotnet`, `gh`, `az`, the file tools, and each command
`build_tools` names (D-202) — **and nothing from a checkout's personal settings**. The worktree is a fresh
checkout, so a rule you added to `.claude/settings.local.json` in your own checkout never reaches it, and in a
headless process there is no prompt to approve it either.

**The other way round.** A rule in your user-level host settings does reach a build on your machine, so a build
here can pass on a command `build_tools` never granted, and the factory is then refused it. `sdlc build` says so
before it starts — *This machine's own host settings also allow …* — and `sdlc doctor` says it too. Grant what the
build needs in `build_tools`, so it holds wherever the build runs.

**Next:** when the refused command is one the solution's own build needs — an evidence script's shell, a
container — add it to `build_tools` with `sdlc draft solution` and a rename, commit it, and build again: the bare
name grants the tool whole, a command line such as `docker build` only that line. When it
is a command a skill should not have needed, such as a pipe into `tail`, build again: the playbook writes its
commands one at a time, and a refusal of one is not a missing tool. A refusal of `sdlc build` itself, **in your
own session**, is your host's permission check rather than the framework: allow `Bash(sdlc *)` in your host's
settings, as you would any command you want an agent to run without asking.

---

## Amending an admitted plan — the two ways it is refused

Once a cycle's plan is on the integration branch, the team and the factory read it there. A reopen after
that is an amendment to what they read, and it goes back by a plan pull request of its own (D-123).

### a reopen on the build branch

`sdlc draft approval <id> --reopen` was run on `cycle/<id>/build` or `build-unattended`, and the cycle's
plan is already on the integration branch. Reopened there, the amendment would reach the integration
branch only with the code, and the factory would keep reading the plan you just withdrew.

**Next:** `sdlc branch <id> plan` — it starts the plan branch from the integration branch — then reopen,
correct and approve again there, and `/sdlc:pr <id> --plan` opens it as an amended plan. The build branch
is then yours: merge the integration branch into it to continue by hand, or delete it and the factory
builds the amended plan on its next run.

### a plan branch left over from earlier planning

`sdlc branch <id> plan` found a local `cycle/<id>/plan` whose approval ledger lacks the approval the
integration branch holds. It is left from earlier planning, and an amendment made on it would amend a
plan nobody reads any more.

**Next:** delete it — `git branch -D cycle/<id>/plan` — and run `sdlc branch <id> plan` again, so the
amendment starts from the integration branch.

---

## Deviations — the one way they are refused

Where the build could not follow the approved text it recorded a deviation in `deviations.md`, and where the
approved text was silent it recorded a choice (D-156, D-180). **Nothing answers them before the pull request**:
they lead it in the build's report, and merging accepts them (D-181). `handling-blockers.md` has what a
deviation is and how to ask for one to be changed. `sdlc deviations <id>` shows each with where it stands.

**Two refusals are gone.** The pull-request check no longer waits for every deviation to be answered, and a
reopen is no longer refused while one is unanswered: there is nothing left to answer before the merge.

### a deviation that does not hold

The check's *the deviations are sound* failed, or `sdlc deviations <id>` exited 1: a deviation quotes text the
approved document does not hold, quotes the wrong document for its kind, adds a file another open cycle
claims, replaces a deviation the file does not record, or **narrows a criterion** — no deviation changes what
*done* means (D-179). On a cycle from before D-181, a criterion narrowing a person accepted in the approval ledger
still holds, and fails only with its successor not written.

**Next:** the stage that wrote it corrects it — quote the approved text word for word. A criterion narrowed is not
corrected but taken back out: the cycle escalates, and you reopen the plan. On an older cycle, write the successor
under `.sdlc/successors/`. A file another cycle claims is not a deviation at all: leave it to that cycle, or
reopen.

---

## Successors — the four ways one is refused

A successor is a slice that waits on another's code, as a file under `.sdlc/successors/` (D-125). Where it
stands is worked out from the record every time, so a refusal here is the record saying so, never a
setting. `sdlc successors` shows every one; `sdlc successor check` refuses the last three, and the
admission check runs it.

### a successor that is not ready

`/sdlc:intent --from successor:<slug>` — through `sdlc lead show` or `sdlc successor start` — was asked to
start a successor something it succeeds has not merged into the integration branch with a passing verify.
The message says which. Started now, its cycle could not be built alone, which is the one thing a slice
promises. One that is **stuck** waits on an abandoned cycle and never comes ready by itself.

**Next:** wait, and start it once `sdlc successors` says `ready` — the sitrep and `sdlc status` name it then.
For one that is stuck, correct its `succeeds:` to name what replaces the abandoned cycle, or delete the
file if the piece is no longer wanted.

### a loop through succeeds

Two or more successors wait on each other — `successor:a -> successor:b -> successor:a` — so none can ever
come ready.

**Next:** decide which really comes first and correct the other's `succeeds:`, so the loop opens.

### a reference to nothing

A `succeeds:` entry names a cycle id that is nowhere — not in this working copy, not on the integration
branch, no plan branch — or a `successor:<slug>` with no file and no intent recording it as its lead.
Usually a typo; sometimes a sibling's plan branch that was never pushed or composed.

**Next:** correct the entry. A sibling opened in another worktree resolves once its plan branch exists here,
and on `slices/<slug>/plan` once the slicing is composed.

### a successor file that cannot be read

The file has no frontmatter, a key a successor does not carry, an empty `succeeds:`, an entry that is
neither a cycle id nor `successor:<slug>`, a name that is not a slug, or no `## Outcome` or `## Why`. The
message lists every problem at once.

**Next:** correct the file. A piece that waits on nothing is not a successor: start it as a cycle of its own.

---

## A slicing's pull request — the two ways it is refused

`slices/<slug>/plan` admits every plan sliced together at once (D-125). Each cycle in it gets the
admission check it would get alone, and one that fails refuses them all — the entries under `sdlc gate`
above say what each of those means. Its successors must be sound, as above. Two refusals are a slicing's
own, and `sdlc verify-chain slices/<slug>/plan --base <branch>` gives both.

### a plan that needs a sibling's code

One plan names a file the integration branch does not have and that it does not create, while a sibling's
plan marks that file `new`. That piece cannot be built against the integration branch as it stands, so it
is not atomic: it needs the sibling merged first. The check names the plan, the sibling and the files.

**Next:** make the dependent piece a successor of its sibling — abandon its cycle, and write
`.sdlc/successors/<slug>.md` with `succeeds:` naming the sibling — or merge the two pieces into one cycle.
Files two plans both name *without* one creating them for the other are only reported: their pull requests
may conflict at merge, which *fix it* (`/sdlc:remediate`) resolves through `resolve-conflict`.

### a slicing that carries more than plans

A path in the pull request is neither a cycle's intent, spec, plan, findings or approval, nor a file under
`.sdlc/successors/`. A slicing admits plans; code comes back by each cycle's own pull request.

**Next:** take the other files off the branch. It holds nothing of its own, so composing it again from
the plan branches with `sdlc slices` is usually the quickest way.

---

## A run's pull request — the ways it is refused

`wave/<id>` and `series/<id>` carry several cycles built one after another into one branch. Each cycle whose
folder the pull request changes gets the whole chain it would get alone, and one that fails refuses the run —
the entries above say what each of those means. An abandoned cycle in it is admitted as a record. Three refusals
are a run's own, and `sdlc verify-chain series/<id> --base <branch>` gives all three; `sdlc pr-ready` on the run's
branch gives the same answer before the pull request opens.

### a run that carries no cycle

Nothing the pull request changes is under `.sdlc/cycles/`, so there is no chain to check. A run's branch
carries the cycles it built.

**Next:** build a cycle on the branch, or open the change from a branch of its own.

### a named test that failed at the end of the run

A test one of the run's plans names in its `## Proof` did not run and pass in the last cycle's recorded
run — the cycle every other one was built before. A later cycle broke what an earlier one proved, and
nothing verifies the earlier cycle again. The check names each test and the cycle whose plan named it.

**Next:** fix it on the run's branch, then record the last cycle's evidence again, so its run is the one
that shows every named test passing.

### code no verified cycle accounts for

A file outside `.sdlc/` that the pull request changes is in no built cycle's recorded change, or changed after
the last cycle's recorded head. A cycle that carries only its paperwork — an abandoned one, a plan nobody built —
is read from its own folder, so without this a run could carry code nobody verified while every cycle in it
passed. Two causes are common: a cycle whose code was committed and whose evidence was never recorded, and an
abandoned or unbuilt cycle whose code was left on the branch. A run with no built cycle at all that changes code
is refused the same way. What a merge of the integration branch brought after the last cycle does not count. The
check names each file.

**Next:** if a cycle built the file, record that cycle's evidence on the run's branch (`sdlc evidence <id>`); if
the file belongs to a cycle that was abandoned or never built, revert that code. A change made after the last
cycle's head is recorded by recording the last cycle's evidence again on top of it, or reverted.

---

## A series, and a run's own commands — the four ways they are refused

A series runs wave after wave into `series/<id>`, in a worktree of its own; `sdlc build --run` and
`sdlc report --run` work on a run's branch, `wave/<id>` or `series/<id>`. Each refuses before it does anything.

### fewer than one wave

`sdlc series start --waves <n>` was given a number below one. A series stops at its number of waves or an empty
backlog, whichever comes first, and the number is the person's: an open-ended run is spend nobody chose. A
`--wave-size` below one is refused the same way: a wave takes at least one item, and 3 when no size is named.

**Next:** start it again with the number of waves it may run, and leave `--wave-size` out or give it a number.

### not on a series' branch

`sdlc series wave`, or `sdlc series status` with no id, ran somewhere other than `series/<id>`. A wave is judged
against the series' branch and recorded in its record, which only its worktree carries.

**Next:** run it in the worktree `sdlc series start` printed, or pass that path with `--solution`.

### a series that has taken every wave

`sdlc series wave` found the record's wave limit reached. The series is finished.

**Next:** open its pull request. What is ready next is a new series' to take.

### not on a run's branch

`sdlc build --run` or `sdlc report --run` ran somewhere other than `wave/<id>` or `series/<id>`. A run builds, and
reports on, the cycles its own branch carries.

**Next:** run it in the run's worktree, or pass it with `--solution`; for one cycle, `sdlc build <id>` or
`sdlc report <id>`.

---

## An upgrade's or a version move's pull request — the five ways it is refused

A pull request on a branch that is no cycle's normally gets *no chain*. **One that changes only the pin in
`.config/dotnet-tools.json`, what `sdlc init` and `sdlc factory` then write, and `techstack.md` is checked as
an upgrade instead** (D-133). The branch can be called anything. The check runs as the release the branch
pins, and compares each framework file with what that release writes from the base's copy, a pipeline step by
step. **A pull request that only moves dependency versions** — in `Directory.Packages.props`, `global.json` or
the tool manifest, of any size — is checked the same way, with no release reproduced unless the pin moves
(D-134). One path outside those and it is neither, so the pull request gets *no chain* as before.

### the check runs as another release

The branch pins no release, or a release other than the one running the check. The comparison would be
with another release's adoption, so it means nothing.

**Next:** the generated pipeline restores the pin before this step; a pipeline written before D-131 does
not, and `sdlc init` rewrites it. Locally, run the check from the solution, so the global `sdlc` hands it
on to the pinned release.

### a framework file adoption did not write

A file in the pull request differs from what this release's adoption writes from the base's copy. Usually
a hand edit — a permission added to `.claude/settings.json`, another tool moved in the manifest — or the
files were written by an older binary than the pin names. A pipeline is not compared this way: see the next
entry.

**Next:** take the team's own change out into a cycle of its own, or run `dotnet tool restore`, `sdlc
init` again, and commit what it changes.

### a pipeline step the release did not write

A pipeline is compared as steps, because a solution keeps steps of its own beside the framework's. Every
step this release writes must be there exactly, in its order; every other step must be one the base already
had, unchanged; and outside the steps — triggers, schedule, parameters, pool — each section must be the
base's or the template's, on its own, so a team keeps its own pool while taking the release's new trigger.
Comments and blank lines are not compared. The refusal names the step or the section: one of the framework's
missing, edited or moved, one of the team's added or changed, or a section that is neither.

**Next:** put the framework's step or section back as `sdlc init` prints it, or `sdlc factory` writes it for
the factory's pipeline, and keep the team's own as the base had them. Adding or changing a step or a section of
the team's own is a cycle's.

### a dependency file that changed more than versions

`Directory.Packages.props`, `global.json` or `.config/dotnet-tools.json` changed something besides a version:
a package or a tool added or removed, a property, a roll-forward policy, the test runner. A version moves
without a cycle because there is nothing to decide; a package added or removed is a decision about what the
solution is built from. The refusal names the package when one was added or removed.

**Next:** keep only the version moves in this pull request, and add or remove the package in a cycle. A major
move or a move back is allowed — the check names it rather than refusing it.

### a stack line that answers nothing

`techstack.md` lost or changed a line, or gained one that is not an answer to a row this release ships and
the base's stack never mentioned. An upgrade may answer what the release asks — a rule `true` or `false`,
a default with the profile's choice, another, or `none` — because nothing already in force moves. Changing
an answer already given re-judges what plans were argued against, which is a cycle's.

**Next:** keep only the new rows in this pull request, and change any other line in a cycle. `sdlc stack`
lists what the release asks.

---

## `sdlc upgrade` — the one way the checklist is refused

### no release to move to

`sdlc upgrade` is refused when the release it would move to is a local build — this machine runs one through
`SDLC_DEV_BUILD` and no release was named — or when the word given is not a release. A local build is never
pinned or published (D-135), so a checklist moving a solution to one would end in a pin no feed can restore.

**Next:** name the release: `sdlc upgrade 0.46.0`, as nuget.org lists `MofonCode.Sdlc.Cli`.

## A promotion's pull request — the one way it is refused

A pull request into a branch that is neither the integration branch nor a `release/*` branch — main, usually —
is a **promotion**, and the pipeline's `verify` job asks `sdlc promotion` about it before anything else. A
promotion has no cycle, so the chain is not asked of it; it is built and tested, and that is all (D-136).

### a branch that may not promote

Only the integration branch and a `release/*` branch may open a pull request into the default branch. Anything
else would put on main code no chain judged: a cycle's branch, a feature branch, a fix made straight against
main.

**Next:** open the pull request into the integration branch instead, as a cycle, and promote from there. A fix
a release needs now is a cycle on its `release/*` branch, which then promotes (D-88).

## A harvest — the ways it is refused

A harvest is not a cycle (D-104): each aspect's sitting proposes, a person places what they accept, `sdlc harvest
record` records the aspect answered (D-191), `sdlc harvest close` closes the cycles every aspect has answered, and
their folders leave through a pull request of their own (D-152, D-192).

### a draft is still beside its file

`sdlc harvest record <aspect>` is refused while that aspect's draft — `constraints.draft.md`,
`decisions.draft.md`, `techstack.draft.md` or `areas.draft.md` — is still in `.sdlc/`, and `sdlc harvest close`
while any of them is. A draft not renamed is a decision the person has not made yet, and recording over it would
mark an aspect answered whose answers were never placed.

**Next:** rename the draft into place, or delete it if you decided against it, then run the command again.

### nothing left to record

`sdlc harvest record <aspect>` is refused when no completed cycle still needs that aspect answered, or when the
proposal file's `## Cycles` names a cycle that is closed, still in flight, not merged, or has answered the aspect
already. An aspect is answered once per cycle.

**Next:** `sdlc harvest` shows which aspects each completed cycle still needs. Copy `## Cycles` from the aspect's
report as written.

### a note for the framework that does not read

`sdlc harvest record <aspect>` is refused when a heading under `## For the framework` names no surface of the
framework — a rule's id such as `CON_06`, `evidence`, or a kind and a name such as `perspective:security` or
`stage:verify` — says nothing after it, has no `Kind:` line of `cost`, `missing` or `defect` beneath it (D-204), or
has no `Cycles:` line of cycle ids, or `all` (D-225). Nothing is recorded, so the sitting is never half answered. The
notes are what a solution may hand the framework, and one that cannot be grouped by what it is about cannot be read
across solutions; one whose cycles are words, or left to default to the whole sitting, is counted wrongly there.

**Next:** fix each heading the refusal names — `### stage:verify — <what fell short>`, a `Kind:` line, and a
`Cycles:` line naming the cycles whose record shows it — and record again.

### a covered answer that names nothing real

`sdlc harvest record <aspect>` is refused when a line under `## Covered` does not read as `- <suggestion id>: <what
covers it> — <how>`, or names something that is not here: a rule not in force, a decision not standing, a
`successor:<slug>` with no file under `.sdlc/successors/` and no cycle started from it, or a path that is not a file
or directory in the solution; or when a line's `Concern:` matches no concern the ledger or the sitting tallies, word for
word; or when a line answers a whole cycle, `<cycle>/*`, for a cycle that is not an abandoned one of this sitting, or
names anything but a successor for it (D-225). It lists every such line at once and records nothing, so the sitting
is never half answered. *Covered* says something already handles the suggestion; one naming nothing real would be a
decline nobody gave a reason for. A whole cycle is answered at once only when a person refused its approach and a
successor replans the work; a passed cycle's suggestions are each about code that shipped.

**Next:** name what really covers each line the refusal lists — `sdlc constraints` and `sdlc decisions` list what
stands — or move it to `## Declined` with the reason, or answer a passed cycle's suggestions one by one, then record
again.

### an aspect is still unanswered

`sdlc harvest close` is refused while no completed cycle has had every aspect answered. It names each cycle and the
aspects it still needs, because a cycle closed early would lose with its folder what it could still give.

**Next:** run the aspect's skill — `/sdlc:harvest-<aspect>` — or `/sdlc:harvest`, which runs every one still
listed, then close again.

### not on the integration branch

`sdlc harvest close` is refused anywhere but the integration branch. The cycles merged there and the drafts were
placed there, and the `harvest/<date>` branch it commits on starts from it; from anywhere else the pull request
would carry other work with it.

**Next:** switch to the integration branch, bring it up to date, and close again.

### a harvest pull request carries something that is not a harvest's

A pull request with no cycle that carries only the records, the harvest ledger, successors and changes under
`.sdlc/cycles/` or `.sdlc/series/` is checked as a harvest. It is refused when a cycle path is not the deletion of a
cycle the ledger records as closed, when a series path is not a deletion, when the records it places do not parse,
when an area covers no file, or when an entry names an area nobody defines. A cycle's folder leaves only once it is
closed, and a series' folder with the last cycle its record lists, each only by deletion.

**Next:** `sdlc verify-chain <branch> --base <integration branch>` names each failing check. Take out what is not a
closed cycle's deletion, and fix the record the check names.

### a command that was renamed

`sdlc mine` and `sdlc draft mining` are now `sdlc harvest` and `sdlc draft harvest`, and `sdlc propose` is
retired (D-191): `propose constraints` and `propose decisions` are `sdlc harvest constraints` and `sdlc harvest
decisions`, and `propose theme` is `sdlc theme`. Each old name is answered with the new one and does nothing.

**Next:** run the name it gives. For a rule you have in mind rather than one the cycles argued, `sdlc constraints
--new` prints an empty entry at the next free id.

## `sdlc init` — the two ways adoption is refused

### an option it does not take

`sdlc init` takes `--platform` and `--integration-branch`, and the global `--solution` every command takes, and
nothing else. Any other
option is refused before anything is written, with exit code `2`, rather than ignored — adoption writes
the hook configuration and the pipeline, so a run that silently dropped part of the line would leave you
believing you had asked for something you had not.

**Next:** run it without the option. `sdlc help`, under *Options for init*, lists what it takes. Which
stack the solution is built from is not adoption's: fill in the draft it writes.

### an integration branch the record disagrees with

`--integration-branch` names a branch other than the one `solution.md` records. That file is a person's,
so adoption cannot change the key, and a run that quietly kept the recorded branch would leave you
believing you had moved it.

**Next:** to move it, `sdlc draft solution`, change `integration_branch` in the draft, rename it into
place, and create the branch. Otherwise run adoption without the option.

---

## `sdlc build` — when the build cannot start

`/sdlc:build` asks the gate, then hands the cycle to `sdlc build` (D-178, D-187), which commits a placed approval,
admits the plan, prepares the build's worktree and starts the build in a process of its own. The gate's refusals
are above; these are the binary's own.

### the build's process cannot start

The binary starts the build with the host's own command line, named tools and permission checks on (D-183). When
that command is not on the path, there is nothing to start it with, and the build is never run in your session
instead: a builder that sat through the planning is the separation the process exists to keep.

**Next:** install the host's command line, or run the build where it is — the factory's agent has it.

### the build ended without a report

The build's process stopped before it reached an outcome: the toolchain failed, or the run was stopped. Nothing
was opened, and `sdlc status <id>` says where the cycle got to.

**Next:** read what `sdlc build` printed, fix the cause, and say *build it* again.

## `sdlc round run` — when a reader cannot run

`sdlc round run` starts every reader of a round, each in a fresh process (D-183). A round is never collected from
a reader that did not answer.

### no host to start a reader with

The host's command line is not on the path, so no reader can be started — and a reader is never this session
judging its own work instead.

**Next:** install it, and run the round again.

### a reader returned nothing

A reader's process ended without an answer — a credential the host did not have, a limit the account hit, a
stopped run. The binary names the reader, and `run.log` in the readers' directory has a line for every reader
that started and how it ended; a failed reader's own words are in `<reader>.err` beside it (D-214). A harvest's
readers end the same way.

**Next:** fix what the message names, and run the same command again (`sdlc round run`, `sdlc harvest read` or
`sdlc harvest review`): it starts only the readers with no answer. If the message does not say enough, set
`SDLC_TRACE=1` and run it again; each reader then writes its host's debug log to `<reader>.debug.log`. If readers
keep failing together, set `SDLC_READERS` lower.

## `sdlc evidence` — every way a recording is refused

`sdlc evidence <id>` writes what the build produced. It refuses rather than writing something partial,
because verify reads what it writes and nothing else, so a half-recorded run is a verdict nobody can
check.

### no cycle, or no plan

There is no directory for the id, or the cycle has no `plan.md`. The recording compares what changed
against what the plan declared, so without a plan there is nothing to compare against.

### code that is not committed

A file outside `.sdlc/` has changes nobody committed. The evidence names the commit the build was recorded
at, `head.sha`, and the patch is measured to it, so code that is not in a commit is code the verify would
never see and the pull-request check could never rebuild (D-124). The cycle's own files and the run output
you passed on the command line — the results directory, the build log, the coverage — do not count.

**Next:** commit the change, then record again. If the file is not part of the change, discard it or
ignore it.

### a verdict already stands

`verify.md` exists, its outcome is not `blocked`, and it adjudicates the run recorded now. Recording a new
run would leave every claim in that document citing evidence that is gone — and a stale verdict is the one
a reader trusts, because it looks finished. A **blocked** verdict is never refused: recording a new run
retires it, which is what remediation does, and the report says so (D-161). A **passed** verdict is retired only
when a person has asked for a change at the pull request: remediate records with `--retire-pass` (D-181).

**Next:** when a person asked for a change, record with `--retire-pass`. Otherwise delete `verify.md`, record the
new run, and verify again. The findings rounds under `findings/`
are kept either way, so the history of what was challenged survives.

### git cannot say where the branch diverged

The diff is measured from where the branch checked out diverged from the integration branch, or from the
default branch when there is none. Without that commit there is nothing to measure. The commonest cause is
a branch with no history in common with either — a fresh clone that never fetched them, or work started
from somewhere unrelated.

**Next:** `git fetch origin`, then ask again. If it still refuses, put the work on the branch
`sdlc branch <id> build` prints, which starts from the right base.

A misspelt branch costs more than this refusal: the turn-end hook reads the cycle out of the current branch,
and a branch it cannot parse is not governed work, so it allows every turn in the meantime. Nothing was
being checked while the building happened.

### an empty patch beside a changed file

Files outside `.sdlc/` changed and the patch came back empty, which cannot both be true — recording it
would put an empty `diff.patch` beside a panel saying everything was in order. If you see it, the
evidence is wrong rather than the code. **Next:** `git status --short`.

### no results file

The `--tests` path does not exist, or the directory holds no `.trx`, or the file holds no results. A run
that produced no results is a run that did not happen, which is not the same as a run with nothing to
report.

### one test name with two outcomes

Two projects reported the same test name, one passing and one failing. The chain check matches a plan's
named test on the end of what the runner reported, so the passing result would answer for the failing
one. **Next:** rename one of them, or run the projects separately.

### a merged-in change that overlaps the work before it

On a run's branch, a cycle is measured from where the cycle before it ended. When the run merged the
integration branch after that, the cycle is measured from that point with the merge taken in, so what the
merge brought is not counted as the cycle's own (D-206) — and that needs git to redo the merge without a
working tree. It refuses when the merge clashes with the work before it, because the cycle's change can then
not be told apart from the merge's, and when git is older than 2.38, which cannot do it.

**Next:** record once the overlap is resolved in a commit of its own, or build the cycle again on top of the
merge. For an older git, upgrade it.

---

## `sdlc pr-ready` — the five ways a pull request is refused

### not verified

There is no cycle, or no `verify.md`.

**Next:** `/sdlc:verify <id>`, which needs recorded evidence — so `sdlc evidence <id>` before it if the
build has not recorded any.

### the verification did not pass

The verdict is `blocked`. Not "no failures recorded" — an actual pass is required, because the verdict is
the only thing standing between unattended building and work presented as ready for review.

**Next:** `/sdlc:remediate <id>`. The build opens a **draft** pull request for it instead
(D-85): marked not ready, its check red, so the attempt is somewhere a person looks rather than built again
the next night. Remedy it on that branch and mark the draft ready once it passes.

### escalated

Remediation stopped getting anywhere with blockers still open, after the one different approach it may take
(D-179): a pass undid what an earlier one answered or two passes in a row reduced nothing, after that deviation,
or a third deviation bent the same approved text. **An escalated cycle never opens a ready pull request** — the
build opens a draft, which the check refuses to merge — and that stopping is the only reason an unattended build
is safe to run at all. There is no pass count to run out of — the refusal says which stopped it, and they want
different things from you: a regression means looking at what the last pass actually did, a stall means asking
whether the finding is answerable, and a third deviation on the same text means the approved text itself is what
needs changing. `handling-blockers.md` has what to do with an escalated cycle.

**Next:** a person decides. Either the criterion is wrong, in which case
`sdlc draft approval <id> --reopen` withdraws the approval with a recorded reason and the documents can be
rewritten, or the work is harder than the plan thought and the cycle is abandoned with
`sdlc draft abandon <id>`.

**There is no budget to raise.** The bound is progress rather than a pass count (D-66), and it is what
everything unattended rests on.

### the chain does not hold

The pull-request check would refuse it anyway, so pushing would cost a round trip through CI to learn what
you were just told.

**Next:** `sdlc verify-chain <id>`, which prints every check — passing and failing both, because a reader
looking at a failed check needs to know what held. The checks are: the cycle exists, no shape problems,
the chain is complete, the approval, the gate is satisfied, every criterion has a proof, every named test
passed, every criterion is adjudicated, the verify was judged against this evidence, git commits the
evidence, the changed lines are covered, the codebase is no less covered than it was, the verdict follows the approval that stands (only
after a reopen), the deviations are sound (only where the build recorded any), the change was verified. When the pull request changes the solution's constraints,
decisions or declared stack, it also says whether that was committed before or after the verify — reported,
never refused (D-113).

**The two coverage checks are arithmetic** (D-103, D-157). At least 70% of the lines the change adds ran
under a test, and the whole codebase holds its coverage: at or above the solution's `coverage_floor` in
`solution.md` (80% when it names none), or, under it, no lower than the baseline recorded at the base
commit in `.sdlc/coverage.json`. The first catches a change that arrived untested; the second catches a
deleted or weakened test uncovering code nobody changed. **The baseline is the last rate a merged cycle
recorded, not a target anybody chose.**

**Git commits the evidence** (D-161). A pattern in the repository's own ignore file -- Visual Studio's
`coverage*.json` is the one met -- leaves a file out of every commit: the check passes locally, where the
file is in the working tree, and fails in the pipeline, where it is not. **Next:** `git check-ignore -v <path>`
names the pattern; add `!<path>` after it, or `git add -f` the file once. A cycle opened since 0.44.0 carries
the exception for the evidence in the cycles directory's own ignore file; `.sdlc/coverage.json` is outside it.

**Next,** for the changed lines: add the tests that run them, build, and record again with `--coverage`.
Code no test should run is excluded in the solution's coverage settings, declared in the plan with its
reason. For the whole codebase, find what fell before writing tests for it: compare the run's report with
the base commit's, file by file. A deleted or weakened test shows in one place; **a change in how coverage
is measured** -- a collector setting, a runner, a test host started in a child process -- moves every
figure at once, and no number of tests closes it. That one is a person's to answer: lower `coverage_floor`
through `sdlc draft solution`, or, after the change merges, re-measure the integration branch and commit
the new rate to `.sdlc/coverage.json` in a pull request of its own. Lowering the floor does not need the
evidence recorded again: `verify-chain` reads it as it stands.


**The two about evidence and reopening are the stale-verdict checks.** Evidence is recorded again after
each remediation, and a verify round records which run it read, so a verdict judged on the run before
last fails. And a reopen leaves the old evidence and verdict in place while the documents change: once
the cycle is approved again, a verify round older than that approval adjudicated documents nobody
approves any more. Delete `verify.md`, build, record the evidence, and verify again.

**"every named test passed" is the one that surprises people.** Each proof in the plan opens with its
test's name in backticks, and this reads those opening names out of the plan and confirms each one ran
and passed according to the normalised summary — not according to anything the build said about itself.
Backticks later in an entry are formatting and are not read.

**"every criterion is adjudicated" is the other one.** Each criterion the spec states needs an entry
under `Criteria` in `verify.md`, numbered to match. A criterion whose proof could not be reached is
adjudicated **not met**; moving it to `## Open` as a limit of the proof is what this refuses, because
that region is for what a document cannot settle and whether a criterion was met is the one thing a
verify is for. A real limit — what the evidence does not reach *beyond* a criterion that was met — still
belongs there.

### a test run

A local build of the framework — one run through `SDLC_DEV_BUILD`, or a pack made outside the pipeline — wrote
at least one of the cycle's rounds, or the solution pins one. Its release ends in `-local`, and the refusal names
each round it wrote. **It refuses every form**: a verified build, a draft, and a plan through
`sdlc verify-chain`, and the solution's pull-request check refuses it too (D-135).

A local build is how a change to the framework is tried before anybody publishes it, and nobody else can restore
it — so nothing it judged can be checked again by a reviewer, the pipeline or the factory. The cycle was a test
run, and the refusal is final.

**Next:** finish with the test run: `sdlc draft abandon <id>`. Publish the framework change as a preview, pin the
integration branch to it by a pull request that changes only the pin, unset `SDLC_DEV_BUILD`, and run the work as
a new cycle. [Testing a local build](testing-a-local-build.md) is the whole loop.

---

## `sdlc up-to-date` — the two ways a branch is not ready to push

The `pr` skill and `close-cycle` ask it before every pull request they open (D-227). It fetches the target from
origin — the integration branch, or `--base` — and refuses a branch that does not hold the target's head.

### behind its target

The target took commits the branch does not have, so the pull request would be reviewed and checked without
them, and anything both sides wrote — a record two cycles each added to, a file the target changed under the
cycle — would be merged by the platform's text merge rather than seen. The refusal says how many commits, and
whether the merge would be clean or where it stops.

**Next,** for a branch that changes only the record under `.sdlc/` — a plan, a slicing, a sandbox's admission, a
harvest's close: nothing is built again, so bring the target in and ask again:

```
git merge origin/<target>
sdlc up-to-date
```

**Merge, never rebase**: the branch may already be pushed, and the pull request is squashed when it lands, so a
merge rewrites nothing.

**Next,** for a branch carrying built code: bringing the target in changes the code the verification judged, so
the verification goes with it. `/sdlc:remediate <id>` merges the target in through `resolve-conflict`, builds,
records fresh evidence and verifies again in a fresh process, which is then judged against the new base. On a
run's branch, remediate the run's last cycle: its fresh run is the one the run's check reads. Inside the build's
own process this happens without asking when the merge is clean; when it stops on a conflict, the build opens no
pull request and its report names the cycle and the files, for a person to remediate.

### the target could not be fetched

Offline, no remote called `origin`, or a target the remote does not have. Nothing was compared, so passing would be
a guess.

**Next:** `git fetch origin <target>` once the remote answers, then `sdlc up-to-date` again.

---

## The pre-tool hook — the eight things an agent is denied

These are denied at the tool call, by file path and by shell command alike. `../mechanisms/protected-files.md` says
how the hook decides and what it deliberately leaves alone; this is why each one is a person's. The rules are absolute rather
than conditional, and that is the point: there is no mode, no flag, and no check on which skill is
running, so a rule with no exception has nowhere for a bypass to live.

| What | Why it is a person's |
|---|---|
| writing `approval.md`, or moving anything onto it | an approval is a person's act, and a recorded one means something because no tool could have recorded it. `sdlc draft approval` writes a draft beside it; renaming that draft is the decision |
| writing `abandoned.md` | an agent stuck on a blocking finding has an obvious escape — abandon and start clean. A hand-written reason keeps the objections that killed it on the record |
| writing `constraints.md` | a cycle judged by the constraints it is changing is the same deadlock as the framework governing itself. `sdlc harvest constraints` reads the completed cycles and proposes an entry, and `sdlc constraints --new` prints an empty one; placing it is the decision |
| writing `decisions.md` | a stage reads the decision record for alignment, so an agent that could write it could invent doctrine and then align to it. `sdlc harvest decisions` reads the completed cycles and proposes an entry; placing it is the decision |
| writing `areas.md` | the areas decide which rules and decisions reach each reader in full, so an agent that could write them could move a rule out of its own judge's sight. `/sdlc:harvest-areas` proposes areas once three cycles' plans name one, and `sdlc draft harvest` writes the draft; renaming it is the decision |
| writing `solution.md` | the origin is a historical fact that must never change. Per-key protection would be the first conditional rule, which is where a bypass would live. Adoption drafts it; after that `sdlc draft solution` does |
| writing `techstack.md` | it is the second rule source a plan is read against, and `CON_15` judges every plan against it — so an agent able to edit it could satisfy that rule by adding the row it wanted instead of arguing for it, or drop a stack rule by switching its row off. `sdlc draft techstack` writes a draft beside it; renaming the draft is the decision |
| writing a findings round | not a person's file but the binary's: `sdlc round` numbers it, stamps when it ran and what it judged, and refuses it out of the chain's order. A round the session wrote itself carried whatever it typed (D-101). Put the findings in a scratch file and hand it to `sdlc round` |

**If your agent reports one of these as a broken tool, it is reading the mechanism as a malfunction.** The
refusal message names the command to run instead — a drafter for four of these files, a harvest for the
rule file and the decision record, and a harvest's drafter for the areas. Run it yourself, and place what it produces.

**A refusal can arrive for a command that only *mentions* a protected file beside a write verb** — the hook reads the
shell command, not the paths it would touch, so a commit message reading *Move techstack.md into place*
is denied. `git commit -F` with the message in a file is the way past that, and it is the rule working
rather than a hole.

**The theme is deliberately not on this list.** `.sdlc/theme.json` is a solution file like the ones
above and no agent write to it is denied, because nobody is judged against a colour: no constraint reads
it and no stage takes its brief from it. A cycle changes a value by declaring the file, like any other
declared work.

**Reading these files is allowed.** Refusing `cat approval.md` would make the gate obstructive without
making it safer.

The shell rule judges each piece of a command on its own — the parts between `;`, `&&`, `||`, `|` and
newlines, outside quotes. A redirect is a write of the file it points at, so `grep x decisions.md
2>/dev/null` and `cat techstack.md > /tmp/x` are reads. A write verb — a move, a copy, a delete, an
in-place edit — counts when the same piece names one of these files as a name of its own, so
a draft, or a file whose longer name ends in one of them, is not mistaken for the record. Beyond that it is
deliberately broad rather than a parser: a false refusal costs you a rephrasing, and a miss costs the
guarantee.

**Nine is the whole list, and the limit is structural.** The hook sees a tool call and not which skill
made it, so it cannot deny an edit that is wrong only because of where it came from — `remediate`
touching `spec.md`, or `refine` touching source. Those rules are real and the skills state them, but
they are the skill's own discipline rather than something denied at the call. What catches a breach is
slower and certain: an approved cycle's three documents are hash-locked, so a planning document edited
after approval makes `sdlc gate` refuse the build and `sdlc verify-chain` refuse the merge, each naming
which document moved. **You cannot edit your way past a criterion; you can only turn a cycle that would
have opened a pull request into one that cannot.**

---

## The turn-end hook — the three ways a turn is sent back

Asked before an agent finishes, on a cycle branch only — `cycle/<id>/<kind>`, or an older cycle's bare
`cycle/<id>`. It is what makes a warnings-as-errors
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
abandoned cycle, before the cycle has a plan, when nothing has changed, when the host is already
continuing because it was sent back once already, and in a **fresh reader** — a process the binary starts with
`SDLC_READER=1` to judge a document, for a round, a verification or a harvest sitting (D-183). A reader builds nothing, and
refusing its turn only replaced its verdict with an answer to the hook; the tool-call hook still holds it
to the protected files. A governance hook that met a person finishing a thought
would be switched off within a day, and then the one case it catches would go uncaught too.

**It is bypassable.** Running `sdlc evidence` against a stale results file satisfies it, and so does
setting `SDLC_READER=1` on a process that builds. That is the local
layer behaving as designed — `sdlc verify-chain` in the pipeline re-reads the evidence and checks that
every test the plan named actually passed, and that is the guarantee.

**Two of the three hosts.** Claude Code calls the moment `Stop` and Copilot `agentStop`, and adoption
configures whichever name the host uses. Antigravity has such an event and is deliberately left
ungoverned — see the paragraph below. A host that genuinely has no turn end is still refused
by name — `sdlc hook --event turn-end` says so and configures nothing, because a plausible-looking
configuration for an event that cannot fire is a check that appears to enforce and does not.

**The send-back needs the host to say it is already retrying**, or a refusal is an endless turn rather
than a governed stop. Claude Code and Copilot both carry that flag and both spell it `stop_hook_active`.
**Antigravity does not**, and that is why its turn end is not governed at all:
`sdlc hook --event turn-end --host antigravity` refuses by name rather than installing a check that
could loop. Its tool calls are governed normally.

---

## When nothing refuses and it should have

The dangerous direction. Five ways the gate goes quiet — and **`sdlc doctor` is the command for this
section**: it checks every host's hook configuration, adoption, and whether a pipeline here runs the one
check that is the guarantee, and exits 1 if any of it is wrong. What it cannot see is marked `elsewhere`
rather than passed: whether a host loaded the plugin, which is the first two causes below, and — when `az`
or `gh` is not signed in — whether branch policy requires the check, which is the fifth.

**A hook is not loaded.** `claude plugin details sdlc@ai-sdlc` must say `Hooks (3) SessionStart, PreToolUse, Stop`. **A
missing `PreToolUse` or `Stop` is a failed install, not a detail** — and the two fail differently. Without `PreToolUse`
nothing is denied; without `Stop` a turn can end on a red build. Either way every cycle still appears to
work. No binary can check this for you, which is why `doctor` names the command instead of answering it.

**The workspace is not trusted.** Project hooks do not run until it is. Same silence, different cause.

**The session predates the install.** Skills and hooks load at startup. Reinstall, then start a new
session.

**The declared stack says less than it looks like it says.** `CON_15` asks a plan to say why it departs
from a declared default, so a row with a concern and no choice is a rule with nothing to bite on, and a
rule row naming an id this release does not ship switches on nothing while reading as if it did. Every
stage passes, every gate opens, and the only thing missing is the argument. `sdlc doctor` reports both as
`declared stack` and names the row. **An empty file is not this**: a person placing a stack with nothing
in it has decided that nothing is pinned, and `doctor` says so as sound. `sdlc draft techstack` writes
the options menu — what each shipped profile declares for every concern, and every stack rule — beside
the draft to edit.

**The check is not required.** `sdlc verify-chain` in the pipeline is the only non-bypassable layer, and
it is only a guarantee while branch policy makes it required. A pipeline that runs it and a merge that
does not wait for it is a directory that appears enforced and is not — which is worse than one that never
pretended.

**On Azure DevOps it is worse: without the policy the check does not run at all.** Azure Repos ignores a
`pr:` trigger in YAML, so `.azuredevops/sdlc-verify.yml` runs on a pull request only when a build validation
policy names it — Repos > Branches > the default branch > Branch policies > Build validation. Until then
every pull request merges with nothing built, tested or verified, and nothing says so. `sdlc doctor`
asks the platform, on both, and reports a problem while the check is not required. Signed out of `az` or
`gh`, it marks the policy `elsewhere` and names this step instead.

**One more, on the generated pipeline:** its first real step restores the `sdlc` release
`.config/dotnet-tools.json` pins, and stops there when it cannot — before a build is spent, in a step whose
comment says why (D-37, D-131). It says one of two things. *This solution pins no sdlc release* means the
solution was adopted before the manifest existed: a person runs adoption on the integration branch and
commits the manifest. *No feed this run can reach holds the release* usually means a `nuget.config` that
clears the default sources, which hides nuget.org.

---

## Where to go next

| | |
|---|---|
| a blocking finding or a blocked verification, and how to answer it | `handling-blockers.md` |
| every refusal as one line | `../reference/refusals.md` |
| a cycle walked end to end | `running-a-cycle.md` |
| the same walk with one solution in it | `../samples/rolling-a-die.md` |
| every command on one screen | `../reference/commands.md` |
| what this solution is built from, and how to change it | `declaring-the-stack.md` |
| what each skill is and who starts it | `../../skills/README.md` |
| installing, updating, removing | `installing-the-plugin.md` |
| the nineteen global constraints, each with what it forbids | `../../CONSTRAINTS.md` |
