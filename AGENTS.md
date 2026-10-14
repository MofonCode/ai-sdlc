# Working in this repository

This file is for an agent changing ai-sdlc. [`README.md`](README.md) describes the product for a person —
read it if you need to know what the framework *is*; read this to know how to work on it.

**One sentence of context:** a change moves through **intent → spec → plan → build → verify → pr**,
each phase producing a file the next phase reads, each file challenged against a fixed set of rules
before it advances, and a person approving the three planning documents before any code is written.
Nothing advances on assertion.

## Establish what the request is about, first

Two things are easy to confuse and they behave differently.

- **This repository is the framework.** A change here changes how every solution is governed.
- **A solution is a repository that adopted the framework.** It has a `.sdlc/` directory, a `solution.md`
  declaring its stack and origin, and usually its own `constraints.md`.

A constraint added here binds everyone; one added in a solution binds only that solution. **If a request
could mean either, ask.**

**The framework does not run cycles on itself.** There is no `.sdlc/` directory here and adding one is
wrong — a framework judged by the constraints it is changing is a deadlock. It is built as an ordinary
product: branch, review, merge.

## The map

**Every directory holding documents carries its own index, named `README.md`, and that index is what
describes the directory's contents.** Read the index of the directory you are about to change *before*
you change anything in it — it says what each file is for and what it deliberately leaves out, which is
the cheapest way to discover that what you were about to add already lives somewhere else.

| Index | Describes |
|---|---|
| [`skills/README.md`](skills/README.md) | the eighteen skills in depth: when, why, how and what for each |
| [`docs/README.md`](docs/README.md) | the components: every concept the framework names, defined once. Start here when a term is unfamiliar |
| [`docs/guides/README.md`](docs/guides/README.md) | the guides, one task each: what each covers, when to read it, what it leaves to another |
| [`docs/cheat-sheets/README.md`](docs/cheat-sheets/README.md) | the cheat sheets: one line per command, skill and refusal, each held to its source by the suite |
| [`docs/samples/README.md`](docs/samples/README.md) | the worked examples: one solution, every command, every answer |
| [`tools/README.md`](tools/README.md) | the two tools that check what the suite cannot, and when to run each |
| [`src/README.md`](src/README.md) | what `Sdlc.Core` and `Sdlc.Cli` are, how one invocation flows, where a new thing goes |

| Where | What |
|---|---|
| [`CONCEPT.md`](CONCEPT.md) | the design, and every decision behind it with what each one rejected |
| [`CONSTRAINTS.md`](CONSTRAINTS.md) | the seventeen global constraints, each with what it forbids and why |
| `src/Sdlc.Core/Constraints/Profiles/` | the stack rules, one document per profile: `PRO_` ids, the only tier where a rule may name a language, framework or package. Embedded in the binary; a solution is held to the ones its declared stack marks `true`, so an id is unique across every document here |
| `CONCEPT.raw.md` | the original longhand draft, preserved unedited |
| `src/Sdlc.Core/` | the gate's logic: constraints, cycles, approvals, findings, hooks, evidence, the chain |
| `src/Sdlc.Cli/` | the one binary every skill on every host calls |
| `skills/` | the skills as they ship, one directory each |
| `docs/guides/`, `docs/cheat-sheets/`, `docs/samples/` | the documents for a person using the framework: one task per guide, one line per thing in a cheat sheet, one solution end to end in a sample |
| `plugin.json`, `.claude-plugin/`, `hooks/` | the plugin as it installs: the portable manifest, Claude Code's manifest, Claude Code's hooks |
| `tests/` | the suite |
| `tools/Sdlc.ScaffoldHarness/` | the harness that writes a scaffolded solution so it can be built |
| `.github/workflows/ci.yml` | build and test on every pull request, which deliberately does not govern this repository with itself (D-42) |

### Querying `CONCEPT.md`

It is the current authority on every design question. Read it before proposing a change to how the
framework works, and cite the decision you are revisiting.

**Do not load the whole file to find one decision.** §1–18 state what is true and read front to back; §20
is an appendix of every decision and about half the document, and it is reference material you query rather
than read. Three lookups, in the directions anybody actually travels:

```sh
# I am changing something. Which decisions constrain it?
#   Either read the section - it carries a trailing (D-nn) marker - or ask by section number:
awk '/^### D-/{d=$2} /^..Governs:/{print d" "$0}' CONCEPT.md | grep '11.3'

# A commit, a code remark or a finding cites D-38. What does it govern, and why?
sed -n '/^### D-38/,/^### D-39/p' CONCEPT.md

# I do not yet know which decision I want. The whole list, one line each, sorted by the section it
# governs - one line each rather than the whole appendix, so neighbours are the decisions that constrain each other:
awk '/^### D-/{h=$0; sub("^### ","",h)}
     /^..Governs:/{g=$0; sub("..Governs:.. ","",g); print g "  |  " h}' CONCEPT.md | sort
```

