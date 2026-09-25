#Requires -Version 5.1
<#
.SYNOPSIS
    Puts an authored intent behind its gate: a branch, a commit, and a pull request.

.DESCRIPTION
    THE INTENT GATE HAS TO CLOSE BEFORE WORK STARTS OR IT IS NOT A GATE. This script is
    that gate, and it exists because the two ways of avoiding it are both worse:

      Writing the intent on the WORK branch leaves its PR open while spec, plan and code
      pile onto the same diff, so approving the intent implicitly approves everything that
      followed.

      Writing the intent straight to MASTER removes the PR, which removes the gate
      entirely. That is what happened twice before this script existed.

    So: two branches per intent, same id, different prefix.

      1  branch  intent-<id>
      2  commit  .sdlc/intents/<id>.md on it
      3  PR
      4  approve -> merge to master
      5  branch  cycle-<id>            <- the dispatcher, later, not here
      6  spec, plan, build, verify all happen there

    Both prefixes are hyphenated because the cycle name doubles as a worktree directory
    name and a slash there would nest a directory. One of the two is forced, so matching it
    is free.

    THE LIFECYCLE DERIVATION KEYS ON `cycle-<id>` SPECIFICALLY, never "any branch
    mentioning this id". Otherwise a lingering `intent-<id>` branch would read as work in
    progress and the dispatcher would refuse to start the cycle it is waiting for.

    NO REMOTE IS NOT AN ERROR. The branch and the commit are the gate's local half and
    always happen; the pull request needs a remote and the GitHub CLI. Where either is
    missing the script says exactly what is left to do rather than failing after having
    already created the branch.

.PARAMETER CycleId
    The intent to submit. Its file must already exist at .sdlc/intents/<id>.md.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.PARAMETER NoPullRequest
    Create the branch and commit but do not open a pull request.

.EXAMPLE
    .\Submit-SdlcIntent.ps1 -CycleId 20260925-a7f3
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $CycleId,

    [Parameter()]
    [string] $RepoRoot,

    [Parameter()]
    [switch] $NoPullRequest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that
# terminates a JSON string. Intent titles are prose and routinely contain one.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

# Dot-sourced, not duplicated: git writes ordinary progress to stderr, and `2>&1` on a
# native command turns that into a terminating error under `Stop`. See SdlcGit.ps1.
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

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

if ($CycleId -notmatch '^[0-9]{8}-[0-9a-f]{4}$') {
    throw "'$CycleId' is not a cycle id. The form is yyyymmdd plus four hex characters."
}

$intentRel = ".sdlc/intents/$CycleId.md"
$intentPath = Join-Path $RepoRoot $intentRel
if (-not (Test-Path -LiteralPath $intentPath)) {
    throw "No intent at '$intentRel'. Author it first."
}

$branch = "intent-$CycleId"
$result = [ordered]@{
    'cycle-id'      = $CycleId
    branch          = $branch
    outcome         = 'not-started'
    'pull-request'  = $null
    'next'          = $null
    steps           = @()
}

$steps = New-Object System.Collections.ArrayList
function Add-Step {
    param([string] $Name, [bool] $Ok, [string] $Detail)
    $null = $script:steps.Add([pscustomobject]@{ step = $Name; ok = $Ok; detail = $Detail })
}

