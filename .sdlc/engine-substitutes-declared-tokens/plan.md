---
slug: engine-substitutes-declared-tokens
status: superseded
spec: none
tier: task
slices:
  - id: declared-tokens
    files: ["plugins/sdlc-core/scripts/Invoke-ScaffoldManifest.ps1", "tests/AiSdlc.Tests/ScaffoldManifestTests.cs", "plugins/sdlc-core/skills/sdlc-router/references/artifact-schemas.md"]
    requirements: [T1, T2, T3, T4, T5]
    depends_on: []
---


> **SUPERSEDED, 2026-09-20, without being built. Re-tiered to feature; see the intent in this
> same directory.**
>
> The build began and stopped on the signal this plan named for itself: *"If the build turns up
> a design question, that is the signal it was mis-tiered."* It did.
>
> **The contradiction is between T1 and Not in scope.** T1 requires a manifest declaring two
> tokens to substitute both. A second token needs a *value*, the only value source is
> `-Solution`, and Not-in-scope forbids adding another. So a declared second token has a
> spelling and nothing to substitute — unusable either way, whether the engine refuses it or
> lets it survive to fail the sweep.
>
> **The narrower version that would have passed is cosmetic.** The engine would stop *saying* it
> knows the only token name while still deciding which names can hold values, so I5's own test —
> *adding a pack must require no change to the engine* — still fails for a pack wanting a module
> token. That relocates the violation rather than fixing it, and calling it a bug fix against I5
> would be an overclaim.
>
> Making it real means generalising `-Solution` into a token-value mechanism, which is the frozen
> command-line contract three slices code against. That is feature work with a spec.
>
> The implementation reached a parsing, passing state before it was reverted — 59 executor tests
> green, back-compat holding — and was discarded deliberately rather than kept, because a spec
> written against code that already exists inverts the chain.

# plan — the engine substitutes what the manifest declares

**Tier: task, stated out loud.** Three files, behaviour agreed in conversation, no design
question open. It is a behaviour change to a shipped engine contract, which is the argument for
feature tier — but the change is small, the direction is settled, and an intent plus a spec for
"stop hardcoding one string" is the ceremony I11 exists to prevent. If the build turns up a
design question, that is the signal it was mis-tiered.

**No spec, so no R-numbers.** Requirements are `T1`–`T5`, from the conversation of 2026-09-20.
They supersede behaviour specified in the shipped `init-scaffolds-solution` spec; see
**Supersession** below.

## Why

**The engine decides what logical tokens exist, which is a pack's decision.**

`Invoke-ScaffoldManifest.ps1:559-570`:

```
  Substitute the tokens an action names. `solution` is the only logical token there is;
  what it is spelled as is the pack's business, and arrives as `solution_token`.
...
  Stop-Run -Message "$Label declares an unknown token '$name';
      the only token this engine substitutes is 'solution'."
```

The comment is explicit and it is the invariant violation: **the engine names a concept a pack
owns.** I5 says the engine never names a pack's files, directories, commands, tokens or assets,
and that adding a pack must require no change to the engine. A pack that needs a second token —
a module name, a namespace root, a region — cannot have one without editing the engine. That is
the invariant's own stated test, failed.

The manifest already half-anticipates this. `tokens` is an **array** on every action, and the
schema's top-level field is the singular `solution_token`. The array shape was right; the engine
narrowed it to one permitted value.

**This is a bug fix against I5 before it is a feature.** It is being done now because the module
scaffold needs a second token and discovered it, but it would be worth doing without that.

## Requirements

- **T1.** The manifest declares its tokens as a map of logical name to spelling. The engine
  substitutes every token an action names and knows none of their names itself.
- **T2.** `solution_token` continues to work unchanged. A manifest declaring it and no map
  behaves exactly as it does today, because the shipped pack declares it and the executor is
  released.
- **T3.** IF an action names a token the manifest does not declare, THEN the executor refuses,
  naming the token and listing the declared ones — the shape it already uses for an unknown
  `kind`.
