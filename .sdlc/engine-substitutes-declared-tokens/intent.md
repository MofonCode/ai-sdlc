---
slug: engine-substitutes-declared-tokens
title: A pack declares its tokens, the questions that fill them, and how a re-run finds them again
status: draft
tier: feature
created: 2026-09-20
owner: peter
---

> **PARKED, 2026-09-20, not abandoned.** The owner chose a solution-level scaffold — a modular
> monolith with an `Application` project — over per-module projects, on the grounds that module
> boundaries gathered in an opening interview are a guess frozen into a directory structure, and
> that MarketSense's eleven modules were discovered rather than designed. Splitting a grown
> solution into modules becomes a separate, explicitly-invoked skill later.
>
> **Nothing below is wrong; it is unneeded.** A second placeholder exists only because `Billing`
> has to reach `src/Billing.Api`. Without per-module projects there is no second placeholder, so
> the five closures stop blocking anything.
>
> **They remain real defects against I5** — the engine still decides which tokens can exist, in
> the executor, in `init`'s invocation, in the report schema, in recovery, and in the derivation
> transform. Seven interrogations' worth of evidence is below and every line reference in it was
> read out of the code. It will be here when a second token is genuinely wanted.


# intent — a pack can add a token without touching the engine

## Problem

**The set of tokens is closed by the engine, in five places, and a pack cannot add one.**

The defect is closure, not vocabulary. An engine containing the string `solution` is not the
problem; an engine deciding which tokens can exist is. I5 states the test: **adding a pack must
require no change to the engine.**

Five closures, each found by a separate reading and each previously mistaken for the whole.
The first four are about *names and mechanisms*; the fifth is about a *value*, and it was found
last because the enumeration below was scoped to substitution and presented as complete:

1. **The executor decides the set.** It compares a declared token name against a literal and
   refuses anything else — *"`solution` is the only logical token there is."*
2. **`init` decides what can be asked.** `skills/init/SKILL.md:227` hardcodes the invocation and
   names `-Solution` at :231. It is the only engine caller, so an accepting executor still leaves
   a second value with nowhere to come from.
3. **The report names one pack's token.** `Invoke-ScaffoldManifest.ps1:120` emits a top-level
   `solution` field; four halt records use it as a record kind; five test rows assert it. The
   shipped spec calls the report *"a second new contract"* whose schema *"every future pack
   implements"* — so the engine's own output format names a token belonging to one pack.
4. **Recovery only knows one shape.** `solution_file_glob` exists because a re-run has no
   `-Solution` and must find the same identifier or emit a duplicate set of paths. The rule is
   `GetFileNameWithoutExtension` over a matching file (line 767) — it recovers `MySol` from
   `MySol.slnx` and cannot recover `Billing` from `Billing.Api.csproj`, because a module name is
   a prefix, not a basename.

5. **The engine owns the derivation transform.** `ConvertTo-Identifier` at line 519 splits a
   directory name on `-`, `_`, `.` and space, discards everything outside `A-Za-z0-9`,
   capitalises each piece and joins — pinned as shipped requirement R17, word for word. A pack
   whose token is a dotted namespace root or a hyphenated package name gets it silently mangled
   and cannot declare otherwise. The engine stops naming token *names* and keeps a rule about one
   token's *shape*.

**And the deepest half: values come from a human, so the questions must come from the pack.**
Nothing lets a manifest say *"ask the owner for a module name."* Without it, `init` either names
a pack's tokens itself — closure again — or cannot know what to ask.

## The mechanism, enumerated from the code

Earlier drafts asserted mechanisms that were not the ones in the file. Every reference here was
read out.

| | Site 1 — the manifest document | Site 2 — asset contents |
|---|---|---|
| Where | line 799-803 | `Expand-Token`, line 557 |
| Driven by | the top-level `solution_token` field, alone | the per-action `tokens` array |
| Called | once, before any action runs | from `copy` and `append` only |
| Reaches | **every field of every action and preflight** — `path`, `args`, `cwd`, `unless_exists`, `glob`, `pattern`, `replacement`, `assert_after`, `asset`, `command`, `expect_match`, `remediation` | the bytes inside one copied file |
| In the real manifest | ~70 of 79 `{{SOLUTION}}` occurrences | 8 `copy` actions of 58 |

