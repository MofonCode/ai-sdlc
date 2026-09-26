#Requires -Version 5.1
<#
.SYNOPSIS
    Decides a cycle's verification outcome from evidence, in code.

.DESCRIPTION
    THIS EXISTS BECAUSE AN AGENT MUST NOT RULE ON ITS OWN REPORT. An agent asked both to
    gather the evidence and to decide what it means rounds in the direction of done: not
    from dishonesty, but because the work visibly succeeded ten minutes ago. So the outcome
    is computed here, from evidence the deciding process did not produce, and the verify
    skill transcribes the result rather than forming a view about it.

    This replaces the decision that used to live in workflows/verify.js. A workflow script
    could also compute it, but nothing could TEST that computation: there is no JavaScript
    runner in this repository, so the old branch was only ever checked by grepping its
    source. A PowerShell script is exercised by the real runtime in the test suite, which
    makes this strictly stronger than what it replaces rather than a lateral move.

    EVERY NUMBER IS DERIVED. Traceability comes from test annotations, adherence from the
    diff against the plan's declaration, tiers from one function applied to two inputs.
    None of it is passed in, because a number a caller supplies is a number a caller can be
    wrong about (INV-07).

    THE RULE, in order. The first condition that matches wins, and the order is the point:

      1. actual tier exceeds predicted        -> halted-tier-escalated
      2. the build or the tests failed        -> blocked
      3. a verifiable criterion has no test   -> blocked
      4. a suppression has no justification   -> blocked
      5. a finding is still undispositioned   -> blocked
      6. otherwise                            -> passed

    Tier escalation outranks a failing build deliberately. A build that fails is a normal
    blocked cycle someone fixes; a tier escalation means the work turned out more dangerous
    than it was approved to be, and the approval does not stretch to cover it. Reporting
    that as an ordinary failure would invite a fix-and-retry instead of a person looking.

.PARAMETER CycleId
    The cycle to decide.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.PARAMETER BuildCommand
    The build gate. Resolved in this order: what a caller passes, then what the DECLARED PACK
    declares, then the .NET default for a solution that consumes no pack.

    The middle step was missing. pack.yaml declared its gates so that tool names would stay out of
    the charter — the charter excludes them and INV-04 delegates them to the pack — and nothing read
    the declaration. A solution on a pack that builds with anything other than `dotnet` would have
    had its gates run against a toolchain it does not use, while the pack sat there being correct
    and ignored.

.PARAMETER TestCommand
    The test gate. Same resolution, same reason.

.EXAMPLE
    .\Get-SdlcVerifyOutcome.ps1 -CycleId 20260924-a1b2
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CycleId,

    [Parameter()]
    [string] $RepoRoot,

    [Parameter()]
    [string] $BuildCommand,

    [Parameter()]
    [string] $TestCommand
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install. An em-dash then leaves this script as a CP1252 best-fit
# substitution, and U+201D best-fits to a plain double quote — which does not merely look
# wrong, it TERMINATES the JSON string and the caller receives a structurally invalid
# document. UTF8Encoding($false) rather than [System.Text.Encoding]::UTF8 because the
# latter prepends a BOM, which is not valid at the start of a JSON document.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

# Dot-sourced, not duplicated: git writes ordinary progress to stderr, and `2>&1` on a
# native command turns that into a terminating error under `Stop`. See SdlcGit.ps1.
. (Join-Path $PSScriptRoot 'SdlcGit.ps1')

# The pack owns the gate commands, because the charter is not allowed to name a tool.
. (Join-Path $PSScriptRoot 'SdlcPack.ps1')

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

$factsScript = Join-Path $PSScriptRoot 'Get-SdlcFacts.ps1'
if (-not (Test-Path -LiteralPath $factsScript)) {
    throw "Missing $factsScript — the outcome cannot be derived without the gatherer."
}

# --- the gates, from whoever is entitled to name them -----------------------
#
# A caller wins, then the declared pack, then the .NET default. The pack step is the one that was
# missing: the charter excludes tool names and delegates them, so the pack IS the authority here,
# and hardcoding `dotnet` made that delegation decorative.
$declaredPack = Get-SdlcDeclaredPack -CharterPath (Join-Path $RepoRoot '.sdlc/CHARTER.md')