- **T4.** The post-run sweep looks for **every** declared token, not only the solution one. A
  second token that survived substitution is the same defect as the first surviving, and the
  charter's register already carries a row for it.
- **T5.** The identifier the caller supplies reaches the token map. Today `-Solution` feeds one
  substitution; whatever mechanism carries a second value is the spec's to design, and this plan
  does **not** add a second command-line parameter — see **Not in scope**.

## Supersession

The shipped `init-scaffolds-solution` spec governs the executor. Three of its requirements
describe single-token behaviour:

- **R2** — the engine names no token belonging to a pack. **Not superseded; this change is what
  makes it true.**
- **R72** — the post-run sweep, which says *"a token the manifest declares"* and is already
  written in the singular-of-a-set rather than the singular. T4 implements what it says.
- The manifest schema in that spec's **Data and contracts** section, which declares
  `solution_token`. T1 extends it.

**The shipped spec is not edited.** It records what shipped. This plan's requirements supersede
its behaviour, and a forward pointer is added to its Data-and-contracts section naming this
slug — the same treatment the frozen layout will need, and the same reason: a fresh session
reading an approved document should not learn something that is no longer true with nothing to
contradict it.

The manifest's `version` field stays at 1. The change is backward-compatible by T2, so a bump
would force every pack to move for no behavioural reason.

## Not in scope

- **A second command-line parameter.** `-Solution` stays as it is. How a module name or any
  other value reaches the executor is the module scaffold's design problem, and deciding it here
  would be designing for a change that has not been approved.
- **The pack declaring a second token.** Nothing in `stack-dotnet` changes. This makes a second
  token possible; it does not create one.
- **Identifier derivation and validation.** The rules for deriving, confirming and refusing the
  solution identifier are unchanged and still apply to it alone.

## Work order

One slice, one writer. The executor, its tests and the schema are each other's contract.

**`Invoke-ScaffoldManifest.ps1`** — read a token map from the manifest, falling back to
`{ solution = <solution_token> }` when only the singular field is present. `Expand-Token` looks
the name up in the map rather than comparing it to a literal, and refuses an undeclared name
listing what is declared. The sweep iterates every declared spelling. The comment that says
`solution` is the only logical token there is goes, and is replaced by one saying why the engine
must not know.

**`ScaffoldManifestTests.cs`** — rows for each requirement, listed under **Tests**.

**`artifact-schemas.md`** — the scaffold manifest is not documented there today; only the five
chain artifacts are. If that holds on reading, this file drops out of the slice and the schema
change is recorded in the executor's own comment block and in the pack-facing documentation
instead. **The implementer decides on inspection and says which**, rather than editing a file to
match a plan that guessed.

## Tests

| Proves | Assertion |
|---|---|
| T1 | A manifest declaring two tokens substitutes both, in a path and in asset content. |
| T2 | A manifest declaring only `solution_token`, with no map, behaves identically — asserted against the real pack manifest, which is that manifest. |
| T3 | An action naming an undeclared token refuses, naming it and listing the declared ones. |
| T4 | A second declared token surviving substitution fails the run, naming the file and that token. This is the row that would otherwise be silently absent. |
| T5 | The supplied identifier still reaches the solution token and nothing else changed about it. |

**Mutation-test each** per I9: make the map lookup fall back to the solution spelling for any
name and watch T3 fail; sweep only the solution token and watch T4 fail; drop the singular-field
fallback and watch T2 fail.

## Risks

**T2 is the one that matters and the one that looks trivial.** The executor is released, the
pack's manifest is the only manifest in existence, and it declares the singular field. A
regression here breaks every scaffold. *Early-warning sign:* the end-to-end scaffold rows
failing for a reason that has nothing to do with tokens — they exercise the real manifest and
would be the first to notice.

**The sweep gets slower in proportion to declared tokens.** It already reads every file under
the target; it would now match each against N spellings rather than one. With one or two tokens
this is noise. *Early-warning sign:* the end-to-end rows taking materially longer than the five
to six minutes they take today.

## Rollback

`git revert` of one commit. The engine returns to substituting one token by name, and the module
scaffold stays blocked — which is where it is now.
