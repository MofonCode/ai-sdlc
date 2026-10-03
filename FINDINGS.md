# Review findings — iteration 4

Reviewed at commit `65594ad`, 2026-10-03. Method: `CONCEPT.md` and `CONSTRAINTS.md` read end to end, all
of `src/` read, then the interesting claims exercised against purpose-built governed repositories using a
Release build of `src/Sdlc.Cli`. Every finding marked **reproduced** was observed, not inferred.

State at review: build clean at `latest-all`, 468 tests pass, and a harness-generated solution builds and
passes its own 19 tests.

This file is working material. Delete it when the fixes land.

## Severity scale

| | Meaning |
|---|---|
| **Critical** | Harms the user's machine or repository, or makes the headline promise unobtainable. Fix before anyone else installs this. |
| **High** | A stated guarantee is not delivered, or a documented path does not work. The framework still runs. |
| **Medium** | A rule can be bypassed, or silently stops covering something. Correctness gaps with a workaround. |
| **Low** | Friction, dead weight, or a failure that is certain but not yet reached. |
| **Cosmetic** | Documentation or comment drift with no behavioural consequence. |

## Index

| Id | Severity | Needs | Finding |
|---|---|---|---|
| F-01 | Critical | done | ~~The pre-tool hook auto-approves every call it does not refuse~~ **fixed, D-39** |
| F-02 | Critical | done | ~~`sdlc init` overwrites the project's `.claude/settings.json`~~ **fixed, D-40** |
| F-03 | Critical | done | ~~The packed tool ships no constraint library, so nothing but `help` and `version` runs~~ **fixed, D-41** |
| F-04 | High | done | ~~A deleted or malformed findings round makes the gate and the chain check blind~~ **fixed** |
| F-05 | High | done | ~~`diff.patch` omits every new file, and the comparison reports agreement~~ **fixed** |
| F-06 | High | done | ~~`outcome: "pass"` passes the schema and fails the chain~~ **fixed** |
| F-07 | High | done | ~~Adoption writes a props file referencing a file it does not write~~ **fixed** |
| F-08 | Medium | done | ~~The `refine` and `remediate` guards are claimed as hook rules and are prose~~ **decided, D-43** |
| F-09 | Medium | done | ~~§11.3.1 and D-38 give contradictory rules for where a response lives~~ **fixed** |
| F-10 | Medium | done | ~~The scaffold's package rule has an unguarded exemption~~ **fixed** |
| F-11 | Medium | done | ~~A blocking finding with no citation is accepted~~ **fixed** |
| F-12 | Medium | done | ~~`NewCycle` uses the look-then-create D-10 rejected~~ **fixed** |
| F-13 | Medium | done | ~~`sdlc claims` schedules unapproved, abandoned and landed cycles~~ **fixed** |
| F-14 | Medium | done | ~~A duplicated section passes the artifact schema~~ **fixed** |
| F-15 | Medium | done | ~~`Repository.Run` can deadlock and discards every git error~~ **fixed** |
| F-16 | Medium | done | ~~The scaffold's response rule is stricter than the decision it implements~~ **decided, two discriminators** |
| F-17 | Low | done | ~~`JsonSchema.Net` is referenced, unused, and most of the package~~ **fixed** |
| F-18 | Low | done | ~~Four `SuppressMessage` attributes are dead and their justifications are inverted~~ **fixed** |
| F-19 | Low | done | ~~`sdlc status` never shows the hash digits §5.3 promises~~ **fixed** |
| F-20 | Low | done | ~~`findings_rounds` in the approval ledger has no reader~~ **dropped** |
| F-21 | Low | done | ~~The first real `Web` project fails the package rule~~ **fixed** |
| F-22 | Low | done | ~~The scaffold harness keeps a second copy of what adoption writes~~ **fixed** |
| F-23 | Low | done | ~~D-38 states a registration shape the template does not carry~~ **fixed** |
| F-24 | Low | **you** | §10.2's run-mode environment variable does not exist |
| F-25 | Low | done | ~~The needs-services heuristic matches "container" and "migration"~~ **fixed** |
| F-26 | Low | done | ~~The generated `AGENTS.md` points at a file `init` does not create~~ **fixed** |
| F-27 | Low | **you** | `CON_10.2` cannot be applied by anything |
| F-28 | Low | **you** | The generated pipeline fails on any branch that is not a cycle branch |
| F-29 | Low | done | ~~The generated pipelines do not match §13's coverage description~~ **fixed** |
| F-30 | Cosmetic | done | ~~§7.5's constraint table is internally inconsistent about revisions~~ **fixed** |
| F-31 | Cosmetic | done | ~~`Scaffold.cs` asserts a gap the next commit closed~~ **fixed** |
| F-32 | Cosmetic | done | ~~Two `Release` comments assert a tool manifest D-37 rejected~~ **fixed** |
| F-33 | Cosmetic | done | ~~§16's cycle-index claim contradicts `CycleIndex.cs`~~ **fixed** |
| F-34 | Cosmetic | done | ~~`tools/` and the repository's own CI are recorded only in `AGENTS.md`~~ **fixed** |
| F-35 | Cosmetic | done | ~~The scaffold prints the line D-35 cited as evidence of a problem~~ **fixed** |
| F-36 | Cosmetic | done | ~~§15.2's command table omits `scaffold` and `version`~~ **fixed** |
| F-37 | Cosmetic | done | ~~`IRepository.TrackedFiles`' documentation describes filtering it does not do~~ **fixed** |
| F-38 | Cosmetic | done | ~~§16's layout names a `tests.trx`; the code keeps the runner's own filename~~ **fixed** |
| F-39 | Cosmetic | done | ~~The no-flags trap for `dotnet test` is documented in one skill and nowhere else~~ **fixed** |
| F-40 | Cosmetic | **you** | A revised constraint leaves nothing readable at the revision findings cite |

---

# Triage

Re-read at commit `7716c63`, after F-01, F-02 and F-03 landed. The question asked of each remaining
finding was narrow: **does fixing it require Peter to decide something, or only to be told it is done?**

**Thirty-seven open. Seven need an answer; thirty do not.** The thirty are not all small — F-04, F-06
and F-15 are real work — but each one's remedy is already determined by a recorded decision, by a rule
the framework states about itself, or by a single obviously-correct reading. None of them needs a
preference.

## The seven

Each is a genuine fork where both branches are defensible and the choice has consequences past this fix.
Stated as questions, so they can be answered in one pass without re-reading the findings.

| Id | The question | Why it is yours |
|---|---|---|
| F-08 | Are the `refine` and `remediate` guards prose, or should the hook learn which stage is running? | §2.4 and §6.3 claim a hook rule that does not exist. Making it real is the framework's first conditional hook rule, which D-09 rejected explicitly — so it needs its own decision and a mechanism that is not forgeable by the thing it constrains. The cheap answer is to correct the documents, and it is a retreat from a stated guarantee. |
| F-16 | What discriminates a response that must live in contracts from one that may live anywhere? | The rule ships into every scaffolded solution and currently fires on `IRequest<int>` and on any internal dispatch. The two candidates — request reachability (`public` vs `internal`), or exempting assemblies the solution does not own — draw the line in different places, and whichever ships teaches every future operation its shape. |
| F-20 | Does `findings_rounds` get a consumer, or get dropped? | Confirmed: only a test reads it. Giving it one means a new gate refusal when a round number is behind what is on disk — a real feature, and a useful one next to F-04. Dropping it means editing §5.2, §5.3, the model and the writer. D-06 forbids leaving it as it is. |
| F-24 | Is `SDLC_RUN_MODE` invented now, or is §10.2 marked as a requirement on a solution's AppHost? | No AppHost ships, so naming a variable now is designing a contract with no implementer. Saying so in §10.2 is honest and leaves the collision §10.2 warns about with nothing acting on it. |
| F-27 | Does `CON_10.2` get a reader, or an admission that it has none? | Nothing can evaluate it: `--for` returns it for none of the five artifacts. A weak mechanical check is possible; so is saying a person applies it when reading the ledger. `CON_11`'s own test makes leaving it as decoration the one unavailable option — and this one edits the constraint library. |
| F-28 | Does a pull request from a non-cycle branch pass or fail? | This is the dependabot and hotfix question, not a wording question. `ExitCode.Refused` blocks every bot bump and every revert until it is opened as a cycle; `Ok` lets ungoverned changes merge by the front door. §3.1 says every class of change runs the chain, which argues for blocking, and nobody has had to live with it yet. |
| F-40 | Does a revised constraint leave its old wording on the page, or does a citation resolve to the current revision? | §7.4 says findings cite the revision and leaves nothing readable at revision 1, so both `CON_07` and `CON_10` now cite wording that exists only in git. Either answer is coherent; they imply different pages and different things about what a past round was judged against. |