if (-not $BuildCommand) { $BuildCommand = Get-SdlcPackGate -Name $declaredPack -Gate 'build' }
if (-not $TestCommand) { $TestCommand = Get-SdlcPackGate -Name $declaredPack -Gate 'test' }

# Solution zero consumes no pack, so a default has to exist somewhere. It lives here rather than in
# the charter, which is not allowed to name a tool.
if (-not $BuildCommand) { $BuildCommand = 'dotnet build' }
if (-not $TestCommand) { $TestCommand = 'dotnet test' }

$cycleDir = Join-Path $RepoRoot ".sdlc/cycles/$CycleId"
$specPath = ".sdlc/cycles/$CycleId/spec.md"
$planPath = ".sdlc/cycles/$CycleId/plan.md"

function Get-Facts {
    param([string] $Artifact)

    $full = Join-Path $RepoRoot $Artifact
    if (-not (Test-Path -LiteralPath $full)) { return $null }

    $json = (& $factsScript -Artifact $Artifact -RepoRoot $RepoRoot) -join "`n"
    if (-not $json.Trim()) { return $null }
    return ConvertFrom-Json $json
}

function Test-Field {
    param($Object, [string] $Name)

    if ($null -eq $Object) { return $false }
    return ($Object.PSObject.Properties.Name -contains $Name)
}

# A gate is run here rather than trusted from a caller. Exit code only: the output is for a
# person, and parsing it would be a second representation of the same fact.
function Invoke-Gate {
    param([string] $Command)

    if (-not $Command) { return $null }

    Push-Location $RepoRoot
    try {
        $r = Invoke-SdlcNative -Command 'cmd.exe' -Arguments @('/c', $Command)
        return $r.ok
    }
    finally {
        Pop-Location
    }
}

$blockers = New-Object System.Collections.ArrayList

# --- evidence ---------------------------------------------------------------

$specFacts = Get-Facts -Artifact $specPath
$planFacts = Get-Facts -Artifact $planPath

$criteriaTotal = 0
$criteriaVerifiable = 0
$criteriaCovered = 0
$uncovered = @()
$orphanAnnotations = @()
$suppressionCount = 0
$unjustified = @()

if ($null -ne $specFacts) {
    if (Test-Field $specFacts 'acceptance-criteria') {
        $criteria = @($specFacts.'acceptance-criteria')
        $criteriaTotal = $criteria.Count
        foreach ($c in $criteria) {
            if ((Test-Field $c 'verifiable') -and $c.verifiable) { $criteriaVerifiable++ }
        }
    }

    if ((Test-Field $specFacts 'traceability') -and $null -ne $specFacts.traceability) {
        $t = $specFacts.traceability
        if (Test-Field $t 'unmapped') { $uncovered = @($t.unmapped | Where-Object { $_ }) }
        if (Test-Field $t 'orphan-annotations') {
            $orphanAnnotations = @($t.'orphan-annotations' | Where-Object { $_ })
        }
        if (Test-Field $t 'suppression-count') { $suppressionCount = [int] $t.'suppression-count' }
        if (Test-Field $t 'unjustified-suppressions') {
            $unjustified = @($t.'unjustified-suppressions' | Where-Object { $_ })
        }
        $criteriaCovered = $criteriaVerifiable - $uncovered.Count
    }
}

$predictedTier = $null
$actualTier = $null
$tierEscalated = $false
$undeclared = @()

if ($null -ne $planFacts) {
    if ((Test-Field $planFacts 'blast-radius') -and $null -ne $planFacts.'blast-radius') {
        $predictedTier = [int] $planFacts.'blast-radius'.tier
    }
    if ((Test-Field $planFacts 'actual-blast-radius') -and $null -ne $planFacts.'actual-blast-radius') {
        $actualTier = [int] $planFacts.'actual-blast-radius'.tier
    }
    if ((Test-Field $planFacts 'plan-adherence') -and $null -ne $planFacts.'plan-adherence') {
        $a = $planFacts.'plan-adherence'
        if (Test-Field $a 'tier-escalated') { $tierEscalated = [bool] $a.'tier-escalated' }
        if (Test-Field $a 'undeclared') { $undeclared = @($a.undeclared | Where-Object { $_ }) }
    }
}

