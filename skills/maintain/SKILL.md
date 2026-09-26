---
name: maintain
description: Survey a solution for dependency drift, CVEs and stack-pack staleness, and file intents for what it finds. Use for routine upkeep, or when someone asks what maintenance this repository is owed.
argument-hint: "[solution path, optional]"
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(dotnet *) Bash(powershell.exe *)
---

# Survey what this solution is owed

Look for drift, and **file intents for what you find**.

## Maintain files intents. It never fixes.

This is the rule, and it is not a formality. A bug fix runs the **normal cycle** with a
lighter intent template: spec, plan, approval, build, verify, land. Maintenance that fixed
things directly would be a second path into the codebase that skips every gate, and it would
be the path of least resistance precisely because the work looks trivial.

A CVE bump is a one-line change that can break a build, alter behaviour, or pull a new
transitive dependency. It gets the same treatment as everything else.

So: survey, then author intents. Then stop.

## What actually fuels this today

| Fuel | Live? | How to look |
|---|---|---|
| **Dependency and CVE drift** | yes | `dotnet list package --vulnerable --include-transitive`, then `--outdated` |
| **Stack-pack staleness** | yes | compare the charter's `Stack pack: <pack>@v<n>` against the pack's current version |
| **Telemetry-driven intent** | **no** | reserved, and unfuelled because nothing is deployed yet |

Say so when the third one is why you found little. An empty survey that does not explain what
it could not see reads as a clean bill of health.

`NuGetAudit` is on in this stack, so a vulnerable direct dependency already fails the build.
What this survey adds is **transitive** vulnerabilities and packages that are merely behind —
neither of which breaks anything today, which is exactly why nothing else prompts you.

## Pack staleness is a charter change, not a maintenance fix

If the pack has moved past the version the charter names, **that bump is a `charter-review`
intent in this solution**. The `Stack pack:` line is charter content: moving it changes every
rule governing the solution at once, so it takes the gate.

Do not bump it here, and do not file it as ordinary maintenance. Check first whether the
question is already asked:

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Find-SdlcCharterIntent.ps1 -Target <rule>
```

## One intent per coherent change

A CVE bump is its own intent. A framework major version is its own intent. Do not bundle
unrelated upkeep into one, because a person has to be able to approve one and reject
another, and a bundle forces a single answer to several questions.

Author each with `/sdlc:intent` and `source: maintain`, and put the evidence **in** the
intent: the advisory id, the current and available versions, what the change touches. An
intent that says "update dependencies" cannot be specced, planned or blast-radius tiered.

For a CVE, state the severity and whether the vulnerable package is direct or transitive. A
transitive one may need a pin rather than an upgrade, and that is a materially different plan.

## What not to do

- **Do not edit a version file.** That is the build stage's job, after a plan.
- **Do not bundle a CVE fix with an unrelated upgrade** because you are already in there.
- **Do not file an intent for every patch release.** Being behind is not a defect. A CVE is,
  a broken build is, and an unsupported framework version will be — the rest is noise, and a
  maintenance survey that files twenty intents nobody reads has trained you to ignore it.

## When you are done

Report what you found, what you filed, and what you deliberately did not file. Then stop:

> Each intent is behind its own gate. Nothing is built until you approve a plan.