## Calls I would make on the other thirty

Seven of the thirty carry an either-or inside the finding, and each has a recommendation I agree with.
Listed so that "no decision needed" is a claim you can check rather than take:

| Id | What I would do, and why it needs no decision |
|---|---|
| F-05 | `--intent-to-add` over a per-file `--no-index` diff: one git call, and it stages no content. Plus the guard that an empty patch beside a non-empty change set is reported as the bug it is. |
| F-07 | Condition the `AdditionalFiles` item. The finding worries a condition lets a deleted ban list stop being enforced silently — it does not: the scaffolded `The_frameworks_response_factories_are_still_banned` reads the file, so a deleted one is a red test rather than a quiet gap. That settles it. |
| F-21 | Prefix-match the `Web` row, with the reason in the remark. The legitimate set really is "the WebAssembly packages". |
| F-22 | Extract the five greenfield stack files from `Initialise` into one list the harness and adoption both read. Verified they are the same five today, by hand, which is the thing that will drift. |
| F-23 | Add the options delegate now. One parameter, and D-38's argument is that an agent copies the shape beside it. |
| F-29 | Correct §13 to name what ships, and move the coverage delta to §19. Building it needs a base-branch run to compare against, which is more than the one step §13 implies. |
| F-38 | Write the trx through under a fixed name. A cycle's evidence directory is read by people, and a GUID is not a name. |

## Two notes from working through them

**F-39 is filed Cosmetic and has now cost two sessions an hour each.** The `dotnet test --nologo`
trap — VSTest-style flags forwarded to the application, "Zero tests ran", exit 5 — caught this review
once and caught the F-01 fix once more. A documentation gap that reliably costs an hour is not cosmetic.
Worth re-rating to Low and doing early, since it is one sentence in §11.1.

**F-34 grew.** The CI file gained three steps in F-03's fix (pack, install, run the installed tool from
outside the tree), and that is another thing recorded only in a YAML comment. Its remedy is unchanged;
there is simply more of it.

---

# Critical

## F-01 — The pre-tool hook auto-approves every call it does not refuse

**Fixed.** `WriteDecision` now writes an empty object on anything but a deny, for both hosts;
`An_allow_says_nothing_rather_than_bypassing_the_hosts_own_gate` asserts it and says why; D-39
records the decision, including why `ask` was rejected.

**Severity:** Critical
**Where:** `src/Sdlc.Core/Hooks/HookHost.cs:282-305` (`WriteDecision`), locked in by
`tests/Sdlc.Tests/HookTests.cs:311` (`An_allow_is_written_in_the_same_shape_as_a_denial`)

**What.** On the pre-tool path the adapter writes `permissionDecision: "allow"` whenever the policy has
nothing to say. In Claude Code `"allow"` is not silence — it bypasses the permission system. So installing
ai-sdlc stops the host asking the user about Bash commands and file writes, and overrides any other
allow/deny rule for the matched tools (`Edit|Write|MultiEdit|NotebookEdit|Bash`).

**Reproduced.**

```
$ echo '{"tool_name":"Bash","tool_input":{"command":"echo hi"}}' | sdlc hook --host claude
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow","permissionDecisionReason":""}}
```

**Why it matters.** A governance tool that silently removes the host's own gate as a side effect is the
inverse of §2.1. The turn-end path already gets this right and says why, in `WriteTurnEndDecision`:
*"An allow writes an empty object rather than an explicit permission. There is nothing to say, and a host
that gains a field for saying it will not break on silence."* The same reasoning applies one event over.

**Remedy.** Return an empty object (or omit `permissionDecision`) for an allow on the pre-tool path, as the
turn-end path does. Then invert `An_allow_is_written_in_the_same_shape_as_a_denial` so it asserts that an
allow carries no decision, and name the reason in the test — a future reader will otherwise "fix" it back.
Check the Copilot branch against its own documentation before changing it; if Copilot requires an explicit
allow, the two hosts diverge here and the adapter is the right place for that to live.

---

## F-02 — `sdlc init` overwrites the project's `.claude/settings.json`

**Fixed,** along with a sibling the finding did not name: the same `Owned` path also replaced an
existing `.github/workflows/pull-request.yml` or `azure-pipelines.yml` — a team's whole CI, replaced
and committed, announced as "updated". `.claude/settings.json` is now merged (hook entries only, every
other key kept, strict-parse-or-print), the GitHub workflow is framework-named `sdlc-verify.yml` so
owning it is honest, an existing `azure-pipelines.yml` is printed rather than replaced, and a
`pull-request.yml` an earlier adoption wrote is named for a person to remove. D-40 records the
ownership rule and partly supersedes D-28; §9's sentence and the first-cycle guide follow it.

**Severity:** Critical
**Where:** `src/Sdlc.Core/Init/Initialise.cs:181`, written through `Owned` (rewritten on every run,
update mode included)

**What.** `.claude/settings.json` is the host's shared project settings file — permissions, env, the team's
own hooks. `init` writes the whole file from `Templates.HookConfig("claude")`, so everything else in it is
destroyed, in every mode, every run.

**Reproduced.** Planted `permissions.deny`, `env.OUR_VAR` and a second `PreToolUse` hook in an adopted
repository, then ran `sdlc init`. Output: `updated .claude/settings.json`. Afterwards the file holds only
the two sdlc hooks; the permissions, the env var and the team's audit hook are gone.

**Why it matters.** §9 and D-28 say update "refreshes only what the framework owns". The framework owns two
hook entries, not the file. A team that adds a `deny` rule or a hook of their own loses it on the next
upgrade with no warning — and losing a hook is §2.1's failure mode caused by the thing meant to prevent
it. Separately, the plugin already ships `hooks/hooks.json`, so for plugin users this file is redundant and
makes every matched tool call pay two process launches.

**Remedy.** Merge rather than replace: read the existing JSON, add or update only the two sdlc hook entries
(match on the command string), and write the rest back untouched. Preserve unknown keys and sibling hook
entries. Where a merge cannot be done safely — malformed JSON, or a conflicting sdlc entry a person
edited — print the block for the person to place, which is the pattern `Agents` already uses for
`AGENTS.md`. Consider not writing it at all for plugin installs, since `hooks/hooks.json` covers it;
`.github/hooks/sdlc.json` is sdlc-named and can stay owned.

**Test to add.** An `init` over a settings file holding an unrelated key leaves that key present.

---

## F-03 — The packed tool ships no constraint library, so nothing but `help` and `version` runs

**Fixed, by embedding rather than packing.** The library is an `EmbeddedResource` in `Sdlc.Core`, so it
cannot be left out of a package; a file found through `SDLC_CONSTRAINTS`, beside the gate or above it
still wins, which keeps this repository's own edit-and-run loop. `Workspace.Run` no longer refuses to
open over a file none of its commands read, and `FileConstraintDocuments`' `throw` is now unreachable
rather than one guard away. `ConstraintDocument.Path` became `Source`, since the global tier's value is
`embedded in sdlc <version>`. D-41 records it, with what embedding costs.

Verified on the packed artifact: installed to a scratch tool path outside the tree with no
`SDLC_CONSTRAINTS` set, `sdlc constraints` renders the same fourteen rules the repo build does, and
`init` and `verify-chain` work. CI now packs, installs and runs it from outside the repository — the
only check in the suite that touches the artifact a person installs, and the reason this survived a
release.

**Severity:** Critical
**Where:** `src/Sdlc.Cli/Sdlc.Cli.csproj` (nothing packs `CONSTRAINTS.md`); `src/Sdlc.Cli/Locate.cs:30`;
`src/Sdlc.Cli/Workspace.cs` (`Run` requires the library before the body)

