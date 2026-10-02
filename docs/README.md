# Documentation

Written for somebody using the framework. The design and the reasoning behind every decision live one
level up, in `../CONCEPT.md`.

| | |
|---|---|
| [`skills.md`](skills.md) | the seventeen skills: what each is, how it is invoked, when it runs, and who starts it |
| [`guides/install.md`](guides/install.md) | installing the plugin and the binary, updating, and removing |
| [`guides/first-cycle.md`](guides/first-cycle.md) | a cycle from an empty directory to a pull request, in fourteen steps |
| [`guides/cycle-commands.md`](guides/cycle-commands.md) | every command for one cycle, in order, on one screen |
| [`guides/when-it-blocks.md`](guides/when-it-blocks.md) | the index to every refusal, and the remedy for each |

Two things to read alongside them:

- **`../reference/iteration-3-cycle/`** — a real cycle run in anger, with its interrogation rounds and
  its evidence. In the previous iteration's shape, and the best worked example the project has.
- **`.sdlc/README.md`**, written into every governed solution by `sdlc init` — the flow, the files and
  the commands, in one page that travels with the repository.

## What these are not

They describe the framework from outside: what to type, what it will say, and what to do when it
refuses. They state nothing about the rules themselves — `sdlc constraints` renders those, and a copy
here would be a second place they live, which is always the copy that goes stale.

Where a guide says something has not been exercised, that is load-bearing. **A directory that appears
enforced and is not is worse than one that never pretended**, and the same goes for a document.
