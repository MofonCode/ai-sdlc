#Requires -Version 5.1
<#
.SYNOPSIS
    Records what was done about one finding. Refuses anything the schema would reject.

.DESCRIPTION
    EVERY FINDING, FROM ADVERSARY OR TOOL, AT ANY STATE, IS DISPOSITIONED. Verification will not
    pass a cycle carrying an open or blocking finding with no disposition — and nothing could
    write one, so that gate could only ever be satisfied by a cycle that had no findings at all.

    STATE AND DISPOSITION ARE DIFFERENT THINGS, and this writes only the second. State says
    whether the cycle halts and was assigned in code from the citation, which is the
    anti-inflation guard; disposition says what was done. Letting a repair re-rank a finding it
    found inconvenient would defeat the point of assigning state by rule, so state is not
    writable here at all.

    `rejected` AND `accepted-wont-fix` ARE NEVER THE SAME ANSWER. `rejected` means the finding
    was wrong and says nothing about the invariant it cited. `accepted-wont-fix` means the
    finding was right and we knowingly did not act, which is the override count and the evidence
    that an invariant may be too broad. Merging them corrupts the only signal the charter
    improves from, which is why both demand a written reason.

    THE SCHEMA IS THE AUTHORITY, NOT THIS SCRIPT. The rule that a dismissal carries its reason
    lives in schemas/findings.schema.json. So rather than restating it here, the edit is written
    and the document revalidated: if the schema refuses it, the file is restored byte-for-byte
    and the schema's own complaint is reported. One rule, one home, and no way for the two to
    disagree.

    Deliberately NOT enforced here: that `promoted` belongs only to a suggestion. The charter
    says so, the schema does not, and adding the rule to this script instead of to the schema
    would put a sixth rule in a seventh place.

.PARAMETER CycleId
    The cycle whose findings to search.

.PARAMETER Finding
    The finding id, which is unique within a cycle.

.PARAMETER Disposition
    fixed, rejected, accepted-wont-fix, deferred-to-dec, or promoted.

.PARAMETER Reason
    Why. Required by the schema for `rejected` and `accepted-wont-fix`.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Set-SdlcDisposition.ps1 -CycleId 20260925-a1b2 -Finding spec-r1-check1 -Disposition fixed
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CycleId,

    [Parameter(Mandatory = $true)]
    [string] $Finding,

    [Parameter(Mandatory = $true)]
    [ValidateSet('fixed', 'rejected', 'accepted-wont-fix', 'deferred-to-dec', 'promoted')]
    [string] $Disposition,

    [Parameter()]
    [string] $Reason,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that terminates
# a JSON string. A disposition reason is prose.
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
    'cycle-id'    = $CycleId
    finding       = $Finding
    disposition   = $Disposition
    document      = $null
    'state'       = $null
    outcome       = 'not-started'
    errors        = @()
}

function Write-Result {
    param([string] $Outcome, [string[]] $Errors, [int] $Exit)

    $result['outcome'] = $Outcome
    $result['errors'] = @($Errors)
    ConvertTo-Json -InputObject $result -Depth 6
    exit $Exit
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$findingsDir = Join-Path $RepoRoot ".sdlc/cycles/$CycleId/findings"
if (-not (Test-Path -LiteralPath $findingsDir)) {
    Write-Result 'refused-no-findings' @(
        "no findings have been filed for '$CycleId'. Interrogate something first.") 2
}

# --- find it, and insist there is exactly one -------------------------------

$matches = New-Object System.Collections.ArrayList

foreach ($file in @(Get-ChildItem -LiteralPath $findingsDir -Filter '*.json' -File)) {
    $doc = ConvertFrom-Json ((Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8))
    if (-not (Test-Field $doc 'findings')) { continue }

    foreach ($f in @($doc.findings)) {
        if ((Test-Field $f 'id') -and ([string] $f.id -eq $Finding)) {
            $null = $matches.Add([pscustomobject]@{ file = $file.FullName; document = $doc; finding = $f })
        }
    }
}

if ($matches.Count -eq 0) {
    Write-Result 'refused-not-found' @(
        "no finding '$Finding' in $findingsDir") 1
}

if ($matches.Count -gt 1) {
    # Ids are unique within a cycle by construction, so two means something filed a document it
    # should not have. Guessing which one to write would record a disposition against a finding
    # nobody chose.
    Write-Result 'refused-ambiguous' @(
        "'$Finding' appears in $($matches.Count) documents, so ids are not unique in this cycle") 1
}

$hit = $matches[0]
$result['document'] = ((($hit.file.Substring($RepoRoot.Length)).TrimStart('\', '/')) -replace '\\', '/')
if (Test-Field $hit.finding 'state') { $result['state'] = [string] $hit.finding.state }

# --- write the disposition, then let the schema rule on it ------------------

$original = Get-Content -LiteralPath $hit.file -Raw -Encoding UTF8

if (Test-Field $hit.finding 'disposition') {
    $hit.finding.disposition = $Disposition
}
else {
    Add-Member -InputObject $hit.finding -NotePropertyName 'disposition' -NotePropertyValue $Disposition
}

if ($Reason) {
    if (Test-Field $hit.finding 'disposition-reason') {
        $hit.finding.'disposition-reason' = $Reason
    }
    else {
        Add-Member -InputObject $hit.finding -NotePropertyName 'disposition-reason' -NotePropertyValue $Reason
    }
}

$text = ConvertTo-Json -InputObject $hit.document -Depth 12
[System.IO.File]::WriteAllText($hit.file, $text, (New-Object System.Text.UTF8Encoding $false))

$validator = Join-Path $PSScriptRoot 'Test-SdlcArtifact.ps1'
if (-not (Test-Path -LiteralPath $validator)) {
    [System.IO.File]::WriteAllText($hit.file, $original, (New-Object System.Text.UTF8Encoding $false))
    Write-Result 'refused-no-validator' @("the validator is missing at $validator") 2
}

$verdictJson = (& $validator -Path $hit.file -Schema 'findings' -RepoRoot $RepoRoot) -join "`n"
$verdict = $null
if ($verdictJson.Trim()) { $verdict = ConvertFrom-Json $verdictJson }

if (($null -eq $verdict) -or (-not $verdict.ok)) {
    # RESTORED, not left half-written. The commonest cause is a dismissal with no reason, which
    # is the schema enforcing the rule that keeps `rejected` and `accepted-wont-fix` apart.
    [System.IO.File]::WriteAllText($hit.file, $original, (New-Object System.Text.UTF8Encoding $false))

    $errors = @('the document was restored unchanged')
    if ($null -ne $verdict) { $errors = @($verdict.errors) + $errors }

    Write-Result 'refused-schema' $errors 1
}

Write-Result 'recorded' @() 0