**What.** `Locate.Library` looks at `SDLC_CONSTRAINTS`, then beside the assembly, then up the tree. A global
tool install satisfies none of those. Every command that opens a workspace therefore fails.

**Reproduced.** Packed the real package, extracted it outside the repository tree, ran it:

```
$ unzip -l MofonCode.Sdlc.Cli.0.9.0.nupkg | grep -c CONSTRAINTS      -> 0
$ sdlc version                                   -> works
$ sdlc constraints                               -> Could not find CONSTRAINTS.md beside the gate or above it.
$ sdlc verify-chain 20260929_1456_32             -> same message, exit 3
$ echo '{}' | sdlc hook --event turn-end --host claude
                                                 -> same message on stderr, then {} — turn allowed
```

So `init`, `gate`, `status`, `draft`, `evidence`, `claims`, `landable` and `verify-chain` all fail, and the
turn-end gate fails **open**.

**Why it matters.** This is the install path `README.md` and `docs/guides/install.md` document, and the
pinned install step D-37 added to both generated pipelines. The one non-bypassable layer can never pass, so
nothing can ever merge — fail-closed, but for a reason unrelated to the chain. §7.3's claim that
`sdlc constraints` "is also how the set reaches a session, on every host" does not hold for an installed
tool. The documented smoke test is `sdlc help`, which is one of the two commands that cannot detect the
fault.

**Remedy.** Two parts, both worth doing.

1. Pack the library. In `Sdlc.Cli.csproj`:
   `<Content Include="..\..\CONSTRAINTS.md" Pack="true" PackagePath="tools\$(TargetFramework)\any\" CopyToOutputDirectory="PreserveNewest" />`
   — then `Locate.Library`'s "beside the assembly" branch finds it. Verify by packing, installing globally,
   and running `sdlc constraints` from an unrelated directory.
2. Stop coupling the whole container to the library. `Workspace.Run` refuses to open when the library is
   absent, but only `EffectiveConstraints` reads it — `gate`, `verify-chain` and both hook paths do not.
   Register `IConstraintDocuments` lazily (or let `FileConstraintDocuments` fail when enumerated rather
   than when constructed) so a missing library breaks the one command that needs it instead of all of them.
   That alone would have turned this from "the tool does not work" into "one command does not".

Also mention `SDLC_CONSTRAINTS` in `docs/guides/install.md` as the escape hatch, and change the documented
smoke test from `sdlc help` to something that opens a workspace.

**Test to add.** Assert the packed `.nupkg` contains `CONSTRAINTS.md` — or, cheaper and almost as good,
that the build output directory beside `sdlc.dll` contains it.

---

# High

## F-04 — A deleted or malformed findings round makes the gate and the chain check blind

**Severity:** High
**Where:** `src/Sdlc.Core/Approvals/ApprovalGate.cs:257` and `:307` (`if (history.IsFailed) continue;`);
`src/Sdlc.Core/Findings/FindingsRound.cs` (`ReadHistory` fails the whole artifact if any one round file is
unreadable); `src/Sdlc.Core/Chain/VerifyChain.cs` (`Gate` delegates to the same handler)

**What.** Two holes with one cause — the gate treats "cannot tell" as "nothing to answer".

1. Make one round file unreadable and every blocker for that artifact disappears from the gate.
2. Delete the `findings/` directory entirely and the gate, `verify-chain` and `landable` all pass, with no
   round having ever existed.

**Reproduced.**

```
# with an open blocking finding in spec.r2.json
$ sdlc gate <id>      -> "1 blocking finding is still open"   exit 1
$ printf '{}' > findings/spec.r2.json
$ sdlc gate <id>      -> "was approved ... all three documents match"   exit 0

# with findings/ moved aside entirely
$ sdlc verify-chain <id>   -> "[ok] the gate is satisfied" / "The chain holds."   exit 0
$ sdlc landable <id>       -> "verified and its chain holds."
```

Meanwhile `sdlc status` on the same cycle prints:

```
  Checks    none yet. A stage will not run until the artifact before it has a round with no
            open blocking findings.
  Gate      approved, and all three documents match what was approved
```

**Why it matters.** D-27 says a blocking finding "cannot be waived, by any mechanism" and this is a
one-command mechanism. §5.4's clean-round requirement and D-21's staleness check are implemented in
`ReadCycleStatus.ArtifactState.IsClear` (which requires `Rounds > 0`) and omitted from `ApprovalGate`, so
the advisory reader is stricter than the guarantee. `ApprovalGate.cs:250`'s comment claims an unreadable
round is "reported where it is read" — true of `status` and `suggestions`, not of the two layers that
decide.

**Remedy.** Three changes in `ApprovalGate.Handler`:

1. Replace both `continue`s with a refusal. Add a `GateRefusal` value — `UnreadableRound` — and return it
   naming the file and the parse error. "Could not tell" must never read as "clean".
2. Add a refusal for an artifact with no round at all (`NoInterrogation`), matching
   `ArtifactState.IsClear` and `ApprovalText`'s `Uninterrogated` refusal. The printer already refuses this;
   the gate should too, since the printer is advice a person can decline.
3. Consider making `ReadHistory` return the rounds it could read *alongside* the errors, so one bad file
   does not hide the others. Either way the gate must fail, not skip.

**Test to add.** Both shapes: a gate refusal when a round file is corrupt, and a gate refusal when an
artifact has no round. Assert `verify-chain` fails in both cases too, since it is the layer the promise
rests on.

---

## F-05 — `diff.patch` omits every new file, and the comparison reports agreement

**Severity:** High
**Where:** `src/Sdlc.Core/Git/Repository.cs:205` (`Diff` runs `git diff <base>`, which cannot see untracked
files) against `:212` (`ChangedFiles` adds `ls-files --others`); ordering made certain by
`skills/build/SKILL.md`, which records evidence before telling you to commit

**What.** `sdlc evidence` writes a patch that excludes untracked files, while the declared-against-changed
comparison includes them. For a cycle that creates files — every first cycle, and most feature work —
`diff.patch` is empty and the panel says everything is in order.

**Reproduced.** On a cycle branch, with `src/Roll.cs` created and declared in the plan:

```
$ sdlc evidence <id>
  Changed   3 files
            every declared file changed, and nothing else did
$ wc -c evidence/diff.patch
0
```

Staging the file first produces a correct 217-byte patch, so the fault is the ordering plus the question
`git diff` is asked.

**Why it matters.** §4.5 has `verify` reading the evidence "and nothing else", citing "test names and diff
lines", and `CON_08` forbids a claim with no cited evidence. With an empty patch there is nothing to cite,
and `verify-chain`'s `Proven` check only reads `tests.json`, so the chain holds regardless. The reassuring
line comes from the generous notion of "changed" while the artifact a reviewer depends on comes from the
strict one. `TurnEndGate` notes this blind spot for its own comparison; the consequence was never carried
back to `sdlc evidence`.

**Remedy.** Make `Diff` answer the same question `ChangedFiles` does. Cheapest correct form: before
diffing, `git add --intent-to-add --` the untracked paths (`ls-files --others --exclude-standard`), then
`git diff <base>`, which now includes them as new files. `--intent-to-add` stages no content, so it does
not disturb a person's index beyond the path entries. Alternatively append a per-file
`git diff --no-index /dev/null <path>` for each untracked file. Either way, add a guard: if `diff.patch` is
empty while `Changed` is non-empty, that is a bug and `sdlc evidence` should say so rather than print a
clean comparison. Then fix `skills/build/SKILL.md`'s ordering regardless, since committing first is the
robust habit.

**Test to add.** Record evidence for a cycle whose only change is an untracked new file; assert the patch
names it.

---

## F-06 — `outcome: "pass"` passes the schema and fails the chain

**Severity:** High
**Where:** `src/Sdlc.Core/Chain/VerifyChain.cs:402` and `src/Sdlc.Core/Cycles/ReadCycleStatus.cs:213` both
find the outcome with a line-prefix scan; `src/Sdlc.Core/Artifacts/ArtifactSchema.cs` reads the same key
with YamlDotNet

**What.** Two readers of one frontmatter key, disagreeing. A quoted scalar is valid YAML and the schema
accepts it; the chain check compares the raw text after `outcome:` and sees `"pass"`.

