---
slug: stack-dotnet-house-stack
status: approved
tier: task
spec: none
slices:
  - id: pack-skill
    files: ["plugins/stack-dotnet/skills/stack-dotnet/SKILL.md", "plugins/stack-dotnet/.claude-plugin/plugin.json"]
    requirements: [C1, C2, C3, C4, C5, C6, C7, C9]
    depends_on: []
  - id: pack-packages
    files: ["plugins/stack-dotnet/skills/stack-dotnet/assets/Directory.Packages.props"]
    requirements: [C8]
    depends_on: []
---

# plan — record the house stack in the .NET pack

**Tier: `task`.** The stack was decided by the owner and stated directly; there is no problem
to interrogate and no design to choose, so there is no `intent.md` and no `spec.md`. What is
left is writing down commitments that already exist, which is exactly what task tier is for.
Requirements come from the conversation rather than R-numbers, and are listed below as C1-C8
so the slices can cite something.

## Commitments to record

- **C1 — Web and API.** Blazor for front ends, ASP.NET Core for APIs.
- **C2 — Data access.** Entity Framework Core, with migrations already exempted in the
  existing style config and coverage exclusions.
- **C3 — Local orchestration.** Aspire, including container-based emulation of cloud
  dependencies for local development.
- **C4 — Deployed infrastructure is PaaS, not containers.** App Service, Function Apps, Azure
  SQL. Containers are a local-development mechanism only.
- **C5 — Terraform is the single source of truth for anything deployed.** The AppHost resource
  graph describes local development and never production. This is the invariant that stops a
  team spending a week trying to reconcile `azd` with Terraform, and it belongs in the charter
  of every repo built with this pack.
- **C6 — Scripts and notebooks are exploration only.** Polyglot Notebooks (VS Code) is the
  committed frontend, over `.ipynb` on the .NET Interactive kernel — the format is standard
  Jupyter, so nothing is locked in, and the frontend is chosen because JupyterLab would drag
  Python onto every machine. Neither `.csx` nor a notebook has a project file, so
  `Directory.Build.props` never reaches them: no analyzers, no style enforcement, no
  warnings-as-errors. Nothing graduates to a deliverable by being copied; it moves into a
  project and passes the same gate as everything else.
- **C7 — Validation is the analyzers and the style config, promoted to errors.** Already true
  in the assets; state it as the pack's position rather than leaving it implicit in XML.
- **C8 — Package versions** for the EF Core, ASP.NET and Blazor packages the above implies,
  centrally managed like everything else.
- **C9 — Pack version and description** bumped to match, because plugins are cached per
  version and skipping the bump makes an update a silent no-op.

## Work order

Both slices have no unmet dependency and run concurrently.

1. `pack-skill` — extend the pack skill with C1-C7: profile table rows, a section on the
   PaaS/Terraform split and why it is an invariant rather than a preference, and a section on
   the scripts/notebooks validation gap.
2. `pack-packages` — add the package versions for C8.

## Files

Exactly as the manifest above. No engine file is touched: a pack revision that required an
engine change would violate charter invariant I5.

## Validation

**Nothing is committed until the build is green and every test passes, with the real output
pasted — not a claim that it passed.** That applies here even though this slice is
instructions rather than code, because the instructions are what the tests read.

| Gate | Command | Must show |
|---|---|---|
| Build + all tests | `dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj` | 49/49 passing, 0 failed |
| Manifest is safe to fan out | `Assert-SliceManifest.ps1 -Plan <this file>` | disjoint, with the wave plan |

Specific to this change: charter invariant I1 must still hold — none of this stack leaks into
`plugins/sdlc-core/`. That is `CharterInvariantI1_EngineImposesNoStack`, which runs as part of
the suite above rather than as a separate manual grep.

There are no integration tests in this repository: it ships instructions, not a running
system. A repo built *with* this pack has both, and the pack states the commands.

## Risks

- **The pack becomes a wall of text nobody reads.** Early warning: a section that states a
  commitment without saying what goes wrong if you ignore it. Those are the ones that get
  skimmed. Every addition names its failure mode.
- **C4/C5 read as a preference rather than an invariant**, and a future repo tries to make the
  AppHost graph authoritative for production. Early warning: a spec proposing `azd` for
  deployment. Mitigated by stating it as a charter line the pack tells `init` to write.

## Rollback

`git revert` the commit. The pack is instructions; nothing is persisted and no repo already
scaffolded is affected, because `init` never overwrites an existing file.
