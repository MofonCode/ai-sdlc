#Requires -Version 5.1
<#
.SYNOPSIS
    Prove a plan.md slice manifest is safe to fan out from, and - with -AcrossPlans - that no
    other live plan claims the same files.

.DESCRIPTION
    Parallel implementers are dispatched one per slice. Two of them holding the same file is
    a merge conflict by construction, not bad luck - so this runs before fan-out and a
    failure here is a PLANNING bug. Fix plan.md and re-approve; never start the build
    intending to reconcile in the merge.

    Two *chains* holding one file is the same conflict discovered later, and -AcrossPlans is
    what makes it observable before either build starts.

    Checks, in order:
      - frontmatter exists and carries a slices block
      - the frontmatter grammar the charter freezes
      - every slice owns at least one file (a slice that owns nothing is not a slice)
      - manifests are pairwise disjoint within this plan
      - depends_on resolves, and the dependency graph terminates
      - with -AcrossPlans: no live plan elsewhere claims a file this one claims

    Prints the wave plan on success, so the approver sees the shape of the fan-out instead
    of being told it is fine.

    The manifest is read by scripts/SliceManifestParser.ps1, the only thing in the engine
    that knows how to read one.

.PARAMETER Plan
    Path to plan.md.

.PARAMETER AcrossPlans
    Also compare this plan's claims against every other live plan under the artifact root.

.PARAMETER Root
    The repository root every claim is resolved against; the artifact root is found from it.
    Defaults to the working directory. Cross-plan mode only.

.EXAMPLE
    powershell -NoProfile -File Assert-SliceManifest.ps1 -Plan .sdlc/audit-log/plan.md

.EXAMPLE
    powershell -NoProfile -File Assert-SliceManifest.ps1 -Plan .sdlc/audit-log/plan.md -AcrossPlans
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Plan,

    [switch] $AcrossPlans,

    [string] $Root
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SliceManifestParser.ps1')

function Stop-WithMessage {
    param([string] $Message)
    [Console]::Error.WriteLine($Message)
    exit 1
}

# A flag that is silently ignored on a shipped command line is the quieter failure.
if ($Root -and -not $AcrossPlans) {
    Stop-WithMessage '-Root applies to -AcrossPlans only; the single-plan mode resolves nothing.'
}

if (-not (Test-Path -LiteralPath $Plan -PathType Leaf)) { Stop-WithMessage "plan not found: $Plan" }

$base = if ($Root) { $Root } else { (Get-Location).Path }
try { $base = (Resolve-Path -LiteralPath $base -ErrorAction Stop).Path } catch { }

$read = Read-SdlcPlan -Path $Plan -BaseDirectory $base

# The plan under check keeps its hard stop: a plan whose own frontmatter breaks a rule cannot
# be reasoned about, in either mode.
$own = @($read.Refusals)
if ($own.Count -gt 0) {
    foreach ($r in $own) { [Console]::Error.WriteLine(("REFUSED [{0}] {1}" -f $r.Rule, $r.Message)) }
    Stop-WithMessage 'plan.md breaks the frontmatter grammar. Fix the plan; every reader of it is guessing until you do.'
}

if ($read.Slices.Count -eq 0 -and $null -eq (Get-SdlcFrontmatter -Path $Plan)) {
    Stop-WithMessage 'plan.md has no frontmatter - the slice manifest is mandatory, not documentation.'
}

# Shape the parser's output the way the rest of this script already expects.
$slices = New-Object System.Collections.Generic.List[object]
foreach ($s in $read.Slices) {
    $slices.Add([pscustomobject]@{
        Id        = $s.Id
        Files     = @($s.Claims | ForEach-Object { $_.Key })
        Spellings = @($s.Claims | ForEach-Object { $_.Spelling })
        DependsOn = @()
    })
}