**Reproduced.**

```
  [ok]  every artifact's shape is sound
        no shape problems
  [no]  the change was verified
        verify records '"pass"'. Only a pass merges; ...
```

**Why it matters.** It blocks a legitimately passing cycle from merging, and `landable` reads the outcome
through the same check so the local refusal agrees with the wrong answer. `Frontmatter`'s own remarks
record iteration 2 losing this argument: *"the YAML itself is handed to a real parser, never to a regular
expression… the skill behaved one way at runtime and 333 tests agreed with the regex."* Comments, block
scalars and a stray `outcome:` inside a multi-line value are all mishandled by the same code.

**Remedy.** Read it through the parser. `ArtifactSchema.CheckKeys` already deserialises the frontmatter to
`Dictionary<string, string?>`; expose that as a small shared reader — something like
`Frontmatter.Keys(string yaml)` returning the dictionary — and have `VerifyChain.Outcome` and
`ReadCycleStatus` both use it. One implementation, so the schema and the gate cannot disagree about what a
key says. Compare case-insensitively against `ArtifactSchema.Outcomes` rather than the literal "pass", so
the allowed set lives in one place.

**Test to add.** `outcome: "pass"` holds the chain; a comment line and an `outcome` nested inside another
value do not confuse it.

---

## F-07 — Adoption writes a props file referencing a file it does not write

**Severity:** High
**Where:** `src/Sdlc.Core/Init/Templates.cs:771` emits
`<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />`; `Initialise` never writes
`BannedSymbols.txt`; `Scaffold.Files` does

**What.** Between `sdlc init` and `sdlc scaffold`, the repository holds a `Directory.Build.props` that
points at a file that is not there. Any project added in that window cannot compile.

**Reproduced.** Fresh repo, `sdlc init`, then a minimal `src/Thing/Thing.csproj`:

```
CSC : error CS2001: Source file '...\BannedSymbols.txt' could not be found.
Build FAILED.
```

**Why it matters.** D-35's table was corrected this iteration to put `BannedSymbols.txt` on the right
(written by the skeleton), which matches the code and matches D-36's rule that a ban ships with the surface
it governs. What was not corrected is the dangling `AdditionalFiles`. The correction therefore turned an
accidental disagreement into a decided one, and made D-35's justification false for this entry:
*"Everything on the left constrains nothing until there is code to constrain"* — the missing ban list does
not constrain, it breaks the build. The happy path (first cycle runs `sdlc scaffold`) masks it, and nothing
forces a plan to use `sdlc scaffold`. CI cannot see it either: the harness writes both halves in one pass,
so the adoption-only state is unreachable by construction.

**Remedy.** Make the item conditional —
`<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" Condition="Exists('$(MSBuildThisFileDirectory)BannedSymbols.txt')" />`
— and add a comment saying the file arrives with the skeleton and why, so nobody removes the condition.
Weigh the cost honestly in that comment: a condition means a deleted ban list stops being enforced
silently, which is why the scaffolded `The_frameworks_response_factories_are_still_banned` and the
framework's own parity test both exist. If that trade is unacceptable, the alternative is for adoption to
write the clock-ban lines only and `sdlc scaffold` to append the response-factory lines — more moving
parts, and D-35 argues against splitting the list.

**Test to add.** A test asserting that whatever `init` writes is self-consistent: every path referenced by
a generated MSBuild file either exists after `init` or is referenced conditionally. One assertion covers
this class rather than this instance.

---

# Medium

## F-08 — The `refine` and `remediate` guards are claimed as hook rules and are prose

**Severity:** Medium
**Where:** `CONCEPT.md:84` (§2.4) and `:443` (§6.3) against `src/Sdlc.Core/Hooks/HookPolicy.cs` and
`src/Sdlc.Core/Hooks/ProtectedPaths.cs`

**What.** §6.3's table says `refine` may never edit source or tests and `remediate` may never edit the
planning documents, and adds: *"Both guards are hook rules on file paths, not instructions in a prompt."*
§2.4 says the corollary — nothing that reacts to a failing test may edit a planning document — "is enforced
by the hook rather than by instruction". `HookPolicy.Decide` has two rules: the four protected files, and
`sdlc init`. There is no skill-awareness anywhere, and `ProtectedPaths`' own remarks explain why there
cannot be: *"the hook can deny every agent write to these paths without a mode, a flag, or a check on which
skill is running — and a rule with no exception has nowhere for a bypass to live."*

The skills are honest about it. `skills/refine/SKILL.md:22`: *"Only one of those is denied at the tool
call… The rest of this table is yours to keep."*

**Why it matters.** The inversion §2.4 exists to prevent — a red test causing the spec to be edited — is
enforced by nothing, and the concept says otherwise in two places. §2.1's own rule applies.

**Remedy.** Decide which way to resolve it; the honest cheap option is the document.

- **Document fix (recommended).** Rewrite §2.4 and §6.3 to say the guards are the skills' own and that the
  hook denies only `approval.md` among them. Add a decision recording why a per-skill hook rule was
  rejected — the `ProtectedPaths` reasoning is already written and just needs to be the record.
- **Or make it real,** which means the hook knowing which stage is running. That is the framework's first
  conditional hook rule, which D-09 rejected explicitly, so it needs its own decision and a mechanism (an
  env var the skill sets is forgeable by the thing it constrains; a lock file in the cycle directory is not
  much better). Do not do this without deciding it.

A middle option worth considering: `remediate` editing a planning document is detectable after the fact,
because the three hashes are recorded and the gate already refuses on a mismatch. So the guarantee partly
exists, and §6.3 could say that instead of claiming a hook.

---

## F-09 — §11.3.1 and D-38 give contradictory rules for where a response lives

**Severity:** Medium
**Where:** `CONCEPT.md:905` (§11.3.1) against D-38 and
`src/Sdlc.Core/Init/Scaffold/ConventionTests.cs.template:250`

**What.** §11.3.1 still reads *"**A response nests with the query that produces it**, and so do the records
and enums that exist only to describe it"*, with no mention of contracts or D-38. D-38 reverses that for a
scaffolded solution and says so explicitly: *"This is a departure from the shape the reference slice
teaches — 'the response nests with the query that produces it' — and its remarks have to state the new rule
rather than leave the old sentence standing."* The slice's remarks were updated. §11.3.1 was not.

**Why it matters.** §1–18 "state what is true" and §20 explains why; they now disagree, and the
disagreement is enforced in one direction only —
`Every_response_the_application_declares_lives_in_the_contracts_project` fails the build of any scaffolded
solution written the way §11.3.1 teaches. The framework's own code follows §11.3.1 throughout (nested
`Response` records across `Sdlc.Core`), which is defensible for a CLI with no wire, but §11.3.1 presents
the nesting rule as "the framework's, not a house preference". Two rules under one heading with nothing
saying which applies where.

**Remedy.** Rewrite the bullet in §11.3.1 to state both halves and the boundary between them: the request
nests in the holder always; the response nests only where nothing outside the process consumes it, and
moves to a contracts assembly where a client deserialises it. Cite D-38. Say plainly that `sdlc` itself is
the first case and a scaffolded solution the second, so a reader of either codebase knows which rule they
are under.

---

## F-10 — The scaffold's package rule has an unguarded exemption

**Severity:** Medium
**Where:** `src/Sdlc.Core/Init/Scaffold/StructureTests.cs.template:69` (`_allowedPackages`) and `:128`
(`Every_project_under_src_is_named_in_the_direction_rule`)

**What.** Two tables govern `src/`. The guard-the-guard test checks membership of
`_allowedProjectReferences` only. The package rule skips any role absent from `_allowedPackages` — which is
how the application project is exempted deliberately. So adding `src/Demo.Infrastructure` with a direction
row and no package row leaves it **silently exempt from the package rule**, and the exemption is the
privileged state.

**Why it matters.** D-38 calls the package rule the one that stops a second project "declaring a second
[DbContext] of its own, which is the move somebody makes when the first rule is in the way". Reaching for
it costs one omitted dictionary entry. The guard's own failure message asks for both rows in prose, which
is the advisory-versus-deterministic split the same file spends paragraphs arguing against — and its
sibling says it outright: *"A rule that silently stops covering part of the repository is worse than no
rule, because the build still goes green."*