Every entry carries `Governs` (the sections that state the rule), `Why`, and `Rejected`. **Read
`Rejected` first when you are about to propose something** — it is where the alternative you are reaching
for has usually already been weighed, and relitigating one is the waste the field exists to prevent. A
test holds that shape, so these work on every entry rather than most of them.

## Building and testing

```
dotnet build
dotnet test
```

Both from the repository root, and **with no flags**. The SDK and the test runner are pinned in
`global.json`; `Directory.Build.props` holds the shared settings, `Directory.Packages.props` the
versions.

**Do not add verbosity flags to `dotnet test`.** This solution runs on Microsoft.Testing.Platform, and
VSTest-style flags like `-v q` make it run **zero tests** and exit 5 while printing `failed: 0` on the
way past.

**Warnings are errors**, and a build that warns is a build that failed. The relaxations for test code are
listed in `tests/Directory.Build.props`, each with its reason, and there are no pragmas in test source.

### Changing a scaffold template

The suite asserts **strings** in the templates, so a template that generates code which does not compile
passes every test here. Generate a solution and build it:

```
dotnet run --project tools/Sdlc.ScaffoldHarness -- <empty-directory>
dotnet build <empty-directory>/Demo.slnx
dotnet test  <empty-directory>/Demo.slnx
```

`.github/workflows/ci.yml` does exactly this on every pull request, so a broken template fails there
rather than in somebody's repository. Run it locally anyway when you change a template — the pipeline is
a backstop, not the inner loop.

## What the suite enforces about this repository

These are properties, not style preferences, and each is a named test in `tests/Sdlc.Architecture.Tests/`:

- no source file switches off a warning with a pragma
- `Sdlc.Core` references no other project — it stays a leaf, because everything else depends on it
- every class is sealed, static or abstract
- every request `Sdlc.Core` declares has a registered handler
- every directory holding projects is scanned by the checks above

If one of these is in your way, the conversation is about the property, not about the test.

Two more live in `tests/Sdlc.Tests/`, because they read files rather than types: every project file the
scaffold writes is well-formed XML, and the pipeline still builds and tests a generated solution. The
second one exists because a step in a YAML file is deletable and deleting it is silent — the same reason
the response-factory ban has a test of its own.

## Two rules about what you may write

**A command is deterministic and the binary owns it; a skill needs language and an agent runs it.**
`sdlc help` lists what a file listing and a hash can decide. Everything in `skills/README.md` is what
cannot be decided that way. Typing a stage name at the binary — `sdlc spec <id>` — is answered rather
than refused: it says the stage is a skill and names the invocation.

**Six files only a person writes**, in a governed solution: `approval.md`, `abandoned.md`,
`constraints.md`, `decisions.md`, `solution.md`, `techstack.md`. No tool writes them. A tool prints what
belongs in them — `sdlc draft approval`, `draft abandon`, `draft solution`, `draft techstack` — and a
person places it. **Two are proposed rather than drafted**, because their
wording is argued from the record rather than dictated: `sdlc propose constraints` from the findings, and
`sdlc propose decisions` from what an approved cycle stated.

`.sdlc/theme.json` is a solution file and is **not** on that list: nobody is judged against a colour, so
a cycle may declare it and change a value like any other file. The pre-tool
hook denies every agent write to all six, by file path and by shell command alike, with no exception.
**A refusal there is the design working, not a configuration problem to route around.** The hook matches
the text of the command, not the paths it resolves, so a commit message naming a protected file is
refused too: pass it with `git commit -F`.

## Keep the documentation current

**This is the section to come back to.** Every document describes a surface, so each goes stale the
moment that surface moves. Update it in the **same change** that moves it — never in a follow-up, because
a follow-up is a thing somebody has to remember, and the document is wrong in the meantime.

