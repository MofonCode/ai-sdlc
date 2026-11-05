---
name: status
description: Show where a cycle has got to -- the stage panel, its checks, the gate, and the next command. Use after any stage, or when someone asks where something stands.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<cycle id> [intent|spec|plan]"
allowed-tools: Bash(sdlc *)
---

# Show where a cycle has got to

```
sdlc status <cycle id> [--stage <artifact>]
```

Show what it prints **verbatim**.

Without `--stage`, the panel is for whichever artifact the cycle is waiting on, which is what a person
wants after any stage finishes.

## Do not rewrite it

The command-line tool prints this panel, not you. That is deliberate: the format is a contract, and a
skill that reformatted it would make the thing people actually read drift between hosts. Four rules it
keeps, which are also the reasons not to touch it:

- it names the state before anything else, right-aligned on the heading line
- an artifact's checks are called **checks**, never verification — `verify` is a phase name here with a
  specific meaning
- it ends with the literal next command, copy-pasteable
- the glyphs are ASCII, so it renders in a pipeline log and in a console that is not UTF-8

## What the panel shows

**Says** — the document's own first heading and its section headings.

**Shape** — what the schema found, and nothing at all when the shape is sound. It comes before the
checks on purpose: a shape problem is usually *why* a round found what it found. An interrogation
reading an artifact with no `Proof` section raises something about the proof, and an author who reads
the finding first goes looking for a bad proof rather than a missing heading.

**Checks** — what each round found, in round order, and the open blocking findings with the rule each
cites.

**Gate** — whether the build may run, and why not.

**Next** — the literal command.

## What it will not tell you, on purpose

**Whether to keep interrogating.** The panel reports what each round found and attaches no
recommendation. That judgement has been wrong before: on one real intent, round seven found the headline
change was unreachable and round eleven found that no success criterion failed without it. A tool that
had suggested stopping at round four would have been confidently wrong.

**A summary of the artifact's content.** A command-line tool cannot summarise, and inventing one would
be worse than showing the headings a person wrote.

If the person asks for your reading of where things stand, give it **after** the panel, in **at most
three lines**, under the literal heading `My reading:` so that nothing mistakes it for something the tool
said. **Unasked, say nothing at all.** The panel is the answer, and prose beside it competes with the only
lines worth reading.
