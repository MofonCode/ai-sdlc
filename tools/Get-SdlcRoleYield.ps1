#Requires -Version 5.1
<#
.SYNOPSIS
    Reports what each interrogator role actually produced, from the findings corpus. Read-only.

.DESCRIPTION
    FOUR ROLES MAY PRODUCE TWO AND A HALF ROLES' WORTH OF FINDINGS AT FOUR ROLES' COST. That was
    the suspicion; this reports the evidence instead of arguing about it. Roles are the fan-out
    unit of an interrogation, so a role that earns nothing is the single most expensive thing in
    the framework - and nothing measured it.

    IT REPORTS AND NEVER RECOMMENDS. Dropping or adding a role is a charter amendment (the roles
    are charter data), so it takes an approved charter intent. A script that proposed one would
    be deciding something INV-08 reserves for a person. Same division as Get-SdlcSignals: a
    signal is a prompt to investigate, not a verdict.

    NOTHING IS STORED. Every number is derived from the corpus on demand. A stored yield table
    would be a second list beside the findings, which is the INV-03 failure this repository has
    already made twice.

    A SUBSTITUTED ROLE IS NOT A ROLE THAT FOUND NOTHING. When the artifact under interrogation
    IS the charter, `charter` is removed and `form` takes its place - a role cannot audit the
    document that defines it. So `charter` shows zero findings on every charter interrogation,
    and reading that as "produces nothing, drop it" would delete the role that checks every
    OTHER artifact against the invariants. The two cases are reported separately and the
    `eligible-runs` count is what a yield judgement must divide by.

    IT REFUSES TO CONCLUDE ON TOO LITTLE DATA. The charter already sets a ten-cycle floor for
    the zero-cite query, for the same reason: one interrogation of one artifact tells you about
    that artifact, not about a role. Below the floor every verdict reads `insufficient-data`,
    and the counts are still reported because they are true - it is the INFERENCE that is
    unavailable, not the evidence.

    WHAT IT DELIBERATELY DOES NOT COMPUTE: cost. Cost is one agent per role plus one per
    candidate at triage, and the corpus records merged findings rather than candidates, so a
    per-role cost would have to be invented. `US-22` is a live story about exactly that -
    load-bearing numbers with no verifying source - and adding a fabricated one here to make the
    report look complete would be the same defect. `candidates-recorded` says plainly whether
    the corpus can answer it.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.PARAMETER Floor
    Eligible runs required before a verdict is offered. Defaults to 10, matching the charter's
    zero-cite floor.

.PARAMETER AsJson
    Emit JSON instead of a table.

.EXAMPLE
    .\Get-SdlcRoleYield.ps1

.EXAMPLE
    .\Get-SdlcRoleYield.ps1 -AsJson | ConvertFrom-Json
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoRoot,

    [Parameter()]
    [int] $Floor = 10,

    [Parameter()]
    [switch] $AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# See Get-SdlcCycleState.ps1: 5.1 encodes stdout in the console codepage, and an em-dash
# best-fits to a plain double quote, which terminates a JSON string rather than merely looking
# wrong.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

<#
.SYNOPSIS
    Reads a property that may not exist, which under StrictMode is not the same as null.

.DESCRIPTION
    `Set-StrictMode -Version Latest` THROWS on a property a PSCustomObject does not have, and
    ConvertFrom-Json omits absent JSON keys rather than adding them as null. So a finding written
    before it was dispositioned has no `disposition` property at all, and reading it directly
    terminates the script - which is strict mode doing its job, not a nuisance to switch off.
#>
function Get-SdlcProperty {
    [CmdletBinding()]
    param(
        [Parameter()] $InputObject,
        [Parameter(Mandatory)] [string] $Name
    )

    if ($null -eq $InputObject) { return $null }

    # A hashtable does not expose its keys as PSObject properties, and this is a general
    # helper, so handle both rather than leaving a footgun for the next caller.
    if ($InputObject -is [System.Collections.IDictionary]) {
        if (-not $InputObject.Contains($Name)) { return $null }
        return $InputObject[$Name]
    }

    if ($InputObject.PSObject.Properties.Name -notcontains $Name) { return $null }
    return $InputObject.$Name
}

<#
.SYNOPSIS
    The pure aggregation. Takes already-read findings documents and returns per-role rows.

.DESCRIPTION
    Separated from the file reading so it can be tested without a corpus on disk. Every
    judgement lives here; the caller only collects documents.
