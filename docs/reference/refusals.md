# Refusals

Every refusal, as one line: what you were told, then what to do. Each name links to its entry in
`../guides/handling-refusals.md`, which has the why.

**A refusal is a command or hook declining to go on.** A blocker — a blocking finding, or a `blocked`
verification — is something found wrong, and it causes some of these.
`../guides/handling-blockers.md` is how a blocker is answered.

| Exit | Means |
|---|---|
| `0` | did what was asked |
| `1` | **a rule refused it** — every command row below, unless the row says otherwise |
| `2` | the command line was not understood |
| `3` | **the gate itself failed** — not a governed stop; read standard error |

The two hooks return no exit code to you: they stop the agent's action, and the agent sees the reason.

| Refusal | Resolution |
|---|---|
| **Adopting — `sdlc init`** | |
| [an option it does not take](../guides/handling-refusals.md#an-option-it-does-not-take) | exit `2`. Run it with `--platform`, `--integration-branch` and the global `--solution` only; the stack is the draft it writes |
| [an integration branch the record disagrees with](../guides/handling-refusals.md#an-integration-branch-the-record-disagrees-with) | leave the option out, or change the key with `sdlc draft solution` and a rename |
| **Opening a cycle — `sdlc new`, and `sdlc branch <id> plan`** | |
| [no declared stack](../guides/handling-refusals.md#no-declared-stack) | fill in `techstack.draft.md`, rename it into place, commit it |
| [uncommitted work](../guides/handling-refusals.md#uncommitted-work) | commit, stash or discard the files it names and switch again, or plan in a sandbox, which switches nothing |
| **Before a build — `sdlc gate`** | |
| [no cycle](../guides/handling-refusals.md#no-cycle) | `sdlc new`, or check the id |
| [abandoned](../guides/handling-refusals.md#abandoned) | open a new cycle; an abandoned one does not reopen |
| [a planning document is missing](../guides/handling-refusals.md#a-planning-document-is-missing) | `sdlc status <id>` names it and the command that writes it |
| [not approved](../guides/handling-refusals.md#not-approved) | `sdlc draft approval <id>`, then rename the draft |
| [the ledger cannot be read](../guides/handling-refusals.md#the-ledger-cannot-be-read) | `sdlc draft approval <id>` writes a complete replacement |
| [the standing decision refuses it](../guides/handling-refusals.md#the-standing-decision-refuses-it) | read the reason in `approval.md`, answer it, approve again |
| [a document changed since it was approved](../guides/handling-refusals.md#a-document-changed-since-it-was-approved) | put it back, or approve again |
| [a findings round cannot be read](../guides/handling-refusals.md#a-findings-round-cannot-be-read) | put the ledger back as `sdlc round` wrote it, from git, and interrogate again if a round is lost |
| [a document was never interrogated](../guides/handling-refusals.md#a-document-was-never-interrogated) | `/sdlc:interrogate <id> <artifact>` |
| [a findings round it anchored has changed](../guides/handling-refusals.md#a-findings-round-it-anchored-has-changed) | restore the ledger from git, or interrogate again and approve again |
| [a blocking finding is open](../guides/handling-refusals.md#a-blocking-finding-is-open) | `/sdlc:refine <id> <artifact>`, then interrogate again |
| [a findings round is stale](../guides/handling-refusals.md#a-findings-round-is-stale) | re-interrogate downstream: the spec, then the plan |
| **Writing a round — `sdlc round`** | |
| [something above it is unsettled](../guides/handling-refusals.md#something-above-it-is-unsettled) | settle the highest document it names, then come down one at a time |
| [a known finding comes back in other words](../guides/handling-refusals.md#a-known-finding-comes-back-in-other-words) | carry the finding as the ledger holds it; a new objection takes a new id |
| [a new blocker has no gist](../guides/handling-refusals.md#a-new-blocker-has-no-gist) | have the reader that raised it give one plain sentence; never write it in this session |
| [a new blocker cites a rule not in force for it](../guides/handling-refusals.md#a-new-blocker-cites-a-rule-not-in-force-for-it) | make it a suggestion, or cite the rule in force it breaks: `sdlc constraints --for <artifact>` |
| [a new finding quotes its rule in other words](../guides/handling-refusals.md#a-new-finding-quotes-its-rule-in-other-words) | put the rule's own line back, which the refusal prints, or collect the round again |
| [a suggestion does not fit the cycle's suggestions](../guides/handling-refusals.md#a-suggestion-does-not-fit-the-cycles-suggestions) | number a new one past the cycle's `S_` ids, carry a restated one under its id, answer it only where it was raised |
| [a suggestion with nothing to read, or in another's words](../guides/handling-refusals.md#a-suggestion-with-nothing-to-read-or-in-anothers-words) | carry the copy under the id it names; a finding with no statement goes back to its reader |
| [a document cites what does not exist](../guides/handling-refusals.md#a-document-cites-what-does-not-exist) | place and commit the decision on this branch, or cite a recorded one; give a criterion the spec's number now |
| [a declared file with no area](../guides/handling-refusals.md#a-declared-file-with-no-area) | name its area in the plan's `## Areas`: `- new <name>: <files>`, or `- none: <files>` |
| [an area covers no file](../guides/handling-refusals.md#an-area-covers-no-file) | `sdlc areas`, then `/sdlc:harvest-areas` for new globs, or correct the `Files:` line by hand |
| [a blocked verdict on evidence a round already judged](../guides/handling-refusals.md#a-blocked-verdict-on-evidence-a-round-already-judged) | `/sdlc:remediate <id>`; the run it records gets a full round |
| **Writing the skeleton — `sdlc scaffold`** | |
| [the gate refuses the cycle](../guides/handling-refusals.md#the-gate-refuses-the-cycle) | whichever gate remedy above matches the message |
| [an adopted solution whose plan declares no build settings](../guides/handling-refusals.md#an-adopted-solution-whose-plan-declares-no-build-settings) | `sdlc stack`; a cycle whose plan declares the files brings them |
| **The build's own process — `sdlc build`** | |
| [a solution document the build cannot read](../guides/handling-refusals.md#a-solution-document-the-build-cannot-read) | `sdlc draft solution`; `build_tools` takes bare command names, comma-separated |
| [a tool the build's process was not given](../guides/handling-refusals.md#a-tool-the-builds-process-was-not-given) | add the command to `build_tools` with `sdlc draft solution`; a local settings rule never reaches the build |
| **Amending an admitted plan — `sdlc draft approval --reopen`, `sdlc branch`** | |
| [a reopen on the build branch](../guides/handling-refusals.md#a-reopen-on-the-build-branch) | `sdlc branch <id> plan`, reopen and approve there, `/sdlc:pr <id> --plan`; then keep or delete the build branch |
| [a plan branch left over from earlier planning](../guides/handling-refusals.md#a-plan-branch-left-over-from-earlier-planning) | `git branch -D cycle/<id>/plan`, then `sdlc branch <id> plan` again |
| **Deviations — `sdlc deviations`, `sdlc report`, `sdlc verify-chain`** | |
| [a deviation that does not hold](../guides/handling-refusals.md#a-deviation-that-does-not-hold) | the stage that wrote it corrects the quote; a narrowed criterion is taken out and the cycle escalates; a claimed file is not a deviation |
| **The build — `sdlc build`** | |
| [the build's process cannot start](../guides/handling-refusals.md#the-builds-process-cannot-start) | install the host's command line; the build never runs in your session instead |
| [the build ended without a report](../guides/handling-refusals.md#the-build-ended-without-a-report) | fix what it printed, and say *build it* again |
| **Starting readers — `sdlc round run`** | |
| [no host to start a reader with](../guides/handling-refusals.md#no-host-to-start-a-reader-with) | install the host's command line, and run the round again |
| [a reader returned nothing](../guides/handling-refusals.md#a-reader-returned-nothing) | fix what the message names, reading `run.log` beside the prompts; the same command again starts only the readers with no answer |
| **Successors — `sdlc successor`, `sdlc lead show successor:`** | |
| [a successor that is not ready](../guides/handling-refusals.md#a-successor-that-is-not-ready) | wait until `sdlc successors` says `ready`; for a stuck one, correct `succeeds:` or delete the file |
| [a loop through succeeds](../guides/handling-refusals.md#a-loop-through-succeeds) | correct one file's `succeeds:` so the loop opens |
| [a reference to nothing](../guides/handling-refusals.md#a-reference-to-nothing) | correct the `succeeds:` entry |
| [a successor file that cannot be read](../guides/handling-refusals.md#a-successor-file-that-cannot-be-read) | correct the file; a piece waiting on nothing is a cycle of its own |
| **A slicing's pull request — `sdlc verify-chain slices/<slug>/plan`** | |
| [a plan that needs a sibling's code](../guides/handling-refusals.md#a-plan-that-needs-a-siblings-code) | make that piece a successor of the sibling, or merge the two pieces |
| [a slicing that carries more than plans](../guides/handling-refusals.md#a-slicing-that-carries-more-than-plans) | take the other files off the branch, or compose it again with `sdlc slices` |
| **A run's pull request — `sdlc verify-chain wave/<id>` or `series/<id>`** | |
| [a run that carries no cycle](../guides/handling-refusals.md#a-run-that-carries-no-cycle) | build a cycle on the branch, or open the change from a branch of its own |
| [a named test that failed at the end of the run](../guides/handling-refusals.md#a-named-test-that-failed-at-the-end-of-the-run) | fix it on the run's branch and record the last cycle's evidence again |
| [code no verified cycle accounts for](../guides/handling-refusals.md#code-no-verified-cycle-accounts-for) | record the evidence of the cycle that built it, or revert the code of a cycle left unbuilt or abandoned |
| **A series, and a run's own commands — `sdlc series`, `sdlc build --run`, `sdlc report --run`** | |
| [fewer than one wave](../guides/handling-refusals.md#fewer-than-one-wave) | start it again with the number of waves it may run |
| [not on a series' branch](../guides/handling-refusals.md#not-on-a-series-branch) | run it in the series' worktree, or pass it with `--solution` |
| [a series that has taken every wave](../guides/handling-refusals.md#a-series-that-has-taken-every-wave) | open its pull request; a new series takes what is ready next |
| [not on a run's branch](../guides/handling-refusals.md#not-on-a-runs-branch) | run it in the run's worktree, or pass it with `--solution` |
| **An upgrade's pull request — `sdlc verify-chain <branch> --base <branch>`** | |
| [the check runs as another release](../guides/handling-refusals.md#the-check-runs-as-another-release) | let the pipeline restore the pin first; `sdlc init` rewrites a pipeline that does not |
| [a framework file adoption did not write](../guides/handling-refusals.md#a-framework-file-adoption-did-not-write) | move the team's own change to a cycle, or run `sdlc init` again as the pinned release |
| [a pipeline step the release did not write](../guides/handling-refusals.md#a-pipeline-step-the-release-did-not-write) | put the framework's step back as the release writes it, and the team's own as the base had them; change a team's step in a cycle |
| [a dependency file that changed more than versions](../guides/handling-refusals.md#a-dependency-file-that-changed-more-than-versions) | keep only the version moves; add or remove a package in a cycle |
| [a stack line that answers nothing](../guides/handling-refusals.md#a-stack-line-that-answers-nothing) | keep only rows the release asks about; change any other line in a cycle |
| **The upgrade checklist — `sdlc upgrade`** | |
| [no release to move to](../guides/handling-refusals.md#no-release-to-move-to) | name the release: `sdlc upgrade <release>` |
| **A promotion's pull request — `sdlc promotion`** | |
| [a branch that may not promote](../guides/handling-refusals.md#a-branch-that-may-not-promote) | open it into the integration branch as a cycle, and promote from there or from a `release/*` branch |
| **A harvest — `sdlc harvest record`, `sdlc harvest close`, and its pull request** | |
| [a draft is still beside its file](../guides/handling-refusals.md#a-draft-is-still-beside-its-file) | rename the draft into place or delete it, then run the command again |
| [nothing left to record](../guides/handling-refusals.md#nothing-left-to-record) | `sdlc harvest` shows what each cycle still needs; copy `## Cycles` as the report wrote it |
| [a note for the framework that does not read](../guides/handling-refusals.md#a-note-for-the-framework-that-does-not-read) | name a surface, a `Kind:` and a `Cycles:` line under each `## For the framework` heading, then record again |
| [a covered answer that names nothing real](../guides/handling-refusals.md#a-covered-answer-that-names-nothing-real) | name the rule, decision, successor or file that really covers each listed line, or decline it with why, or answer a passed cycle's suggestions one by one, then record again |
| [an aspect is still unanswered](../guides/handling-refusals.md#an-aspect-is-still-unanswered) | run that aspect's skill, or `/sdlc:harvest`, then close again |
| [not on the integration branch](../guides/handling-refusals.md#not-on-the-integration-branch) | switch to the integration branch and close again |
| [a harvest pull request carries something that is not a harvest's](../guides/handling-refusals.md#a-harvest-pull-request-carries-something-that-is-not-a-harvests) | take out what is not a closed cycle's or a closed series' deletion, and fix the record the check names |
| [a command that was renamed](../guides/handling-refusals.md#a-command-that-was-renamed) | run the name it gives: `sdlc harvest`, `sdlc draft harvest`, `sdlc theme`, or `sdlc constraints --new` |
| **Recording evidence — `sdlc evidence`** | |
| [no cycle, or no plan](../guides/handling-refusals.md#no-cycle-or-no-plan) | check the id; the cycle needs a `plan.md` |
| [code that is not committed](../guides/handling-refusals.md#code-that-is-not-committed) | commit the change, then record again |
| [a verdict already stands](../guides/handling-refusals.md#a-verdict-already-stands) | a pass a person asked to change: record with `--retire-pass`. Otherwise delete `verify.md`, record again, verify again. A blocked one is retired by the recording |
| [git cannot say where the branch diverged](../guides/handling-refusals.md#git-cannot-say-where-the-branch-diverged) | `git fetch origin` and ask again; if it still refuses, work on the branch `sdlc branch <id> build` prints |
| [an empty patch beside a changed file](../guides/handling-refusals.md#an-empty-patch-beside-a-changed-file) | `git status --short`; the evidence is wrong, not the code |
| [no results file](../guides/handling-refusals.md#no-results-file) | point `--tests` at the directory holding the `.trx` files |
| [one test name with two outcomes](../guides/handling-refusals.md#one-test-name-with-two-outcomes) | rename one test, or run the projects separately |
| [a merged-in change that overlaps the work before it](../guides/handling-refusals.md#a-merged-in-change-that-overlaps-the-work-before-it) | record once the overlap is resolved in a commit of its own, or build the cycle again on top of the merge |
| **Opening a pull request — `sdlc pr-ready`** | |
| [not verified](../guides/handling-refusals.md#not-verified) | `sdlc evidence <id>` if needed, then `/sdlc:verify <id>` |
| [the verification did not pass](../guides/handling-refusals.md#the-verification-did-not-pass) | `/sdlc:remediate <id>` |
| [escalated](../guides/handling-refusals.md#escalated) | a person decides: reopen the approval, or abandon the cycle |
| [the chain does not hold](../guides/handling-refusals.md#the-chain-does-not-hold) | `sdlc verify-chain <id>` prints every check |
| [a test run](../guides/handling-refusals.md#a-test-run) | abandon it; publish a preview, pin the integration branch to it, and run the work again |
| **Before any pull request — `sdlc up-to-date`** | |
| [behind its target](../guides/handling-refusals.md#behind-its-target) | records alone: `git merge origin/<target>` and ask again. Built code: `/sdlc:remediate <id>`, which merges, builds and verifies again |
| [the target could not be fetched](../guides/handling-refusals.md#the-target-could-not-be-fetched) | `git fetch origin <target>` once the remote answers, and ask again |
| **Denied to an agent — the pre-tool hook\*** | |
| writing `approval.md` | `sdlc draft approval <id>`, then rename |
| writing `abandoned.md` | `sdlc draft abandon <id>`, then place it |
| writing `constraints.md` | `sdlc harvest constraints` or `sdlc constraints --new`, then paste |
| writing `decisions.md` | `sdlc harvest decisions`, then paste |
| writing `areas.md` | `/sdlc:harvest-areas`, then `sdlc draft harvest <file>` and rename |
| writing `solution.md` | `sdlc draft solution`, then rename |
| writing `techstack.md` | `sdlc draft techstack`, then rename |
| writing a findings round | `sdlc round <id> <artifact> --from <file>` |
| **Sent back at turn end — the turn-end hook** | |
| [the recorded suite has a failure](../guides/handling-refusals.md#the-recorded-suite-has-a-failure) | fix the code, run the suite, record again. **Not the test** |
| [there are changes and no recorded evidence](../guides/handling-refusals.md#there-are-changes-and-no-recorded-evidence) | `sdlc evidence <id> --tests <dir> --build-log <log>` |
| [the working tree has moved since the evidence](../guides/handling-refusals.md#the-working-tree-has-moved-since-the-evidence) | build and test what is there now, and record it |
| [when it says nothing](../guides/handling-refusals.md#when-it-says-nothing) | expected off a `cycle/` branch and before a plan; the pipeline check is the guarantee |

\* **The pre-tool hook's eight denials are absolute: no mode, no flag.** Run the command the denial
names, and place what it produces yourself. A command that only *mentions* one of these files beside a
write verb is denied too — for a commit message, use `git commit -F` with the message in a file. The pre-tool hook section of
`../guides/handling-refusals.md` has why each is a person's.

## See also

- [Handling refusals](../guides/handling-refusals.md) — every refusal in full, with why
- [Handling blockers](../guides/handling-blockers.md) — a blocking finding or a blocked verification, which cause some of these
- [Commands](commands.md) — every command, one line each
