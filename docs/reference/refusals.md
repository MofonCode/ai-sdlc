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
| [an option it does not take](../guides/handling-refusals.md#an-option-it-does-not-take) | exit `2`. Run it with `--platform` and `--integration-branch` only; the stack is the draft it writes |
| [an integration branch the record disagrees with](../guides/handling-refusals.md#an-integration-branch-the-record-disagrees-with) | leave the option out, or change the key with `sdlc draft solution` and a rename |
| **Opening a cycle — `sdlc new`** | |
| [no declared stack](../guides/handling-refusals.md#no-declared-stack) | fill in `techstack.draft.md`, rename it into place, commit it |
| [uncommitted work](../guides/handling-refusals.md#uncommitted-work) | commit, stash or discard the files it names, then open again |
| **Before a build — `sdlc gate`** | |
| [no cycle](../guides/handling-refusals.md#no-cycle) | `sdlc new`, or check the id |
| [abandoned](../guides/handling-refusals.md#abandoned) | open a new cycle; an abandoned one does not reopen |
| [a planning document is missing](../guides/handling-refusals.md#a-planning-document-is-missing) | `sdlc status <id>` names it and the command that writes it |
| [not approved](../guides/handling-refusals.md#not-approved) | `sdlc draft approval <id>`, then rename the draft |
| [the ledger cannot be read](../guides/handling-refusals.md#the-ledger-cannot-be-read) | `sdlc draft approval <id>` writes a complete replacement |
| [the standing decision refuses it](../guides/handling-refusals.md#the-standing-decision-refuses-it) | read the reason in `approval.md`, answer it, approve again |
| [a document changed since it was approved](../guides/handling-refusals.md#a-document-changed-since-it-was-approved) | put it back, or approve again |
| [a findings round cannot be read](../guides/handling-refusals.md#a-findings-round-cannot-be-read) | fix the file, or delete that round and interrogate again |
| [a document was never interrogated](../guides/handling-refusals.md#a-document-was-never-interrogated) | `/sdlc:interrogate <id> <artifact>` |
| [a blocking finding is open](../guides/handling-refusals.md#a-blocking-finding-is-open) | `/sdlc:refine <id> <artifact>`, then interrogate again |
| [a findings round is stale](../guides/handling-refusals.md#a-findings-round-is-stale) | re-interrogate downstream: the spec, then the plan |
| **Writing the skeleton — `sdlc scaffold`** | |
| [the gate refuses the cycle](../guides/handling-refusals.md#the-gate-refuses-the-cycle) | whichever gate remedy above matches the message |
| [an adopted solution whose plan declares no build settings](../guides/handling-refusals.md#an-adopted-solution-whose-plan-declares-no-build-settings) | `sdlc stack`; a cycle whose plan declares the files brings them |
| **Recording evidence — `sdlc evidence`** | |
| [no cycle, or no plan](../guides/handling-refusals.md#no-cycle-or-no-plan) | check the id; the cycle needs a `plan.md` |
| [a verdict already stands](../guides/handling-refusals.md#a-verdict-already-stands) | delete `verify.md`, record again, verify again |
| [git cannot say where the branch diverged](../guides/handling-refusals.md#git-cannot-say-where-the-branch-diverged) | read the branch name in the message; rename yours to `cycle/<id>` |
| [an empty patch beside a changed file](../guides/handling-refusals.md#an-empty-patch-beside-a-changed-file) | `git status --short`; the evidence is wrong, not the code |
| [no results file](../guides/handling-refusals.md#no-results-file) | point `--tests` at the directory holding the `.trx` files |
| [one test name with two outcomes](../guides/handling-refusals.md#one-test-name-with-two-outcomes) | rename one test, or run the projects separately |
| **Opening a pull request — `sdlc pr-ready`** | |
| [not verified](../guides/handling-refusals.md#not-verified) | `sdlc evidence <id>` if needed, then `/sdlc:verify <id>` |
| [the verification did not pass](../guides/handling-refusals.md#the-verification-did-not-pass) | `/sdlc:remediate <id>` |
| [escalated](../guides/handling-refusals.md#escalated) | a person decides: reopen the approval, or abandon the cycle |
| [the chain does not hold](../guides/handling-refusals.md#the-chain-does-not-hold) | `sdlc verify-chain <id>` prints every check |
| **Denied to an agent — the pre-tool hook\*** | |
| writing `approval.md` | `sdlc draft approval <id>`, then rename |
| writing `abandoned.md` | `sdlc draft abandon <id>`, then place it |
| writing `constraints.md` | `sdlc propose constraints`, then paste |
| writing `decisions.md` | `sdlc propose decisions`, then paste |
| writing `solution.md` | `sdlc draft solution`, then rename |
| writing `techstack.md` | `sdlc draft techstack`, then rename |
| running the adoption command | run it yourself; `sdlc help` for its options |
| **Sent back at turn end — the turn-end hook** | |
| [the recorded suite has a failure](../guides/handling-refusals.md#the-recorded-suite-has-a-failure) | fix the code, run the suite, record again. **Not the test** |
| [there are changes and no recorded evidence](../guides/handling-refusals.md#there-are-changes-and-no-recorded-evidence) | `sdlc evidence <id> --tests <dir> --build-log <log>` |
| [the working tree has moved since the evidence](../guides/handling-refusals.md#the-working-tree-has-moved-since-the-evidence) | build and test what is there now, and record it |
| [when it says nothing](../guides/handling-refusals.md#when-it-says-nothing) | expected off a `cycle/` branch and before a plan; the pipeline check is the guarantee |

\* **The pre-tool hook's seven denials are absolute: no mode, no flag.** Run the command the denial
names, and place what it produces yourself. A command that only *mentions* one of these files is denied
too — for a commit message, use `git commit -F` with the message in a file. The pre-tool hook section of
`../guides/handling-refusals.md` has why each is a person's.

## See also

- [Handling refusals](../guides/handling-refusals.md) — every refusal in full, with why
- [Handling blockers](../guides/handling-blockers.md) — a blocking finding or a blocked verification, which cause some of these
- [Commands](commands.md) — every command, one line each
