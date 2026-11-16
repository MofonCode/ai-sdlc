# Guidance

How the framework would do one kind of thing — build a pipeline, say — handed to a plan that is about to do it.
Read it when a plan's `Shape and departures` mentions a guidance entry, when you want the framework's opinion
somewhere your solution has not decided, or before you write an entry of your own.

**Guidance is a push, not a gate.** It only guides planning. Nobody judges a plan against it, nothing it says
blocks, and your solution overrides it at any point. The design and the reasoning behind it are `CONCEPT.md` §6.6
and decision D-195.

## Guidance or a perspective

Both are documents the binary carries, and both are switched on by the files a plan declares. They differ in what
happens afterwards:

| | A perspective | Guidance |
|---|---|---|
| **is** | a way of examining a change: security, operability | how to do one kind of thing well |
| **used at** | every stage, to write and to judge | planning only |
| **can block** | on the rules it lists | never |
| **a solution changes it** | by its constraints | by writing its own entry, or simply by already doing it another way |

## Which comes first

**Guidance comes last.** Where your code already does the thing another way, or a decision or one of your rules
says how, the plan follows that and does not argue. The entry applies only where nothing has decided. Where a plan
departs from it anyway, it says why in a line, and no reader asks for the line.

**The steps that are the gate are not opinion.** The pipeline entry, for example, never moves or weakens the steps
that run `sdlc verify-chain`, `sdlc admission` or `sdlc promotion`.

## Which entries a plan gets

```
sdlc guidance                  # every entry, where it came from, and the files that switch it on
sdlc guidance show pipeline    # one entry in full
sdlc guidance <id>             # the entries this cycle's plan switches on, in full
```

An entry names file patterns — `azure-pipelines`, `.github/workflows/` — and a plan that declares a matching file
is handed it. The binary decides it from the plan's `Files` section, so every host gets the same entries.

## Writing your own

Put a Markdown file under `.sdlc/guidance/`:

```markdown
---
name: pipeline
files: "azure-pipelines"
---

# How we build our pipeline

One job. We accept the wait rather than keep two in step.
```

**An entry with the same name as the framework's replaces it**; any other name adds one. It needs a name, the
files that switch it on, and a heading, or `sdlc guidance` refuses to read it. It is an ordinary solution file —
not one of the files only a person writes — so a cycle may add or change one like any other declared file.

**When advice keeps being right, make it a rule.** Guidance never becomes a constraint by itself. A harvest
proposes rules from what the cycles' findings keep citing; `../guides/harvesting-the-cycles.md` is how.
