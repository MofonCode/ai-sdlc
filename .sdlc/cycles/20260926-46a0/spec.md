---
cycle-id: 20260926-46a0
intent: 20260926-46a0
charter-version: 4
pack-version: null
---

# Spec — close the shell route, and make two checks stop crying wolf

Two mechanisms, one shared licence answer. This is the second version: the first was interrogated and
**five** of its answers were wrong about the tree, in ways that would have reached the build, and one
case it did not cover at all. Those corrections are the substance below, marked where they land — five
*corrected here* and one *added here*, which is the count this sentence now matches.

*The number was four until an interrogation of this document counted the markers.* It is corrected
rather than left standing because a document that miscounts its own corrections is the shape of
defect it exists to catch.

## Why this widened

Two mechanical checks report false positives on this repository, and the verification outcome blocks
on both — so **no cycle here can currently pass verification**, including this one and including any
cycle that tried to fix something else. Both are the same defect as the gate: a mechanism that does
not do what it claims.

**A match inside a string literal is data, not code.** That is the whole diagnosis, and it needs two
mechanics because the two false positives have two shapes:

- `tests/Sdlc.Tests/PackTests.cs` names a pragma spelling **inside a quoted string on a code line**,
  in an assertion about whether a pack carries that spelling. The suppression scan matches the text
  and, finding no comment beside it, calls it unjustified.

  *Corrected here:* the first version also named `NoWarn` as a matched spelling in that file. The
  scan matches `<NoWarn>` **with its angle brackets**, and the assertion names the bare word, so that
  line never matched and was never part of the false positive. One spelling in one file, not two —
  and the fix is unchanged either way, since masking answers both shapes.
- `tests/Sdlc.Tests/PackTests.cs` and `tests/Sdlc.Tests/VerifyOutcomeTests.cs` contain annotated test
  methods **inside C# raw string literals**, as the source of generated test projects. The annotation
  scan reads those ids as real traceability and reports them as naming criteria no spec declares.

*Corrected here:* the first version credited `tests/Sdlc.Tests/SourceText.cs` as a helper the scanners
should reuse. It is a C# class in the test assembly, the PowerShell scans in `tools/Get-SdlcFacts.ps1`
cannot call it, and it strips PowerShell and JavaScript comments — neither of which addresses a match
inside a C# string literal. The work is new literal-aware scanning on the PowerShell side, not reuse.

## The answers

**Creation is not modification, and creation is a question about the filesystem.** A write to a
protected path that **does not currently exist** needs no licence. Creating a new pack is an ordinary
cycle — the charter says so, and *you cannot loosen something that does not exist yet* — and `init`
writing a first charter into an ungoverned repository is the same shape.

*Corrected here:* the first version tested creation against the baseline, which cannot be consulted in
a repository with no commit, which is exactly where `init` runs. Existence on disk needs no git, works
in a fresh repository, and settles the ordering: the existence test runs before the licence test.

**Destruction is a write shape, not the absence of one.** Removing, renaming, moving or truncating a
protected path is the largest change available to it, and the licence question is whether the branch
may change that path at all.

*Added here:* the first version recognised only writes that produce content, so a command deleting the
charter would have been permitted on an unlicensed branch.

**Could-not-evaluate is not no-write-found.** *Evaluated the command and found no write shape* is a
determination, and allowing it is the accepted leak the intent records. *Could not evaluate the command
at all* is no determination, so it denies — the same reason a thrown gate denies. A malformed or
unparseable command string falls on the deny side.

**Detection gates by emitting a fact, not only a check.** A named check makes the finding citable in an
interrogation. **It does not gate verification**, because the verification outcome consumes no check at
all — it reads specific fact fields, which is how unjustified suppressions already block. So detection
emits both: the check for citability, and a fact field the outcome reads and blocks on.

*Corrected here:* the first version justified siting detection in the gatherer on the ground that
"verification runs the gatherer, so any cycle that reaches its own gate is checked". Verification runs
the gatherer and ignores its checks entirely, so the detection half would have gated nothing.

**The baseline is the merge target, degrading explicitly.** The remote-tracking ref for master when it
resolves, else a local master when HEAD is not on it, else HEAD. On master with a remote this sees a
protected path committed directly. Each fallback is named in the check's detail, so a reader knows
which comparison was actually made.

**One licence answer, loaded where a failure denies.** Both mechanisms resolve the licence through one
shared function, and the gate loads it **inside its own try/catch**, so a module that cannot be loaded
is caught and denied rather than leaving a fail-open path in the one component that must fail closed.

*Corrected here:* the first version required the shared function without saying where it loads. The
gate reads a failure above its try/catch as no opinion, so an unspecified load was an unspecified
fail-open.

**A repository with no protected paths passes**, and does not skip. There is nothing to compare and
nothing to violate; skipping would imply a check was owed and not made.

**The existing refusals are a regression criterion.** Both the matcher and the script's own tool-name
check are being rewritten, and the gate denies a recognised tool that yields no recognised path — so a
mistake here turns the gate into a total block or a total pass.

*Corrected here:* the first version made a test count the criterion, asserting a number no check
verifies. The criterion is now about the suite.

## Acceptance criteria

