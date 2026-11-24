---
name: doctor
description: Check this install and this repository -- the binary, the plugin, the solution's files, its branches and its pipeline -- and say what is wrong and whose it is to fix. Reports and never repairs. Use when something about ai-sdlc seems broken, after installing or upgrading, or when someone asks whether the setup is sound.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *)
---

# Check the install

```
sdlc doctor
```

Show what it prints **verbatim**. It exits 1 when anything is wrong; that is the report, not this skill failing.

## The one check you can answer

**`the skills` is marked elsewhere**, because a binary cannot ask a host what it loaded (D-215). You are running in
that host, so you can see it: count the skills this session was given from the ai-sdlc plugin, and after the
output add **one line** — how many there are against the number the check expects. Running this skill already
shows the plugin loaded; the count is what says it is the whole plugin, at the release the binary expects.

A count that differs is a binary and a plugin from different releases. Name `upgrade`, which moves this machine's
two together; do not run it.

## Never repair

**Report, and fix nothing.** Each problem line says what to do and whose it is to do. Several are a person's alone —
a draft to rename into place, a branch policy to set — and the rest are a step a person chooses to take. Hand back
the output and your line about the skills; where a problem names a step a skill takes, offer it by name.