A third site **detects**: the sweep (line 1162) checks every swept file for `solution_token` and
is the charter's registered instrument for a surviving token. It would be silent about a second.

**Values have three sources, all hardcoded rules about one token:** supplied via `-Solution`;
recovered by basename from a file matching `solution_file_glob`; derived from the target
directory name.

**And one rule constrains every value:** `Test-Identifier` at line 513 is
`^[A-Za-z][A-Za-z0-9]*$`. It exists because the document substitution is a text replace inside
JSON, justified by *"an identifier is A-Za-z0-9 by the rule above, so it cannot make the JSON
invalid."* A value carrying a quote corrupts the manifest mid-parse.

## Proposed outcome

**A manifest declares its tokens: what each is spelled as, how it is filled, how a re-run finds
it again, and what shape its value may take. The engine resolves; it defines nothing.**

The engine keeps a closed set of **value-source kinds** — supplied, derived, recovered — and
holds no token names, no questions and no per-token rules. This is the division the executor
already uses for actions: six kinds, closed, with every directory, command and argument inside
them coming from the pack. Applied one level up.

Four things follow from the four closures, and each was a separate discovery:

- **`init` asks what the manifest says to ask**, and names no token of its own.
- **The report carries a map of every resolved token.** `solution` stays as a compatibility
  field, because three test rows and a shipped spec depend on it.
- **Recovery generalises from basename to capture.** A pack declares a glob and the pattern that
  extracts the value from what it matches. The current basename rule becomes one declared case —
  `*.slnx` capturing everything before the extension — and a module becomes expressible:
  `src/*.Api` capturing everything before the role suffix. **Without this a second token cannot
  survive a re-run**, which would break the charter's registered guarantee that a re-run changes
  nothing.
- **Validation becomes a floor plus a pack rule.** The engine enforces only what keeps the
  document parseable — no quote, no backslash, no control character. A pack may narrow further,
  and `solution` declares today's identifier rule as its own. Otherwise a pack still could not
  hold a namespace root or a hyphenated name without an engine change: the same closure, moved
  from names to values.

The existing `solution_token`, `solution_file_glob` and `-Solution` keep working, because the one
manifest in existence uses them and a released executor breaking it breaks every scaffold.

## Success criteria

- **S1.** A fixture pack declaring a second token, with a declared question and a declared
  recovery rule, scaffolds correctly when driven against **the executor as shipped** — the value
  reaching an emitted path, a generator argument and a copied asset. Falsifiable, and the
  instrument exists: the suite already drives the real executor as an external process. *An
  earlier draft asserted `git diff` over the engine was empty, which is true of any clean
  checkout regardless of what the engine contains, and for which no test in this suite has an
  instrument.*
- **S2.** A second run over that fixture's output recovers the second token from disk and changes
  no file. This is the criterion the whole recovery generalisation exists for, and the one no
  previous draft had.
- **S3a.** The engine's prose names no pack token. Falsifiable, and the instrument already
  exists: `EngineInstructions_NameNoPackFile` asserts no engine skill or agent names a file a
  pack scaffolds, and the same scan covers a token spelling.
- **S3b.** `init` asks the questions the manifest declares and no others. **This has no
  instrument and will ship unverified**, stated rather than implied: `init` is markdown read by
  a model, nothing in the suite can observe a prompt, and the charter records behavioural
  measurement as BLOCKED externally - `plugin eval` refuses as early access, and the charter
  marks it not actionable by the owner. An earlier draft claimed a fixture declaring two
  questions could be asserted to produce two prompts; it cannot. S3b is the half of closure #2
  that rests on prose until that blocker clears, and it is the weakest point in the change.
- **S4.** The engine's value-source kinds are a closed, enumerated set, and an unknown one is
  refused naming the declared kinds.
- **S5.** The shipped pack's manifest and the existing `-Solution` invocation behave exactly as
  today, asserted against the real manifest.
- **S6.** Two tokens whose spellings collide or nest are handled by a stated rule, not iteration
  order. Falsifiable with `{{NAME}}` and `{{NAME_ROOT}}`.