**Remedy.** Have the guard assert membership of **both** tables, with the application project named as the
one documented exception — a single `private const string PackagesLiveHere = "Application";` that the guard
skips and the package rule skips, so the exemption is one named thing rather than an absence. Then the only
way to exempt a new project is to edit that constant, which is a visible decision. Mirror the change in the
framework's own `tests/Sdlc.Architecture.Tests/StructureTests.cs` if it grows a second table.

---

## F-11 — A blocking finding with no citation is accepted

**Severity:** Medium
**Where:** `src/Sdlc.Core/Findings/FindingsRound.cs` (`FindingsReader.Read` validates only that a
`findings` array is present); `Finding.Cites` is nullable

**What.** §7 says every blocking finding cites exactly one constraint, and D-30 says a citation is required
on a blocking finding and optional on a suggestion. Nothing enforces it. The gate renders the missing
citation as whitespace.

**Reproduced.**

```
$ sdlc gate <id>
Cycle ... is approved, and 1 blocking finding is still open:

  spec.F_09    no citation at all          <- the constraint column is empty
```

**Why it matters.** It is mechanically decidable, so by §2.3 it belongs in the reader rather than in a
prompt. An uncited blocker is also unarguable, which is the property §7.1 says the citation buys: a person
at the gate cannot judge whether the finding is cheap or expensive without the rule behind it.

**Remedy.** In `FindingsReader.Read`, fail a round that carries a blocking finding with a null, empty or
unparseable `cites`. Validate the id through `ConstraintId.TryParse` while you are there, so `CON_99` or
`CON_4` is caught as well as absence. Name the finding id in the error. Also check the id exists in the
effective set — that needs the constraint documents, so it may belong in the gate rather than the reader;
if so, do the shape check in the reader and the existence check in `ApprovalGate` alongside F-04's changes.

---

## F-12 — `NewCycle` uses the look-then-create D-10 rejected

**Severity:** Medium
**Where:** `src/Sdlc.Core/Cycles/NewCycle.cs:57`

**What.** The handler reads `if (Directory.Exists(cycle.Directory)) fail; … Directory.CreateDirectory(…)` —
directly beneath its own remark: *"A look-then-create would leave the race it was written to close, so this
creates and lets the filesystem decide — which it does atomically."* It also cannot be done the stated way:
`Directory.CreateDirectory` succeeds on a directory that already exists, so creation alone can never be the
collision check.

**Why it matters.** D-10 is a recorded decision with a rejection list, and the code does the rejected thing
while asserting otherwise. The window is one second wide and the cost is two cycles sharing a directory, so
the impact is small — but §16 and §15.2 both repeat the atomicity claim.

**Remedy.** Make the creation atomic with something that actually fails on a collision. Create a marker
file inside the cycle directory with `FileMode.CreateNew` (which throws `IOException` when it exists) and
treat that throw as the collision; `Directory.CreateDirectory` first is then harmless. Or keep
look-then-create and correct D-10, §16 and the remark to say the check is advisory and the real protection
is that two people rarely open a cycle in the same second. The first is a few lines and keeps the decision
true; prefer it.

---

## F-13 — `sdlc claims` schedules unapproved, abandoned and landed cycles

**Severity:** Medium
**Where:** `src/Sdlc.Core/Evidence/ComputeClaimGraph.cs:91` — `CycleIndex.All(...)`, skipping only cycles
with no `plan.md`

**What.** §10.1 says the graph is built from "the approved cycles in **this** working copy". The handler
applies no approval filter, no abandonment filter and no "already verified" filter. An abandoned cycle's
plan keeps claiming its files forever; every landed cycle narrows the graph for every future batch.

**Why it matters.** Not a safety hole — the gate refuses an unapproved build — but the graph is what
decides concurrency, and over-serialising is the cost D-15 names as self-correcting *for wide claims*, not
for cycles that should not be in the set at all. `batch-build` reads this output to plan a run.

**Remedy.** Filter in the handler, not in the skill: skip a cycle that `IsAbandoned`, skip one whose gate
does not permit a build, and skip one that already has a `verify.md`. Dispatch `ApprovalGate.Query` rather
than re-reading the ledger, so "approved" means one thing. Report the skipped ones in the response so the
renderer can say "3 cycles with a plan, 1 abandoned, 1 already verified" — a silently shorter list is its
own confusion.

---

## F-14 — A duplicated section passes the artifact schema

**Severity:** Medium
**Where:** `src/Sdlc.Core/Artifacts/ArtifactSchema.cs` (`CheckSections` tests membership only);
`src/Sdlc.Core/Artifacts/ArtifactOutline.cs` (the reader returns duplicates);
`src/Sdlc.Core/Artifacts/ArtifactSections.cs` claims otherwise

**What.** Two `## Acceptance criteria` sections validate cleanly. `ArtifactSections.Read`'s comment says
*"A repeated heading accumulates rather than replacing. Two sections of one name is a schema violation
reported elsewhere"* — there is no such check.

**Reproduced.**

```
  Says      A die roll, specified
            4 sections: Acceptance criteria, Out of scope, Open, Acceptance criteria
  Checks    r1: clean
```

**Why it matters.** The two lists merge in `NumberedItems`, where the first mention of each number wins —
so criterion 1 in the second list is silently discarded, and `verify-chain`'s proof matching never sees it.
A criterion that nothing proves and nothing reports is exactly what §5.4's second mechanical check exists
to catch.

**Remedy.** In `CheckSections`, report a violation for any heading appearing more than once, naming it.
Then correct `ArtifactSections.Read`'s comment — it is right that accumulating is the safer behaviour given
a duplicate exists, and wrong that something else reports it.

---

## F-15 — `Repository.Run` can deadlock and discards every git error

**Severity:** Medium
**Where:** `src/Sdlc.Core/Git/Repository.cs:256`

**What.** `RedirectStandardError = true` and stderr is never read, while `StandardOutput.ReadToEnd()` blocks
with no timeout before `WaitForExit(_patience)` is reached. If git writes enough to stderr to fill the pipe
buffer, git blocks on stderr, the parent blocks on stdout, and the five-second patience never applies.
`git add` on Windows emits one *"LF will be replaced by CRLF"* warning per file, so staging enough files
reaches it. Separately, every failure returns `null`, so git's own message is thrown away: `Commit` reports
"git could not stage what init created" with no reason.

**Why it matters.** The hang is in `init` and `evidence`, which have no external timeout (the hook paths are
capped at 10s by the host config, so they fail rather than hang). The discarded error contradicts the
framework's own standard that a refusal says what has to change — and `CON_10.2` is that rule.

**Remedy.** Read both streams without blocking: `BeginOutputReadLine`/`BeginErrorReadLine` with a
`StringBuilder` each, or `Task.WhenAll` over two `ReadToEndAsync` calls with the wait around them. Return
the captured stderr on failure — widen the return to a `Result<string>` or an `(string? Output, string?
Error)` so callers can quote git. Then make `Commit`'s two failures include it.

---

## F-16 — The scaffold's response rule is stricter than the decision it implements

**Severity:** Medium
**Where:** `src/Sdlc.Core/Init/Scaffold/ConventionTests.cs.template:250`

**What.** The rule requires *every* `IRequest<T>`'s `T` to be declared in the contracts assembly, with no
exception. Two consequences follow, neither yet reached because the slice has one operation:

- §11.3.1's stated exception — *"a response that is a shared type rather than one operation's payload…
  Those stay top-level"* — has no counterpart. `IRequest<int>`, `IRequest<bool>` or a response internal to
  the application project all fail, because the assembly is `System.Private.CoreLib` or the application
  itself.
- §11.3.1's forcing function says a nested handler *should* dispatch through the mediator rather than
  construct another handler. The first internal dispatch returning anything but a contracts type fails the
  build.

**Why it matters.** A rule that fires on legitimate work is the kind this file repeatedly says somebody
deletes on the day it is in the way — and deleting it reopens F-09's real gap, where a nested response
leaves the client with no type.

