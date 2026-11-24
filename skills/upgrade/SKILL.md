---
name: upgrade
description: Move this machine's sdlc to a release -- the binary and the installed plugin, together -- and check the two agree. This machine only; a solution moves by its upgrade pipeline or `sdlc upgrade apply`, never from here. Use when someone asks to update or upgrade their local ai-sdlc, or a session says the plugin and the binary disagree.
compatibility: Requires the dotnet SDK, and the sdlc binary on PATH from the MofonCode.Sdlc.Cli dotnet tool. The plugin step is written for Claude Code; another host follows the install guide, The plugin.
argument-hint: "[<release>]"
allowed-tools: Bash(sdlc *), Bash(dotnet tool *)
---

# Move this machine to a release

**One job: this machine's two halves, the binary and the plugin, on one release.** It changes no repository. A
solution's pin moves by a run of its upgrade pipeline, which a person starts on the platform and which opens the pull
request, or by `sdlc upgrade apply` on its integration branch; `sdlc upgrade` inside the solution says what applies there. Do not run or edit a solution's pipeline, pin or
files from here.

## The release

The one named, or with none, the newest on nuget.org. Say which, and what this machine runs now:

```
sdlc version
```

Run it outside any governed solution: inside one, the global binary hands on to the release the solution pins, and
that is what answers.

If `SDLC_DEV_BUILD` is set, this machine runs a local build of the framework and the global tool is not what answers.
Say so and stop: https://github.com/MofonCode/ai-sdlc/blob/main/docs/guides/testing-a-local-build.md is how to leave one.

## The binary

```
dotnet tool update --global MofonCode.Sdlc.Cli --version <release>
```

With no release named, leave `--version` off. If it says the tool is already installed at that version, nothing
moved, which is the answer when this machine is already there.

## The plugin

A plugin install is cached by version, so a plain update does nothing when the number has not moved. On Claude Code,
in this order:

```
claude plugin marketplace update ai-sdlc
claude plugin uninstall sdlc@ai-sdlc
claude plugin install sdlc@ai-sdlc
```

The last two are a pair: stopping after the uninstall leaves no plugin. On another host, follow
https://github.com/MofonCode/ai-sdlc/blob/main/docs/guides/installing-the-plugin.md, The plugin, and say which steps the person runs.

## Check

```
sdlc version
claude plugin details sdlc@ai-sdlc
```

Both name the release's line, its first two numbers. The binary names the whole release; a plugin from the
marketplace always ends in `.0`, because the build is stamped only into the package, so `0.55.0` beside `0.55.3` is
one release and nothing to fix. Say what each reports, and that the skills this session loaded are the old ones until
a new session starts. When the lines disagree, say which half is behind and run its step again; never work around a
command the binary lacks.

**Then hand back.** If the person is in a governed solution, `sdlc upgrade` there lists what moves for it, starting
with its upgrade pipeline.
