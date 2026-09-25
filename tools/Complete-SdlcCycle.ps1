#Requires -Version 5.1
<#
.SYNOPSIS
    Lands one verified cycle: rebase, re-verify, merge. Serialized by design.

.DESCRIPTION
    Landing is the ONE serialization point in the whole design. Cycles are otherwise
    fully independent — a worktree each, no shared files, collision made structurally
    impossible by the harness rather than by a registry of claimed files. That earlier
    design (track which files each live plan claims, serialize on overlap) was itself a
    second list beside the filesystem, and worktrees removed the need for it rather than
    implementing it better.

    But worktrees do not prevent SEMANTIC collision. Cycle A renames a method, cycle B
    adds a caller in a different file; git merges both cleanly and the build breaks. That
    is only detectable after the rebase, which is why re-verifying is mandatory here
    rather than optional, and why landings happen one at a time.

    Sequence: rebase onto master, re-run verification, merge. If the post-rebase
    verification FAILS, the cycle drops back to in-progress — the world changed underneath
    it and there is real work to do again. It does not merge and it does not pass quietly.

    The merge itself is squashed. The artifacts already carry the narrative — spec.md,
    plan.md and verify.md are in the diff — so the commit graph need not duplicate it, and
    one cycle becoming one commit makes `git revert` of a cycle one operation. The retro is
    written on the branch BEFORE the merge, so it rides in with the squash: re-verify runs
    before merging, so everything the retro needs from the landing is available there, and
    the commit count stays at one.

.PARAMETER CycleId
    The cycle to land. Must be in state `awaiting-landing`.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Complete-SdlcCycle.ps1 -CycleId 20260924-a1b2 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
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

if (-not $RepoRoot) { $RepoRoot = (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$stateScript = Join-Path $PSScriptRoot 'Get-SdlcCycleState.ps1'
$states = @((& $stateScript -CycleId $CycleId -RepoRoot $RepoRoot) | ConvertFrom-Json)

if ($states.Count -eq 0) {
    throw "No intent found for '$CycleId'."
}

$state = $states[0]

if ($state.state -ne 'awaiting-landing') {
    throw "'$CycleId' is $($state.state): $($state.reason). Only an awaiting-landing cycle can be landed."
}

$branch = $state.branch
$result = [ordered]@{
    'cycle-id' = $CycleId
    branch     = $branch
    steps      = New-Object System.Collections.ArrayList
    outcome    = 'not-started'
}

function Add-Step {
    param([string] $Name, [bool] $Ok, [string] $Detail)
    $null = $result['steps'].Add([pscustomobject]@{ step = $Name; ok = $Ok; detail = $Detail })
}

Push-Location $RepoRoot
try {
    $currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()

    if (-not $PSCmdlet.ShouldProcess($CycleId, "rebase $branch onto master, re-verify, then squash-merge")) {
        $result['outcome'] = 'skipped'
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # --- 1. rebase ---------------------------------------------------------

    git checkout $branch 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Step 'checkout' $false "could not check out $branch"
        $result['outcome'] = 'failed-checkout'
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    git rebase master 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        # A conflict here is the ordinary consequence of another cycle landing first. It
        # is a human's to resolve, and the rebase is left in progress deliberately so the
        # working state is the one they need.
        Add-Step 'rebase' $false 'conflict — left in progress for a person to resolve'
        $result['outcome'] = 'conflict'
        Write-Host 'Rebase conflict. Resolve it, then run this again.' -ForegroundColor Yellow
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }
    Add-Step 'rebase' $true 'onto master'

    # --- 2. re-verify ------------------------------------------------------
    #
    # Mandatory, not optional. This is what catches semantic collision: the cycle
    # verified against a tree that has since moved.

    dotnet test 2>&1 | Out-Null
    $testsPass = ($LASTEXITCODE -eq 0)
    Add-Step 're-verify' $testsPass $(if ($testsPass) { 'tests pass after rebase' } else { 'tests FAIL after rebase' })

    if (-not $testsPass) {
        $result['outcome'] = 'reverify-failed'
        Write-Host '' -ForegroundColor Red
        Write-Host 'Tests fail after the rebase. The world changed underneath this cycle.' -ForegroundColor Red
        Write-Host 'It drops back to in-progress: there is real work to do, and it will not' -ForegroundColor Red
        Write-Host 'merge on the strength of a verification that is now out of date.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # --- 3. unresolved suggestions become intake ---------------------------
    #
    # Written HERE, on the cycle branch, before the squash, so the file rides in with the
    # merge exactly as the retro does. Written after re-verification rather than before,
    # because a cycle that fails re-verify is not landing and its suggestions are not yet
    # intake.
    #
    # ONE FILE PER CYCLE, never a shared backlog: a single file every cycle appends to
    # conflicts the way plugin.json does under concurrent cycles, which is a guaranteed
    # collision rather than a possible one.
    $findingsPath = Join-Path $RepoRoot ".sdlc/cycles/$CycleId/findings.json"
    if (Test-Path -LiteralPath $findingsPath) {
        $formatScript = Join-Path $PSScriptRoot 'Format-SdlcFindings.ps1'
        $suggestionsRel = ".sdlc/suggestions/$CycleId.md"

        & $formatScript -Path $findingsPath -State 'suggested' `
            -OutFile $suggestionsRel -RepoRoot $RepoRoot | Out-Null

        git add -- $suggestionsRel 2>&1 | Out-Null
        # --quiet so an empty suggestion set is not an error: a cycle with nothing
        # suggested is a good cycle, not a failed landing.
        git diff --cached --quiet
        if ($LASTEXITCODE -ne 0) {
            git commit -m "$CycleId`: unresolved suggestions" 2>&1 | Out-Null
            Add-Step 'suggestions' $true "wrote $suggestionsRel"
        }
        else {
            Add-Step 'suggestions' $true 'nothing suggested, nothing to record'
        }
    }
    else {
        Add-Step 'suggestions' $true 'no findings document for this cycle'
    }

    # --- 4. squash-merge ---------------------------------------------------

    $title = (git log -1 --format='%s' $branch).Trim()

    git checkout master 2>&1 | Out-Null
    git merge --squash $branch 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Step 'merge' $false 'squash-merge failed'
        $result['outcome'] = 'failed-merge'
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    git commit -m "$CycleId`: $title" 2>&1 | Out-Null
    Add-Step 'merge' $true "squashed into one commit on master"

    $result['outcome'] = 'landed'

    Write-Host ''
    Write-Host "$CycleId landed." -ForegroundColor Green
    Write-Host 'The retro rode in with the squash, so this cycle now reads as done.'
    Write-Host 'Unresolved suggestions landed as .sdlc/suggestions/' + "$CycleId.md" + ', for triage'
    Write-Host 'on your own schedule. Promoting one means filing an ordinary intent with'
    Write-Host '`source: suggestion`.'
    Write-Host ''
    Write-Host 'Note: plugin.json is bumped when the `release` ref moves, not here. Nobody'
    Write-Host 'consumes master, so a bump at merge would signal nothing.'
}
finally {
    Pop-Location
}

ConvertTo-Json -InputObject $result -Depth 6
