# Shared plan-reading functions, dot-sourced by the hook and by the diff comparison.
#
# One definition of "what did the plan declare", because two would drift and the answer decides whether
# a change is allowed to exist.
#
# Windows PowerShell 5.1. -Encoding UTF8 on every read: 5.1 assumes the system codepage for a file with
# no BOM, which turns an em dash into mojibake.

function Get-SdlcDeclaredFile {
    <#
    .SYNOPSIS
    The repository-relative paths a plan declares, one per line.

    .DESCRIPTION
    Reads only the first column of the table under the plan's `## Files` heading.

    The first version of this scanned the whole document for any backticked token holding a slash or a
    dot, and so believed `net10.0`, `text/html`, `GET /` and `Random.Shared.Next(1, 7)` were declared
    files. A gate matching against that is not a gate.
    #>
    param([Parameter(Mandatory = $true)][string]$PlanPath)

    if (-not (Test-Path -LiteralPath $PlanPath)) {
        return @()
    }

    $lines = Get-Content -LiteralPath $PlanPath -Encoding UTF8
    $declared = New-Object System.Collections.Generic.List[string]
    $inFiles = $false

    foreach ($line in $lines) {
        if ($line -match '^\s*##\s') {
            # Any second-level heading ends the Files section - including Files itself, which is how the
            # section is entered.
            $inFiles = $line -match '^\s*##\s+Files\s*$'
            continue
        }

        if (-not $inFiles) { continue }
        if ($line -notmatch '^\s*\|') { continue }

        # A table row. Take the first cell only: later cells describe the change and mention types,
        # commands and frameworks that are not files.
        $cells = $line.Split('|')
        if ($cells.Count -lt 2) { continue }

        $first = $cells[1].Trim()

        # Skip the header row and the separator row.
        if ($first -match '^:?-{2,}:?$') { continue }
        if ($first -eq 'File') { continue }

        $m = [regex]::Match($first, '^`(?<p>[^`]+)`$')
        if (-not $m.Success) { continue }

        $path = $m.Groups['p'].Value.Replace('\', '/').Trim()
        if ($path.StartsWith('./')) { $path = $path.Substring(2) }
        if ($path) { $declared.Add($path) }
    }

    return $declared.ToArray()
}

function Get-SdlcBranchState {
    <#
    .SYNOPSIS
    Whether this directory is a repository, and the cycle id of its branch if it is on one.

    .DESCRIPTION
    Two facts, returned together, because a caller has to tell them apart. "No cycle id" because the
    branch is `main` means stay out of the way; "no cycle id" because there is no repository at all
    means something is badly wrong if the directory is governed. Collapsing both into $null lost that
    distinction and silently dropped a refusal.

    Windows PowerShell 5.1 also turns a native command's stderr into a terminating error under
    $ErrorActionPreference = 'Stop'. Outside a repository git writes to stderr, so the preference is
    lowered around the call - without that the caller crashes instead of deciding, and a crashed hook
    enforces nothing.
    #>
    param([Parameter(Mandatory = $true)][string]$WorkingDirectory)

    Push-Location $WorkingDirectory
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
        $failed = $LASTEXITCODE -ne 0
    }
    finally {
        $ErrorActionPreference = $previous
        Pop-Location
    }

    $cycleId = $null
    if (-not $failed) {
        if ("$branch".Trim() -match '^cycle/(?<id>[0-9]{8}_[0-9a-f]{4})$') {
            $cycleId = $Matches['id']
        }
    }

    return [pscustomobject]@{
        IsRepository = (-not $failed)
        CycleId      = $cycleId
    }
}

function Test-SdlcPathDeclared {
    <#
    .SYNOPSIS
    Whether a repository-relative path is one the plan declared.
    #>
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Declared,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $needle = $RelativePath.Replace('\', '/').Trim()
    if ($needle.StartsWith('./')) { $needle = $needle.Substring(2) }

    foreach ($d in $Declared) {
        if ($needle.Equals($d, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }

    return $false
}

function Get-SdlcCycleBase {
    <#
    .SYNOPSIS
    The commit a cycle's build declared as the base of its change, or $null if none was recorded.

    .DESCRIPTION
    Build writes this the moment it has committed the cycle's artifacts and before it touches source,
    so everything after it belongs to the change under review.

    It is recorded rather than recomputed because the obvious recomputation is wrong. The first version
    of Compare-DiffToPlan.ps1 took the base as `git rev-parse HEAD` at the moment it ran, which is the
    artifacts commit only while nothing has been committed since. One ordinary `git commit` during build
    moved the base on top of the change and the diff came back empty - with an undeclared file in it and
    exit code 0. A pinned base cannot do that, however many commits a build makes.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$CycleId
    )

    $path = Join-Path $RepositoryRoot ".sdlc/cycles/$CycleId/evidence/base.sha"

    if (-not (Test-Path -LiteralPath $path)) { return $null }

    $sha = "$(Get-Content -LiteralPath $path -Raw -Encoding UTF8)".Trim()

    if ($sha -notmatch '^[0-9a-f]{7,40}$') {
        throw "The base recorded for $CycleId is not a commit sha: '$sha'. It is written by build as " +
              'the single line of .sdlc/cycles/<id>/evidence/base.sha.'
    }

    return $sha
}
