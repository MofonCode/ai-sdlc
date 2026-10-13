---
name: constraints
description: Show the rules in force for this solution, all tiers resolved. Use before writing or judging any artifact, and whenever someone asks what the rules are.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[intent|spec|plan|diff|verify]"
allowed-tools: Bash(sdlc *)
---

# Show the constraints in force

```
sdlc constraints [--for <artifact>] [--brief]
```

Show what it prints **verbatim**, and **add nothing of your own**. Do not summarise the set, do not
reorder it, and do not quote it from memory — the set in force is the global tier plus this solution's
profile plus its own `LOC_` constraints, and the one you remember is none of those.

## This is how the rules reach you

Running this command is the first step of every skill that writes or judges an artifact. It is not a
convenience: the constraints live in the framework and are never copied into a solution, so a session
that did not run this is working from recollection. Per-host native delivery, if it ever exists, is an
optimisation layered on top and never the mechanism.

## What a constraint is

Four parts, all required:

| Part | Holds |
|---|---|
| the heading | the id, and the rule in one line |
| `Applies to` | which artifact, and at what moment |
| `Forbids` | what becomes illegal, concretely enough that an example could be written |
| `Why` | what goes wrong if the rule is absent |

The `Why` is what makes a constraint arguable rather than arbitrary, and what tells a person at a gate
whether a finding is cheap or expensive in this case. Read it before deciding a finding is pedantry.

## Three tiers

| Tier | Id | Where it lives |
|---|---|---|
| global | `CON_nn` | the framework |
| profile | `PRO_nn` | the profile `solution.md` records, carried by the binary |
| solution | `LOC_nn` | `.sdlc/constraints.md` |

Resolved in that order, so a solution's own rules are the last word.

**The profile tier moves with the `profile` key, and not every profile fills it.** `dotnet-default`
ships rules that have to name a framework — the global set may not, which is what the tier is for.
`ml-tensorflow` ships none, because nothing has been built from it yet and a rule written ahead of the
first real solution would be a guess with the authority of a constraint. So a profile migration changes
what binds, and the output of this command after one is the only place to read what.

## Ids and revisions

A bare id is revision 1, and that is the canonical form: `CON_01` means `CON_01.1`. A wording change
makes a revision — `CON_10.2` is the same rule better worded. A change to **what is forbidden** is a new
constraint with a new id; the old one stays on the page verbatim and gains `Superseded-by`, so a finding
that cited it remains readable against the rule it was actually judged under.

Ids are a monotonic sequence, assigned once and never reused. A gap is the record of a removed
constraint, not a vacancy.

## Adding one

`sdlc propose constraints` is where one comes from. It reads this solution's whole history and reports
which rules its findings keep citing, with the cycles behind each; where the record holds no pattern it
prints the next free `LOC_` id with its four parts empty. A person writes the rule and places it; every
agent write to `.sdlc/constraints.md` is denied, because a cycle judged by the constraints it is
changing is the same deadlock as the framework governing itself.

Three admission tests, all of which must pass, or the rule is decoration that costs attention at every
interrogation:

1. its `Forbids` clause names something that becomes illegal
2. **no analyzer, schema or hook could decide it**
3. it is incompatible with at least one plausible implementation

Test two rejects most candidates. A rule about code belongs in an analyzer — and where an existing
analyzer already decides it, use that analyzer rather than writing a rule. A mechanical rule about an
artifact belongs in a schema or a hook.