# depends_on is not a claim, so the parser does not carry it; it is read here.
$fm = Get-SdlcFrontmatter -Path $Plan
$currentId = $null
foreach ($rawLine in ($fm -split "`n")) {
    $line = $rawLine.TrimEnd("`r")
    if ($line -match '^\s*-\s+id:\s*(.+?)\s*$') { $currentId = $Matches[1].Trim().Trim('"').Trim("'"); continue }
    if ($null -eq $currentId) { continue }
    if ($line -match '^\s*depends_on:\s*(.*)$') {
        $raw = $Matches[1]
        if ($raw -match '^\s*\[(.*)\]\s*$') {
            $deps = @()
            foreach ($piece in ($Matches[1] -split ',')) {
                $id = $piece.Trim().Trim('"').Trim("'")
                if ($id) { $deps += $id }
            }
            ($slices | Where-Object { $_.Id -eq $currentId }) | ForEach-Object { $_.DependsOn = $deps }
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
# Reported by the spelling its author wrote, not by the comparison key. The key is
# lowercased and normalised so that two spellings of one file collide; showing it back would
# name a path that appears nowhere in the plan being fixed.
$owner = @{}
$overlaps = @()
foreach ($slice in $slices) {
    for ($i = 0; $i -lt $slice.Files.Count; $i++) {
        $file = $slice.Files[$i]
        $shown = $slice.Spellings[$i]
        if ($owner.ContainsKey($file)) {
            $overlaps += ("OVERLAP {0} <-> {1}: {2}" -f $owner[$file], $slice.Id, $shown)
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

if (-not $AcrossPlans) { exit 0 }

# --- cross-plan: no other live plan claims a file this one claims -----------------------
$root = Get-SdlcArtifactRoot -Cwd $base
if ($null -eq $root) {
    Stop-WithMessage "no artifact root resolves from $base - there is nothing to compare against, which is not the same as nothing to report."
}

# The plan under check must live under the root the comparison reads, or the comparison is
# against a different repository entirely and its healthy-looking output means nothing.
$planFull = (Resolve-Path -LiteralPath $Plan).Path
$rootFull = (Resolve-Path -LiteralPath $root).Path
if (-not $planFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
    Stop-WithMessage ("the plan is not under the artifact root being compared: plan {0}, root {1}. Pass -Root, or run from the repository the plan belongs to." -f $planFull, $rootFull)
}

$rootKey = ConvertTo-SdlcPathKey -Path $rootFull -BaseDirectory $base

# Claims under the artifact root leave the comparison: the version-8 rule makes two
# check-adding chains claim the charter by construction. Containment, which is the charter's
# own wording - not the hook's segment-equality skip, which answers a different question.
$mine = @{}
foreach ($claim in $read.Claims) {
    if (Test-SdlcPathUnderRoot -PathKey $claim.Key -RootKey $rootKey) { continue }
    $mine[$claim.Key] = $claim.Spelling
}

$readCount = 0; $liveCount = 0; $refusedCount = 0
$contested = @()

foreach ($other in (Get-SdlcPlanPath -Root $rootFull)) {
    if ((Resolve-Path -LiteralPath $other).Path -eq $planFull) { continue }   # by path, not by slug
    $readCount++

    # $otherPlan, not $plan: PowerShell is case-insensitive and $Plan is this script's own
    # parameter, so the obvious name silently shadows the path the whole script is about.
    try { $otherPlan = Read-SdlcPlan -Path $other -BaseDirectory $base }
    catch {
        $refusedCount++
        [Console]::Error.WriteLine("REFUSED $other - unreadable; excluded from the comparison.")
        continue
    }

    # Drop exactly what the gate hook drops for the same refusal: the claims for a
    # claim-level one, the whole plan for a plan-level one. A refusal elsewhere names itself
    # and the comparison carries on - it is not this plan's fault and not its hard stop.
    $planLevel = @($otherPlan.Refusals | Where-Object { $_.Kind -eq 'plan' })
    $claimLevel = @($otherPlan.Refusals | Where-Object { $_.Kind -eq 'claim' })
    if ($planLevel.Count -gt 0 -or $claimLevel.Count -gt 0) {
        $refusedCount++
        foreach ($r in $otherPlan.Refusals) {
            [Console]::Error.WriteLine(("REFUSED {0} [{1}] {2}" -f $otherPlan.Slug, $r.Rule, $r.Message))
        }
    }

    if (-not (Test-SdlcPlanIsLive -Plan $otherPlan)) { continue }
    $liveCount++

    foreach ($claim in $otherPlan.Claims) {
        if (Test-SdlcPathUnderRoot -PathKey $claim.Key -RootKey $rootKey) { continue }
        if ($mine.ContainsKey($claim.Key)) {
            $contested += [pscustomobject]@{ Spelling = $mine[$claim.Key]; Slug = $otherPlan.Slug }
        }
    }
}

# Counts only, never a verdict: on the refusal path the comparison may not have run. An
# overlay rather than a partition - a claim-level refusal leaves its plan live and counted
# refused - so these three do not sum and are not meant to.
$rootLeaf = Split-Path -Leaf $rootFull
Write-Output ("read {0} other plan.md under {1}/ ({2} live, {3} refused)" -f $readCount, $rootLeaf, $liveCount, $refusedCount)

if ($contested.Count -gt 0) {
    foreach ($c in $contested) {
        [Console]::Error.WriteLine(("CONTESTED {0}: this plan <-> {1}" -f $c.Spelling, $c.Slug))
    }
    Stop-WithMessage 'Two live plans claim one file. This is a planning conversation, not something to reconcile in the merge: settle which chain owns it and re-plan the other.'
}

Write-Output '  no contested files'
exit 0
