#Requires -Version 5.1
<#
.SYNOPSIS
    Blocks a cycle on a charter intent: records the link, then discards the work built under the
    old rules.

.DESCRIPTION
    A CYCLE NEVER AMENDS THE CHARTER GOVERNING IT. It files a request and stops — the same reason a
    process cannot grant itself a permission. What it does instead was described in four steps and
    implemented in none of them:

      1. halt
      2. file a charter intent, carrying the evidence it just generated
      3. write `blocked-by: <charter intent id>` into its own intent
      4. discard its worktree
      5. write NO retro

    Steps 3 and 4 are this script. Step 2 is an ordinary intent with `source: charter-review`, and
    Find-SdlcCharterIntent.ps1 decides first whether one already exists — a batch of eight cycles
    hitting one bad invariant must file one request, not eight.

    STEP 5 IS WHY THIS REFUSES WHEN A RETRO EXISTS. A retro is the completion certificate: the
    lifecycle derivation reads "a retro exists" as `done`. Writing one for a blocked cycle makes
    work that never landed read as finished, which is the same defect as gitignoring retros arriving
    through a different door.

    BLOCKEDNESS IS DERIVED, NOT WRITTEN. `blocked-by` is a semantic link — the same category as
    `caused-by` and `superseded-by`, written because it is not derivable from anything. Blocked-NESS
    is then derived from it: the intent is blocked exactly while its `blocked-by` target is unmerged.
    Which means it SELF-CLEARS. The moment the charter intent merges, the link's target has landed,
    the intent is no longer blocked, and the dispatcher picks it up again. There is no unblock step
    and this script has no counterpart.

    THE WORKTREE AND THE BRANCH BOTH GO. The work was built under rules that are about to change, so
    resuming it later would be worse than re-running from spec — and the spec and plan live on that
    branch, so deleting it IS "re-run from spec". It also leaves the cycle reading as `queued` rather
    than `dead`, which is the honest state: nothing is half-finished, there is simply no plan any
    more.

    It refuses to discard a LOCKED worktree. A lock means an agent is still running in it, and
    killing someone else's agent is not this script's business.

.PARAMETER CycleId
    The cycle to block.

.PARAMETER BlockedBy
    The charter intent it waits on. Must already exist, pending or abandoned.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.NOTES
    Under -WhatIf, PowerShell prints its own "What if:" line before this script's JSON, and running
    a script as `powershell.exe -File` merges that host output into stdout. The JSON is always the
    LAST thing printed, so a caller reads from the first `{` — the same thing Test-SdlcArtifact.ps1
    does with a build line it cannot suppress either.

.EXAMPLE
    .\Block-SdlcCycle.ps1 -CycleId 20260926-a1b2 -BlockedBy 20260926-c3d4 -WhatIf
#>
# Deliberately NOT ConfirmImpact = 'High'. That setting prompts, and a prompt needs a host that can
# ask; invoked the documented way there is none, and ShouldProcess throws a null reference instead
# of saying so. -WhatIf stays, because previewing a deletion is worth having.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $CycleId,

    [Parameter(Mandatory = $true)]
    [string] $BlockedBy,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that terminates
# a JSON string. An abandoned charter intent's reason is prose.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

# Dot-sourced, not duplicated: git writes ordinary progress to stderr, and `2>&1` on a native
# command turns that into a terminating error under `Stop`. See SdlcGit.ps1.
. (Join-Path $PSScriptRoot 'SdlcGit.ps1')

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

$result = [ordered]@{
    'cycle-id'   = $CycleId
    'blocked-by' = $BlockedBy
    'blocker-state' = $null
    branch       = "cycle-$CycleId"
    'worktree-removed' = $false
    'branch-deleted'   = $false
    steps        = @()
    outcome      = 'not-started'
    errors       = @()
}

$steps = New-Object System.Collections.ArrayList

function Add-Step {
    param([string] $Name, [bool] $Ok, [string] $Detail)
    $null = $script:steps.Add([pscustomobject]@{ step = $Name; ok = $Ok; detail = $Detail })
}

function Write-Result {
    param([string] $Outcome, [string[]] $Errors, [int] $Exit)

    $result['steps'] = @($steps)
    $result['outcome'] = $Outcome
    $result['errors'] = @($Errors)
    ConvertTo-Json -InputObject $result -Depth 6
    exit $Exit
}

foreach ($pair in @(@{ name = 'CycleId'; value = $CycleId }, @{ name = 'BlockedBy'; value = $BlockedBy })) {
    if ($pair.value -notmatch '^[0-9]{8}-[0-9a-f]{4}$') {
        Write-Result 'refused-bad-id' @(
            "$($pair.name) '$($pair.value)' is not a cycle id. The form is yyyymmdd plus four hex characters.") 2
    }
}