- **S7.** The sweep fails the run for any declared token that survives, naming file and token.
- **S8.** A declared token with no value is refused before anything is written; a supplied value
  no manifest declares is refused naming it.
- **S9.** A value that would corrupt the manifest is refused whatever the pack's own rule says.
  Falsifiable: a value containing a quote, against a pack whose pattern permits it.
- **S10.** The report carries every resolved token, and still carries `solution`.
- **S11.** A token spelling appearing in a `command` field is refused before anything runs,
  naming the action. Falsifiable, and it closes a class rather than an instance: a value
  substituted there decides which executable is launched.

## Constraints

- **Three contracts are extended, not one.** The executor's command line — frozen by a shipped
  plan, coded against by three slices; the manifest schema, persisted in every pack; and the
  report schema, which the shipped spec says *"every future pack implements"* and which *"the
  engine's parser is the only thing that may define."*
- **`init`'s prose is part of the change.** Its hardcoded invocation and its `-Solution` sentence
  are where the second closure lives. An intent leaving them out repairs nothing, which the
  superseded plan and three drafts of this intent each did in turn.
- **Text replacement inside JSON is safe only while values cannot break JSON.** S9 holds that and
  is a design constraint rather than a preference.
- **A token value must never reach a `command` field, and this widens a trust boundary the
  charter's argument does not cover.** `command` goes to `ProcessStartInfo.FileName` at line 476
  from both the preflight path and the `run` action; arguments are quoted by
  `ConvertTo-ArgumentString`, the command is not. I5 trusts a pack because it is installed
  deliberately and reviewed like any other code — but token values are **not pack-supplied**.
  They come from a human at a prompt and, under the generalised recovery above, **from filenames
  on disk**, which nobody reviews. The old identifier rule was doing three jobs and an earlier
  draft replaced it having identified one. S11 closes the class by refusing the spelling in that
  field rather than by constraining the value.
- **`stack-dotnet` is the only manifest in existence**, so backward compatibility has one witness
  and a regression is caught by the end-to-end scaffold rows or not at all.
- **Windows PowerShell 5.1** binds the executor.
- **The superseded task-tier plan stays**, recording a scoping error caught mid-build by a signal
  it wrote for itself.

## Non-goals

- **Not the module scaffold.** This makes a second token declarable, fillable and recoverable;
  what a module is called and what it emits is `scaffold-emits-a-module`, parked and blocked here.
- **Not a templating language.** Literal strings replaced by literal strings — no expressions, no
  conditionals, no nesting. A manifest is read by reviewers the way a script is.
- **Not new value-source kinds beyond the three `solution` already needs.** Supplied, derived,
  recovered exist because one token needed them. Inventing a fourth for a hypothetical pack is
  how a closed set stops being small.
- **Not changing what `stack-dotnet` declares.** The pack keeps its single token.

## Open questions

- **How does a value reach the executor?** *Owner: peter.* A repeatable `-Token name=value`, a
  JSON argument, or something else. The frozen contract's extension and the largest decision here.
- **Collision rule: refuse or resolve?** *Owner: peter.* Refusing a manifest whose spellings nest
  is in character with the executor's closed sets; resolving by longest-first is cleverer and
  quieter, which is usually the wrong trade here.
- **How does a pack phrase a question?** *Owner: Design.* A prompt string is obvious and puts
  pack prose somewhere the engine reads aloud. Whether it needs a default, a validation hint or
  an ordering is a real design question.
- **Does `solution` fully become an ordinary declared token?** *Owner: peter.* Folding it in means
  one path rather than two and is the only version where the engine holds no rule about any
  particular token; keeping `-Solution` is kinder to three slices that code against it.
- **What happens to the derivation transform — closure five?** *Owner: peter.* Three answers and
  none is obviously right: the pack declares the split characters and casing, which is config
  rather than the templating language the non-goals forbid; derivation stays `solution`-only as a
  stated limit, which leaves a pack wanting a derived token blocked; or derivation leaves the
  executor entirely and becomes `init`'s job, which costs R17 its instrument. This is a design
  decision rather than a defect in the intent, and it is named here rather than answered.
- **Schema version 2, or a backward-compatible addition?** *Owner: Design.* Now a larger question
  than it was: tokens, questions, recovery rules and validation patterns are four additions.
