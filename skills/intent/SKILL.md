---
name: intent
description: Interview the user to author a governed intent, the first artifact of a cycle. Use when someone wants to start new work, reports a problem to fix, or asks to turn an idea into something the SDLC can process.
argument-hint: "[a sentence describing the work, optional]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Author an intent

An intent is the first artifact of a cycle and the only one written by interview. Your job
is to **elicit**, not to draft: the user knows what they want and does not yet know what is
ambiguous about it. Finding that out is the work.

This is why `intent` is a skill and not a workflow. A workflow cannot ask a question.

## Before you start

Read `.sdlc/CHARTER.md`. Two invariants apply to intents and shape every question below:

- **INV-01** forbids describing behaviour without its failure modes. An intent missing an
  error path propagates that gap into the spec, the plan, the code and the tests, and this
  is the cheapest place to catch it.
- **INV-02** forbids a statement admitting two readings that lead to materially different
  downstream work.

You are not interrogating the intent while you interview — that comes after it is written. You
are trying to make that interrogation find nothing.

## The interview

Ask about these. **Ask them a few at a time, not as a form**, and skip what the user has
already told you. If their opening sentence answers something, do not ask it again.

1. **What is wrong, or what is wanted?** In their words. Write it down as they said it.
2. **How will we know it worked?** Push until the answer is observable. "It should be
   faster" is not yet an answer; "the queue drains in under a minute" is.
3. **What should happen when it fails?** This is INV-01 and it is the question most often
   skipped. Ask it for each distinct thing that can go wrong, not once in general.
4. **What is explicitly out of scope?** A stated exclusion prevents scope drift later far
   more cheaply than a finding does.
5. **Is this fixing something an earlier cycle produced?** If so, get the cycle id for
   `caused-by` and which stage leaked for `caused-at-stage`. This is the retro loop's raw
   material — an intent that records its cause is how the process learns.

When an answer admits two readings, say so and ask which one. Do not pick for them and do
not write both.

## Stop and ask rather than guess

If the user is vague about something material, ask. An intent written on your assumption
becomes a spec, a plan and code built on your assumption, and the first person to notice is
whoever reviews the diff. You are cheaper than that.

## Writing the file

Generate the id — `yyyymmdd` plus four hex characters:

```
powershell.exe -NoProfile -Command "'{0:yyyyMMdd}-{1}' -f (Get-Date), ((1..4 | ForEach-Object { '{0:x}' -f (Get-Random -Max 16) }) -join '')"
```

Write `.sdlc/intents/<id>.md`. The frontmatter must validate against
`schemas/intent.schema.json` — read it rather than trusting this list:

```yaml
---
id: <the generated id>
source: authored
charter-version: <version from the charter frontmatter>
pack-version: null
---
```

`source` is `authored` for ordinary work. Use `suggestion` when the intent comes from a
promoted finding, `maintain` for upkeep, and `charter-review` **only** for a change to the
charter itself — that one is human-approved and one-way under INV-08.

Structure the body around what you elicited: what is wrong, how we will know it worked,
what happens when it fails, what is out of scope. Prose, not a form. Lead with what each
thing says and keep identifiers as trailing detail, per the charter's Communication rule.

## Interrogate it before it goes behind its gate

**The intent is the cheapest place in the whole cycle to catch a defect**, and it is why two
invariants were deliberately widened to reach intents: a missing error path here propagates into
the spec, the plan, the code and the tests. Interrogating only the spec and the plan left the one
artifact with the most leverage unchecked.

```
/sdlc:interrogate .sdlc/intents/$0.md
```

Write the returned JSON to `.sdlc/cycles/$0/interrogation.json` — gitignored and regenerable —
and file it:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Add-SdlcFindings.ps1 -CycleId $0 -Path ".sdlc/cycles/$0/interrogation.json"
```

Then act on it. A `blocking` finding here is cheap to fix — you are still in the interview, the
user is still present, and nothing downstream exists yet. **Where the finding turns on what they
wanted, ask them**; that is the whole reason this stage is an interview. Record a disposition for
each with `tools/Set-SdlcDisposition.ps1`, and re-interrogate after repairs: in every measurement
this project has taken, repairs introduce findings of their own.

Findings stay with the cycle, so the intent's pull request carries the interrogation that was run
on it. That is the review seeing what the artifact survived rather than only the artifact.

## Put it behind its gate — this is not optional

An intent written straight to master has **no pull request, and therefore no gate**. Do not
commit it to master, and do not write it on a cycle branch either: there its PR would stay
open while spec, plan and code piled onto the same diff, so approving the intent would
implicitly approve everything that followed.

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Submit-SdlcIntent.ps1 -CycleId <id>
```

That creates `intent-<id>`, commits only the intent file, pushes, and opens the pull
request. Two branches per intent, same id, different prefix — and the second one,
`cycle-<id>`, is created by the dispatcher **after** this merges, never before.

Report whatever the script reports. If it says there is no remote or no GitHub CLI, say so
plainly rather than presenting a local branch as a closed gate.

## When you are done

Show the user the intent and the pull request. Then stop:

> Nothing happens until you merge that. Once it lands, `/sdlc:spec <id>` is the next step,
> and you drive it.

Do not merge it yourself and do not run the spec. **You can request; only a person can
grant.**
