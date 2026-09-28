# Compares what a cycle actually changed against what its plan declared.
#
# This is the complete check, and the reason it exists rather than a cleverer hook: a hook inspects an
# ACTION - "you are about to write this file" - so it has to recognise every way a file can be written,
# and `python - <<PY` that writes one is opaque. That list never closes.
#
# This inspects the RESULT. It reads git, so it sees every change however it arrived: the Write tool, a
# shell redirect, a heredoc, a script. Nothing routes around it.
#
# Two failures, and they are different:
#
#   undeclared - changed but not in the plan. The plan is incomplete, or this is scope nobody approved.
#   undelivered - declared but unchanged. Usually the plan over-promised; occasionally work is missing.
#
# Verify cites this output as evidence. It writes nothing and decides nothing.
#
# Windows PowerShell 5.1.
#
#   .\Compare-DiffToPlan.ps1 -CycleId 20260928_a43a [-Base <sha>] [-RepositoryRoot <path>]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CycleId,
    [string]$Base,
    [string]$RepositoryRoot = '.'
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SdlcPlan.ps1')

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$planPath = Join-Path $root ".sdlc/cycles/$CycleId/plan.md"

if (-not (Test-Path -LiteralPath $planPath)) {
    throw "No plan at .sdlc/cycles/$CycleId/plan.md."
}

$declared = Get-SdlcDeclaredFile -PlanPath $planPath

if ($declared.Count -eq 0) {
    throw "The plan for $CycleId declares no files. Its ## Files table is missing or malformed, and " +
          'without it there is nothing to compare a change against.'
}

# Default base: the commit that recorded this cycle's artifacts, which is what build diffs against.
if ([string]::IsNullOrWhiteSpace($Base)) {
    Push-Location $root
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $Base = (& git rev-parse HEAD 2>$null)
        $failed = $LASTEXITCODE -ne 0
    }
    finally {
        $ErrorActionPreference = $previous
        Pop-Location
    }

    if ($failed) { throw "Not a git repository: $root" }
    $Base = "$Base".Trim()
}

Push-Location $root
$previous = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    # Staged, unstaged and untracked together: a change that exists only in the working tree still
    # landed, and an untracked file is the commonest way an undeclared one appears.
    $tracked = (& git diff --name-only $Base 2>$null)
    $staged = (& git diff --name-only --cached $Base 2>$null)
    $untracked = (& git ls-files --others --exclude-standard 2>$null)
    $failed = $LASTEXITCODE -ne 0
}
finally {
    $ErrorActionPreference = $previous
    Pop-Location
}

if ($failed) { throw "Could not read the diff against $Base." }

$changed = New-Object System.Collections.Generic.List[string]
foreach ($set in @($tracked, $staged, $untracked)) {
    foreach ($line in @($set)) {
        $p = "$line".Trim().Replace('\', '/')
        if (-not $p) { continue }
        # A cycle's own artifacts are the record of the change, never part of it.
        if ($p -like '.sdlc/*') { continue }
        if (-not $changed.Contains($p)) { $changed.Add($p) }
    }
}

$undeclared = @($changed | Where-Object { -not (Test-SdlcPathDeclared -Declared $declared -RelativePath $_) })
$undelivered = @($declared | Where-Object { -not ($changed -contains $_) })

Write-Output "cycle: $CycleId"
Write-Output "base: $Base"
Write-Output "declared: $($declared.Count)"
Write-Output "changed: $($changed.Count)"
Write-Output ''

if ($undeclared.Count -gt 0) {
    Write-Output 'UNDECLARED - changed but the plan does not declare it:'
    foreach ($p in $undeclared) { Write-Output "  $p" }
    Write-Output ''
}

if ($undelivered.Count -gt 0) {
    Write-Output 'UNDELIVERED - declared but unchanged:'
    foreach ($p in $undelivered) { Write-Output "  $p" }
    Write-Output ''
}

if ($undeclared.Count -eq 0 -and $undelivered.Count -eq 0) {
    Write-Output 'MATCH - every changed file is declared, and every declared file changed.'
}

# Exit code is for a caller that wants one; verify reads the text.
if ($undeclared.Count -gt 0) { exit 1 }
exit 0
