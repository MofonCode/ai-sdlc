---
name: refine
description: Apply an artifact's findings to that artifact. Edits one planning document and nothing else, and never judges. Use after an interrogation round raised blocking findings.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> <intent|spec|plan>"
allowed-tools: Bash(sdlc *), Bash(git *), Read, Write, Edit, Glob, Grep
---

# Refine an artifact

This skill **edits and never judges**. The findings were decided by a reader with no stake; your job
is to answer them, not to re-litigate them.

## What you may and may not touch

| | |
|---|---|
| **May edit** | the one named artifact |
| **May never edit** | source, tests, `approval.md`, or any other artifact |
| **Capped** | no. The loop is uncapped on purpose |
| **Refuses when** | the cycle is approved, or no blocking finding is open |

**Only one of those is denied at the tool call.** The hook refuses every agent write to `approval.md`,
by file path and by shell command alike. The rest of this table is yours to keep — the hook sees a tool
call and not which skill made it, so it cannot tell an edit to source from here from the same edit made
by `remediate`, where it is the whole job.

So read the table as a rule rather than a railing. What it is for: if a failing check could cause an
agent to adjust a planning document, the criteria become whatever the code happens to do, and the whole
chain exists to stop that one inversion. What catches you if you break it anyway is slower and
certain — a document edited after approval no longer matches its recorded hash, so `sdlc gate` refuses
the build and `sdlc verify-chain` refuses the merge, both naming which document moved.

**Refuses once approved.** The plan is hash-locked at that point, so editing it would break the gate
rather than satisfy it. If a plan turns out to be wrong after approval, the move is a recorded reopen:
`sdlc draft approval <id> --reopen`, which returns the cycle to unapproved so the documents can be
rewritten and re-approved. That is the recovery path, and it leaves a record of why.

## Disagreeing with a finding

You may. Not every finding is right — two of the twenty-four problems found in the last reconciliation
were cases where the document was wrong and the code was right.

What you may not do is answer a finding by making the artifact vaguer. That is the one move that
removes the problem from the page while leaving the belief in it:

| Finding | Not a fix | A fix |
|---|---|---|
| `CON_02`: "fourteen seconds" is unmeasured | change it to "slow" | record it as an unconfirmed assumption, or ask |
| `CON_03`: "handles large files" cannot fail | add "reliably" | name a size and an observable |
| `CON_06`: a unit test cannot prove a latency claim | add "and integration tests" | name the test and what it measures |

If a finding is wrong, say so in your report with the reason, and leave the artifact alone. Do not
withdraw it from the findings file — a later round withdraws a finding, and a round you edited is no
longer the record of what was challenged.

## The three exits

Every finding ends in exactly one of these. There is no fourth.

| Exit | When | Where it goes |
|---|---|---|
| **healed** | fixable from information already on the page | the artifact changes |
| **assumed** | something had to be supplied to proceed | the decisions region, marked unconfirmed |
| **asked** | it cannot proceed without a person | `## Open`, and you stop and ask |

**The decisions region** is `Fixed decisions` on an intent and `Shape and departures` on a plan. A
spec has neither and needs neither — a criterion that needs an assumption is usually a sign the intent
is the thing to fix, which is an `asked` rather than an `assumed`.

**Never assume silently.** The rule is not "never assume". A report is read once; a section is read
every time the artifact is read, and it is what a reviewer meets at the gate.

## Update the dispositions

A finding you answered moves to `answered` in the **next** round, written by the next interrogation —
not by you editing the round file. Rounds accumulate and are never rewritten; a round you edited
stops being the record of what was challenged.

What you do record is what changed, in your report.

## Report

Say, per finding: its id, which exit it took, and what you did. Then:

```
sdlc status <id> --stage <artifact>
```

Show it verbatim and stop. Do not interrogate again from here — that is the stage skill's loop, and
the panel's next line says the command.