**Remedy.** Narrow the rule to what it is actually for: responses that cross the wire. The cheapest
discriminator that needs no list is the request's reachability — a `public` request is dispatchable from the
Api and its response must be in contracts; an `internal` request is an internal operation and its response
may live anywhere. Alternatively exempt types from assemblies the solution does not own (so primitives pass)
and require only solution-declared responses to be in contracts. Whichever you choose, state it in the
remark so the exception is a rule rather than a hole, and keep the positive control (`requests.Count > 0`).

---

# Low

## F-17 — `JsonSchema.Net` is referenced, unused, and most of the package

**Severity:** Low
**Where:** `src/Sdlc.Core/Sdlc.Core.csproj:6`; justified at length in `Directory.Packages.props`

**What.** No C# file references it. `ArtifactSchema` validates in hand-written C#. The reference pulls in
`Humanizer.Core`, `Json.More.Net` and `JsonPointer.Net`, which together are roughly 1 MB of the 1.03 MB
package.

**Why it matters.** Dead weight on the start-up path of a binary the project explicitly tunes for start-up
(`InvariantGlobalization`, `TieredPGO`, because a hook runs on every tool call), and dependency surface for
`NuGetAudit` to go red on. Its justification in `Directory.Packages.props` describes JSON Schema validation
that iteration 4 replaced.

**Remedy.** Remove the `PackageReference` and the `PackageVersion`, and delete the comment. If a schema
evaluator is wanted later, D-31's "declared extension" is the thing that would want it, and that is a
decision rather than a dependency to keep warm.

---

## F-18 — Four `SuppressMessage` attributes are dead and their justifications are inverted

**Severity:** Low
**Where:** `src/Sdlc.Core/Approvals/ApprovalLedger.cs` (two), `src/Sdlc.Core/Cycles/AbandonRecord.cs`,
`src/Sdlc.Core/Solutions/SolutionDocument.cs`; `Directory.Build.props` has
`<NoWarn>$(NoWarn);CA1034;CA1812</NoWarn>`

**What.** All four suppress `CA1812`, which is already in `NoWarn` repository-wide, so they suppress
nothing. Each justification reads *"Scoped here rather than suppressed repository-wide, where it would hide
a class that really is dead"* — the inverse of what the build does.

**Why it matters.** §11.4 says every suppression lives in `Directory.Build.props` with its reason. These are
in source, assert a policy that is not in force, and `StructureTests` exempts them with a hard-coded count
in a comment ("There are four in the tree") that will go stale.

**Remedy.** Delete the four attributes and the `using System.Diagnostics.CodeAnalysis;` they need. Remove
the count from `StructureTests`' remark and say instead that attributes are not caught, and why. If any of
them is wanted as documentation of *why* a type looks dead, make it a plain comment — it reads the same and
claims nothing about the build.

---

## F-19 — `sdlc status` never shows the hash digits §5.3 promises

**Severity:** Low
**Where:** `src/Sdlc.Core/Cycles/StatusRenderer.cs`; `ContentHash.Abbreviate` is used only by
`ApprovalGate`

**What.** §5.3 names two mitigations for the paste flow: *"`status` shows the first twelve characters of
each recorded hash beside each document's current twelve and a refusal names **which** document changed"*.
Only the second ships. The panel says `approved, but a document has changed since` without saying which, so
a person has to run `sdlc gate` to find out.

**Why it matters.** The panel is the most-read output in the plugin (§14), and the mitigation is for the one
cost §5.3 admits the paste flow has.

**Remedy.** Add the per-document hash pair to the gate section of the stage panel when the gate reports a
mismatch — `intent.md 3f9a2c7b2e41 / 3f9a2c7b2e41`, one line each, using `ContentHash.Abbreviate`.
`ReadCycleStatus.ArtifactState` already carries `Hash` and `ApprovalGate.Response.Hashes` carries both
sides, so the data is present. Keep it to the mismatch case; three hash pairs on every panel is noise.

---

## F-20 — `findings_rounds` in the approval ledger has no reader

**Severity:** Low
**Where:** written by `src/Sdlc.Core/Approvals/ApprovalText.cs` (`Entry`), modelled in
`GateEntry.FindingsRounds`, read by `tests/Sdlc.Tests/ApprovalLedgerTests.cs:38` and nothing else

**What.** §5.2's example carries it and §5.3's table lists it as part of a gate decision. Nothing consumes
it.

**Why it matters.** D-06's test — a section earns its place by having a consumer — is the framework's own,
and `approval.md` escaped it by not being an artifact with a schema. A field nothing reads is a field
nothing maintains.

**Remedy.** Pick one. Either give it a consumer — the gate could refuse when a recorded round number is
behind the highest round on disk, which is a cheap "somebody interrogated again after you approved" signal
and complements F-04 — or drop it from the entry, the model, §5.2 and §5.3. The first is more useful; the
second is honest. Do not leave it as is.

---

## F-21 — The first real `Web` project fails the package rule

**Severity:** Low
**Where:** `src/Sdlc.Core/Init/Scaffold/StructureTests.cs.template:69`

**What.** `_allowedPackages["Web"] = ["Microsoft.AspNetCore.Components.WebAssembly"]`, matched with exact
ordinal equality. A Blazor WebAssembly project also needs
`Microsoft.AspNetCore.Components.WebAssembly.DevServer`, which is not that string.

**Why it matters.** The row is pre-written for a project that does not exist yet, which is right. It will
fail on the day it does, in the cycle that is busy doing something else.

**Remedy.** Match on prefix for this entry, or list both packages. Prefix matching is the better shape here
— the legitimate set is "the WebAssembly packages" — but say so in the remark, because a prefix is a wider
grant than the file's other rows and the reason should be visible.

---

## F-22 — The scaffold harness keeps a second copy of what adoption writes

**Severity:** Low
**Where:** `tools/Sdlc.ScaffoldHarness/Program.cs`, the `Stack()` local function

**What.** The harness lists `.editorconfig`, both props files, `global.json` and `tests/Directory.Build.props`
by hand rather than asking `Initialise` what adoption writes. Nothing asserts the two agree.

**Why it matters.** The harness exists to catch what string tests cannot see. A sixth file added to adoption
leaves CI building a solution that no longer matches what `sdlc init` produces — the harness passes and the
real thing breaks, which is the failure it was built to prevent, one level up.

**Remedy.** Derive it. Either dispatch `Initialise.Command` against the target directory (it needs a git
repository, so `git init` first, which is a fair thing for a harness to do and also exercises more of the
real path) or expose the greenfield file list from `Initialise` as a single source both use. If neither is
convenient, add a test comparing `Stack()`'s paths against the `Written` set from a greenfield `init`.

---

## F-23 — D-38 states a registration shape the template does not carry

**Severity:** Low
**Where:** D-38, under "What it costs"; `src/Sdlc.Core/Init/Scaffold/Services.cs.template`

**What.** D-38 says *"The registration extension takes an options delegate. `Program` cannot name an
internal `DbContext`, so registration moves inside the project that owns the schema — which is also what
keeps the connection string there."* `Add__SOLUTION__(this IServiceCollection services)` takes no delegate.

**Why it matters.** Nothing needs one yet, since the default profile ships no EF. But it is written as a
present-tense property of the shape, so the first cycle adding EF invents it rather than copying it — and
D-38's whole argument is that an agent copies the code beside it more reliably than it follows an
instruction.

**Remedy.** Either add the delegate parameter now, with a remark saying it is where a `DbContext`
registration and its connection string will go, or move the sentence in D-38 into the future tense and say
the first cycle that takes EF is the one that adds it. The first costs one parameter and makes the shape
copyable; prefer it.

---

## F-24 — §10.2's run-mode environment variable does not exist

**Severity:** Low
**Where:** `CONCEPT.md:710` (§10.2); `skills/batch-build/SKILL.md` restates the table

**What.** §10.2 says the local-dev versus batch run modes are *"Selected by an environment variable that
`batch-build` sets"*. No variable is named in the binary, in the skill, or in any template, and the scaffold
has no AppHost to read one.

**Why it matters.** The concrete warning the section is built around — *"Named volumes are a worse collision
than ports… two writers on one database, which fails strangely rather than loudly"* — has nothing to act on
it. Both the concept and the skill describe a table with no mechanism.

