# Documentation

Everything written for somebody using the framework rather than changing it. Today that is one directory:

| | |
|---|---|
| [`guides/`](guides/) | the four guides. [`guides/README.md`](guides/README.md) is the index — what each covers and when to read it |

**The index lives beside the guides, not here.** This file does not describe them, because a second
description is the copy that goes stale the next time one is renamed.

What is documented elsewhere, and why it is not here:

| | |
|---|---|
| the seventeen skills: what each is, how it is invoked, who starts it | [`../skills/README.md`](../skills/README.md), beside the skills themselves |
| the design, and every decision behind it with what each one rejected | [`../CONCEPT.md`](../CONCEPT.md) |
| the rules in force | `sdlc constraints` renders them, all tiers resolved. A copy would be a second place they live |
| what the code is and how it fits together | [`../src/README.md`](../src/README.md) |
| the tools that support changing the framework | [`../tools/README.md`](../tools/README.md) |

One thing to read alongside the guides:

- **`.sdlc/README.md`**, written into every governed solution by `sdlc init` — the flow, the files and
  the commands, in one page that travels with the repository.
