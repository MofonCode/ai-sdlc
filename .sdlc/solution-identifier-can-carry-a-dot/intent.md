---
slug: solution-identifier-can-carry-a-dot
title: A scaffolded solution can keep the dot in its name
status: draft
tier: feature
created: 2026-09-20
owner: peter
---

# intent — a solution identifier can carry a dot

> **PARKED, 2026-09-20, after eleven fresh-context interrogations — none of which it passed.**
>
> **Not abandoned and not wrong.** Every measurement below reproduces, and the last interrogation
> said so explicitly. What is wrong is the scope: this chain kept trying to invent a way for the
> engine to *answer a question* — derive a candidate before any charter exists, touching nothing —
> inside a change that should only have widened a regular expression. Each revision improvised
> that mechanism differently and each improvisation failed differently: bypass the approval gate,
> reorder the interview, add a flag, three unchosen output contracts.
>
> **The engine has no query primitive**, and that is the actual blocker. See .
>
> **What this chain becomes once queries exist:** roughly thirty lines — permit interior dots in
> the identifier grammar, change the anchors from `^`/`$` to `\A`/`\z`, and move derivation
> behind the query. Most of the findings below stop applying because they are about the mechanism
> that no longer needs inventing here.
>
> **Worth keeping from below, all of it measured against the shipped code:** the two-stage
> derivation rule and the twelve-row table it reproduces; the supplied grammar
> `\A[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*\z`; that all nine generator invocations accept a
> dotted `-n` unsanitised while turning a hyphen into an underscore; that `"Probe
"` passes the
> rule today; that `init` supplies `-Solution` on every invocation so the executor's derivation is
> unreachable; and that shipped requirement R17 describes a system that was never built.

> **Rewritten after ten fresh-context interrogations; the earlier drafts are in git.** The last
> one shortened a six-step rule into one sentence that disagreed with its own table on half the
> rows. The rule is stated exactly here, once, because a compressed statement of it has now been
> wrong twice.

## Problem

**A repository whose name contains a dot cannot be scaffolded under its own name, and the
substitute is permanent.** Two mechanisms cause it and only one is in code.

**The validity rule refuses the name.** `Test-Identifier` is `^[A-Za-z][A-Za-z0-9]*$` and every
route to a value passes through it. Measured: `Probe.Core` supplied is refused. This is what
blocks a dotted package today, because `init` instructs *"Supply all four"* and always passes
`-Solution` — **so the supplied path is the only path, and it is the one that refuses.**

**The derivation rule is prose, and the requirement describing it was never implemented.**
`init/SKILL.md` tells a reader the generator derives the candidate. The generator has not run at
that point and will not run until after the name is settled, so **a model computes the candidate
by following a paragraph**, and the executor's own derivation is unreachable from `init`. Shipped
requirement R17 says the executor derives and `init` presents *that* candidate. It never has.

**The name cannot be fixed afterwards.** It becomes the package ID, every project name, assembly
name, root namespace and directory under `src/` and `tests/`. `init/SKILL.md`: *"once written,
this string is out of reach."*

**Neither stated reason for the rule covers a dot.** The executor justifies it by manifest safety
— the identifier is text-replaced into JSON before parsing and *"cannot make the JSON invalid"* —
which holds for a quote or a backslash, not a dot. The second reason is that the value must be
legal where it lands. **Measured across all nine generator invocations the pack issues:** every
one accepts a dotted `-n` unsanitised, and `aspire-mstest -n Probe.Core.aspire-mstest` emits
`namespace Probe.Core.aspire_mstest;` — the *hyphen* becomes an underscore, the dots survive.
The template engine already treats a dot as a legal separator and a hyphen as not, which is the
same line this change draws.

## Affected users and systems

- **Any repository whose name carries a dot** — for .NET, the ordinary way a library is named.
- **`Invoke-ScaffoldManifest.ps1`** — the validity rule, the derivation function, three refusal
  messages, and the substitution comment that justifies the rule by the property being widened.
  Four recitations of the old rule, not two.
- **`init/SKILL.md`** — where the derivation rule actually lives; its synopsis renders
  `-Solution` optional one line above an instruction to always supply it; and its `-Manifest`
  sentence already cites the wrong section, which is worth fixing while the paragraph is open.