#>
function Get-SdlcRoleYieldReport {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        # Each element: @{ 'cycle-id' = ...; target = ...; findings = @(...) }
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [array] $Documents,

        [Parameter(Mandatory)]
        [string[]] $DeclaredRoles,

        [Parameter()]
        [int] $Floor = 10
    )

    # Dispositions split the evidence into opposite signals, exactly as in Get-SdlcSignals.
    # `rejected` means the finding was WRONG, which is the only disposition that argues against
    # the role. `accepted-wont-fix` means it was RIGHT and we chose not to act, which says
    # nothing bad about the role at all. Merging them would make an accurate role look noisy.
    $rows = New-Object System.Collections.ArrayList
    $eligibleRuns = @{}
    foreach ($role in $DeclaredRoles) { $eligibleRuns[$role] = 0 }

    foreach ($doc in $Documents) {
        $isCharterTarget = ([string] (Get-SdlcProperty $doc 'target')) -match 'charter'
        foreach ($role in $DeclaredRoles) {
            # `charter` is removed and `form` substituted when the artifact IS the charter, so
            # that run cannot be evidence about `charter` in either direction.
            $substituted = $isCharterTarget -and ($role -eq 'charter')
            if (-not $substituted) { $eligibleRuns[$role]++ }
        }
    }

    foreach ($role in $DeclaredRoles) {
        $mine = @()
        foreach ($doc in $Documents) {
            foreach ($f in @(Get-SdlcProperty $doc 'findings')) {
                if (([string] (Get-SdlcProperty $f 'role')) -eq $role) { $mine += $f }
            }
        }

        $states = @($mine | ForEach-Object { [string] (Get-SdlcProperty $_ 'state') })
        $dispositions = @($mine | ForEach-Object { [string] (Get-SdlcProperty $_ 'disposition') })

        $blocking = @($states | Where-Object { $_ -eq 'blocking' }).Count
        $fixed = @($dispositions | Where-Object { $_ -eq 'fixed' }).Count
        $rejected = @($dispositions | Where-Object { $_ -eq 'rejected' }).Count
        $wontFix = @($dispositions | Where-Object { $_ -eq 'accepted-wont-fix' }).Count
        $deferred = @($dispositions | Where-Object { $_ -eq 'deferred-to-dec' }).Count
        $undispositioned = @($dispositions | Where-Object { [string]::IsNullOrEmpty($_) }).Count

        $runs = $eligibleRuns[$role]

        # The verdict is a prompt to investigate, never an instruction. Three readings, and the
        # first is the one that needs a person: a role with no blocking findings is either
        # decoration, or it is preventing things upstream, and those call for opposite actions.
        $verdict = 'insufficient-data'
        if ($runs -ge $Floor) {
            if ($mine.Count -eq 0) {
                $verdict = 'no-findings-at-all'
            }
            elseif ($blocking -eq 0) {
                $verdict = 'never-blocked'
            }
            elseif ($rejected -gt $fixed) {
                $verdict = 'more-wrong-than-right'
            }
            else {
                $verdict = 'earning-its-place'
            }
        }

        $null = $rows.Add([pscustomobject] [ordered] @{
            role                = $role
            'eligible-runs'     = $runs
            findings            = $mine.Count
            blocking            = $blocking
            fixed               = $fixed
            rejected            = $rejected
            'accepted-wont-fix' = $wontFix
            'deferred-to-dec'   = $deferred
            undispositioned     = $undispositioned
            verdict             = $verdict
        })
    }

    return @{
        rows  = @($rows)
        runs  = $Documents.Count
        floor = $Floor
        # Cost cannot be derived from this corpus: it records merged findings, not the
        # candidates each role raised, and triage spends one agent per candidate. Reported as a
        # fact about the corpus rather than filled in with a guess.
        'candidates-recorded' = $false
        'below-floor' = ($Documents.Count -lt $Floor)
    }
}

<#
.SYNOPSIS
    Reads the roles the charter declares, so the report cannot invent or miss one.
#>
function Get-SdlcDeclaredRoles {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [string] $CharterPath
    )

    $text = Get-Content -LiteralPath $CharterPath -Raw -Encoding UTF8

    # Scoped to the roles section. The subsection after it tabulates ARTIFACTS, and reading the
    # whole chapter would silently add those to the role list.
    $heading = [regex]::Match($text, '(?m)^#{2,4}\s+Interrogator roles.*$')
    if (-not $heading.Success) { throw "The charter declares no Interrogator roles section: $CharterPath" }

    $rest = $text.Substring($heading.Index + $heading.Length)
    $next = [regex]::Match($rest, '(?m)^#{2,4}\s')
    $section = if ($next.Success) { $rest.Substring(0, $next.Index) } else { $rest }

    $roles = @()
    foreach ($m in [regex]::Matches($section, '(?m)^\|\s*`([^`]+)`\s*\|')) {
        $roles += $m.Groups[1].Value
    }

    if ($roles.Count -eq 0) { throw "No roles found in the charter's Interrogator roles table." }
    return $roles
}

# --- gather -----------------------------------------------------------------

if (-not $RepoRoot) {
    $RepoRoot = (& git rev-parse --show-toplevel)
    if ($LASTEXITCODE -ne 0) { throw 'Not inside a git repository, and -RepoRoot was not given.' }
    $RepoRoot = ([string] ($RepoRoot | Out-String)).Trim()
}

$charterPath = Join-Path $RepoRoot '.sdlc/CHARTER.md'
$declaredRoles = Get-SdlcDeclaredRoles -CharterPath $charterPath

$documents = @()
$cyclesDir = Join-Path $RepoRoot '.sdlc/cycles'
if (Test-Path -LiteralPath $cyclesDir) {
    foreach ($file in @(Get-ChildItem -LiteralPath $cyclesDir -Filter 'findings.json' -File -Recurse)) {
        $raw = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $documents += ($raw | ConvertFrom-Json)
    }
}

$report = Get-SdlcRoleYieldReport -Documents $documents -DeclaredRoles $declaredRoles -Floor $Floor

# --- report -----------------------------------------------------------------

if ($AsJson) {
    $report | ConvertTo-Json -Depth 6
}
else {
    $columns = @('role', 'eligible-runs', 'findings', 'blocking', 'fixed', 'rejected',
        'undispositioned', 'verdict')
    $report.rows | Format-Table -AutoSize -Property $columns | Out-String | Write-Host

    Write-Host ("{0} interrogation(s) in the corpus; the floor for a verdict is {1}." -f
        $report.runs, $report.floor)

    if ($report.'below-floor') {
        Write-Host 'Below the floor: the counts are true, the INFERENCE is not available yet.'
    }

    Write-Host ('Cost is not reported. It is one agent per role plus one per candidate at ' +
        'triage, and the corpus records merged findings rather than candidates, so a per-role ' +
        'cost would have to be invented.')
}
