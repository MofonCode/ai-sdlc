# Pins the commit a cycle's change is measured from.
#
# Build calls this once, after committing the cycle's artifacts and before touching source. Everything
# reachable from that commit predates the change; everything after it is the change.
#
# Why a script and not `git rev-parse HEAD > base.sha`: build is granted powershell.exe, git and dotnet -
# not a shell that creates directories - and the evidence directory does not exist yet on a first build.
#
# It refuses to move a base that is already pinned. Re-pinning is not a repair: it puts work already done
# behind the new base, which is precisely how this check was voided before. A second build inside one
# cycle keeps the first one's base so the comparison still spans everything the cycle did.
#
# Windows PowerShell 5.1.
#
#   .\Set-CycleBase.ps1 -CycleId 20260928_a43a [-RepositoryRoot <path>] [-Sha <sha>]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CycleId,
    [string]$RepositoryRoot = '.',
    [string]$Sha
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SdlcPlan.ps1')

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path

$branch = Get-SdlcBranchState -WorkingDirectory $root
if (-not $branch.IsRepository) {
    throw "Not a git repository: $root. A base is a commit, and there are none."
}

if (-not (Test-Path -LiteralPath (Join-Path $root ".sdlc/cycles/$CycleId"))) {
    throw "No cycle directory at .sdlc/cycles/$CycleId. The artifacts come before the base."
}

# An already-pinned base stands. Report it and stop.
$existing = Get-SdlcCycleBase -RepositoryRoot $root -CycleId $CycleId

if ($null -ne $existing) {
    Write-Output "ALREADY PINNED - $CycleId is measured from $existing."
    Write-Output ''
    Write-Output '  A base is pinned once per cycle. Moving it would put work already done behind it and'
    Write-Output '  hide that work from the comparison, which is the failure this file exists to prevent.'
    Write-Output '  If the base is genuinely wrong, that is a decision for a person.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Sha)) {
    Push-Location $root
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $Sha = (& git rev-parse HEAD 2>$null)
        $failed = $LASTEXITCODE -ne 0
    }
    finally {
        $ErrorActionPreference = $previous
        Pop-Location
    }

    if ($failed) { throw "Could not read HEAD in $root. A repository with no commit has no base." }
}

$Sha = "$Sha".Trim()

if ($Sha -notmatch '^[0-9a-f]{7,40}$') {
    throw "Not a commit sha: '$Sha'."
}

$evidence = Join-Path $root ".sdlc/cycles/$CycleId/evidence"
if (-not (Test-Path -LiteralPath $evidence)) {
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
}

$path = Join-Path $evidence 'base.sha'

# No BOM: this file is read by git, by these scripts and by a person, and a BOM breaks the first.
[System.IO.File]::WriteAllText($path, $Sha, (New-Object System.Text.UTF8Encoding($false)))

Write-Output "PINNED - $CycleId is measured from $Sha."
Write-Output "  recorded at .sdlc/cycles/$CycleId/evidence/base.sha"
Write-Output '  commit your source changes as often as you like; the comparison spans all of them.'
exit 0
