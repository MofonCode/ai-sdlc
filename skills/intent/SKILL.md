---
name: intent
description: Interview a person into a governed intent, the first artifact of a cycle. Use when someone wants to start new work, reports a problem, or asks to turn an idea into something the process can carry.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "[a sentence describing the work, optional]"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Author an intent

An intent says **why** work exists. It is the only artifact written by interview, and your job is to
**elicit, not draft**. The person knows what they want; they do not yet know what is underspecified
about it. Finding that out is the work.

Run `sdlc constraints --for intent` first and keep what it prints in front of you. That is the set in
force for this solution, which is not the same as the set you remember.

## Keep a task list, if your host has one

If your host gives you a task list, open one before the first step, with these items, and mark each
done as it finishes. If it does not, carry on without one — nothing below depends on it.

1. open the cycle and its branch
2. turn the request into a problem
3. establish the type, and interview
4. pin down the one word
5. write the intent
6. interrogate and refine until a round is clean
7. report and hand back

**The loop is one item, however many rounds it takes.** A list that grows a line per round reads as
progress while an artifact goes round in circles.

**This stage only.** Nothing after the hand-back goes on the list: whether the next stage runs is a
person's decision, and `sdlc status <id>` names it. **The list is a view, never the record.** What is
done is what is on disk and what the binary reports, so where the two disagree the list is the one
that is wrong.

## 1. Open the cycle

```
sdlc new
```

It prints the id on standard output and the branch command beside it. Do not allocate the id yourself:
the command **claims** the id as it allocates it, and that claim is the collision check — two cycles
opened in the same second would otherwise write into one directory. Creating the directory is not the
check and cannot be, because it succeeds on a directory that is already there.

Then put the working copy on the cycle's plan branch:

```
sdlc branch <id> plan
```

Run the one command it prints. It creates `cycle/<id>/plan` from the integration branch, or from what is
checked out when there is none. The intent, spec, plan and approval are written there, and its pull request
— `pr <id> --plan`, once a person has approved — admits the plan to the integration branch.

## 2. Convert the request into a problem

Most requests arrive already shaped as a solution — "add a cache", "make the export async".
`CON_01` forbids an intent that is an implementation instruction with no statement of what is wrong.
So the first move is not a question, it is a conversion:

> What happens today that shouldn't?

If the answer names another solution, ask once more what its absence causes. "We have no cache" is a
missing thing. "The dashboard takes fourteen seconds and people stop opening it" is an intent.

**Do not discard their solution.** A decision already taken is legitimate — record it under **Fixed
decisions** with who decided it and when. That keeps the intent a problem statement while letting the
plan honour the decision, and six months later it distinguishes a requirement from an assumption.

## 3. Establish the type, then interview

Four questions, in roughly this order. Stop asking when an answer would only be a guess.

**Ask them in the tense the work is actually in.** The wording below is for something that exists and
misbehaves. For a new solution nothing is broken yet, and asking how they would know it was *fixed*
reads as though you have not understood the request.

| | Something exists and misbehaves | Nothing exists yet |
|---|---|---|
| 1 | What happens today that shouldn't? | What can nobody do today that they need to? |
| 2 | Who notices, and how often? | Who needs this, and how often would they use it? |
| 3 | What would you be able to see once this is right? | What would you be able to see that you cannot today? |
| 4 | What must not change? | What is out of bounds — what must this not touch or become? |

Question 3 is the highest-value answer you will get: it is the seed the spec grows from. Do not let
them answer in thresholds; that is the spec's job.

Then, conditionally: **is there a decision already made that I should treat as fixed?**

The type you settle on goes in the frontmatter as one of `new solution`, `feature addition`,
`feature change`, `bug fix`. It is not cosmetic — a bug fix's spec derives its criteria from a
reproduction rather than from a desired outcome, and the spec stage reads this to know which.

## Never ask

- **Anything the repository can answer** — where code lives, what the current behaviour is. An
  interview spends a person's attention; do not spend it on what you can read.
- **How to build it.** That is the plan.
- **Criteria in testable form.** That is the spec. The boundary: an intent says "the export finishes
  without the user waiting"; a spec says "returns a job id within 200ms".
- **Anything about the stack, a framework, a package or a project shape.** The solution's stack is
  declared in `.sdlc/techstack.md`, and a departure from it is argued in the plan, where every other
  technology choice is made. Asking here puts a technology answer into the one artifact whose whole
  job is to be free of one.

An intent is the product owner's document: a problem, the outcomes wanted, decisions already taken,
and what must not change. None of those needs a technology to express. If an answer you are about to
record could only be written by someone who knows the codebase, it belongs downstream.

**Fixed decisions are the one exception, and only when the person volunteered one.**

## 3a. One word to pin down before you write anything

**When somebody bounds the work with *simple*, *basic*, *minimal* or *nothing fancy*, ask once which
they mean — small in scope, or plain in appearance.** Then record which, in their words.

