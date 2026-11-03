---
name: verify
description: Adjudicate a build against its spec, from the recorded evidence alone, in a fresh process that did not see the build. Use after a build has recorded its evidence. Never edits code.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id>"
allowed-tools: Bash(sdlc *), Bash(claude *), Read, Write, Glob, Grep
---

# Verify a build against its spec

Verify is the only artifact whose entire value is being **derived**. A claim that reaches beyond its
evidence turns an auditor into an author, and nothing downstream can tell which one wrote it.

## The verdict is written in a fresh process

**This session does not adjudicate.** It has the binary write what the adjudicator reads, starts it, and
records what it returns. The session running this skill is very often the one that built the change — the
build stage hands straight to this one, and `batch-build` runs both in one agent — and a verdict written by
the author of the code is the author grading their own work (D-01). It is not hypothetical: a cycle's
building session wrote *every criterion met* over 22 criteria, and then argued one of the four its
interrogation found back to met by refining the verdict, with no change to the code. A fresh adjudicator
overclaims too, and the interrogation still catches that. What it does not have is a stake in defending
the verdict once it is challenged.

```bash
sdlc round prompt <id> verify --dir <scratch>/verdict --reader adjudicate
SDLC_READER=1 claude -p --output-format json --tools= --strict-mcp-config --no-session-persistence \
  < <scratch>/verdict/adjudicate.prompt.md > <scratch>/verdict/adjudicate.json
```

The process is started the way `interrogate` starts a reader, for the same reasons: a new process rather
than a subagent, so every host runs one path; the same line, flag for flag, so it has no tools and
reads only what the binary put in its prompt (D-118); the prompt piped in, never passed as an argument; and
`SDLC_READER=1`, so a red build's turn-end hook lets the adjudicator's turn end with its verdict (in
PowerShell, `$env:SDLC_READER = '1'` before the call). **Never switch the hooks off to get it through.**

**The prompt is the binary's, and you neither edit it nor add to it** (D-117). It carries the plan, the
spec and the evidence — `tests.json`, `base.sha`, `build.log` where this checkout has one, and
`diff.patch` — then the constraints for a verify, each active perspective's questions for this stage, and
the method: read the evidence and nothing else, adjudicate criterion by criterion, quote the assertion
every Met rests on, cite everything, choose the outcome, and write the document. Nothing else — not the
conversation, not what the build set out to do, not this session's view of how it went. Every one of
those is the bias the process was opened to remove.

**The binary records what it returns, byte for byte**, as `.sdlc/cycles/<id>/verify.md`:

```bash
sdlc round verdict <id> --from <scratch>/verdict/adjudicate.json
```

It refuses an answer that is not a verify document — prose around it, a refusal, a question. Start the
adjudicator again; do not repair it yourself, because the repair is this session writing the verdict after
all. Then `sdlc status <id>` checks its shape. Interrogate the verdict with the same directory as
`--dir`, so the round's collect counts what the adjudicator cost as that round's.

## Check the citations, and report what it says

```bash
sdlc evidence check <id>
```

A string match of the verdict against the evidence (D-120): every name `verify.md` cites in backticks
that appears in none of the evidence — `tests.json`, `diff.patch`, `coverage.json`, and the build log where there is one — and every test it says passed that `tests.json`
does not record as passed. **It reports and refuses nothing**, and you act on nothing it lists — a
backticked name may be a type or a log line, and whether a claim rests on its evidence is the round's
reader's to judge. Show what it prints verbatim before the round, so the person sees it beside what the
round finds.

## Then run the loop, and stop

Interrogate the verify document like any other artifact — `CON_08` applies to it, and the rounds it
accumulates are also what counts remediation passes.

**A round's blocking findings are answered by a new adjudication, not by an edit.** Changing a verdict
is adjudicating, so it happens where adjudicating happens: start a fresh adjudicator from a prompt written
anew. While a verdict has blockers open, the binary adds the current `verify.md` and those findings, and
asks it to answer each from the evidence — standing by a claim and citing why is an answer,
as is changing it. Record what it returns, as above. Do not run `refine` on a verify document: a
criterion moved from Not met to Met by this session's argument is the self-grading this skill exists to
prevent, however good the argument.

```
sdlc status <id>
sdlc pr-ready <id>
```

Show both verbatim. Then **at most two lines of your own**, and only the outcome in your own words.
Then stop.

**Nothing that is not already in `verify.md`.** You have just written the document that is the
adjudication; a hand-back that re-argues it produces a second, shorter verdict with no hash and no
round, and a reader who disagrees with one cannot tell which one the chain carries.

- **Passed?** The person decides whether to open a pull request.
- **Blocked?** The person decides whether to remediate. Do not start remediating; you
  have just finished judging, and the separation is the point.
