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
| **Capped** | not on rounds. It stops when one rule has come back three times |
| **Refuses when** | the cycle is approved, or no blocking finding is open |

**Read the panel's `Loop` line before you edit.** You are handed findings and no history, so without
it you cannot tell a first objection from the third time the same rule has been refought — and
refighting is answered by finding an exit, not by another edit of the same sentence. When the line names
a rule that *has come back*, it also names what that rule is caught against: ask whether the remainder
can be named in `Out of scope`, or whether the question belongs under `## Open`, before touching the
words again. When it says *stuck on*, do not edit at all. Report it and stop; a person decides what
gives, and `/sdlc:steer` is usually how they do it.

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

## Answer the mistake, not the instance

**A finding names a place; what it objects to is a mistake, and a mistake made once is usually made
again.** Before you heal a finding, read the rest of the artifact for the same defect -- every other
proof that waits on the same thing, every other criterion with the same unmeasured word -- and heal
each one in the same pass. Fixing only the line you were pointed at hands the next round the next
instance, and the loop spends a round on each.

Two limits keep this from becoming a rewrite. It is the **same defect** under the **same rule** as the
finding, and nothing else: a different problem you notice is the next reader's to raise, not yours to
fix unasked. And it is still only this artifact.

Say what the sweep touched on the finding's own line in the report, so the next round knows the other
places changed and why:

```
F_15  healed     proof 8 waits for the rendered roll before its second scan; so does proof 10, which had the same gap
```

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

**You do not.** A finding you answered moves to `answered` in the **next** round, written by the next
interrogation — not by you editing the findings ledger. Only `sdlc round` writes it, each round chained to
the ones before, and a ledger edited any other way fails its chain and is refused (D-107).

What you record is what changed, in your report — and that report is not a courtesy. The next round
cannot tell a finding you *answered* from one you *disagreed with* by looking at the document, and
those become different dispositions: `answered` for one, `withdrawn` for the other. Your report is
where that distinction comes from, so a report that omits an id leaves the next round unable to clear
it, and a blocker nobody clears refuses the build.

## Report

**One line per finding, in this shape**, and nothing else:

```
F_01  healed     added the fourteen-second measurement to `## Why now`
F_02  withdrawn  the criterion does name a check; the reader read the wrong line
F_03  asked      recorded in `## Open` — nobody can say what the retention period is
```

Three columns: the id, the exit it took, what you did. The report's reader is the next interrogation
round, which needs the id and the exit to decide a disposition — so a line that argues its case buries
the one thing the round came for.

**Name every id you were given, including the ones you did not change.** Three groups, because the
next round reads them differently:

| | What it means for the next round |
|---|---|
| ids you changed the document for — `healed` or `assumed` | candidates for `answered` |
| ids you disagreed with and left alone, with the reason | candidates for `withdrawn` |
| ids you could not proceed on — `asked` | recorded in `## Open`, and you stop and ask |

Then:

```
sdlc status <id> --stage <artifact>
```

Show it verbatim and stop. Do not interrogate again from here — that is the stage skill's loop, and
the panel's next line says the command.