if ($CycleId -eq $BlockedBy) {
    Write-Result 'refused-self-block' @(
        'a cycle cannot be blocked on itself; that is the self-amendment the rule forbids') 2
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$intentDir = Join-Path $RepoRoot '.sdlc/intents'
$abandonedDir = Join-Path $intentDir 'abandoned'

$intentPath = Join-Path $intentDir "$CycleId.md"
if (-not (Test-Path -LiteralPath $intentPath)) {
    Write-Result 'refused-no-intent' @("no intent at .sdlc/intents/$CycleId.md") 2
}

# THE BLOCKER MUST EXIST SOMEWHERE, and "somewhere" is the subtle part.
#
# Blockedness is derived from whether `blocked-by`'s target has MERGED, and the derivation reads
# merged as "the intent file is on master". So at the moment a cycle blocks, the charter intent it
# waits on is normally NOT on master at all -- it sits on its own `intent-<id>` branch with a pull
# request open. An earlier version of this script required the file to be present, which refused
# exactly the ordinary case and accepted only a blocker that had already landed, where the block
# would not hold for a moment.
#
# So a pending blocker is one with a branch, and a resolved one is a file -- landed in intents/ if it
# was approved, or in intents/abandoned/ if it was refused. Anything else is an id nobody filed, and
# `blocked-by` pointing at nothing NEVER resolves: the cycle would wait forever, which is the one
# outcome this state exists to prevent.
$blockerLanded = Test-Path -LiteralPath (Join-Path $intentDir "$BlockedBy.md")
$blockerRefused = Test-Path -LiteralPath (Join-Path $abandonedDir "$BlockedBy.md")

$blockerBranch = $false
Push-Location $RepoRoot
try {
    $refs = @((Invoke-SdlcGit for-each-ref '--format=%(refname:short)' refs/heads refs/remotes).stdout -split "`n" |
        ForEach-Object { $_.Trim() } | Where-Object { $_ })

    foreach ($ref in $refs) {
        if ($ref -eq "intent-$BlockedBy" -or $ref -like "*/intent-$BlockedBy") { $blockerBranch = $true }
    }
}
finally {
    Pop-Location
}

if (-not $blockerLanded -and -not $blockerRefused -and -not $blockerBranch) {
    Write-Result 'refused-no-blocker' @(
        "nothing filed as '$BlockedBy': no intent-$BlockedBy branch, and no file in .sdlc/intents/ " +
        'or .sdlc/intents/abandoned/. File the charter intent first, and run ' +
        'Find-SdlcCharterIntent.ps1 in case one already targets this rule.') 2
}

# Recorded, because it changes what happens next and a caller should not have to work it out. A
# PENDING blocker means the cycle waits and self-clears on the merge. A blocker that has already
# landed means the block will not hold for a moment: the decision is made, and a person has to act
# on it now rather than the cycle retrying -- especially when it landed in abandoned/, which says the
# WORK needs re-scoping rather than the charter.
$blockerState = 'pending'
if ($blockerLanded) { $blockerState = 'already-approved' }
elseif ($blockerRefused) { $blockerState = 'already-refused' }
$result['blocker-state'] = $blockerState

# A BLOCKED CYCLE WRITES NO RETRO. If one exists, this cycle landed or something wrote a
# certificate for work that did not — either way, blocking it now would leave the derivation
# reading `done` while the branch is deleted underneath it.
$retroPath = Join-Path $RepoRoot ".sdlc/cycles/$CycleId/retro.md"
if (Test-Path -LiteralPath $retroPath) {
    # No backtick-quoted words in a double-quoted string: there a backtick is the ESCAPE character,
    # so the quotes are silently eaten and the message renders without them.
    Write-Result 'refused-retro-exists' @(
        "a retro exists at .sdlc/cycles/$CycleId/retro.md, which the lifecycle derivation reads as " +
        'done. A blocked cycle writes no retro, so this cycle is not in a state to be blocked.') 2
}

$branch = "cycle-$CycleId"

Push-Location $RepoRoot
try {
    $current = (Invoke-SdlcGit rev-parse --abbrev-ref HEAD).stdout.Trim()

    if ($current -eq $branch) {
        # Deleting the branch you are standing on fails, and doing it from inside the worktree being
        # removed fails too. Refusing here says which, rather than half-completing.
        Write-Result 'refused-on-the-branch' @(
            "HEAD is on '$branch'. Run this from the main checkout: the branch and its worktree are " +
            'what is being discarded.') 2
    }

    if (-not $PSCmdlet.ShouldProcess($CycleId, "record blocked-by $BlockedBy, then discard $branch and its worktree")) {
        Write-Result 'whatif' @() 0
    }

    # --- 1. the link ---------------------------------------------------------

    $lines = @(Get-Content -LiteralPath $intentPath -Encoding UTF8)
    $original = [string] (Get-Content -LiteralPath $intentPath -Raw -Encoding UTF8)

    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') {
        Write-Result 'refused-no-frontmatter' @(
            "the intent at .sdlc/intents/$CycleId.md has no frontmatter block to record the link in") 2
    }

    $close = -1
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { $close = $i; break }
    }

    if ($close -lt 0) {
        Write-Result 'refused-no-frontmatter' @('the intent frontmatter block is never closed') 2
    }

    $existing = $null
    $existingAt = -1
    for ($i = 1; $i -lt $close; $i++) {
        if ($lines[$i] -match '^blocked-by:\s*(\S+)') {
            $existing = $Matches[1].Trim()
            $existingAt = $i
            break
        }
    }

    if ($existing -eq $BlockedBy) {
        Add-Step 'link' $true "already blocked-by $BlockedBy"
    }
    else {
        $updated = New-Object System.Collections.ArrayList
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($i -eq $existingAt) {
                $null = $updated.Add("blocked-by: $BlockedBy")
                continue
            }
            if ($i -eq $close) {
                if ($existingAt -lt 0) { $null = $updated.Add("blocked-by: $BlockedBy") }
                $null = $updated.Add($lines[$i])
                continue
            }
            $null = $updated.Add($lines[$i])
        }

        # UTF-8 without a BOM, like every other artifact this framework writes.
        [System.IO.File]::WriteAllText(
            $intentPath, (($updated -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $false))

        # THE SCHEMA RULES ON IT, rather than this script restating what a valid intent looks like.
        # A restore on refusal, because a half-written intent is worse than an unblocked cycle.
        $validator = Join-Path $PSScriptRoot 'Test-SdlcArtifact.ps1'
        if (Test-Path -LiteralPath $validator) {
            $verdictJson = (& $validator -Path $intentPath -RepoRoot $RepoRoot) -join "`n"
            $verdict = $null
            if ($verdictJson.Trim()) { $verdict = ConvertFrom-Json $verdictJson }

            if (($null -eq $verdict) -or (-not $verdict.ok)) {
                [System.IO.File]::WriteAllText(
                    $intentPath, $original, (New-Object System.Text.UTF8Encoding $false))

                $errors = @('the intent was restored unchanged')
                if ($null -ne $verdict) { $errors = @($verdict.errors) + $errors }
                Write-Result 'refused-schema' $errors 1
            }
        }

        $detail = "wrote blocked-by: $BlockedBy"
        if ($existingAt -ge 0) { $detail = "replaced blocked-by: $existing with $BlockedBy" }
        Add-Step 'link' $true $detail
    }

    # --- 2. discard the work -------------------------------------------------

    $worktrees = (Invoke-SdlcGit worktree list --porcelain).stdout
    $worktreePath = $null
    $locked = $false

    foreach ($block in ($worktrees -split "(`n`n|`r`n`r`n)")) {
        if ($block -notmatch ('(?m)^branch\s+refs/heads/' + [regex]::Escape($branch) + '\s*$')) { continue }
        if ($block -match '(?m)^worktree\s+(.+)$') { $worktreePath = $Matches[1].Trim() }
        # Anchored: an unanchored 'locked' also matches a worktree whose PATH contains the word.
        if ($block -match '(?m)^locked') { $locked = $true }
    }

    if ($worktreePath -and $locked) {
        Add-Step 'worktree' $false 'locked, so an agent is still running in it'
        Write-Result 'refused-worktree-locked' @(
            "the worktree for $branch is locked, which means an agent is still running there. The " +
            'link is recorded; discard the worktree once that agent has stopped.') 1
    }

    if ($worktreePath) {
        $removed = Invoke-SdlcGit worktree remove --force $worktreePath
        if (-not $removed.ok) {
            Add-Step 'worktree' $false $removed.stderr
            Write-Result 'failed-worktree-remove' @($removed.stderr) 1
        }
        $result['worktree-removed'] = $true
        Add-Step 'worktree' $true "removed $worktreePath"
    }
    else {
        Add-Step 'worktree' $true 'no worktree for this cycle'
    }

    $heads = @((Invoke-SdlcGit for-each-ref '--format=%(refname:short)' refs/heads).stdout -split "`n" |
        ForEach-Object { $_.Trim() } | Where-Object { $_ })

    if ($heads -contains $branch) {
        $deleted = Invoke-SdlcGit branch -D $branch
        if (-not $deleted.ok) {
            Add-Step 'branch' $false $deleted.stderr
            Write-Result 'failed-branch-delete' @($deleted.stderr) 1
        }
        $result['branch-deleted'] = $true
        Add-Step 'branch' $true "deleted $branch, so the cycle re-runs from spec"
    }
    else {
        Add-Step 'branch' $true 'no cycle branch to delete'
    }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "$CycleId is blocked on $BlockedBy." -ForegroundColor Yellow
Write-Host 'It self-clears: the moment that intent merges, the link resolves and the cycle is'
Write-Host 'picked up again. There is no unblock step.'
Write-Host ''
Write-Host 'Commit the intent edit to master — the dispatcher reads the link from there, and it is'
Write-Host 'a semantic link rather than work, so it carries no diff of its own.'

Write-Result 'blocked' @() 0
