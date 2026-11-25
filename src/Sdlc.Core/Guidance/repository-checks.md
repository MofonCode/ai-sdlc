---
name: repository-checks
files: "Architecture, Fitness"
---

# Writing a check that reads the repository

How the framework would write a test that holds a property of the repository itself — a layering rule, a naming
rule, a file every project must carry — where the solution has not said otherwise.

## What is not opinion

A check like this is still a test the plan's proof names, and the verification reads its recorded outcome like any
other. Nothing here lets a check skip, or pass where it cannot read what it was asked to read.

## The defaults

- **A check never seen to fail proves nothing.** The repository already holds the property when the check is
  written, so a check that passes on it passes whether it works or not. Run it on altered copies as well — a
  fixture, a temporary repository, or the text as it read at the cycle's base — and assert that each copy fails.
- **The check takes what it reads and returns each failure.** A method over the files or types it was handed,
  rather than one that finds them itself and asserts, so the same check runs over the repository and over each
  copy, and a copy's test can assert the failure's message.
- **A failure names what it read and where.** The file, the type or the line, so a person fixing it is not left to
  search. A check that found nothing to read fails, and says so, rather than passing on nothing.
- **One altered copy per way the property can break.** A layering rule that a reference and a using directive can
  each break gets a copy of each.
