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

**Then what each perspective asks you to cover** (D-90):

```
sdlc perspectives <id>
sdlc perspective show <name> --for spec --cover
```

For each perspective written for this cycle, its short list of what this spec could cover. **Write an
item in where the intent asks for it**, or where a rule in force requires it — `CON_17`'s criterion for
what somebody is told on a failure is one — and let it shape how you write that criterion: a keyboard
user's path through an interaction the intent asked for, the hostile input to a field it asked for.

**An item the intent does not ask for gets no criterion.** Name it in `Out of scope` — *not asked for by
the intent* — with the perspective's name, so the approval shows the gap and the person can take it in
with `/sdlc:steer`. Writing the criterion anyway is the scope creep `CON_05` blocks: a list that arrives
with every cycle is not the person asking.

## Refuse early

```
sdlc status <cycle id> --stage intent
```

- **No intent?** Say so and stop. There is nothing to derive from.
- **Intent not clean?** Stop. `CON_16` is coverage of what the intent *states*, so deriving criteria
  from an intent that is still being argued about means deriving them twice.
- **A spec already exists?** Stop, and name what the person probably meant: `/sdlc:interrogate <id>
  spec` to challenge it, or `/sdlc:steer <id> "..."` to change it. Do not overwrite a spec.

## Keep a task list, if your host has one

If your host gives you a task list, open one once nothing above refuses, with these items, and mark
each done as it finishes. If it does not, carry on without one — nothing below depends on it.

1. read the intent
2. derive the criteria: every outcome covered whole, and each one able to fail
3. dispose of every open item the intent carries
4. write the spec
5. interrogate and refine until a round is clean
6. hand back

**The loop is one item, however many rounds it takes.**

**This stage only.** Nothing after the hand-back goes on the list: whether the next stage runs is a
person's decision, and `sdlc status <id>` names it. **The list is a view, never the record.** What is
done is what is on disk and what the binary reports, so where the two disagree the list is the one
that is wrong.

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

## Cover the whole of each outcome, and quote it

`CON_16` is not satisfied by every outcome having *a* criterion. It asks whether the criterion covers
what the outcome actually says — and the way an outcome loses half of itself is a paraphrase nobody
compares to the original.

**So carry the outcome's own words into the criterion, and keep them.** A reader can then see the
difference rather than having to remember the intent:

| The intent said | A criterion that narrows it | A criterion that covers it |
|---|---|---|
| see a **die** on the screen showing a number between 1 and 6 | displays a **face value** that is an integer from 1 to 6 | displays a **die face** showing that many pips, the number being an integer from 1 to 6 |

The left-hand column is real. It passed every gate, and the solution shipped a numeral in a bare
paragraph tag — because the narrowed criterion still traced to the outcome, still read as derived, and
still satisfied a coverage check. Nothing downstream ever compares a spec to an intent again: build
implements the spec and verify checks the spec.

**Where part of an outcome genuinely cannot be checked, put that part in `Out of scope` and say so.**
That is the exit, and using it is what makes the narrowing a decision somebody wrote down rather than
a word that went missing. Judging the look of something is the usual case — no test makes that
judgement, and `CON_12` keeps it out of a criterion.

**A "never" is the other usual case, and it is bounded in the first draft, not the fourth.** "No
animation", "nothing stored", "no sign-in" each forbid an open-ended set, and no test observes every
member of one. Write the criterion over the set a test can observe — no CSS transition or animation on
the die, nothing in cookies, `localStorage` or `sessionStorage` — and put the remainder in `Out of
scope` by name, in the same draft. Left unbounded, the negative comes back from each round with the
next member nobody checked: one real cycle spent two spec rounds and two plan rounds finding, in turn,
script-driven animation, a canvas, and an observer too narrow to see either, all from the words "no
animation", and converged only once the canvas went to `Out of scope`.

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

Four traps worth naming:

- **A criterion about what happens *in* the browser cannot be proven by parsing a response.** The
  response is the input to the behaviour, not the behaviour.
- **A criterion about data cannot be proven against an in-memory provider.** It does not translate
  SQL, so it passes queries a real database rejects.
- **A failure has two audiences, and each gets a criterion.** `CON_17` is the person who met it, `CON_20`
  is whoever investigates afterwards. Write the second one's check as causing the failure and reading
  what it left: a log line no test reads is one a refactor deletes for free.
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

**Detail is yours; a new obligation is not.** How an outcome is observed — a threshold, a viewport
width, the wording of a failure message, a stand-in for a service — is what a spec adds, and nothing in
the intent needs to name it. What *done* means is the intent's: a criterion that would make the work
owe something nobody asked for is creep however sensible it is.

**If the intent is missing something it should have asked for, say so, and do not write the
criterion.** Name it in `Out of scope`. If a reader raised it as a suggestion, the person takes it in
with `/sdlc:steer <id> <finding id> …`, which writes it into the intent with their name on it and
carries it down; if nobody raised it, it is a new cycle.

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

Interrogate, refine, interrogate again until a round is clean, a reader reports a conflict, or the
panel's `Loop` line says the loop is stuck. The three exits for a finding are the same as the intent's —
healed, assumed, asked — except that a spec has no decisions region, so an assumption here is almost
always a sign the intent is the thing to fix.

**"Nothing further can be settled" is no longer your call to make.** It used to be, and it was the one
judgement in this loop that the session running it had no standing to make — the whole interrogate and
refine split exists so that the thing being judged is not the judge. The `Loop` line on the panel is the
answer now: silence means keep going, a rule that *has come back* means look for an exit instead of
editing the same words again, and *stuck on* means stop and hand back to a person. Nothing caps the
rounds, so a slow convergence is never a reason to stop.

See the intent skill's loop section for the full table; it is the same loop.

**First, the suggestions the spec's rounds left open, if there are any.** Ask once: show them grouped
by perspective, each with its statement, and ask which to take in, which to decline, and which to leave.
Leaving them is an answer, and the commonest one — skip the question entirely when nothing is open. What
the person names goes to `/sdlc:steer <id> <finding id>...` in one run, which writes each into the intent
with their name on it and re-interrogates down to here (D-97). **Never take one in on their behalf**: a
suggestion nobody chose is exactly what the spec may not derive from.

```
sdlc status <id> --stage spec --brief
```

Show it verbatim, then stop. The person decides whether to move to the plan.

**Say nothing else beyond two sentences.** The panel carries the cycle, the counts, the gate and the next
command; prose around it competes with the only lines worth reading. Drop `--brief` when somebody asks
for the whole panel.
