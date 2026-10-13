# The tools

Two things that support changing the framework and are not part of it. **Neither ships.** The harness is
`IsPackable=false`, the script is not installed anywhere, and nothing a governed solution runs reaches
either of them.

They exist for the same reason: **a claim this repository's own suite cannot check.** The tests assert
strings and types, so they cannot tell whether a generated solution compiles or whether a skill obeyed
its output cap. Each tool turns one of those into something somebody can run.

| Tool | What it checks | Run it |
|---|---|---|
| [`Sdlc.ScaffoldHarness/`](Sdlc.ScaffoldHarness/) | that the scaffold templates generate a solution which builds and tests clean | after changing any template — and CI runs it on every pull request |
| [`Measure-SkillOutput.ps1`](Measure-SkillOutput.ps1) | how much prose each skill actually wrote when it handed back, against the cap it states | after a trial cycle, against the trial repository |

---

## `Sdlc.ScaffoldHarness/`

**Writes what a governed solution looks like on its first cycle, so that `dotnet build` and `dotnet test`
can be run against it.** A console project named `scaffold-harness`, referencing `Sdlc.Core` so it writes
the same templates `sdlc scaffold` does.

```pwsh
dotnet run --project tools/Sdlc.ScaffoldHarness -- <empty-directory> [solution-name]
dotnet build <empty-directory>/Demo.slnx
dotnet test  <empty-directory>/Demo.slnx
```

**The gap it closes: every test in `Sdlc.Tests` asserts strings in the templates, so a template that
generates code which does not compile passes the whole suite.** That is not hypothetical — one change
shipped a `--` inside an XML comment, which is illegal and kills the generated solution's first build,
and then a CA1859 that warnings-as-errors turns into an error. Neither was visible until a solution was
generated and built by hand.

It writes both halves, because neither compiles without the other: the build settings, which carry the
analyzer policy and the package versions, and the skeleton. `sdlc scaffold` writes both behind an
approved plan. It **refuses a directory that is not empty** rather than merging into one — a harness that
wrote half a solution over existing files would produce a build failure nobody could attribute.

`.github/workflows/ci.yml` runs exactly this on every pull request, so a broken template fails there
rather than in somebody's repository. **Run it locally anyway when you change a template** — the pipeline
is a backstop, not the inner loop. A test holds that the pipeline still does it, because a step in a YAML
file is deletable and deleting it is silent.

## `Measure-SkillOutput.ps1`

**Measures how much prose each skill invocation returned, from a Claude Code transcript.** Windows
PowerShell 5.1, like everything else shipped here.

```pwsh
.\tools\Measure-SkillOutput.ps1 -Repo C:\Repos\dice-roll-rev-5
.\tools\Measure-SkillOutput.ps1 -Repo C:\Repos\dice-roll-rev-5 -All
```

The skills cap what a stage may say when it hands back. **A cap is a claim about output, so it is
checkable — but only against a real run**, because no unit test can judge what a model will write. This
reads a session transcript and counts the assistant's own text per invocation: how many separate blocks it
wrote while working, how many words in total, and how many in the final hand-back, which is the one the
caps govern.

Compare the `LastWords` column to the cap the skill states. **A stage over its cap is the finding; a stage
far under it usually means the licence moved somewhere the cap does not reach**, which is worth looking at
too. Mid-stage blocks are counted but uncapped by design — one line per step is the rule, and a build that
touches thirty files legitimately says thirty things, so the number to watch there is words per block.

Two limits it states about itself. A text block is attributed to the most recent `/sdlc:` invocation
before it, so plain conversation after a stage is counted against that stage. And the transcript format is
Claude Code's rather than a contract — **if a future version moves the fields this reports zero invocations
rather than wrong numbers**, which is the failure worth preferring.

Pass `-Transcript` for a specific session rather than the newest; without it, the newest transcript that
actually invoked a skill is read, because a repository accumulates sessions that never ran one.