Push-Location $RepoRoot
try {
    $current = (git rev-parse --abbrev-ref HEAD).Trim()

    # Refusing here rather than later. Submitting from a cycle branch would put the intent
    # commit on top of that cycle's work, which is the mixed-diff failure this whole
    # mechanism exists to prevent.
    if ($current -like 'cycle-*') {
        $result['outcome'] = 'refused-on-cycle-branch'
        $result['steps'] = @($steps)
        Write-Host "On '$current'. An intent is not submitted from a cycle branch: its commit" -ForegroundColor Red
        Write-Host 'would land on top of that cycle and the two diffs would be approved together.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $existing = @(git for-each-ref --format='%(refname:short)' refs/heads) -contains $branch
    if ($existing) {
        $result['outcome'] = 'already-submitted'
        $result['steps'] = @($steps)
        Write-Host "Branch '$branch' already exists, so this intent is already behind its gate." -ForegroundColor Yellow
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # The title, for the branch's commit and the pull request. Taken from the intent's first
    # heading rather than invented, so the PR and the artifact cannot disagree.
    $title = $CycleId
    foreach ($line in (Get-Content -LiteralPath $intentPath -Encoding UTF8)) {
        if ($line -match '^#\s+(.+)$') { $title = $Matches[1].Trim(); break }
    }

    if (-not $PSCmdlet.ShouldProcess($CycleId, "create $branch, commit $intentRel, open a pull request")) {
        $result['outcome'] = 'whatif'
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $r = Invoke-SdlcGit checkout -b $branch
    if (-not $r.ok) {
        Add-Step 'branch' $false "could not create $branch"
        $result['outcome'] = 'failed-branch'
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }
    Add-Step 'branch' $true "created $branch from $current"

    # Only the intent file. A submission that swept up unrelated working-tree changes would
    # put them behind the intent gate, where nobody is looking for them.
    $null = Invoke-SdlcGit add -- $intentRel
    # Exit code 0 means "nothing staged", so there is nothing to commit. Reads .code
    # rather than .ok because for this command a non-zero code is the ordinary case.
    $staged = Invoke-SdlcGit diff --cached --quiet
    if ($staged.code -eq 0) {
        Add-Step 'commit' $true 'already committed; nothing to add'
    }
    else {
        $null = Invoke-SdlcGit commit -m "$CycleId`: $title"
        Add-Step 'commit' $true "committed $intentRel"
    }

    if ($NoPullRequest) {
        $result['outcome'] = 'branched'
        $result['next'] = "open a pull request for $branch, then merge it"
        $result['steps'] = @($steps)
        Write-Host "$branch is ready. No pull request was requested." -ForegroundColor Cyan
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $remotes = @(git remote)
    if ($remotes.Count -eq 0) {
        Add-Step 'pull-request' $false 'no remote configured'
        $result['outcome'] = 'branched-no-remote'
        $result['next'] = "add a remote, push $branch, and open a pull request"
        $result['steps'] = @($steps)
        Write-Host "$branch is committed locally. There is no remote, so the gate's review" -ForegroundColor Yellow
        Write-Host 'half cannot be created yet.' -ForegroundColor Yellow
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $push = Invoke-SdlcGit push -u origin $branch
    if (-not $push.ok) {
        Add-Step 'push' $false 'push failed'
        $result['outcome'] = 'failed-push'
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }
    Add-Step 'push' $true "pushed $branch"

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        Add-Step 'pull-request' $false 'the GitHub CLI is not installed'
        $result['outcome'] = 'pushed-no-gh'
        $result['next'] = "open a pull request for $branch by hand"
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $body = @(
        'This is the intent gate. Approving this pull request approves the WORK BEING',
        'PROPOSED, not any particular implementation of it: spec, plan and code all happen',
        'afterwards, on a separate branch, and are reviewed separately.',
        '',
        "Merging creates nothing on its own. The dispatcher creates `cycle-$CycleId` once",
        'this has landed.',
        '',
        "Intent: ``$intentRel``"
    ) -join "`n"

    $pr = Invoke-SdlcNative -Command 'gh' -Arguments @(
        'pr', 'create',
        '--title', "$CycleId`: $title",
        '--body', $body,
        '--base', 'master',
        '--head', $branch)
    $url = ($pr.stdout -split "`n" | Where-Object { $_ -match 'http' } | Select-Object -Last 1)
    if (-not $pr.ok) {
        $url = $pr.stderr
        Add-Step 'pull-request' $false "gh pr create failed: $url"
        $result['outcome'] = 'failed-pull-request'
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    Add-Step 'pull-request' $true "$url"
    $result['pull-request'] = "$url"
    $result['outcome'] = 'submitted'
    $result['next'] = 'a person approves and merges; only then does the cycle branch get created'
    $result['steps'] = @($steps)

    Write-Host ''
    Write-Host "$CycleId is behind its gate." -ForegroundColor Green
    Write-Host "  $url"
    Write-Host ''
    Write-Host 'Nothing happens until a person merges it. The cycle branch is created after,'
    Write-Host 'never before, because an intent gate that does not close before work starts'
    Write-Host 'is not a gate.'

    ConvertTo-Json -InputObject $result -Depth 6
}
finally {
    Pop-Location
}
