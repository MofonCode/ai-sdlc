---
name: init
description: Adopt the SDLC into a solution by scaffolding .sdlc/ and a charter from a stack pack. Use when a repository has no .sdlc/ directory and the user wants to start using this process there.
argument-hint: "[pack name, optional]"
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(powershell.exe *)
---

# Adopt the SDLC into this solution

Scaffold `.sdlc/` and a charter for the repository you are in.

## Refuse if it is already adopted

If `.sdlc/CHARTER.md` exists, stop. Re-running this would overwrite a charter, and a charter
changes **only** through a human-approved charter intent (INV-08). Tell the user it is
already adopted and point them at `/sdlc:intent` instead.

## Which pack — ask, do not infer

A charter is copied from a stack pack. Which pack applies is a question, not a deduction,
which is one reason this is a skill rather than a workflow.

If the user named a pack in **$ARGUMENTS**, use it. Otherwise look at the repository — its
project files, the languages present, what it builds — then **propose** a pack and ask. Say
what you saw that led you to propose it, so the user can correct the evidence rather than
only the conclusion.

If no pack fits, say so plainly rather than forcing the nearest one. A wrong pack means
every artifact in that solution is judged against rules written for a different stack.

## Two ways in, and they are not interchangeable

| | `scaffolded` | `adopted` |
|---|---|---|
| When | a new solution, or one that will conform | an existing codebase with its own shape |
| Charter | copied from the pack as-is | the pack's charter, plus a recorded baseline of what currently violates it |
| `Origin:` | `scaffolded from <pack>@<version>` | `adopted, inferred at <sha>`, with its baseline count |

`Origin:` is not decoration. It tells every later interrogation **how much to trust the
document it is checking against**: a scaffolded charter is a copy of a known-good template,
an inferred one is a guess about a codebase. It also makes the invariant id gaps readable,
which they are not without knowing the source.

For an adoption, count the existing violations and record the number. Do not fix them, and
do not quietly narrow the charter so they pass. An honest baseline is what lets the count go
down visibly later.

## Scaffold, then copy the charter — in that order, and they are different jobs

**Scaffolding is the pack's code.** It is the one verb that is code rather than data, because
creating files and running `dotnet new` is execution:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File packs/<pack>/scaffold.ps1 -SolutionName <Name>
```

It is idempotent: a re-run skips what exists rather than overwriting it, because `init` is
allowed to be re-run and a scaffolder that clobbers is one keystroke from destroying work.
Pass `-WhatIfOnly` first if the user has not seen the file list.

**The charter is yours to copy, and the scaffolder deliberately does not touch it.** Copy
`packs/<pack>/CHARTER.md` to `.sdlc/CHARTER.md` and replace every `TEMPLATE`:

| Field | With |
|---|---|
| `solution` | the solution's name |
| `since` | today |
| `origin` | `scaffolded from <pack>@<version>`, or `adopted, inferred at <sha>` with its baseline count |
| `version` | `1` |

A scaffolder that also wrote the charter could claim a scaffolded origin for an inferred one,
and `Origin:` is what tells every later interrogation how much to trust the document. That is
why the two steps are separate.

**Do not copy the pack rules.** `pack.rules.yaml` stays in the plugin and is resolved from
there by version, which is why the charter carries `Stack pack: <pack>@v<n>`. Only the charter
is copied. A local copy of the rules would be a second list with nothing binding it.

The available packs are the directories under `packs/`. Today that is **`dotnet-library`**:
a .NET class library with analyzers, MSTest and central package management.

**Never renumber an invariant id**, including across the copy from the pack. An id is
assigned once and never reused. A solution that drops an inapplicable invariant leaves a
**gap**, and the gap is itself the record that something was removed. Local additions use
`LOC-nn`, numbered independently.

Renumber into a vacated slot and `cites: INV-04` means different things in different
solutions on the same pack, which breaks aggregation across the retro corpus. That is the
whole reason the rule exists.

## When you are done

Show the user the charter and say what it commits them to. Then:

> The charter is deliberately thin, so early cycles are calibration runs. Do not run
> unsupervised until it can halt on something. Start with `/sdlc:intent`.
