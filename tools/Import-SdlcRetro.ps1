#Requires -Version 5.1
<#
.SYNOPSIS
    Copies landed retros into the corpus, keyed by cycle id, and refuses to overwrite one that
    differs.

.DESCRIPTION
    THIS IS THE MECHANISM THE CHARTER'S EXEMPTION NAMES. INV-03 forbids a second representation of
    a fact without, in the same change, the check that fails when the two disagree — and the retro
    corpus is a copy of every solution's retros sitting beside the originals. The exemption for it
    is narrow and conditional: a copy of an IMMUTABLE artifact is not a second list, because
    divergence is impossible, PROVIDED the mechanism that makes the copy refuses to overwrite with
    different content.

    Until this existed there was no such mechanism, and the charter says so about itself: "None is
    built yet, so the retro corpus does not currently qualify." So the refusal below is not
    defensive coding. It is the entire reason the corpus is permitted to exist.

    A RETRO THAT CHANGED AFTER LANDING MEANS ONE OF TWO THINGS, and both are worse than a missed
    absorption: the source was edited after the fact, or two cycles share an id. Neither is
    something to merge past, so a difference stops the run and names the file.

    IDEMPOTENT BY ID, NOT BY A LEDGER. Re-running is safe because an identical copy is a no-op, and
    that is deduplication enough: a "retros processed" ledger would be a second list beside the
    corpus directory, which is derivable by listing it. There is no gap check either — cycle ids are
    assigned at intent creation, so completion order is arbitrary and a gap means either "not yet
    absorbed" or "not yet finished", indistinguishably. Re-running removes the need for one.

    THE DIRECTORY FORM IS A LOOP OVER THE SINGLE-FILE CASE. One piece of logic, not two: no
    reconciliation, nothing bidirectional, nothing stateful. The single-file case is the normal one
    — you are standing there when a cycle lands — and the directory form only buys convenience.

.PARAMETER Path
    A retro file, or a directory to search for retros. A solution repository works: every
    .sdlc/cycles/<id>/retro.md under it is found.

.PARAMETER Corpus
    Where the corpus lives, relative to this repository. Defaults to `retros`.

.PARAMETER RepoRoot
    The ai-sdlc repository the corpus belongs to. Defaults to the enclosing git repository.

.EXAMPLE
    .\Import-SdlcRetro.ps1 -Path ..\MofonCode.Core\.sdlc\cycles\20260925-a7f3\retro.md

.EXAMPLE
    .\Import-SdlcRetro.ps1 -Path ..\MofonCode.Core
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [Parameter()]
    [string] $Corpus = 'retros',

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that terminates
# a JSON string. A retro is prose and is full of them.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

. (Join-Path $PSScriptRoot 'SdlcArtifact.ps1')

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

function Get-FrontmatterField {
    param([string] $File, [string] $Field)

    $lines = @(Get-Content -LiteralPath $File -Encoding UTF8)
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $null }

    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match ('^' + [regex]::Escape($Field) + ':\s*(.+)$')) {
            return $Matches[1].Trim()
        }
    }

    return $null
}

$result = [ordered]@{
    corpus    = ($Corpus -replace '\\', '/')
    examined  = 0
    imported  = 0
    unchanged = 0
    refused   = 0
    entries   = @()
    outcome   = 'not-started'
    errors    = @()
}

$entries = New-Object System.Collections.ArrayList

function Write-Result {
    param([string] $Outcome, [string[]] $Errors, [int] $Exit)

    $result['entries'] = @($entries)
    $result['outcome'] = $Outcome
    $result['errors'] = @($Errors)
    ConvertTo-Json -InputObject $result -Depth 6
    exit $Exit
}

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Result 'refused-no-source' @("nothing at '$Path'") 2
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

# --- what to import ---------------------------------------------------------

$sources = New-Object System.Collections.ArrayList
$source = (Resolve-Path -LiteralPath $Path).Path

if (Test-Path -LiteralPath $source -PathType Container) {
    foreach ($file in @(Get-ChildItem -LiteralPath $source -Filter 'retro.md' -File -Recurse)) {
        $null = $sources.Add($file.FullName)
    }
}
else {
    $null = $sources.Add($source)
}

if ($sources.Count -eq 0) {
    # Not an error. A solution with no landed cycles has no retros, and saying so is more useful
    # than failing.
    Write-Result 'nothing-to-import' @("no retro.md found under '$Path'") 0
}

