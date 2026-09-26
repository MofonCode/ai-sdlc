#Requires -Version 5.1
<#
.SYNOPSIS
    Files one interrogation's findings under its cycle, after validating them.

.DESCRIPTION
    THE MISSING WIRE. Four things read a cycle's findings — verify counts what is still
    undispositioned, landing renders the suggestions file, the renderer produces the human view,
    and the signals query aggregates the corpus — and nothing wrote one. Every consequence was
    silent rather than loud: verify found zero undispositioned findings because it found no
    findings at all, so a cycle carrying blocking findings passed its gate.

    A workflow script has no filesystem access by design, so `interrogate` returns its document
    and the caller writes it. What the caller must not also own is the LAYOUT and the CHECK,
    because four callers each spelling out a path is four chances to drift. So this script owns
    both: where a document goes, and whether it is allowed to go there.

    ONE DOCUMENT PER RUN, NOT ONE PER CYCLE. The findings schema carries a single `target` and
    `round` per document, which says what it is: the record of one interrogation. A cycle
    interrogates its intent, its spec, its plan and its verification, each possibly over several
    rounds, so it accumulates several documents. Merging them into one file would need a target
    and round per finding, which the schema deliberately does not have — and it would collapse an
    append-only log, which is the thing the retention rule forbids.

    IT VALIDATES BEFORE IT WRITES. The findings schema is where the anti-inflation rules live: a
    blocking finding must carry a citation, `unclassified` can never block, a mechanical finding
    never sits at `open`, a location pair belongs only to a contradiction, a dismissal states its
    reason. An invalid document is refused outright rather than filed with a warning, because a
    warning on the way in is a warning nobody sees on the way out.

    AND IT REFUSES A DUPLICATE ID. Ids must be unique within a cycle, not merely within a run,
    or dispositioning a finding by id is ambiguous across the cycle's own record.

.PARAMETER CycleId
    The cycle these findings belong to.

.PARAMETER Path
    The document to file, as the caller wrote it out.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Add-SdlcFindings.ps1 -CycleId 20260925-a1b2 -Path interrogation.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CycleId,

    [Parameter(Mandatory = $true)]
    [string] $Path,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that terminates
# a JSON string. Finding summaries are agent-written prose and are full of them.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

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

function Test-Field {
    param($Object, [string] $Name)

    if ($null -eq $Object) { return $false }
    return ($Object.PSObject.Properties.Name -contains $Name)
}

$result = [ordered]@{
    'cycle-id' = $CycleId
    filed      = $null
    target     = $null
    round      = $null
    blocking   = 0
    open       = 0
    suggested  = 0
    outcome    = 'not-started'
    errors     = @()
}

function Write-Result {
    param([string] $Outcome, [string[]] $Errors, [int] $Exit)

    $result['outcome'] = $Outcome
    $result['errors'] = @($Errors)
    ConvertTo-Json -InputObject $result -Depth 6
    exit $Exit
}

if ($CycleId -notmatch '^[0-9]{8}-[0-9a-f]{4}$') {
    Write-Result 'refused-bad-cycle-id' @(
        "'$CycleId' is not a cycle id. The form is yyyymmdd plus four hex characters.") 2
}

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Result 'refused-no-input' @("no document at '$Path'") 2
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

# --- 1. is it the shape it claims to be -------------------------------------

$validator = Join-Path $PSScriptRoot 'Test-SdlcArtifact.ps1'
if (-not (Test-Path -LiteralPath $validator)) {
    # Fails closed. A document that could not be checked has not been approved.
    Write-Result 'refused-no-validator' @("the validator is missing at $validator") 2
}

$verdictJson = (& $validator -Path $Path -Schema 'findings' -RepoRoot $RepoRoot) -join "`n"
$verdict = $null
if ($verdictJson.Trim()) { $verdict = ConvertFrom-Json $verdictJson }

if ($null -eq $verdict) {
    Write-Result 'refused-validator-silent' @('the validator returned nothing') 2
}

if (-not $verdict.ok) {
    Write-Result 'refused-invalid' @($verdict.errors) 1
}

# --- 2. does it agree with the cycle it is being filed under ----------------

$doc = ConvertFrom-Json ((Get-Content -LiteralPath $Path -Raw -Encoding UTF8))

$docCycle = $null
if (Test-Field $doc 'cycle-id') { $docCycle = [string] $doc.'cycle-id' }

if ($docCycle -ne $CycleId) {
    # Not pedantry. The document travels into the retro and then into the corpus, where its own
    # `cycle-id` is the only thing saying where it came from — so filing it under a directory
    # that disagrees would produce evidence attributed to the wrong cycle forever.
    Write-Result 'refused-cycle-mismatch' @(
        "the document declares cycle '$docCycle' and is being filed under '$CycleId'") 1
}

$target = [string] $doc.target
$round = 1
if ((Test-Field $doc 'round') -and $doc.round) { $round = [int] $doc.round }

$result['target'] = $target
$result['round'] = $round

$findings = @()
if (Test-Field $doc 'findings') { $findings = @($doc.findings) }

foreach ($f in $findings) {
    $state = ''
    if (Test-Field $f 'state') { $state = [string] $f.state }
    if ($state -eq 'blocking') { $result['blocking']++ }
    elseif ($state -eq 'open') { $result['open']++ }
    else { $result['suggested']++ }
}

# --- 3. is every id still unique within this cycle --------------------------

$cycleDir = Join-Path $RepoRoot ".sdlc/cycles/$CycleId"
$findingsDir = Join-Path $cycleDir 'findings'
$destination = Join-Path $findingsDir "$target.r$round.json"

$existingIds = @{}
if (Test-Path -LiteralPath $findingsDir) {
    foreach ($sibling in @(Get-ChildItem -LiteralPath $findingsDir -Filter '*.json' -File)) {
        # A re-run of the same interrogation replaces its own document, so its own ids are not
        # a collision with itself.
        if ($sibling.FullName -eq $destination) { continue }

        $siblingDoc = ConvertFrom-Json ((Get-Content -LiteralPath $sibling.FullName -Raw -Encoding UTF8))
        if (-not (Test-Field $siblingDoc 'findings')) { continue }
        foreach ($f in @($siblingDoc.findings)) {
            if (Test-Field $f 'id') { $existingIds[[string] $f.id] = $sibling.Name }
        }
    }
}

$collisions = @()
foreach ($f in $findings) {
    if (-not (Test-Field $f 'id')) { continue }
    $id = [string] $f.id
    if ($existingIds.ContainsKey($id)) {
        $collisions += "$id is already used by $($existingIds[$id])"
    }
}

if ($collisions.Count -gt 0) {
    Write-Result 'refused-duplicate-id' $collisions 1
}

# --- 4. file it ------------------------------------------------------------

if (-not (Test-Path -LiteralPath $findingsDir)) {
    $null = New-Item -ItemType Directory -Path $findingsDir -Force
}

$replaced = Test-Path -LiteralPath $destination

# Rewritten from the parsed document rather than copied, so what lands is what was validated
# in the form this script read it.
$text = ConvertTo-Json -InputObject $doc -Depth 12
[System.IO.File]::WriteAllText($destination, $text, (New-Object System.Text.UTF8Encoding $false))

$result['filed'] = ((($destination.Substring($RepoRoot.Length)).TrimStart('\', '/')) -replace '\\', '/')

$outcome = 'filed'
if ($replaced) { $outcome = 'filed-replacing-a-rerun' }

Write-Result $outcome @() 0
