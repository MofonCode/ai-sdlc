#Requires -Version 5.1
<#
.SYNOPSIS
    Moves the `release` ref to master's tip, bumping the plugin version and tagging it.

.DESCRIPTION
    `release` IS A SINGLE MOVING BRANCH, plus a version tag at each move. It has no commits
    of its own: its tip is always a commit already on master. Teammates subscribe once —
    `…#release` — and a person decides when they see something new.

    Rejected: a branch per release. A marketplace source can be pinned to a branch or a tag
    but NOT to a commit SHA, so version pinning would mean teammates editing their marketplace
    URL to update, which nobody does — leaving people on 0.1.0 six months later. A branch per
    release only pays off for hotfixing an old release WITHOUT master's newer work, and where
    the verify gate is meant to keep master shippable, that scenario means master is broken
    and the fix is master. Tags give the immutable per-version record meanwhile.

    THE VERSION IS BUMPED HERE, NEVER IN-CYCLE. Two reasons, and the first is decisive:

      Never in-cycle: CI requires a bump on every plugin change, so two concurrent cycles
      bumping `plugin.json` conflict EVERY time. A guaranteed collision, not a possible one.

      At release rather than at merge: nobody consumes master, so a bump there signals
      nothing. Several merged cycles sharing one released version is normal — that is what a
      release is.

    So the CI rule reads: fail a `release` move that does not bump the version. More accurate
    than gating every merge, and far less noisy.

.PARAMETER Version
    The version to release, as x.y.z. Must be greater than the current one.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.PARAMETER NoPush
    Do everything locally and push nothing.

.PARAMETER TestCommand
    The release gate. Defaults to the .NET one because this solution consumes no pack; a
    solution with a pack takes its gate commands from there, since INV-04 delegates the stack
    and tool names stay out of the charter.

.EXAMPLE
    .\Move-SdlcRelease.ps1 -Version 0.2.0
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
    [string] $Version,

    [Parameter()]
    [string] $RepoRoot,

    [Parameter()]
    [switch] $NoPush,

    # The release gate. A parameter rather than a hardcoded command, for the same reason
    # Get-SdlcVerifyOutcome takes one: INV-04 delegates the stack to the pack, so tool names
    # belong there. An earlier version hardcoded `dotnet test`, which also made the release
    # gate untestable - a fixture repository with no .NET project could only ever report
    # `refused-tests-fail`, so nothing past that point was reachable by a test.
    [Parameter()]
    [string] $TestCommand = 'dotnet test'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that
# terminates a JSON string.
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

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "'$Version' is not a version. The form is x.y.z."
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$manifestRel = '.claude-plugin/plugin.json'
$manifestPath = Join-Path $RepoRoot $manifestRel
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "No plugin manifest at '$manifestRel'. There is nothing to release."
}

$manifestText = [string] (Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8)
$current = $null
if ($manifestText -match '"version"\s*:\s*"([^"]+)"') { $current = $Matches[1] }

if (-not $current) {
    throw "The manifest declares no version. A release with no previous version cannot be checked for a bump."
}

function ConvertTo-Comparable {
    param([string] $V)
    $parts = $V -split '\.'
    return [version]::new([int] $parts[0], [int] $parts[1], [int] $parts[2])
}

$result = [ordered]@{
    'from-version' = $current
    'to-version'   = $Version
    tag            = "v$Version"
    outcome        = 'not-started'
    steps          = @()
}

$steps = New-Object System.Collections.ArrayList
function Add-Step {
    param([string] $Name, [bool] $Ok, [string] $Detail)
    $null = $script:steps.Add([pscustomobject]@{ step = $Name; ok = $Ok; detail = $Detail })
}