| If the change | update |
|---|---|
| adds, removes or renames a skill, or changes what one takes or refuses | [`skills/README.md`](skills/README.md) — its own section, and the count in the heading if it moved — **and the skills table in [`docs/README.md`](docs/README.md) and the row in [`docs/cheat-sheets/skills.md`](docs/cheat-sheets/skills.md)**, both held by the suite against disk, and the cheat sheet's `Takes` against the skill's `argument-hint` |
| adds or renames a concept the framework has a name for | [`docs/README.md`](docs/README.md), which defines each one once. A concept defined nowhere is one the next session works out from the code |
| changes a command's behaviour or options | `src/Sdlc.Cli/Usage.cs`, which is what `sdlc help` prints, and [`docs/cheat-sheets/commands.md`](docs/cheat-sheets/commands.md) |
| changes what a correct install produces — a hook, a skill count, a version, either plugin manifest | [`docs/guides/installing-the-plugin.md`](docs/guides/installing-the-plugin.md), **including the table at the top**, and the Getting started steps in [`README.md`](README.md) |
| adds, removes or reworks a refusal | [`docs/guides/handling-refusals.md`](docs/guides/handling-refusals.md), and its row in [`docs/cheat-sheets/refusals.md`](docs/cheat-sheets/refusals.md), which the suite holds against the guide's entries |
| changes how a blocking finding or a blocked verification is answered — the loops, refine, remediate, escalation | [`docs/guides/handling-blockers.md`](docs/guides/handling-blockers.md) |
| changes the flow a person walks through | [`docs/guides/adopting-the-framework.md`](docs/guides/adopting-the-framework.md) for adoption, [`docs/guides/running-a-cycle.md`](docs/guides/running-a-cycle.md) for a cycle, and [`docs/samples/rolling-a-die.md`](docs/samples/rolling-a-die.md) where the sample walks it |
| adds, removes or renames a guide, a cheat sheet or a sample | that folder's `README.md` — its table row, its own `##` section, and any count in the opening line — **and** the Where to read more table in [`README.md`](README.md). The suite holds, for each of `docs/guides/`, `docs/cheat-sheets/` and `docs/samples/`, that the index's headings and that table name the same set as the directory. A new kind of document gets a folder of its own, with its own index, and joins that test |
| adds or changes what a profile declares, or a stack rule | `Solutions/StackProfile` for the rows, `Constraints/Profiles/<profile>.md` for the rules, and [`docs/guides/declaring-the-stack.md`](docs/guides/declaring-the-stack.md), which is where a person reads both. A rule count stated in a document is held by the suite. **A new rule reaches no existing solution until its row is added there** — say so wherever the release is described |
| adds, removes or changes a build settings file, or what one does | `Solutions/BuildSettings`, which is the one list — its path, its one-line summary, its detail and its template — and [`docs/guides/declaring-the-stack.md`](docs/guides/declaring-the-stack.md), which carries the table of the five. The draft's rows, the options menu, `sdlc scaffold`, `sdlc stack` and the harness all read the list, so none of them is a second place to edit |
| adds, removes or renames a tool under `tools/` | [`tools/README.md`](tools/README.md) — its row and its own section |
| adds or removes a file only a person writes | `ProtectedPaths` (its description, its reason, and `Names`), the count in [`README.md`](README.md), [`skills/README.md`](skills/README.md), [`skills/draft/SKILL.md`](skills/draft/SKILL.md), the denial table in [`docs/guides/handling-refusals.md`](docs/guides/handling-refusals.md) and its row in [`docs/cheat-sheets/refusals.md`](docs/cheat-sheets/refusals.md), the table in [`docs/guides/adopting-the-framework.md`](docs/guides/adopting-the-framework.md), and `CONCEPT.md` §5.3 and §8 |
| moves the project boundary, an operation's shape, or where a new thing goes | [`src/README.md`](src/README.md) |
| adds or removes a supported host, or changes what one supports | the hosts table in [`README.md`](README.md), and `CONCEPT.md` D-46 if the reasoning moves |
| changes the layout, the build, the enforced properties, or what to keep current | this file |
| changes where the project stands, its version, or what is not done | the Where it stands section of [`README.md`](README.md) |

### Who owns what

**No document may restate what another owns.** The constraints live in `CONSTRAINTS.md` and are rendered
by `sdlc constraints`; the skills' surface in [`skills/README.md`](skills/README.md); every design
decision in `CONCEPT.md`, cited elsewhere rather than repeated; each directory's contents in that
directory's own index and nowhere else. **A new directory of documents gets its own `README.md`** —
describing it from a parent is the shape that drifts, because the parent is not what somebody opens when
they are already in the directory.

**[`README.md`](README.md) is for a person deciding whether to use this; this file is for an agent
changing it.** Product description, install, status and honest gaps belong there; the map, the build, the
enforced properties and the table above belong here. Neither grows a copy of the other.

**Two duplications are accepted, and both are paid for mechanically rather than promised.** The root
README's Where to read more table names every guide, cheat sheet and sample as well as their folders'
own indexes. And a cheat sheet is by nature a one-line copy of something a guide or `skills/README.md`
owns — so each one links every line to its owner, and the suite holds it to that owner: the skills
sheet against the skills on disk and their `argument-hint`, the refusals sheet against the refusals
guide's entries. A cheat sheet line with no owner to link to is a guide paragraph in the wrong place.

### What is mechanised, and what is not

`tests/Sdlc.Tests/PluginTests.cs` holds most of the above: the skills on disk are exactly the ones
`CONCEPT.md` inventories, every skill names a command the binary has, no skill restates a constraint, the
README's counts and version are true, every guide it points at exists, every path this file links to
exists with the capitalisation it states — and index-parity tests, which compare each index to its
directory in both directions (the skills, the three document folders, the tools), so an index naming
something gone fails as loudly as one missing something new.

**What no test holds is whether a sentence is still true.** The install guide's table claimed one hook for
as long as the turn-end hook had existed, while the verification step eleven paragraphs down said two and
called one a failed install. Both were countable; neither was counted. **If you are changing a surface,
read the document that describes it** rather than trusting the suite to have caught it.

This file carries nothing a cycle could make untrue — no status, no current work, no version, no test
counts. `README.md` holds where the project stands.