# Findings: anything still open or blocking without a recorded disposition. Every finding,
# from adversary or tool, at any severity, is dispositioned before a cycle may land.
#
# EVERY DOCUMENT IN THE CYCLE, not one file. A cycle interrogates its intent, its spec, its plan
# and its verification, each possibly over several rounds, and each run files its own document —
# the findings schema carries one target and one round per document, which is what says so.
# Reading a single findings.json would count the last interrogation only, so a blocking finding
# left undispositioned on the spec would vanish the moment the plan was interrogated.
$undispositioned = 0
$findingsDir = Join-Path $cycleDir 'findings'
if (Test-Path -LiteralPath $findingsDir) {
    foreach ($file in @(Get-ChildItem -LiteralPath $findingsDir -Filter '*.json' -File)) {
        $raw = (Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8)
        if (-not $raw.Trim()) { continue }

        $doc = ConvertFrom-Json $raw
        if (-not (Test-Field $doc 'findings')) { continue }

        foreach ($f in @($doc.findings)) {
            $state = ''
            if (Test-Field $f 'state') { $state = [string] $f.state }
            $hasDisposition = $false
            if (Test-Field $f 'disposition') { $hasDisposition = [bool] $f.disposition }
            if (($state -eq 'open' -or $state -eq 'blocking') -and -not $hasDisposition) {
                $undispositioned++
            }
        }
    }
}

# --- gates ------------------------------------------------------------------

$buildPassed = Invoke-Gate -Command $BuildCommand
$testsPassed = Invoke-Gate -Command $TestCommand

# --- the decision -----------------------------------------------------------

$outcome = 'passed'
$reason = 'every gate passed and nothing is outstanding'

if ($null -eq $specFacts -or $null -eq $planFacts) {
    # Not a pass and not a tier halt: there is nothing to decide from. Saying `passed`
    # because no evidence contradicted it is the failure this whole script exists to stop.
    $outcome = 'blocked'
    $reason = 'missing spec.md or plan.md, so there is no evidence to decide from'
    $null = $blockers.Add($reason)
}
elseif ($tierEscalated) {
    $outcome = 'halted-tier-escalated'
    $reason = "actual tier $actualTier exceeds the approved tier $predictedTier"
    $null = $blockers.Add($reason)
}
else {
    if ($false -eq $buildPassed) { $null = $blockers.Add('the build failed') }
    if ($false -eq $testsPassed) { $null = $blockers.Add('a test failed') }
    if ($uncovered.Count -gt 0) {
        $null = $blockers.Add('verifiable criteria with no annotated test: ' + ($uncovered -join ', '))
    }
    if ($orphanAnnotations.Count -gt 0) {
        $null = $blockers.Add('annotations naming criteria the spec does not declare: ' + ($orphanAnnotations -join ', '))
    }
    if ($unjustified.Count -gt 0) {
        $null = $blockers.Add('suppressions with no written justification: ' + ($unjustified -join ', '))
    }
    if ($undispositioned -gt 0) {
        $null = $blockers.Add("$undispositioned finding(s) still undispositioned")
    }

    if ($blockers.Count -gt 0) {
        $outcome = 'blocked'
        $reason = $blockers[0]
    }
}

$result = [ordered]@{
    'cycle-id'                 = $CycleId
    outcome                    = $outcome
    reason                     = $reason
    blockers                   = @($blockers)
    'predicted-tier'           = $predictedTier
    'actual-tier'              = $actualTier
    'tier-escalated'           = $tierEscalated
    undeclared                 = @($undeclared)
    'criteria-total'           = $criteriaTotal
    'criteria-verifiable'      = $criteriaVerifiable
    'criteria-covered'         = $criteriaCovered
    uncovered                  = @($uncovered)
    'orphan-annotations'       = @($orphanAnnotations)
    'suppression-count'        = $suppressionCount
    'unjustified-suppressions' = @($unjustified)
    undispositioned            = $undispositioned
    'build-passed'             = $buildPassed
    'tests-passed'             = $testsPassed
}

ConvertTo-Json -InputObject $result -Depth 6
