# {{SOLUTION}}

A .NET solution scaffolded by `/sdlc-core:init` with the `stack-dotnet` pack, competition kind:
a feature-and-scoring library, a training runner, a unit suite, and a starter notebook for
exploration.

Replace this paragraph with which competition this is and what the target metric is.
Everything below it is the pack's, and stays true whoever owns the repository.

## Layout

```
{{SOLUTION}}.slnx
src/{{SOLUTION}}.Features/    feature engineering and scoring - the code that has to be right
src/{{SOLUTION}}.Training/    the runner that trains and writes a submission
tests/{{SOLUTION}}.UnitTests/ one class at a time
notebooks/{{SOLUTION}}.ipynb  exploration only
terraform/                    every deployed resource
.sdlc/                        the artifact chain
```

## Build

```
dotnet build
```

Warnings are errors here, and `EnforceCodeStyleInBuild` pulls the `.editorconfig` style rules
into the same gate, so **the build is the first test**. A nullable warning, a style violation
and a moderate-or-worse NuGet advisory each fail it. Fix the finding rather than adding a
`NoWarn`.

## Test

```
dotnet test
dotnet test --settings coverage.runsettings
```

**Read the count, not the colour.** A run that collected zero tests exits clean. A renamed or
mis-targeted test project reports success by finding nothing, which is why the suite ships
with a starter test that asserts something real about this solution.

A scoring or validation-split bug is the failure this suite exists to catch, because it does
not look like a failure: it produces a number, the number looks plausible, and it is wrong.
Test the split and the metric before trusting either.

## Run locally

```
dotnet run --project src/{{SOLUTION}}.Training
```

## The notebook

`notebooks/{{SOLUTION}}.ipynb` runs on the .NET Interactive kernel - Polyglot Notebooks in VS
Code, or any Jupyter frontend with that kernel registered. It ships as headings only.

A notebook has no project file, so `Directory.Build.props` never reaches it: **no analyzers,
no `EnforceCodeStyleInBuild`, no warnings-as-errors.** The gate the rest of this solution
relies on is silently absent there.

> **Nothing graduates from the notebook to a deliverable by being copied.** It moves into
> `src/{{SOLUTION}}.Features/`, compiles under the same analyzers as everything else, and gets
> tests. Until then it is a sketch, regardless of how finished it looks.

## The artifact chain

`.sdlc/` holds the SDLC artifacts, and it is where a change starts rather than in the code:

- `.sdlc/CHARTER.md` - what this repository is, what it commits to, and the invariants a
  change may not break. It is read before a spec is written and it is the thing an intent is
  argued against.
- `.sdlc/<slug>/` - one directory per change, holding `intent.md`, `spec.md` and `plan.md`.
  Each carries a `status:` field, and the next stage reads that field from the file rather
  than trusting that approval happened.

## The invariant this solution cannot break

> **Terraform is the single source of truth for anything deployed.** A resource that exists
> only in a script, a portal click or a local run is not deployed. Nothing in this solution
> needs cloud infrastructure to train or to score, so the honest default is that `terraform/`
> stays empty until something is genuinely deployed.
