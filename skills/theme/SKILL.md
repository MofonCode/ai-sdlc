---
name: theme
description: Show the solution's theme -- the roles its surfaces use (a cancel button's colour, a heading's typeface) and the values that fill them -- or, when it has none, the skeleton of roles for a person to fill. Never proposes a value. Use when someone asks what colours or fonts the solution uses, or needs a theme.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *)
---

# Show the theme

```
sdlc theme
```

Show what it prints verbatim.

**Roles, not values.** `cancel` is a role; `blue-400` is a value wearing a role's clothes. A value changes in a
rebrand and a role does not, and a stage looks a role up rather than guessing.

**It never proposes a colour.** For a solution with no theme it prints the skeleton of roles for a person to fill;
for one that has a theme it shows each role and its value, `--` where a role is empty. A colour is a brand's or a
designer's: do not fill a value in yourself, and do not offer to.

Three things to say when you show a skeleton:

- **Import the file; never copy values out of it.** Two places the truth lives means the stale one gets trusted.
- **Add the roles this solution needs and delete the ones it does not.** A long skeleton is a guess with the
  authority of a default.
- **Set `"settled": true` once somebody has decided**, which separates a theme somebody chose from one nobody
  filled in — including a role deliberately left empty.

**`.sdlc/theme.json` is not protected.** Nobody is judged against a colour, so a cycle may declare the file and
change a value as declared work. **Enforcement is a linter's**, not a constraint's: a colour or typeface outside the
theme is mechanical, so it belongs below a constraint, where it can fail a build.
