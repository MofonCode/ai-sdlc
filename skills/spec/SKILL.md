---
name: spec
description: Derive testable acceptance criteria from an approved-shape intent, producing the spec of a cycle. Use after an intent has a clean interrogation round.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Author a spec

A spec says **what done means**. Every criterion is something that can be observed and checked, and
nothing in it was invented here: each criterion traces to something the intent asked for.

Run `sdlc constraints --for spec` first.

## Refuse early

```
sdlc status <cycle id> --stage intent
```

- **No intent?** Say so and stop. There is nothing to derive from.
- **Intent not clean?** Stop. `CON_04` is coverage of what the intent *states*, so deriving criteria
  from an intent that is still being argued about means deriving them twice.
- **A spec already exists?** Stop, and name what the person probably meant: `/sdlc:interrogate <id>
  spec` to challenge it, or `/sdlc:steer <id> "..."` to change it. Do not overwrite a spec.

## Read the intent as the only input

Read `intent.md`. That is the source. Do not read the request that started the cycle, do not read the
conversation, and do not read the codebase for requirements — the codebase tells you what *is*, and a
spec says what *should be*.

The intent's `type` tells you how to derive:

| type | Where criteria come from |
|---|---|
| `bug fix` | the reproduction. Criterion one is "the reported behaviour no longer happens", stated as the observable |
| `feature addition` | each wanted outcome, turned into something checkable |
| `feature change` | the new behaviour, **plus** a criterion for each `Must not change` item |
| `new solution` | each wanted outcome, plus whatever the solution must do to be usable at all |

## Write criteria that can fail

`CON_03` forbids a criterion that cannot fail. The test: **can you describe, concretely, a run of the
system where this criterion is not met?** If you cannot, it is not a criterion.

| Not a criterion | A criterion |
|---|---|
| handles large files | a 500 MB upload completes without the process exceeding 1 GB of memory |
| performs well | the dashboard's first paint happens within 2 seconds over 50,000 rows |
| works correctly | requesting the page returns a face between 1 and 6 inclusive |
| is secure | a request with no bearer token is answered 401, with no body |

**And the check has to be one this solution's testing layers can run.** That is `CON_12`. The layers
are in `.sdlc/techstack.md`; typically unit, component, integration against real backing services, and
end-to-end in a browser engine. A criterion whose only check is a person looking at a device is not
admissible — it belongs in the intent as a bound, verified outside the cycle, which is honest about
not being governed.

Three traps worth naming:

- **A criterion about what happens *in* the browser cannot be proven by parsing a response.** The
  response is the input to the behaviour, not the behaviour.
- **A criterion about data cannot be proven against an in-memory provider.** It does not translate
  SQL, so it passes queries a real database rejects.
- **Number the criteria.** The plan's proof entries cite them by number, and `verify-chain` compares
  the two sets. A criterion deleted from the middle must not renumber the ones after it.

## Dispose of every open item the intent carries

This is mechanical and it is checked. Every entry under the intent's `## Open` must end up in exactly
one of three places:

| Destination | When |
|---|---|
| a criterion | the question got answered, so it is in scope and testable |
| `Out of scope`, with a reason | it was decided against |
| the spec's own `## Open`, citing the intent item | still nobody can settle it |

Leaving one undisposed is the gap that goes invisible downstream, because build implements the spec
and verify checks the spec — so an intent question nobody carried forward is never mentioned again.

## Stay inside the intent

`CON_05` forbids a criterion no statement in the intent supports. Scope added here was never approved
by whoever approved the intent, and it arrives carrying the authority of an approved artifact.

The two fail differently and are remedied differently, which is why they are separate rules: a gap
sends you back for more criteria, creep sends you to delete one.

**If the intent is missing something it should have asked for, say so and stop.** Do not fix it by
writing the criterion. That is what the steer stage is for, and it edits the intent with attribution
rather than smuggling the change in here.

## Write it

`.sdlc/cycles/<id>/spec.md`:

```markdown
---
framework_version: 1.0.0
cycle: <id>
---

# <what done means, in one line>

## Acceptance criteria

1. <the observable outcome>, checked by <how>.
2. ...

## Out of scope

- <what was decided against>, because <why>.

## Open

- <still unsettleable>, because <why>. From the intent's open item: <which>.
```

A spec carries no `Fixed decisions` section and needs none: its criteria derive from the intent, so a
decision appearing here would derive from nothing.

## Run the loop, then hand back

Interrogate, refine, interrogate again until a round is clean or nothing further can be settled. The
three exits are the same as the intent's — healed, assumed, asked — except that a spec has no
decisions region, so an assumption here is almost always a sign the intent is the thing to fix.

```
sdlc status <id> --stage spec
```

Show it verbatim, then stop. The person decides whether to move to the plan.