Push-Location $RepoRoot
try {
    $branch = (Invoke-SdlcGit rev-parse --abbrev-ref HEAD).stdout.Trim()
    if ($branch -ne 'master') {
        $result['outcome'] = 'refused-not-master'
        $result['steps'] = @($steps)
        Write-Host "On '$branch'. A release is a pointer to a commit already on master, so it" -ForegroundColor Red
        Write-Host 'is moved from master and nowhere else.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $status = (Invoke-SdlcGit status --porcelain).stdout.Trim()
    if ($status) {
        $result['outcome'] = 'refused-dirty'
        $result['steps'] = @($steps)
        Write-Host 'The working tree is not clean. A release must be a commit that exists.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $existingTag = (Invoke-SdlcGit tag --list "v$Version").stdout.Trim()
    if ($existingTag) {
        $result['outcome'] = 'refused-tag-exists'
        $result['steps'] = @($steps)
        Write-Host "Tag v$Version already exists. A version tag is immutable." -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # THE FIRST RELEASE HAS NOTHING TO BUMP FROM, and an earlier version of this script could
    # not cut one: it demanded `$Version > $current` unconditionally, so releasing the version
    # the manifest already declared was refused and the project could never reach v0.1.0 at all.
    #
    # Worse, it contradicted the CI gate written for the same rule, which says "First release.
    # Nothing to compare against, and that is not a failure." Two halves of one rule
    # disagreeing. Found by running it.
    #
    # So the rule is stated once: a release must never go BACKWARDS, and every release after
    # the first must go FORWARDS. `v*` tags are how we know which this is, so non-version tags
    # like `raw-concept` are correctly ignored.
    $versionTags = @((Invoke-SdlcGit tag --list 'v*').stdout -split "`n" |
        ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $isFirstRelease = ($versionTags.Count -eq 0)

    if ($isFirstRelease) {
        if ((ConvertTo-Comparable $Version) -lt (ConvertTo-Comparable $current)) {
            $result['outcome'] = 'refused-below-manifest'
            $result['steps'] = @($steps)
            Write-Host "Version $Version is below the manifest's $current. A first release may" -ForegroundColor Red
            Write-Host 'match the manifest, but never go backwards from it.' -ForegroundColor Red
            ConvertTo-Json -InputObject $result -Depth 6
            return
        }
        Add-Step 'first-release' $true ("no v* tag exists; releasing the manifest's own " + $Version)
    }
    elseif ((ConvertTo-Comparable $Version) -le (ConvertTo-Comparable $current)) {
        $result['outcome'] = 'refused-no-bump'
        $result['steps'] = @($steps)
        Write-Host "Version $Version is not greater than the current $current. A release move" -ForegroundColor Red
        Write-Host 'must bump, or everyone subscribed receives new code under an old version.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # The gate that matters, and it runs BEFORE the bump. A release is the one moment the
    # whole tree is claimed to be shippable, and shipping a failing build to everyone who
    # subscribed to `release` is worse than not shipping.
    Write-Host 'Verifying before release. This is the claim a release makes.' -ForegroundColor Cyan
    $tests = Invoke-SdlcNative -Command 'cmd.exe' -Arguments @('/c', $TestCommand)
    Add-Step 'verify' $tests.ok $(if ($tests.ok) { 'tests pass' } else { 'tests FAIL' })

    if (-not $tests.ok) {
        $result['outcome'] = 'refused-tests-fail'
        $result['steps'] = @($steps)
        Write-Host 'Tests fail. A release move would publish this to everyone on `release`.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    if (-not $PSCmdlet.ShouldProcess("v$Version", "bump $current to $Version, tag, and move release")) {
        $result['outcome'] = 'whatif'
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # Replace only the version, and only its first occurrence: a blunt string replace would
    # also rewrite a dependency's version if one ever matched.
    $updated = [regex]::Replace(
        $manifestText,
        '("version"\s*:\s*")' + [regex]::Escape($current) + '(")',
        '${1}' + $Version + '${2}',
        1)

    [System.IO.File]::WriteAllText($manifestPath, $updated, (New-Object System.Text.UTF8Encoding $false))
    $staged = Invoke-SdlcGit add '--' $manifestRel

    # On a first release at the manifest's own version there is nothing to rewrite, so `commit`
    # would fail with "nothing to commit" - which is success here, not failure. Reads .code
    # because for `diff --cached --quiet` a zero code means nothing is staged.
    $pending = Invoke-SdlcGit diff --cached --quiet
    if ($pending.code -eq 0) {
        Add-Step 'bump' $true ("manifest already declares " + $Version + "; nothing to rewrite")
        $bump = [pscustomobject]@{ ok = $true; code = 0; stdout = ''; stderr = '' }
    }
    else {
        $bump = Invoke-SdlcGit commit -m "release v$Version"
        if ($bump.ok) {
            Add-Step 'bump' $true ($current + ' to ' + $Version)
        }
    }

    if (-not ($staged.ok -and $bump.ok)) {
        # Tagging an uncommitted bump would put v$Version on a tree whose manifest still
        # says the old version - the exact disagreement the release gate exists to catch.
        Add-Step 'bump' $false ($staged.stderr + ' ' + $bump.stderr).Trim()
        $result['outcome'] = 'failed-bump'
        $result['steps'] = @($steps)
        Write-Host 'Could not commit the version bump, so nothing was tagged.' -ForegroundColor Red
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }
    $tag = Invoke-SdlcGit tag "v$Version"
    Add-Step 'tag' $tag.ok "v$Version"

    if ($NoPush) {
        $result['outcome'] = 'local-only'
        $result['steps'] = @($steps)
        Write-Host "v$Version is committed and tagged locally. Nothing was pushed." -ForegroundColor Cyan
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    # master:release rather than a checkout and merge. `release` has no commits of its own,
    # so there is nothing to merge — it is a pointer being moved.
    $push = Invoke-SdlcGit push origin master "master:release" "v$Version"
    Add-Step 'push' $push.ok $(if ($push.ok) { 'master, release and the tag' } else { $push.stderr })

    if (-not $push.ok) {
        $result['outcome'] = 'failed-push'
        $result['steps'] = @($steps)
        ConvertTo-Json -InputObject $result -Depth 6
        return
    }

    $result['outcome'] = 'released'
    $result['steps'] = @($steps)

    Write-Host ''
    Write-Host "v$Version released." -ForegroundColor Green
    Write-Host 'Everyone subscribed to `release` sees it at their next install or update.'
    Write-Host 'Nobody needs to edit a marketplace URL, which is the whole reason `release`'
    Write-Host 'is one moving branch rather than a branch per version.'

    ConvertTo-Json -InputObject $result -Depth 6
}
finally {
    Pop-Location
}