**Remedy.** Name the variable and say who reads it. Minimum viable: pick a name (`SDLC_RUN_MODE`, values
`local` and `batch`), have `batch-build` export it per worktree along with a run id, and document in
`.sdlc/README.md` that an AppHost is expected to read both. Then §10.2 describes a contract rather than an
intention. If that is too early — no AppHost ships — say so in §10.2 and mark the table as what a solution's
AppHost has to implement, with the variable named so the first one does not invent its own.

---

## F-25 — The needs-services heuristic matches "container" and "migration"

**Severity:** Low
**Where:** `src/Sdlc.Core/Evidence/ComputeClaimGraph.cs`, `_serviceWords`

**What.** The list includes `"container"` and `"migration"`. In a .NET plan "container" almost always means
the DI container, and the word appears all over this repository's own prose.

**Why it matters.** It caps concurrency on nearly every plan, which is the cost the comment says it accepts
— but the comment justifies erring toward caution against a *miss*, and this is not caution, it is a
near-certain false positive on the most overloaded word in the stack.

**Remedy.** Drop `"container"` and keep the specific spellings that mean what is intended — `"docker"`,
`"WithDataVolume"`, `"testcontainers"`, `"compose"`. Keep `"migration"`, which earns its place through the
EF serialisation rule, but consider narrowing to `"EF Core migration"` and `"dotnet ef"`. Then say in the
remark which words were rejected and why, so the next addition is argued.

---

## F-26 — The generated `AGENTS.md` points at a file `init` does not create

**Severity:** Low
**Where:** `src/Sdlc.Core/Init/Templates.cs`, `Agents(...)` — the "Where things are" row for this solution's
own rules

**What.** It points at the solution's own constraints file inside `.sdlc/`, which `init` does not create; it
appears when a person writes the first local rule. A reader following the pointer on day one finds nothing.

**Why it matters.** Small, but this repository has a test asserting no document points at a path that does
not exist, applied to its own tree. The generated tree deserves the same courtesy.

**Remedy.** Change the row to name the command instead — `sdlc constraints` renders the effective set and
works on day one — and mention the constraints file as where this solution's own rules go once there are
any. That also matches the paragraph lower in the same file telling the agent not to restate rules.

---

## F-27 — `CON_10.2` cannot be applied by anything

**Severity:** Low
**Where:** `CONSTRAINTS.md`, `CON_10.2`; `src/Sdlc.Core/Constraints/EffectiveConstraints.cs` (`Applies`)

**What.** It applies to "a gate decision that is not an approval". `interrogate` runs against an intent,
spec, plan or verify document, and `sdlc constraints --for` returns `CON_10.2` for none of the five artifact
names. Verified:

```
intent  CON_01 CON_02 CON_09 CON_14
spec    CON_03 CON_04 CON_05 CON_09 CON_12 CON_14
plan    CON_06 CON_09 CON_13 CON_14
diff    CON_07.2 CON_09 CON_11
verify  CON_08 CON_09
```

**Why it matters.** The only rule governing a refusal's reason has no mechanism that evaluates it. The
ledger reader checks that a reason is non-empty; nothing judges whether it says what has to change.

**Remedy.** Give it a reader or say it has none. The cheapest real option: have the gate refuse a ledger
entry whose reason is purely a restatement of a finding id — a weak check, but a real one. The better option
is honesty: add a line to §7.5 or to the constraint itself saying it is applied by a person reviewing the
ledger rather than by an interrogation, since no artifact carries a gate decision. Decide which, because a
constraint nothing applies is decoration by `CON_11`'s own test.

---

## F-28 — The generated pipeline fails on any branch that is not a cycle branch

**Severity:** Low
**Where:** `src/Sdlc.Core/Init/Templates.cs`, both `Pipeline(...)` branches; `src/Sdlc.Cli/Program.cs`
(`TryCycle`)

**What.** The workflow triggers on every pull request and runs `sdlc verify-chain` with the head ref. A PR
from a branch that is not `cycle/<id>` exits 2 with *"'dependabot/nuget/x' is not a cycle id."*

**Why it matters.** §3.1 says every class of change runs the full chain, so refusing is arguably correct —
but the refusal arrives as a usage complaint about an argument, not as a governance decision, and a
reverting hotfix or a bot bump reads as a broken pipeline rather than an ungoverned change.

**Remedy.** Have `verify-chain` recognise a non-cycle branch as a distinct outcome and say so: *"this branch
is not a governed cycle, so there is no chain to check — open the change as a cycle, or merge it knowing
nothing verified it"*. One sentence turns an apparent install fault into a decision somebody has to make.
Decide deliberately whether that exit code is zero or non-zero and record it; non-zero is the safer default
and matches the promise.

---

## F-29 — The generated pipelines do not match §13's coverage description

**Severity:** Low
**Where:** `CONCEPT.md:1036-1038` against `src/Sdlc.Core/Init/Templates.cs`, both `Pipeline(...)` branches

**What.** Two mismatches. §13 says *"Coverlet plus ReportGenerator"*; both templates use
Microsoft.Testing.Platform's `--coverage` with a summary action on GitHub and `PublishCodeCoverageResults@2`
on Azure DevOps. §13 also states *"One cheap addition inside the no-threshold rule: report the coverage
**delta** against the target branch"* as taken; neither pipeline reports a delta.

**Why it matters.** The toolchain mismatch is only a stale sentence — the shipped choice is better, since
`--coverage` needs no extra package. The delta is a stated feature that does not exist.

**Remedy.** Correct §13 to name what ships. Then either implement the delta or move it to §19 as an open
item. On GitHub the delta needs a base-branch run to compare against, which is more than one step, so §19
is the honest home unless somebody wants to build it.

---

# Cosmetic

## F-30 — §7.5's constraint table is internally inconsistent about revisions

**Severity:** Cosmetic
**Where:** `CONCEPT.md:531-548`

**What.** The table now reads `CON_07.2` with its revision and `CON_10` without, while the library ships
`CON_10.2` with different wording ("says what has to change", not "says why").
`ShippedLibraryTests.A_rebound_constraint_keeps_its_id_and_gains_a_revision` asserts the `.2`.

**Why it matters.** §7.4 makes a bare id revision 1, so the table names an id that is not on the page, in
the place a reader copies a citation from.

**Remedy.** Update the row to `CON_10.2` and its statement to the library's wording. Then close the class:
add a test comparing §7.5's table against the parsed library — ids and statements both. The suite already
compares §15's skill table against `skills/` and the README's counts against the tree, so the pattern and
the machinery exist.

---

## F-31 — `Scaffold.cs` asserts a gap the next commit closed

**Severity:** Cosmetic
**Where:** `src/Sdlc.Core/Init/Scaffold.cs:39-49`

**What.** The remarks say *"**Nothing in the suite compiles what these produce, and that is a real gap.**…
A test doing it automatically would need a package restore from a network, which is why it does not exist
yet rather than why it should not."* Commit `65594ad` added the harness and three CI steps that do exactly
that, and did not touch this file. `AGENTS.md` was updated.

**Remedy.** Rewrite the paragraph to describe what exists: the suite asserts strings, so the harness and
`.github/workflows/ci.yml` generate a solution and build and test it on every pull request, and a template
change should be verified locally the same way. Keep the two defects it cites as the reason the step exists
— that is the valuable half.

---

## F-32 — Two `Release` comments assert a tool manifest D-37 rejected

**Severity:** Cosmetic
**Where:** `src/Sdlc.Core/Release.cs:10` and `src/Sdlc.Cli/Release.cs:22`

**What.** Both say the version is needed by *"the tool manifest `init` writes"*. D-37 decided the opposite
and says so: *"No tool manifest is written into an adopted solution."* No template writes one.

**Remedy.** Replace both with the real second consumer, which is the pinned `dotnet tool install` step in
the generated pipelines (`Templates.Pipeline`). The reasoning — two readers of one stamped attribute would
eventually disagree — survives unchanged.

---

## F-33 — §16's cycle-index claim contradicts `CycleIndex.cs`

**Severity:** Cosmetic
**Where:** `CONCEPT.md:1230` and §10.1's closing paragraph against `src/Sdlc.Core/Cycles/CycleIndex.cs`

