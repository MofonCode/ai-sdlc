#Requires -Version 5.1
<#
.SYNOPSIS
    Runs approved intents unattended, one worktree each, after a confirmation.

.DESCRIPTION
    There is NO CONCURRENCY CAP. A cap is the engine guessing at a resource question it
    cannot know — this machine, what else is running, how much disk is free. Informed
    consent at launch beats an invented ceiling, and it is consistent with the rest of
    the design: gates are human decision points, not automated limits.

    So the control is the confirmation, and what it shows is the WORKTREE COUNT, because
    worktree count predicts the thing most likely to bite. Each worktree is a fresh
    checkout with its own restore and build; eight of them against an Aspire graph is
    real disk and real minutes, and that is the number worth seeing before saying yes.

    One command rather than two. Single and batch are the same operation with a different
    N — the landing phase serializes when N > 1 and trivially when N = 1, but that is a
    property of landing rather than a second mode. The difference that matters to a person
    is visible in the confirmation, which is where it belongs.

    Nothing irreversible happens before the plan: spec and plan are documents, and only
    build touches code. So a run proceeds unattended to the plan and halts there at tier
    2 or 3, which is why the blast-radius tier gates the build rather than the launch.

.PARAMETER Intent
    Cycle ids to run. Omit with -All to run every queued intent.

.PARAMETER All
    Run every intent whose derived state is `queued`.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Invoke-SdlcRun.ps1 -Intent 20260924-a1b2

.EXAMPLE
    .\Invoke-SdlcRun.ps1 -All -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter()]
    [string[]] $Intent,

    [Parameter()]
    [switch] $All,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $All -and (-not $Intent -or $Intent.Count -eq 0)) {
    throw 'Name at least one intent, or pass -All to run every queued one.'
}

if (-not $RepoRoot) { $RepoRoot = (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$stateScript = Join-Path $PSScriptRoot 'Get-SdlcCycleState.ps1'
if (-not (Test-Path -LiteralPath $stateScript)) {
    throw "Missing $stateScript — the run cannot derive what is queued without it."
}

$stateJson = (& $stateScript -RepoRoot $RepoRoot) -join "`n"
$states = @()
if ($stateJson.Trim()) {
    $parsed = ConvertFrom-Json $stateJson
    if ($null -ne $parsed) { $states = @($parsed) }
}

# Filter on the property's presence as well as its value: an empty or malformed queue
# should read as "nothing to do" rather than throwing under strict mode, because an empty
# queue is the commonest state and it is not an error.
if ($All) {
    $selected = @($states | Where-Object {
        $_.PSObject.Properties.Name -contains 'dispatchable' -and $_.dispatchable
    })
}
else {
    $selected = @()
    foreach ($id in $Intent) {
        $match = @($states | Where-Object { $_.'cycle-id' -eq $id })
        if ($match.Count -eq 0) {
            Write-Warning "No intent found for '$id' — skipping."
            continue
        }
        if (-not $match[0].dispatchable) {
            Write-Warning "'$id' is $($match[0].state): $($match[0].reason). Skipping."
            continue
        }
        $selected += $match[0]
    }
}

if ($selected.Count -eq 0) {
    Write-Host 'Nothing queued.' -ForegroundColor Yellow
    if (-not $All) {
        Write-Host 'Run without -Intent and with -All to see what is available, or /sdlc:intent to author one.'
    }
    return
}

# --- the confirmation -------------------------------------------------------
#
# This is the control that replaces a concurrency cap, so it states the cost rather
# than only the list.

Write-Host ''
Write-Host "$($selected.Count) intent(s) will run unattended, each in its own worktree:" -ForegroundColor Cyan
foreach ($s in $selected) {
    Write-Host "  $($s.'cycle-id')  →  branch $($s.branch)"
}
Write-Host ''
Write-Host "$($selected.Count) worktree(s) will be created under .claude/worktrees/." -ForegroundColor Yellow
Write-Host 'Each is a fresh checkout with its own package restore and build.' -ForegroundColor Yellow
Write-Host ''
Write-Host 'Each run proceeds unattended through spec, interrogate, plan — and HALTS'
Write-Host 'before build at blast-radius tier 2 or 3. Nothing irreversible happens'
Write-Host 'before the plan, so a tier that turns out higher than expected costs a'
Write-Host 'halt rather than a rollback.'
Write-Host ''

$launched = New-Object System.Collections.ArrayList

foreach ($s in $selected) {
    $id = $s.'cycle-id'
    $worktree = "cycle-$id"

    $prompt = @(
        "Run the SDLC cycle for intent $id, unattended.",
        '',
        'In order, stopping at the first that does not pass:',
        "  1. /sdlc:spec $id",
        "  2. /sdlc:interrogate .sdlc/cycles/$id/spec.md",
        '     If it returns blocking findings, run /sdlc:repair and continue only if it converges.',
        "  3. /sdlc:plan $id",
        "  4. /sdlc:interrogate .sdlc/cycles/$id/plan.md",
        '     Same rule.',
        '  5. Read the plan frontmatter. If predicted-tier is not 1, STOP and report.',
        "     Do not build. A person signs off above tier 1.",
        "  6. /sdlc:build $id",
        "  7. /sdlc:verify $id",
        '',
        'Do not merge, do not move any ref, and do not write a retro. Landing is a',
        'separate, serialized, human-gated step.'
    ) -join "`n"

    if ($PSCmdlet.ShouldProcess("$id", "create worktree $worktree and run the cycle unattended")) {
        Write-Host "launching $id …" -ForegroundColor Green

        # A worktree per cycle makes collision between concurrent cycles structurally
        # impossible, and the harness enforces it rather than trusting convention: edits
        # targeting the main checkout are blocked outright.
        $args = @(
            '-p', $prompt,
            '--worktree', $worktree
        )

        $process = Start-Process -FilePath 'claude' -ArgumentList $args `
            -WorkingDirectory $RepoRoot -PassThru -NoNewWindow

        $null = $launched.Add([pscustomobject]@{
            'cycle-id' = $id
            worktree   = $worktree
            pid        = $process.Id
        })
    }
    else {
        $null = $launched.Add([pscustomobject]@{
            'cycle-id' = $id
            worktree   = $worktree
            pid        = $null
        })
    }
}

Write-Host ''
Write-Host "$($launched.Count) run(s) started. Watch with: .\tools\Get-SdlcCycleState.ps1" -ForegroundColor Cyan
Write-Host 'A run that dies leaves an unlocked worktree with no verify.md, which reads as'
Write-Host '`dead` rather than as still working — relaunch or discard it.'

ConvertTo-Json -InputObject @($launched) -Depth 4
