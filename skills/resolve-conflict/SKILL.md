---
name: resolve-conflict
description: Resolve a collision on one of the few files no cycle can claim exclusively -- project files, DI registration, appsettings, migrations, Terraform. Use when a batch run reports a conflict on one of those.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: "<file path>"
allowed-tools: Bash(sdlc *), Bash(git *), Bash(dotnet *), Read, Write, Edit, Glob, Grep
---

# Resolve a collision on a shared file

Most collisions are scheduled around rather than resolved: plans declare the files they claim, so a batch
knows before it builds and serialises what overlaps. This is for the small set of files that **cannot**
be claimed exclusively, because almost every cycle touches them.

## The list, and why it is a list

| File | Why every cycle touches it |
|---|---|
| `*.csproj`, `Directory.Packages.props` | a new dependency |
| dependency-injection registration | a new service |
| `appsettings*.json` | a new setting |
| EF Core migrations | a new model change |
| Terraform files near state | a new resource |

**Enumerated rather than discovered.** A skill that resolved whatever happened to conflict would quietly
become the thing that merges real code, and the moment it does, scheduling around collisions stops being
the strategy. If a conflict is on a file not in this list, that is a scheduling failure to report, not a
merge to perform.

## What resolution means here

These files are **additive by nature**, which is why they can be resolved at all. Two cycles each add a
package reference, a registration, a setting. The resolution is to keep both.

| Shape | Resolution |
|---|---|
| two package references | keep both, one version each, alphabetical |
| two service registrations | keep both, in the order the file already uses |
| two settings | keep both. If they are the same key with different values, **stop** |
| two resources | keep both |

**The same key with two different values is not additive, and not yours.** Two cycles disagreeing about
one setting is two plans disagreeing about the solution, which is a person's to settle. Say which cycles,
which key, and which values, then stop.

## Migrations are different, and do not come here

Two branches that each add an EF Core migration snapshot the same parent model. Merging the two chains
produces something that compiles and then fails at deploy — the worst shape a failure takes, because
every local check is green.

**The resolution is not a merge.** Take one, rebase the other on it, and regenerate the second migration
against the new model. A batch run should have serialised them; if one reached here, the serialisation is
what to fix.

## After resolving

```
dotnet build
dotnet test
```

Both cycles' tests, not one. A resolution that satisfies the file and breaks a cycle's proof has moved the
problem rather than solved it.

Then re-record the evidence for each cycle whose files you touched, because the diff changed:

```
sdlc evidence <id> --tests <results>
```

## Report

Which file, which cycles, what you kept from each, and anything you declined to resolve. Then stop.

**Do not land anything from here.** Landing is a decision, and a conflict resolution is an odd moment to
have somebody make one.