| ID | Verifiable | Criterion |
|---|---|---|
| AC-20260926-46a0-1 | yes | A shell payload writing `.sdlc/CHARTER.md` from a branch with no licence is denied, and the refusal names the path and says what would permit it. |
| AC-20260926-46a0-2 | yes | The same payload is permitted on the branch of an intent whose source is `charter-review`. |
| AC-20260926-46a0-3 | yes | A shell payload writing a pack's `pack.yaml`, `pack.rules.yaml` or charter template is permitted on the branch of an intent whose source is `pack-review`. |
| AC-20260926-46a0-4 | yes | A `pack-review` branch writing the charter is denied, and a `charter-review` branch writing pack files is denied. |
| AC-20260926-46a0-5 | yes | A shell payload that only reads a protected path is permitted. |
| AC-20260926-46a0-6 | yes | A shell payload naming no protected path, in any shape, is permitted. |
| AC-20260926-46a0-7 | yes | A command string the classifier cannot evaluate is denied, and the refusal says it could not be evaluated. |
| AC-20260926-46a0-8 | yes | A write to a protected path that does not currently exist is permitted with no licence, for both a new pack file and a first charter in a repository with no commit. |
| AC-20260926-46a0-9 | yes | Every existing refusal for `Edit`, `Write`, `MultiEdit` and `NotebookEdit` still fires, and every case in the shipped gate test file passes unchanged with none deleted or skipped. |
| AC-20260926-46a0-10 | yes | The fact-gatherer emits a named check that fails when a protected path differs from its baseline on a branch with no licence for that path. |
| AC-20260926-46a0-11 | yes | That check reports skipped, with the reason, when no baseline resolves, and does not fail. |
| AC-20260926-46a0-12 | yes | That check fails when it cannot run, distinctly from skipping. |
| AC-20260926-46a0-13 | yes | A protected path committed directly to master with no licence is reported when a remote-tracking baseline resolves. |
| AC-20260926-46a0-14 | yes | Both mechanisms resolve the licence through one shared function. |
| AC-20260926-46a0-15 | yes | The new check id is classified in the interrogator's check-to-category map as a category that can block. |
| AC-20260926-46a0-16 | no | Whether the harness delivers `PreToolUse` for the PowerShell tool. The gate's decision is testable by feeding it a payload; whether such a payload ever arrives is not, and a person dispositions it from the debug log. |
| AC-20260926-46a0-17 | yes | The suppression scan ignores a suppression spelling occurring inside a quoted string, and no line of `tests/Sdlc.Tests/PackTests.cs` is reported. |
| AC-20260926-46a0-18 | yes | The suppression scan still reports a genuinely unjustified suppression in real code. |
| AC-20260926-46a0-19 | yes | The annotation scan ignores an annotation occurring inside a C# raw string literal, and neither fixture id is reported. |
| AC-20260926-46a0-20 | yes | The annotation scan still reports a genuine annotation, in code, naming a criterion no spec declares. |
| AC-20260926-46a0-21 | yes | The verification outcome for this cycle reports no unjustified suppressions and no orphan annotations. |
| AC-20260926-46a0-22 | yes | A shell payload that removes, renames, moves or truncates a protected path, from a branch with no licence for it, is denied, and the refusal names the path. |
| AC-20260926-46a0-23 | yes | When the shared licence module cannot be loaded, the gate denies. |
| AC-20260926-46a0-24 | yes | A protected write from a branch whose named intent file is absent is denied. |
| AC-20260926-46a0-25 | yes | A protected write from a branch whose intent has unreadable or invalid frontmatter is denied. |
| AC-20260926-46a0-26 | yes | A protected write from a branch whose intent declares a source that is neither permitted value is denied. |
| AC-20260926-46a0-27 | yes | A protected path whose difference from the baseline arrived by a merged amendment does not fail the check. |
| AC-20260926-46a0-28 | yes | The verification outcome blocks when the gatherer reports an unlicensed protected-path difference, so the detection half gates rather than only reporting. |

## Out of scope

Carried from the intent, unchanged: `PostToolUse` detection; changing which paths are protected;
amending the charter, including INV-08's *approved* wording; and the twelve blocking findings
outstanding against no open cycle from `20260926-bc24`.

**Whether a disposition can clear a mechanical check.** The verification outcome blocks on the
scanners' raw results, so a finding dispositioned `rejected` — the finding was wrong — does not clear
the block. That is why these two false positives had to be fixed rather than dismissed, and it is a
real question about what a disposition means for a check. It is charter-level and belongs to its own
intent.

**The path heuristic reading a git ref as a repository path.** It reported the remote-tracking ref for
master as a path that does not exist. Not a blocker, and narrowing a heuristic that already prefers
false negatives needs more care than this cycle has room for. Avoided rather than fixed: this document
names that ref in prose.

**The traceability check firing at spec stage by construction.** It demands an annotated test for every
verifiable criterion, and tests cannot exist before build. Correct at verify, noise at spec, and a
stage condition is the fix rather than a weaker check.

**The lifecycle derivation reading `dead` for supervised work on a cycle branch.** No unattended agent
means no locked worktree, so work in progress reads *relaunch or discard*. Not dangerous, since only a
`planned` cycle is dispatchable, but a real signal defect.
