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

# Dot-sourced, not duplicated: git writes ordinary progress to stderr, and `2>&1` on a
# native command turns that into a terminating error under `Stop`. See SdlcGit.ps1.
. (Join-Path $PSScriptRoot 'SdlcGit.ps1')

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
    'pull-request' = $null
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

    $co = Invoke-SdlcGit checkout $branch
    if (-not $co.ok) {
        Add-Step 'checkout' $false "could not check out $branch"
        $result['outcome'] = 'failed-checkout'
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $rebase = Invoke-SdlcGit rebase master
    if (-not $rebase.ok) {
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

    $t = Invoke-SdlcNative -Command 'dotnet' -Arguments @('test')
    $testsPass = $t.ok
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
    #
    # EVERY INTERROGATION THE CYCLE RAN, not one file. Rendered by -CycleId rather than by path,
    # because one interrogation is one document and a cycle has several — its intent, its spec,
    # its plan, its verification, plus a document per repair round. Naming a single findings.json
    # here would have carried only the last stage's suggestions into the intake file and silently
    # dropped every earlier one, which is the evidence a missing invariant is inferred from.
    $findingsDir = Join-Path $RepoRoot ".sdlc/cycles/$CycleId/findings"
    $hasFindings = $false
    if (Test-Path -LiteralPath $findingsDir) {
        $hasFindings = @(Get-ChildItem -LiteralPath $findingsDir -Filter '*.json' -File).Count -gt 0
    }

    if ($hasFindings) {
        $formatScript = Join-Path $PSScriptRoot 'Format-SdlcFindings.ps1'
        $suggestionsRel = ".sdlc/suggestions/$CycleId.md"

        # Splatted rather than continued with a backtick: a blank line or a trailing space
        # after a backtick silently ends the statement, and the call then runs with the wrong
        # arguments while still parsing cleanly.
        $formatArgs = @{
            CycleId  = $CycleId
            State    = 'suggested'
            OutFile  = $suggestionsRel
            RepoRoot = $RepoRoot
        }
        & $formatScript @formatArgs | Out-Null

        $staged = Invoke-SdlcGit add '--' $suggestionsRel
        if (-not $staged.ok) {
            Add-Step 'suggestions' $false $staged.stderr
            $result['outcome'] = 'failed-suggestions'
            ConvertTo-Json -InputObject $result -Depth 6
            return
        }
        # --quiet so an empty suggestion set is not an error: a cycle with nothing
        # suggested is a good cycle, not a failed landing.
        # Exit code 1 means "there are staged changes", which is the normal case here
        # rather than a failure. That is why this reads .code and not .ok.
        $staged = Invoke-SdlcGit diff --cached --quiet
        if ($staged.code -ne 0) {
            $sc = Invoke-SdlcGit commit -m "$CycleId`: unresolved suggestions"
            if (-not $sc.ok) {
                # Not cosmetic: the file rides in with the squash, so a silent failure
                # lands the cycle without its intake.
                Add-Step 'suggestions' $false $sc.stderr
                $result['outcome'] = 'failed-suggestions'
                ConvertTo-Json -InputObject $result -Depth 6
                return
            }
            Add-Step 'suggestions' $true "wrote $suggestionsRel"
        }
        else {
            Add-Step 'suggestions' $true 'nothing suggested, nothing to record'
        }
    }
    else {
        # A cycle that was never interrogated reaches here too, and that is not the same thing as
        # a cycle with nothing to suggest. Said plainly rather than as an absence, because for
        # weeks this line was printed by every cycle: nothing wrote a findings document at all.
        Add-Step 'suggestions' $true 'no interrogation was filed for this cycle'
    }

    # --- 4. the retro must be on the branch before anything merges ---------
    #
    # The retro is the COMPLETION CERTIFICATE: the lifecycle derivation reads "a retro
    # exists" as `done`. It is written on the cycle branch and rides in with the squash, so
    # merging without one produces a cycle that landed and will never read as finished -
    # which an unsupervised dispatcher would treat as never started.
    $retroPath = Join-Path $RepoRoot ".sdlc/cycles/$CycleId/retro.md"
    if (-not (Test-Path -LiteralPath $retroPath)) {
        Add-Step 'retro' $false 'no retro.md on the cycle branch'
        $result['outcome'] = 'refused-no-retro'
        Write-Host ''
        Write-Host 'No retro. The retro is the completion certificate, and it rides in with' -ForegroundColor Red
        Write-Host 'the squash - merging without one lands the work and leaves the cycle' -ForegroundColor Red
        Write-Host 'reading as never started. Run /sdlc:retro first.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }
    Add-Step 'retro' $true 'present, and will ride in with the squash'

    # --- 5. deploy: a pull request, not a local merge ----------------------
    #
    # DEPLOY MEANS PR + REVIEW + MERGE. Landing used to squash straight onto master here,
    # which skipped the review half entirely - the same shape of hole the intent gate had.
    # A person merges; this prepares the merge and stops.
    #
    # Squashing happens at the merge, so one cycle still becomes one commit and `git revert`
    # of a cycle stays one operation.

    $log = Invoke-SdlcGit log -1 --format=%s $branch
    $title = $log.stdout.Trim()

    $remotes = @((Invoke-SdlcGit remote).stdout -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($remotes.Count -eq 0) {
        $result['outcome'] = 'verified-no-remote'
        Write-Host ''
        Write-Host "$CycleId is verified and ready, but there is no remote, so the review" -ForegroundColor Yellow
        Write-Host 'half of the gate cannot be created.' -ForegroundColor Yellow
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $push = Invoke-SdlcGit push -u origin $branch
    if (-not $push.ok) {
        Add-Step 'push' $false $push.stderr
        $result['outcome'] = 'failed-push'
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }
    Add-Step 'push' $true "pushed $branch"

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        $result['outcome'] = 'pushed-no-gh'
        Write-Host "Pushed $branch. Open its pull request by hand." -ForegroundColor Yellow
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $body = @(
        "Verified cycle, rebased onto master and re-verified after the rebase.",
        '',
        'SQUASH-MERGE THIS. One cycle becomes one commit, which is what keeps `git revert`',
        'of a cycle a single operation. The retro rides in with the squash and is what makes',
        'the cycle read as done.',
        '',
        "Artifacts: ``.sdlc/cycles/$CycleId/``"
    ) -join "`n"

    $pr = Invoke-SdlcNative -Command 'gh' -Arguments @(
        'pr', 'create', '--title', "$CycleId`: $title", '--body', $body,
        '--base', 'master', '--head', $branch)

    $url = ($pr.stdout -split "`n" | Where-Object { $_ -match 'http' } | Select-Object -Last 1)
    if (-not $pr.ok) {
        Add-Step 'pull-request' $false $pr.stderr
        $result['outcome'] = 'failed-pull-request'
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    Add-Step 'pull-request' $true "$url"
    $result['pull-request'] = "$url"
    $result['outcome'] = 'awaiting-review'

    Write-Host ''
    Write-Host "$CycleId is ready to land." -ForegroundColor Green
    Write-Host "  $url"
    Write-Host ''
    Write-Host 'Squash-merge it. The retro rides in with the squash, and only then does this'
    Write-Host 'cycle read as done.'
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