**What.** §16 says *"A cycle index exists so `batch-build` does not glob directories to find work, and each
cycle records its branch and work item."* `CycleIndex` globs the directory deliberately and explains why a
manifest would be wrong: *"A manifest is a second place the truth lives, and the one that goes stale is
always the one a tool trusts."* Nothing records a branch or a work item. §10.1's *"A local cycle index
persists state so a run can be resumed and read"* is also unbuilt.

**Remedy.** Rewrite both to say finding work is a directory listing and why, and move the persisted resume
state to §19 as an open item for `batch-build`. `CycleIndex`'s own remarks are the text to draw from — they
already distinguish the two purposes correctly.

---

## F-34 — `tools/` and the repository's own CI are recorded only in `AGENTS.md`

**Severity:** Cosmetic
**Where:** `AGENTS.md` lines 41-42 and 60-78, against `CONCEPT.md` §16 and `README.md`'s
"Repository layout"

**What.** A new top-level directory, a new project, and the decision to build the framework as an ordinary
product with its own CI deliberately not pointed at itself, all live in `AGENTS.md` and in a YAML comment.
`CONCEPT.md` §16's layout block does not list `tools/`; README's layout table lists neither `tools/` nor
`.github/workflows/ci.yml`.

**Why it matters.** `AGENTS.md:45` says *"`CONCEPT.md` is the current authority on every design question"*,
and whether the framework runs CI on itself is a design question with good reasoning behind it.

**Remedy.** Add `tools/` and `.github/workflows/ci.yml` to README's layout table and to §16. Record the CI
decision as a short entry in §20 — what it runs, that it deliberately excludes `verify-chain` on itself
(§2.7), and that it builds a generated solution because the template tests assert strings. The `ci.yml`
header comment is most of the text already.

---

## F-35 — The scaffold prints the line D-35 cited as evidence of a problem

**Severity:** Cosmetic
**Where:** `src/Sdlc.Core/Init/InitRenderer.cs:188`

**What.** `sdlc scaffold` prints *"The reference slice … is meant to be deleted once there is real work."*
D-35 cites exactly that sentence as the symptom it was fixing: *"the reference slice's own 'delete it once
there is real work here' was admitting that rather than solving it."*

**Why it matters.** Nothing breaks. But the sentence is now inside an approved cycle, which is what D-35
changed, so the admission it was making no longer applies.

**Remedy.** Reword to what is now true: the slice is one worked example of the shape, it is part of this
cycle's change, and it is replaced by real work rather than left as decoration. The templates' own remarks
("This exists to be copied") carry the right tone.

---

## F-36 — §15.2's command table omits `scaffold` and `version`

**Severity:** Cosmetic
**Where:** `CONCEPT.md` §15.2 against `src/Sdlc.Cli/Program.cs`'s dispatch

**What.** The binary answers `scaffold` (added by D-35) and `version`. Neither is in §15.2's table, and
`version` appears nowhere in `CONCEPT.md`.

**Remedy.** Add both rows. Then close the class the way the suite already closes its siblings: a test
comparing §15.2's table against the verbs `Program` dispatches, in both directions.
`Every_skill_names_a_command_the_binary_actually_has` checks skills against the binary; this is the same
check pointed at the document, and it would also have caught F-30.

---

## F-37 — `IRepository.TrackedFiles`' documentation describes filtering it does not do

**Severity:** Cosmetic
**Where:** `src/Sdlc.Core/Git/Repository.cs`, the `TrackedFiles` interface comment

**What.** The summary says *"The files git is tracking, **excluding anything the framework owns**"*. The
implementation runs plain `git ls-files` and returns everything; the exclusion happens in
`Initialise.IsCode`.

**Remedy.** Drop the clause from the interface comment and say the caller decides what counts as code,
pointing at `IsCode`. The distinction matters because a second caller would otherwise assume filtering it
does not get.

---

## F-38 — §16's layout names a `tests.trx`; the code keeps the runner's own filename

**Severity:** Cosmetic
**Where:** `CONCEPT.md` §16's layout block against `src/Sdlc.Core/Evidence/RecordEvidence.cs` (`Normalise`
writes `Path.GetFileName(trx)`)

**What.** The evidence directory is documented as holding a `tests.trx`. The runner's output keeps whatever
name it had, which is usually a GUID.

**Remedy.** Either write the original through under a fixed name beside `tests.json`, which makes the
directory predictable for a reader and for `verify` citing it, or change §16 to show the real shape. The
first is slightly better: a cycle's evidence directory is read by people.

---

## F-39 — The no-flags trap for `dotnet test` is documented in one skill and nowhere else

**Severity:** Cosmetic
**Where:** `skills/build/SKILL.md`; `AGENTS.md` and `.github/workflows/ci.yml` now mention it too;
`CONCEPT.md` §11.1 does not

**What.** On Microsoft.Testing.Platform, VSTest-style flags on `dotnet test` are forwarded to the test
application, which runs nothing and reports "Zero tests ran" with exit 5. §11.1 owns *"`dotnet test` needs
two settings or it does not run the suite"* and documents a different route to the same symptom at length.

**Why it matters.** I lost an hour of this review to it and wrongly concluded the scaffolded suite was broken
before `skills/build/SKILL.md` set me straight. A reader of the concept would not have been warned.

**Remedy.** Add a sentence to §11.1 beside the `TestingPlatformDotnetTestSupport` warning: VSTest-style
flags are forwarded to the application and make it run zero tests while exiting non-zero. Three places now
say it; the section that owns the fact should be one of them.

---

## F-40 — A revised constraint leaves nothing readable at the revision findings cite

**Severity:** Cosmetic
**Where:** `CONSTRAINTS.md` preamble; §7.4; `CON_07.2` and `CON_10.2`

**What.** §7.4 requires a *superseded* constraint to stay on the page verbatim with `Superseded-by`, and says
nothing about a *revised* one — while also saying "Findings cite the revision". Both revisions bumped the
suffix and left nothing at revision 1, so a finding citing `CON_07` or `CON_10` cites wording that is no
longer readable anywhere but in git.

**Why it matters.** The whole point of the revision suffix is that *"a round judged under older wording stays
distinguishable from one judged under newer wording"* — which only buys something if the older wording can
be found.

**Remedy.** Decide and write down which it is. Either a revision leaves its predecessor on the page the way
a supersession does (more page, fully readable), or a citation resolves to the current revision of that
number and the suffix is provenance rather than a pointer — in which case say so in §7.4 and in
`CONSTRAINTS.md`'s preamble, and note that git is where older wording lives. `CON_07.2` is the cheapest case
to decide on, since only its `Why` changed.

---

# Suggested order

1. **F-01, F-02** — both change what the framework does to a user's machine. Smallest diffs here.
2. **F-03** — nothing else can be trusted while the installed binary cannot run. Do the packaging fix and
   the lazy-library fix together.
3. **F-04, F-06** — the two that make the non-bypassable layer say the wrong thing. F-06 brings a shared
   frontmatter reader that F-04's tests will want anyway.
4. **F-05, F-07** — both produce a repository that looks correct and is not.
5. **F-08, F-09** — two document-versus-code contradictions where the document is the authority and is
   wrong. Cheap, and they stop the next reader building on a false premise.
6. Everything else, by severity.

# Patterns worth fixing once

Three of these recur, and a single change addresses each class:

- **The advisory layer knows a rule the deterministic layer does not.** F-04 (`IsClear` requires a round;
  `ApprovalGate` does not), F-06 (`ArtifactSchema` parses YAML; `VerifyChain` scans lines), F-08 (the skills
  state guards the hook does not have), F-11 (the interrogation prompt requires a citation; the reader does
  not). Worth a pass asking, of each rule in §1–18: which layer actually decides this?
- **A document asserts a mechanism that does not exist.** F-08, F-24, F-33, F-23, F-29. The suite already
  has the machinery to close this class — it compares §15's skill table against `skills/`, the README's
  counts against the tree, and every markdown path against the filesystem. Extending it to §15.2's commands
  (F-36) and §7.5's constraints (F-30) would catch two more automatically.
- **A comment describes the state before the commit that changed it.** F-12, F-18, F-31, F-32, F-37. These
  are the ones a reviewer cannot catch by reading the diff, because the comment and the code are in
  different hunks or different commits.
