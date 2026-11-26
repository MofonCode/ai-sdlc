---
name: close-cycle
description: Close the harvested cycles -- check that every aspect of every completed cycle has been answered and every draft placed or refused, commit the closed cycles' removal on a branch of its own, push it and open the pull request into the integration branch a person merges. Use once a harvest has answered every aspect, or when somebody asks to clear out old cycles.
compatibility: Requires the sdlc binary on PATH, from the MofonCode.Sdlc.Cli dotnet tool, and a git repository. Every command here is that binary, so without it the first step fails.
argument-hint: ""
allowed-tools: Bash(sdlc *), Bash(git *), Bash(gh *), Bash(az *), Read, Write
---

# Close the harvested cycles

```
sdlc harvest close
```

**A cycle closes only once there is nothing left it can give** (D-192): every aspect — constraints, decisions,
suggestions, stack, areas — answered for it, and every draft placed or deleted. The binary decides that, from the
ledger and the files, so nothing here judges it.

## 1. Close

Run it **on the integration branch**, where the cycles merged and the drafts were placed.

- **It refuses while anything is left**, naming each cycle and the aspects it still needs, or the draft still beside
  its file. Say what is missing in one line and stop: the aspect is the harvest skill's, the rename the person's.
- **Otherwise** it records each closed cycle in `.sdlc/harvested.json` — its merge commit, and what later harvests
  read — and on a new `harvest/<date>` branch commits the removal of their folders with the ledger, the notes for
  the framework and the records the person placed. Every folder is still in git at the merge the ledger names. A cycle abandoned before any of it
  merged was read from its own branch: its folder is deleted from the working tree, the ledger names its branch's
  last commit, and the output names the branch.

## 2. Push and open the pull request

The person asked to close, which is their word to push this branch. `.sdlc/solution.md` records the platform; if it
says `local`, or `git remote -v` prints nothing, stop and say there is nowhere to push.

**Then ask whether the branch has taken the latest integration branch** (D-227), on the `harvest/<date>` branch:

```
sdlc up-to-date
```

It fetches first. The branch changes only records, so when it is behind and says the merge is clean, run the merge
it prints and ask again. When the merge stops on a conflict — the ledger, or a cycle somebody merged meanwhile —
`git merge --abort`, show the refusal and stop; and when the target could not be fetched, show that and stop.

```
git push -u origin <the branch it named>
```

Open the pull request into the integration branch so that merging it deletes the branch — `gh pr create --base
<integration> --fill` on GitHub, `az repos pr create --target-branch <integration> --delete-source-branch true` on
Azure DevOps. The body says which cycles closed and that every aspect was answered for each. **The check admits it as
a harvest**: it carries only the records, the ledger, successors and the removed folders.

## Hand back

One line: which cycles closed, the pull request's link, and any cycle that stays open with what it still needs. When
the output names branches closed cycles were read from, add that `sdlc tidy` lists them for deletion once the pull
request merges.
