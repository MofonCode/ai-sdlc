---
name: grill
description: Convene an agent team to argue a design question to a conclusion, outside the cycle. Use when a decision is genuinely ambiguous, a root cause is contested, or the charter itself needs challenging — not for checking an artifact.
argument-hint: [the question to grill]
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(powershell.exe *)
---

# Grill a question

Convene an **agent team** to argue **$ARGUMENTS** to a conclusion.

## This is not interrogation, and the difference is the whole point

| | `interrogate` | `grill` |
|---|---|---|
| Shape | four orthogonal roles | a team of teammates |
| Do they talk? | **never** — independence is the method | **constantly** — the argument is the method |
| Reads | one artifact, against the charter | a question, against whatever it takes |
| Produces | schema-valid findings | a conclusion, and the reasoning that survived |
| Inside a cycle? | yes, at every gate | **no. deliberately outside** |

Interrogation works because the roles cannot contaminate each other: four agents reaching one
document independently is what stops four wordings of one problem. Grilling works for the
opposite reason: **teammates challenging each other directly** is the one thing orthogonal
roles cannot do, and it is what a contested question needs.

So do not use this to check an artifact. If the question is "does this spec contradict the
charter", that is `/sdlc:interrogate` and it is cheaper and sharper. Use this when the
question is "which of these two readings is right", "what is actually causing this", or "is
this invariant wrong".

## It produces no governed artifact

Nothing here lands in `.sdlc/`. No findings document, no cycle, no state transition.

That is deliberate. A grilling is thinking out loud at scale, and its output is a conclusion
a person acts on — usually by authoring an intent. If it wrote a governed artifact it would
be a stage, it would need a gate, and it would stop being the place you go to work out what
you think.

If the conclusion is that something should change, the next step is `/sdlc:intent`, and the
grilling is the reasoning you bring to it.

## How to run one

**Name the question precisely, then spawn teammates with genuinely opposed jobs.** Three to
five. The failure mode is teammates who agree: a team that converges in one round did not
need to be a team.

Give each a position to defend or attack, not an area to cover:

> Spawn four teammates to settle whether INV-02 is too broad. One argues it is: it has the
> highest override count and every override was an agent resolving an ambiguity a person
> would have accepted. One argues it is not, and that the overrides are the rule working.
> One takes the position that the rule is fine and the WORDING is unclear, which is a
> different repair. One plays devil's advocate against whichever position is winning. Have
> them challenge each other directly and report what survived.

Two things to insist on:

- **Have them talk to each other, not only report to you.** A team that reports in parallel
  is four subagents wearing a costume, and you are paying team prices for subagent value.
- **Ask what survived, not who won.** The useful output is the argument that no teammate
  could break, plus the ones that fell and why.

## Cost, stated plainly

A team is the most expensive shape available: each teammate is a full session with its own
context window, and token use scales linearly with the count. It is worth that for a
contested decision and is poor value for anything an interrogation could answer.

Teams also need an interactive session. They **cannot spawn under `claude -p`**, so this is
supervised work by construction — which is fitting, since the output is a judgment rather
than an artifact.

## When you are done

Report what survived, what fell, and what is still open. Then stop:

> If this changes what we build, the next step is `/sdlc:intent`. If it changes a rule, that
> is a `charter-review` intent, and `/sdlc:repair` will tell you whether the question has
> already been asked and answered.

**Do not act on the conclusion.** A grilling has no authority: it did not go through a gate,
and the whole reason it is cheap to run is that it cannot change anything by itself.