The word carries both meanings and nobody notices they have chosen one. A person who said *"keep it very
simple — nothing fancy"* meant one die, no multiplayer, no stored history. It was recorded faithfully as
a bound on the whole of the work, and the plan a stage later applied it to the surface: *"the whole
surface is one control and a list of numbers, so the page uses plain markup."* Both readings are honest.
Only one of them was meant, and the gates have no way to tell them apart afterwards — a bound this
general reaches every stage and is argued with by none of them.

One question: *"when you say nothing fancy — do you mean keep the scope small, or keep the look plain?"*
A person who means both says both, and that is a better answer than the one you would have guessed.

**What the solution should look like is not this document's business.** If the answer is about
appearance in general rather than about this piece of work, it belongs to the solution rather than to an
intent that would be rewritten next cycle — as a `LOC_` constraint where a plan has to record the
judgement, or as a theme's role names. Say which, say that the `init` skill is where those are proposed,
and carry on.

## 4. Write it

`.sdlc/cycles/<id>/intent.md`, exactly this shape:

```markdown
---
framework_version: 1.0.0
cycle: <id>
type: feature addition
---

# <the problem, in one line, as the person would say it>

## Problem

What happens today that shouldn't, who it happens to, and how often. Their words where you can.

## Wanted outcomes

- What they would be able to see once this is right.

## Fixed decisions

- <the decision>. <who>, <date>: <why they decided it>.

## Must not change

- <what must keep working>

## Open
```

**`## Open` is required even when empty.** Without it, nothing can tell "nothing is open" from "the
author forgot the section", and those need different responses.

What goes in it: a question nobody can answer yet, with the reason it cannot. **Not** work nobody has
started — that is a later cycle. **Not** something that is wrong — that is a finding. **Not**
something chosen out of scope — that is the spec's `Out of scope`. `CON_14` forbids the other three,
and it is the constraint most often tripped, because the open region is the one place where asserting
nothing satisfies every other rule.

**An unmeasured figure is an assumption, not an open item.** Record it under `Fixed decisions` marked
unconfirmed. `CON_02` forbids stating it as settled fact — and it also forbids the move that looks
like a fix: rewording "fourteen seconds" into "slow" removes the problem from the page while leaving
the belief in it.

## 5. Run the loop

You are not done when the file is written. Interrogate it, apply what comes back, and interrogate
again until a round raises nothing blocking or nothing further can be settled:

```
sdlc constraints --for intent
# interrogate in a fresh process -- see the interrogate skill
# apply the findings -- see the refine skill
```

Every finding has exactly three exits:

| Exit | When | Where it shows |
|---|---|---|
| **healed** | fixable from what is already on the page | the artifact changes; say what changed |
| **assumed** | something had to be supplied to proceed | `Fixed decisions`, marked unconfirmed |
| **asked** | it cannot proceed without a person | `## Open`, and you stop and ask |

**Never assume silently.** A report is read once; a section is read every time the artifact is read,
and it is what a reviewer's eye lands on at the gate.

**Stop on convergence, on conflict, or on a halt — never on a count.** A round producing no new
findings is convergence. Two constraints whose remedies exclude each other is a conflict: report it as
one rather than picking a side. A halt is the panel's `Loop` line saying one rule has come back three
times, which means the loop is refighting ground it already won.

**There is no round limit, and the `Loop` line is not one.** A loop that is converging scores nothing
however many rounds it takes, and eleven rounds has been worth it before now. What is counted is a rule
answered and then raised again — going backwards, not going slowly.

| The `Loop` line says | What to do |
|---|---|
| nothing | keep going. There is no cap to run out of |
| a rule *has come back* | **look for an exit, not another edit of the same words.** The line names what the rule is caught against: can the remainder be named in `Out of scope`, or does the question belong under `## Open`? Another lap at the same approach is what the warning exists to prevent |
| *stuck on* a rule | **stop and hand back.** Show the panel and say which rule, what it is against, and that the exits are a correction with `/sdlc:steer`, a scope change, a rule change, or abandoning the cycle |

**A halt is not a verdict on the artifact.** It says automation got stuck, and the open blockers are
still what decides whether anything advances. Do not describe a halt as a conflict unless a reader
declared one: the count cannot tell a genuine conflict from a refiner repeating a mistake, and both want
a person.

## 6. Report and hand back

```
sdlc status <id> --stage intent --brief
```

Show what it prints **verbatim**. Do not summarise it, reformat it, or add a recommendation about
whether to keep interrogating — that judgement is the owner's, and the panel deliberately withholds
it.

**Say nothing else beyond two sentences.** The panel already carries the cycle, the counts, the gate and
the next command, so prose around it is noise competing with the only lines worth reading. Drop
`--brief` when somebody asks for the whole panel; the full form is the same answer with the round
history and the hashes.

Then stop. The person decides whether to move to the spec. That decision is the human-in-the-loop
point, and a skill that chained straight into the spec would remove it while appearing to be helpful.
