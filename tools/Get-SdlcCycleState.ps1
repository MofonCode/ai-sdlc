#Requires -Version 5.1
<#
.SYNOPSIS
    Derives the lifecycle state of every intent from git and the filesystem.

.DESCRIPTION
    No intent carries a `status:` field. State is DERIVED, because a status field beside
    git is a second list with nothing binding the two — and the derivation below is the
    binding. Seven states, each from evidence that already exists:

      queued            no approved plan yet, so the supervised phase owes it work
      planned           plan.md carries `approved:`, no branch yet — READY TO BATCH
      in-progress       cycle branch exists, worktree LOCKED, no verify.md
      dead              cycle branch exists, worktree UNLOCKED, no verify.md
      awaiting-landing  cycle branch exists, verify.md present
      blocked           blocked-by names a charter intent that has not merged
      done              a retro exists
      abandoned         the intent file sits under intents/abandoned/

    Three of those are worth the explanation:

    QUEUED vs PLANNED, and this is the one that gates unattended work. Only `planned` is
    dispatchable. Intent, spec and plan are supervised because they need judgment and may
    need to ask the user something; build is the execution of a contract a person already
    approved. The `approved:` field in the plan frontmatter IS that contract, so a plan
    without it is not batchable however complete it looks. Without this distinction a
    dispatcher would pick up an intent with no plan at all and try to build nothing.

    DEAD vs IN-PROGRESS. Claude Code holds a `git worktree lock` while an agent runs and
    releases it when the agent finishes. Without that distinction a cycle killed by
    resource exhaustion is indistinguishable from one still working, so it would sit
    forever while the dispatcher refuses to touch it — and with no concurrency cap,
    exhaustion is the likelier failure.

    DONE, from the retro. The retro is the completion certificate: it is written only at
    landing, and the retro workflow refuses to write one for a cycle that did not pass.
    A naive derivation — "on master with no branch" — also matches a FINISHED intent,
    because the branch is gone once it merges, so an unsupervised dispatcher reading that
    queue would relaunch every completed cycle forever.

.PARAMETER CycleId
    One cycle id. Omit to report every intent.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Get-SdlcCycleState.ps1 | Where-Object { $_.state -eq 'queued' }
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $CycleId,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install. An em-dash then leaves this script as a CP1252 best-fit
# substitution, and U+201D best-fits to a plain double quote — which does not merely look
# wrong, it TERMINATES the JSON string and the caller receives a structurally invalid
# document. Every governed artifact in this repository contains em-dashes, so this is the
# normal case rather than an edge one. UTF8Encoding($false) rather than
# [System.Text.Encoding]::UTF8 because the latter prepends a BOM to the output stream,
# which is not valid at the start of a JSON document.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

function Resolve-RepoRoot {
    param([string] $Start)

    $dir = (Resolve-Path -LiteralPath $Start).Path
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) { return $dir }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }

    throw "No git repository found at or above '$Start'."
}

function Get-FrontmatterField {
    param([string] $Path, [string] $Field)

    if (-not (Test-Path -LiteralPath $Path)) { return $null }

    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $null }

    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match ('^' + [regex]::Escape($Field) + ':\s*(.+)$')) {
            return $Matches[1].Trim()
        }
    }

    return $null
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$intentDir = Join-Path $RepoRoot '.sdlc/intents'
$abandonedDir = Join-Path $intentDir 'abandoned'
$cyclesDir = Join-Path $RepoRoot '.sdlc/cycles'

Push-Location $RepoRoot
try {
    $branches = @(git for-each-ref --format='%(refname:short)' refs/heads)
    $mergedBranches = @(git branch --format='%(refname:short)' --merged HEAD)

    # `git worktree list --porcelain` emits a `locked` line for a locked worktree. Claude
    # Code holds that lock for the lifetime of the agent using the worktree.
    $worktreeBlocks = (git worktree list --porcelain) -join "`n"
}
finally {
    Pop-Location
}

# THE BRANCH IS THE LINK, NOT THE DIRECTORY NAME. This collected only the worktree
# directory's leaf and compared it to `cycle-<id>` below, so `in-progress` held only while
# every worktree happened to be named after its branch. `Invoke-SdlcRun.ps1` does name them
# that way, which is why it worked and why nothing noticed.
#
# The failure it hid is not cosmetic. A worktree for the same branch at any other path reads
# as `dead` - and `dead` means "relaunch or discard", so an unattended dispatcher could start
# a second agent on a branch someone is actively working in. That double launch is the exact
# thing this state exists to prevent.
#
# `git worktree list --porcelain` states the branch outright, so use it. The directory leaf
# stays as a fallback for a detached-HEAD worktree, which has no branch line at all.
$lockedWorktrees = @()
foreach ($block in ($worktreeBlocks -split "(`n`n|`r`n`r`n)")) {
    # Anchored: an unanchored 'locked' also matches a worktree whose PATH contains the word.
    if ($block -notmatch '(?m)^locked') { continue }

    if ($block -match '(?m)^branch\s+refs/heads/(.+)$') {
        $lockedWorktrees += $Matches[1].Trim()
    }

    if ($block -match '(?m)^worktree\s+(.+)$') {
        $lockedWorktrees += (Split-Path -Leaf $Matches[1].Trim())
    }
}