- **`stack-dotnet/SKILL.md`** — a second copy of that synopsis.
- **`README.md`** — documents the derivation in prose.
- **The shipped `init-scaffolds-solution` spec** — R17, R18, R19, R51, R52, R53 and R60 describe
  behaviour that moves, plus two passages of design narrative.
- **`ScaffoldManifestTests`** — pins the changing case as `[DataRow("widget_shop.core",
  "WidgetShopCore")]`, and is where every criterion below that omits `-Solution` belongs.
- **`ScaffoldEmitsBuildableSolutionTests`** — the only test that builds real solutions, both
  kinds, from a compile-time constant, and it supplies `-Solution` on every invocation.
- **The three starter assets** beginning `namespace {{SOLUTION}}.<Role>;`, which make identifier
  validity a question about whether the emitted repository compiles — and
  `web.IntegrationStarterTests.cs`, which additionally spells an Aspire type as
  `Projects.{{SOLUTION}}_AppHost`. Aspire builds that name by replacing dots with underscores, so
  a dotted identifier makes it permanently wrong. It sits in a doc comment, so no build catches it.
- **`Directory.Build.props`** — **unchanged in kind, named so it is not read as a new defect.** A
  sibling project becomes `ProbeCore.Probe.Tools` today and `Probe.Core.Probe.Tools` after; the
  prefix guard misses a sibling either way.

## Proposed outcome

**A solution identifier may carry interior dots.** Stated exactly, because compressing it has
been wrong twice.

**Deriving a candidate from a directory name:** split the name on `.`; turn each piece into a
segment the way the tool already does — split on `-`, `_` and space, drop everything outside
`A-Za-z0-9`, capitalise each part, join; **drop a segment that comes out empty**; then **refuse
the whole name if no segment survives or if any surviving segment does not start with a letter**;
otherwise join the segments with `.`.

**Validating a supplied identifier:** `\A[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*\z`. So
`Probe.Core` and `Probe` are accepted; `.Probe`, `Probe.`, `Probe..Core`, `Probe.2Go`,
`Probe-Core`, `2Fast` and a value with a trailing newline are refused.

**The two are deliberately asymmetric about empty pieces** — derivation drops them, validation
refuses them — because derivation filters a folder name chosen for other reasons, while
validation judges a value typed as the answer.

**`\A`/`\z`, not `^`/`$`.** `"Probe\n"` passes today because `$` matches before a final newline,
which makes any claim of an exhaustive accepted set false by one character.

**The rule exists once.** `init` obtains the candidate from the executor instead of computing it
from a paragraph, making R17 true for the first time. Two implementations have already drifted
once, and the drift hid this defect through nine rounds of review.

**What that produces**, and nothing else changes:

| Directory | Today | After |
|---|---|---|
| `probe.core` | `ProbeCore` | `Probe.Core` |
| `widget_shop.core` | `WidgetShopCore` | `WidgetShop.Core` |
| `widget..core` / `.config` | `WidgetCore` / `Config` | `Widget.Core` / `Config` |
| `widget-shop` / `my app` | `WidgetShop` / `MyApp` | unchanged |
| `2fast.core` / `--` | refused | refused |
| `probe.code.2go`, `widget.2go`, `v1.2.3`, `core.2fast` | a derived name | **refused — the owner supplies one** |

**Why a dot and not a hyphen, without naming a language:** a dot separates identifiers and each
side of it is one; a hyphen is not permitted inside an identifier by the grammar the engine
already enforces. The measurement above shows the template engine drawing the same line.

## Success criteria

Each names the path it exercises. Two earlier drafts failed on exactly this.

- **S1.** `Probe.Core` supplied is accepted, and a solution scaffolded with it **builds, both
  kinds, tests passing**. The end-to-end fixture's constant becomes dotted; no second build is
  added. Supplied is the path `init` takes.
- **S2.** Derivation produces the table above, or refuses. Driven **without `-Solution`**, so it
  belongs in the executor's test file — the end-to-end fixture never omits it.
- **S3.** A re-run recovers the dotted identifier from the solution file and changes nothing.
  **Also without `-Solution`**: supplied beats recovery, so a re-run that supplies never reaches
  the recovery branch.
