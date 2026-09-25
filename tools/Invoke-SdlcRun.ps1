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

    THE BATCH BUILDS. IT DOES NOT THINK. Intent, spec and plan are supervised, one cycle
    at a time, because that is where the judgment is and because those stages may need to
    ask the user something a workflow could never ask. An unsupervised spec is perfectly
    safe and almost certainly wrong.

    So a run does exactly two things per cycle: build, then verify. A plan a person
    approved is a contract — a declared file list and an approved tier — which makes
    executing it mechanical and checkable, and that is the only part worth batching.

    Eligibility is therefore `planned`, not `queued`: the plan frontmatter must carry
    `approved:`. The mid-run tier check that used to live here is gone, because the tier
    was seen and signed off when the plan was approved.

.PARAMETER Intent
    Cycle ids to run. Omit with -All to run every cycle with an approved plan.

.PARAMETER All
    Run every intent whose derived state is `planned` — an approved plan and no build yet.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Invoke-SdlcRun.ps1 -Intent 20260924-a1b2

.EXAMPLE
    .\Invoke-SdlcRun.ps1 -All -WhatIf
#>
# Deliberately NOT ConfirmImpact = 'High'.
#
# That setting makes ShouldProcess prompt automatically, and a prompt needs a host that can
# ask. Invoked the documented way - `powershell.exe -File <script>` - there is none, and
# ShouldProcess throws "Object reference not set to an instance of an object" instead of
# saying so. All three scripts that used it were unusable non-interactively, which is how the
# tests and the dispatcher invoke them, and none had ever been run that way.
#
# SupportsShouldProcess stays, because -WhatIf is genuinely useful and is tested. What is gone
# is the automatic prompt: each of these scripts already prints its own confirmation, which is
# visible interactively and does not crash anywhere else.
[CmdletBinding(SupportsShouldProcess = $true)]
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

if (-not $All -and (-not $Intent -or $Intent.Count -eq 0)) {
    throw 'Name at least one intent, or pass -All to run every cycle with an approved plan.'
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
    Write-Host 'Nothing has an approved plan.' -ForegroundColor Yellow
    if (-not $All) {
        # Deliberately not '/sdlc:intent' — that skill is not built yet, and a hint naming a
        # command that does not resolve is the unresolvable-reference failure this framework
        # exists to catch. PluginManifestTests enforces that; restore the hint with the skill.
        Write-Host 'Pass -All to list every cycle with an approved plan.'
        Write-Host 'A cycle becomes eligible when a person approves its plan, which adds'
        Write-Host '`approved:` to the plan frontmatter. Until then the supervised phase owes it work.'
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
Write-Host 'Each run builds an ALREADY APPROVED plan and then verifies it. It does not'
Write-Host 'author artifacts, interrogate, or land anything. Every cycle finishes at'
Write-Host 'awaiting-landing, and landing is serialized and human-gated.'
Write-Host ''
Write-Host 'A build still halts on its own if the work turns out more dangerous than the'
Write-Host 'plan predicted: the actual blast-radius tier is recomputed from the real diff'
Write-Host 'and an escalation stops the cycle rather than reporting it.'
Write-Host ''

$launched = New-Object System.Collections.ArrayList

foreach ($s in $selected) {
    $id = $s.'cycle-id'
    $worktree = "cycle-$id"

    $prompt = @(
        "Build the approved plan for cycle $id, unattended, then verify it.",
        '',
        'In order, stopping at the first that does not pass:',
        "  1. /sdlc:build $id",
        "  2. /sdlc:verify $id",
        '',
        'The plan is already approved and its tier already signed off, so do not re-plan,',
        'do not re-interrogate the spec or the plan, and do not widen the declared file',
        'list. If the plan turns out to be wrong, STOP and report it as a plan defect.',
        'Absorbing a plan defect silently is the one failure that survives verification.',
        '',
        'Do not merge, do not move any ref, and do not write a retro. Landing is a',
        'separate, serialized, human-gated step, and the retro is the completion',
        'certificate written only once a cycle has actually landed.'
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