$intentFiles = @()
if (Test-Path -LiteralPath $intentDir) {
    $intentFiles += @(Get-ChildItem -LiteralPath $intentDir -Filter '*.md' -File)
}
$abandonedFiles = @()
if (Test-Path -LiteralPath $abandonedDir) {
    $abandonedFiles += @(Get-ChildItem -LiteralPath $abandonedDir -Filter '*.md' -File)
}

$results = New-Object System.Collections.ArrayList

foreach ($entry in @($intentFiles + $abandonedFiles)) {
    $id = [System.IO.Path]::GetFileNameWithoutExtension($entry.Name)
    if ($CycleId -and $id -ne $CycleId) { continue }

    $isAbandoned = $entry.FullName.StartsWith($abandonedDir, [System.StringComparison]::OrdinalIgnoreCase)
    $branch = "cycle-$id"
    $branchExists = $branches -contains $branch
    $branchMerged = $mergedBranches -contains $branch
    $worktreeLocked = $lockedWorktrees -contains $branch

    $cycleDir = Join-Path $cyclesDir $id
    $verifyPath = Join-Path $cycleDir 'verify.md'
    $retroPath = Join-Path $cycleDir 'retro.md'
    $hasVerify = Test-Path -LiteralPath $verifyPath
    $hasRetro = Test-Path -LiteralPath $retroPath

    $planPath = Join-Path $cycleDir 'plan.md'
    $hasPlan = Test-Path -LiteralPath $planPath
    # Presence is the gate, so the value must be non-empty to count: a plan whose
    # `approved:` line exists but is blank has not been approved by anyone.
    $planApproved = $false
    if ($hasPlan) {
        $approvedAt = Get-FrontmatterField -Path $planPath -Field 'approved'
        $planApproved = [bool]$approvedAt
    }

    $blockedBy = Get-FrontmatterField -Path $entry.FullName -Field 'blocked-by'
    $blockerUnresolved = $false
    if ($blockedBy) {
        # A charter intent's PR merges either way — approved with the charter edit, or
        # rejected into intents/abandoned/ with a reason. So `blocked-by` ALWAYS resolves,
        # and nothing can be blocked on something that never merges. Where the target
        # landed is the answer.
        $blockerLanded = (Test-Path -LiteralPath (Join-Path $intentDir "$blockedBy.md")) -or
                         (Test-Path -LiteralPath (Join-Path $abandonedDir "$blockedBy.md"))
        $blockerUnresolved = -not $blockerLanded
    }

    # Order matters: the first matching condition wins, most-final first.
    $state = 'queued'
    $reason = 'no approved plan yet — the supervised phase owes this a spec and a plan'

    if ($isAbandoned) {
        $state = 'abandoned'
        $reason = 'the intent file sits under intents/abandoned/'
    }
    elseif ($hasRetro) {
        $state = 'done'
        $reason = 'a retro exists — the completion certificate'
    }
    elseif ($blockerUnresolved) {
        $state = 'blocked'
        $reason = "blocked-by $blockedBy, which has not merged"
    }
    elseif ($branchExists -and $hasVerify) {
        $state = 'awaiting-landing'
        $reason = 'verified on its branch; the merge gate is human'
    }
    elseif ($branchExists -and $worktreeLocked) {
        $state = 'in-progress'
        $reason = 'worktree locked — an agent is running'
    }
    elseif ($branchExists) {
        $state = 'dead'
        $reason = 'branch exists, worktree unlocked, no verify.md — relaunch or discard'
    }
    elseif ($planApproved) {
        $state = 'planned'
        $reason = 'plan approved by a person — eligible for an unattended build'
    }

    $null = $results.Add([pscustomobject]@{
        'cycle-id'         = $id
        state              = $state
        reason             = $reason
        'intent-path'      = ($entry.FullName.Substring($RepoRoot.Length).TrimStart('\', '/') -replace '\\', '/')
        branch             = $branch
        'branch-exists'    = $branchExists
        'branch-merged'    = $branchMerged
        'worktree-locked'  = $worktreeLocked
        'has-plan'         = $hasPlan
        'plan-approved'    = $planApproved
        'has-verify'       = $hasVerify
        'has-retro'        = $hasRetro
        'blocked-by'       = $blockedBy
        dispatchable       = ($state -eq 'planned')
    })
}

# An empty result must still be valid JSON. ConvertTo-Json on an empty collection emits
# nothing in 5.1, and a caller parsing nothing fails in a way that looks like a bug rather
# than like an empty queue — which is the commonest state of all.
if ($results.Count -eq 0) {
    '[]'
}
else {
    ConvertTo-Json -InputObject @($results) -Depth 6
}