$validator = Join-Path $PSScriptRoot 'Test-SdlcArtifact.ps1'
$corpusRoot = Join-Path $RepoRoot $Corpus

foreach ($file in $sources) {
    $result['examined']++

    # --- is it a retro at all ------------------------------------------------

    if ((Get-SdlcArtifactKind -RelativePath (($file -replace '\\', '/'))) -ne 'retro') {
        $null = $entries.Add([pscustomobject]@{
            source = ($file -replace '\\', '/'); outcome = 'refused'; detail = 'not a retro.md' })
        $result['refused']++
        continue
    }

    if (Test-Path -LiteralPath $validator) {
        $verdictJson = (& $validator -Path $file -Schema 'retro' -RepoRoot (Split-Path -Parent $file)) -join "`n"
        $verdict = $null
        if ($verdictJson.Trim()) { $verdict = ConvertFrom-Json $verdictJson }

        if (($null -eq $verdict) -or (-not $verdict.ok)) {
            # A retro missing `landed-at` or `solution` cannot be interpreted once it is in the
            # corpus: the landing date lives in the solution repository's history, which this
            # repository does not have, and a retro that does not say where it came from cannot be
            # attributed. So it is refused here rather than filed and puzzled over later.
            $detail = 'does not satisfy the retro schema'
            if ($null -ne $verdict) { $detail = ($verdict.errors -join '; ') }

            $null = $entries.Add([pscustomobject]@{
                source = ($file -replace '\\', '/'); outcome = 'refused'; detail = $detail })
            $result['refused']++
            continue
        }
    }

    $cycleId = Get-FrontmatterField -File $file -Field 'cycle-id'
    $solution = Get-FrontmatterField -File $file -Field 'solution'

    if (-not $cycleId -or -not $solution) {
        $null = $entries.Add([pscustomobject]@{
            source = ($file -replace '\\', '/'); outcome = 'refused'
            detail = 'no cycle-id or no solution in the frontmatter' })
        $result['refused']++
        continue
    }

    # --- where it goes -------------------------------------------------------
    #
    # Partitioned by solution, because aggregation groups by solution and pack rather than pooling
    # everything: "where does our process leak" may have a genuinely different answer for a library
    # than for a competition, and pooling them hides that rather than revealing it.
    $destinationDir = Join-Path $corpusRoot $solution
    $destination = Join-Path $destinationDir "$cycleId.md"
    $relative = ((($destination.Substring($RepoRoot.Length)).TrimStart('\', '/')) -replace '\\', '/')

    $incoming = [string] (Get-Content -LiteralPath $file -Raw -Encoding UTF8)

    if (Test-Path -LiteralPath $destination) {
        $existing = [string] (Get-Content -LiteralPath $destination -Raw -Encoding UTF8)

        # Compared on content with line endings normalised, because a copy that crossed a
        # checkout with different settings is the same retro and refusing it would make the
        # mechanism unusable rather than strict.
        $a = $existing -replace "`r`n", "`n"
        $b = $incoming -replace "`r`n", "`n"

        if ($a -eq $b) {
            $null = $entries.Add([pscustomobject]@{
                source = ($file -replace '\\', '/'); outcome = 'unchanged'; detail = $relative })
            $result['unchanged']++
            continue
        }

        # THE REFUSAL THE EXEMPTION RESTS ON. Not overwritten, not merged.
        $null = $entries.Add([pscustomobject]@{
            source = ($file -replace '\\', '/'); outcome = 'refused'
            detail = "$relative already exists with DIFFERENT content. Either the source was " +
                     'edited after landing or two cycles share an id; both are worse problems ' +
                     'than a missed absorption.' })
        $result['refused']++
        continue
    }

    if (-not (Test-Path -LiteralPath $destinationDir)) {
        $null = New-Item -ItemType Directory -Path $destinationDir -Force
    }

    # Byte-for-byte, so the comparison above is meaningful next time.
    Copy-Item -LiteralPath $file -Destination $destination -Force

    $null = $entries.Add([pscustomobject]@{
        source = ($file -replace '\\', '/'); outcome = 'imported'; detail = $relative })
    $result['imported']++
}

if ($result['refused'] -gt 0) {
    # Non-zero, because a check that only prints gets skimmed — and this one refusing is the
    # condition under which the corpus stops being a permitted copy.
    Write-Result 'refused' @("$($result['refused']) retro(s) were not imported") 1
}

Write-Result 'imported' @() 0