- **S4.** A recovered identifier the widened rule still refuses is refused, naming it. Recovery
  is the third path through one validator and the one no earlier draft pinned.
- **S5.** No shipped manifest puts the solution token in **`pattern`, `assert_after` or
  `expect_match`** — the three fields written into a regex unescaped. `glob` is *not* among them:
  `ConvertTo-GlobRegex` escapes every literal character, and the shipped manifest uses the token
  in eleven globs safely.
- **S6.** The end-to-end fixture's dotted constant is escaped where it is interpolated into a
  pattern, **and the mechanism is named rather than left to the builder**: the assertion is a
  source-generated regex, which takes a compile-time constant, so `Regex.Escape` cannot be called
  in the attribute. Either a second pre-escaped constant or an ordinary `Regex`. Leaving it
  unescaped still *passes* — the assertion quietly loosens — which is why this is a criterion.
- **S7.** The Aspire asset stops spelling a type name the substitution would corrupt.

**Verified by a reviewer, not by a test, and deliberately not entering the silent-failure
register:** that `init` obtains the candidate from the executor rather than computing it; that
both synopses stop rendering `-Solution` as optional; and that every refusal message and comment
states the rule the code implements. These are prose-against-prose comparisons with no instrument
in this suite, and claiming a register row for them would claim a check that does not exist.

## Constraints

- **The charter amendment is a prerequisite, not a footnote.** This chain adds named checks, and
  the register grows when a new class of failure is found. Versions 4, 5, 6 and 7 all edited the
  charter mid-build for exactly this. **The amendment conversation happens before the spec**, as
  its own approval — the intent command says a charter conflict *"is a charter amendment
  conversation, not an intent."*
- **Asking the executor is larger than a new argument.** The candidate is needed at interview
  step 2; the executor creates the target directory and refuses without an approved charter,
  which is written at step 3 and approved at step 6. The answer must come from a path that runs
  before that gate, creates nothing, and returns something that is not a scaffold report — and
  the executor's header says the JSON report is *the only thing* on stdout. **Whether that is a
  second stdout shape, a new `outcome` in the existing envelope, or a bump to `report_version`
  is a permanent public contract for every pack and both fixtures that parse it.**
- **Invariant 1 binds the executor line by line** — *"a script has no illustrative prose, so
  every line of one counts"* — so the rule may not be justified in code by naming a language.
- **Permanent non-goal N2 binds this chain**: no organisation names in fixtures or measurements.
- **The identifier is unreachable after the first run, and the rule gates recovery.** Once one
  repository holds a dotted solution file, narrowing the grammar later makes that repository's
  own later runs refuse, on a name nothing can rename.
- **After this, nothing in CI builds an undotted solution.** One fixture builds real solutions
  and its constant becomes dotted. That a dotted build implies an undotted one is an assumption,
  and the register row covering it keeps naming a class that still exists.

## Non-goals

- **Not making `solution_file_glob` mandatory.** It is optional and unvalidated, so a pack
  omitting it re-derives on every run — and this change makes that bite precisely where it
  matters, since a repository under `probe.core` derives a different name before and after. One
  pack exists and it declares a glob. **Named and not fixed**, rather than left to be discovered.
- **Not the other four closures** in `engine-substitutes-declared-tokens`, which stay parked.
- **Not a pack declaring its own identifier rule.** The engine keeps one rule.
- **Not the accepted-but-unbuildable holes that already exist** — `class` and `Con` both pass
  today. This widens the reach into them without creating them: `aux.core` derives `AuxCore` now
  and `Aux.Core` after, and both create fine on this platform, measured.
- **Not limiting how many dots a name may carry**, and not path-length limits.
- **Not changing what the interview asks**, only where the candidate comes from.
- **Not changing the coverage patterns, the prefix convention, the glob rule, or case
  sensitivity.**
- **Not renaming anything in an existing repository.**
- **Not the `library` kind.** This unblocks it; it does not build it.

## Open questions

- **Does permanent non-goal N2 reach `.sdlc/` artifacts, or only what the plugins ship?**
  *Owner: peter.* Nothing here is blocked either way — this draft names no organisation — but the
  owner's org appears in four committed chain artifacts, so either the charter should say N2 binds
  `plugins/**`, or those four need editing.
