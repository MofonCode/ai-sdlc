# Documentation

Written for somebody using the framework. The design and the reasoning behind every decision live one
level up, in `../CONCEPT.md`; what the code is and how it fits together is in `../src/README.md`.

**`../README.md` lists what is here and what each one covers.** This file does not repeat that list — a
second copy of it is the one that goes stale the next time a guide is added or renamed.

Two things to read alongside these guides:

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
