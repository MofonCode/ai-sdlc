#Requires -Version 5.1
<#
.SYNOPSIS
    Prove a plan.md slice manifest is safe to fan out from.

.DESCRIPTION
    Parallel implementers are dispatched one per slice. Two of them holding the same file is
    a merge conflict by construction, not bad luck - so this runs before fan-out and a
    failure here is a PLANNING bug. Fix plan.md and re-approve; never start the build
    intending to reconcile in the merge.

    Checks, in order:
      - frontmatter exists and carries a slices block
      - every slice owns at least one file (a slice that owns nothing is not a slice)
      - manifests are pairwise disjoint
      - depends_on resolves, and the dependency graph terminates

    Prints the wave plan on success, so the approver sees the shape of the fan-out instead
    of being told it is fine.

.PARAMETER Plan
    Path to plan.md.

.EXAMPLE
    powershell -NoProfile -File Assert-SliceManifest.ps1 -Plan .sdlc/audit-log/plan.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Plan
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Stop-WithMessage {
    param([string] $Message)
    [Console]::Error.WriteLine($Message)
    exit 1
}

if (-not (Test-Path -LiteralPath $Plan -PathType Leaf)) { Stop-WithMessage "plan not found: $Plan" }

$text = Get-Content -LiteralPath $Plan -Raw
if ($text -notmatch '(?s)\A---\r?\n(.*?)\r?\n---(\r?\n|\z)') {
    Stop-WithMessage 'plan.md has no frontmatter - the slice manifest is mandatory, not documentation.'
}
$frontmatter = $Matches[1]

# A deliberate line-oriented parse rather than a YAML dependency: this must run anywhere the
# plugin is installed, and the schema already mandates the shape it reads.
$slices = New-Object System.Collections.Generic.List[object]
$current = $null

foreach ($rawLine in ($frontmatter -split "`n")) {
    $line = $rawLine.TrimEnd("`r")

    if ($line -match '^\s*-\s+id:\s*(.+?)\s*$') {
        $current = [pscustomobject]@{
            Id        = $Matches[1].Trim().Trim('"').Trim("'")
            Files     = New-Object System.Collections.Generic.List[string]
            DependsOn = New-Object System.Collections.Generic.List[string]
        }
        $slices.Add($current)
        continue
    }
    if ($null -eq $current) { continue }

    if ($line -match '^\s*files:\s*(.*)$') {
        foreach ($m in [regex]::Matches($Matches[1], '["'']([^"'']+)["'']')) {
            $current.Files.Add($m.Groups[1].Value.Replace('\', '/').TrimStart('.', '/'))
        }
        continue
    }
    if ($line -match '^\s*depends_on:\s*(.*)$') {
        $raw = $Matches[1]
        if ($raw -match '^\s*\[(.*)\]\s*$') {
            foreach ($piece in ($Matches[1] -split ',')) {
                $id = $piece.Trim().Trim('"').Trim("'")
                if ($id) { $current.DependsOn.Add($id) }
            }
        }
    }
}

if ($slices.Count -eq 0) {
    Stop-WithMessage 'no slices found in frontmatter - plan.md must carry a slices block.'
}

# --- every slice owns files ------------------------------------------------------------
$empty = @($slices | Where-Object { $_.Files.Count -eq 0 } | ForEach-Object { $_.Id })
if ($empty.Count -gt 0) {
    Stop-WithMessage ("empty manifest: {0} - a slice that owns no files is not a slice." -f ($empty -join ', '))
}

# --- pairwise disjoint -----------------------------------------------------------------
$owner = @{}
$overlaps = @()
foreach ($slice in $slices) {
    foreach ($file in $slice.Files) {
        if ($owner.ContainsKey($file)) {
            $overlaps += ("OVERLAP {0} <-> {1}: {2}" -f $owner[$file], $slice.Id, $file)
        } else {
            $owner[$file] = $slice.Id
        }
    }
}
if ($overlaps.Count -gt 0) {
    foreach ($o in $overlaps) { [Console]::Error.WriteLine($o) }
    Stop-WithMessage 'Manifests are not disjoint. This is a planning bug: merge the slices, or extract the shared file into a third slice both depend on. Do not reconcile it in the merge.'
}

# --- dependencies resolve, and the graph terminates -------------------------------------
$ids = @{}
foreach ($slice in $slices) { $ids[$slice.Id] = $true }
foreach ($slice in $slices) {
    foreach ($dep in $slice.DependsOn) {
        if (-not $ids.ContainsKey($dep)) {
            Stop-WithMessage ("slice '{0}' depends on '{1}', which is not a slice in this plan." -f $slice.Id, $dep)
        }
    }
}

$waves = New-Object System.Collections.Generic.List[string]
$done = @{}
$remaining = New-Object System.Collections.Generic.List[object]
foreach ($slice in $slices) { $remaining.Add($slice) }

while ($remaining.Count -gt 0) {
    $ready = @($remaining | Where-Object {
        $unmet = @($_.DependsOn | Where-Object { -not $done.ContainsKey($_) })
        $unmet.Count -eq 0
    })
    if ($ready.Count -eq 0) {
        $stuck = ($remaining | ForEach-Object { $_.Id }) -join ', '
        Stop-WithMessage "dependency cycle among: $stuck"
    }
    $waves.Add((($ready | ForEach-Object { $_.Id }) -join ', '))
    foreach ($slice in $ready) { $done[$slice.Id] = $true }
    $still = New-Object System.Collections.Generic.List[object]
    foreach ($slice in $remaining) { if (-not $done.ContainsKey($slice.Id)) { $still.Add($slice) } }
    $remaining = $still
}

$fileCount = 0
foreach ($slice in $slices) { $fileCount += $slice.Files.Count }

Write-Output ("{0} slices, {1} files, manifests disjoint" -f $slices.Count, $fileCount)
for ($i = 0; $i -lt $waves.Count; $i++) {
    Write-Output ("  wave {0}: {1}" -f ($i + 1), $waves[$i])
}
if ($waves.Count -eq $slices.Count -and $slices.Count -gt 1) {
    Write-Output '  NOTE: every wave holds one slice, so this plan is sequential. A single writer in one worktree will beat a fan-out here.'
}
exit 0
