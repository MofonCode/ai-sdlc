#Requires -Version 5.1
<#
.SYNOPSIS
    Computes the charter amendment signals from the findings corpus. Read-only.

.DESCRIPTION
    AMENDMENT IS BY COUNTS, NOT OPINION. Every signal here is derived on demand from the
    corpus and NEVER stored in a committed file: a stored signal would be a second list
    beside the corpus, and the charter would grow with every cycle.

    A SIGNAL IS A PROMPT TO INVESTIGATE, NOT A VERDICT. No process may act on one, because
    acting means changing the charter and that takes an approved charter intent (INV-08). This
    script therefore reports and never proposes — reporting and proposing are separate acts,
    and only the second needs a gate.

    THE ZERO-CITE QUERY IS THE REASON THIS EXISTS. Four of the five amendment triggers
    announce themselves: a blocked cycle, a growing suggestions pile, a change you made, an
    arriving intent. The fifth produces no event at all. An invariant that no finding ever
    points at is decoration, unenforceable, or prevented upstream — three readings calling for
    OPPOSITE actions — and nothing will ever prompt you to notice which. You can be prompted
    about what happens; you have to go looking for what does not.

    `rejected` AND `accepted-wont-fix` ARE NEVER MERGED. They feed opposite signals:
    `rejected` means the finding was wrong and says nothing about the invariant, while
    `accepted-wont-fix` means the finding was right and we knowingly did not act. Merging them
    inflates the override count and makes a sound invariant look too broad.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.PARAMETER Charter
    The charter whose invariants are surveyed. Defaults to .sdlc/CHARTER.md.

.EXAMPLE
    .\Get-SdlcSignals.ps1 | ConvertFrom-Json
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoRoot,

    [Parameter()]
    [string] $Charter = '.sdlc/CHARTER.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that
# terminates a JSON string. Invariant titles and finding summaries are full of them.
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

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$charterPath = Join-Path $RepoRoot $Charter
if (-not (Test-Path -LiteralPath $charterPath)) {
    throw "No charter at '$Charter'. There is nothing to survey."
}

# --- what the charter declares ----------------------------------------------

$charterText = [string] (Get-Content -LiteralPath $charterPath -Raw -Encoding UTF8)

# Struck-through ids are records of ABSENCE, not declarations. Reading one as declared would
# report a removed invariant as having zero cites, which is true and meaningless.
$declared = New-Object System.Collections.ArrayList
$preventedBy = @{}

foreach ($line in ($charterText -split "`r?`n")) {
    if ($line -match '^###\s+(~~)?((?:INV|LOC)-[0-9]{2})(~~)?\s*(·\s*(.+))?$') {
        if ($Matches[1]) { continue }
        $null = $declared.Add([pscustomobject]@{
            id    = $Matches[2]
            title = $(if ($Matches[5]) { $Matches[5].Trim() } else { '' })
        })
    }

    if ($line -match '^\s*Prevented-by:\s*(\S.*)$' -and $declared.Count -gt 0) {
        $preventedBy[$declared[$declared.Count - 1].id] = $Matches[1].Trim()
    }
}

# --- the corpus -------------------------------------------------------------
#
# Retros first, because the retro is what TRAVELS between repos and is therefore what the
# signals are computed from. verify.md files stay in their solution and ai-sdlc never sees
# them. Findings documents are read too, for a solution surveying itself before anything has
# landed.

$corpus = New-Object System.Collections.ArrayList
$cyclesDir = Join-Path $RepoRoot '.sdlc/cycles'

if (Test-Path -LiteralPath $cyclesDir) {
    foreach ($dir in @(Get-ChildItem -LiteralPath $cyclesDir -Directory)) {
        $findingsPath = Join-Path $dir.FullName 'findings.json'
        if (-not (Test-Path -LiteralPath $findingsPath)) { continue }

        $raw = [string] (Get-Content -LiteralPath $findingsPath -Raw -Encoding UTF8)
        if (-not $raw.Trim()) { continue }

        $doc = ConvertFrom-Json $raw
        if (-not (Test-Field $doc 'findings')) { continue }

        foreach ($f in @($doc.findings)) {
            $null = $corpus.Add([pscustomobject]@{
                cycle       = $dir.Name
                category    = $(if (Test-Field $f 'category') { $f.category } else { 'unclassified' })
                state       = $(if (Test-Field $f 'state') { $f.state } else { 'suggested' })
                disposition = $(if (Test-Field $f 'disposition') { $f.disposition } else { $null })
                cites       = $(if (Test-Field $f 'cites') { @($f.cites) } else { @() })
            })
        }
    }
}

# --- per-invariant counts ---------------------------------------------------

$rows = New-Object System.Collections.ArrayList

foreach ($inv in $declared) {
    $cited = @($corpus | Where-Object {
        $hit = $false
        foreach ($c in $_.cites) {
            if ((Test-Field $c 'kind') -and $c.kind -eq 'invariant' -and
                (Test-Field $c 'id') -and $c.id -eq $inv.id) { $hit = $true }
        }
        $hit
    })

    # Counted apart, deliberately. Merging these two is the one mistake that corrupts the
    # only evidence the charter improves from.
    $rejected = @($cited | Where-Object { $_.disposition -eq 'rejected' }).Count
    $overridden = @($cited | Where-Object { $_.disposition -eq 'accepted-wont-fix' }).Count

    $prevented = $null
    if ($preventedBy.ContainsKey($inv.id)) { $prevented = $preventedBy[$inv.id] }

    # THE THREE READINGS OF ZERO CITES, and they call for opposite actions. The script does
    # not choose between them: whether a violation can be constructed is a judgment, which is
    # why this reports the question rather than an answer.
    $signal = $null
    $reading = $null

    if ($cited.Count -eq 0) {
        if ($prevented) {
            # A `Prevented-by:` target must be VERIFIED before it may read as working as
            # designed. Unverified, the field would convert an unfalsifiable claim into a
            # permanent exemption, which is the failure it exists to prevent.
            $targetPath = Join-Path $RepoRoot $prevented
            if (Test-Path -LiteralPath $targetPath) {
                $signal = 'zero-cites-prevented'
                $reading = "prevented by '$prevented', which exists. Whether it still REJECTS a violating artifact is not checked here and must be before this reads as working as designed."
            }
            else {
                $signal = 'zero-cites-prevention-unverified'
                $reading = "names '$prevented' as its preventer, and that target does not resolve. Zero cites with no demonstrated prevention: a question, not an exemption."
            }
        }
        else {
            $signal = 'zero-cites'
            $reading = 'decoration, unenforceable, or prevented upstream. Three readings, opposite actions, and no event will ever prompt you. Decide by asking whether a violation can be constructed.'
        }
    }
    elseif ($overridden -ge 3) {
        $signal = 'high-override'
        $reading = "cited then accepted-wont-fix $overridden times: the rule may be too broad. Candidate for NARROWING, not deletion."
    }

    $null = $rows.Add([pscustomobject]@{
        id           = $inv.id
        title        = $inv.title
        cites        = $cited.Count
        blocking     = @($cited | Where-Object { $_.state -eq 'blocking' }).Count
        fixed        = @($cited | Where-Object { $_.disposition -eq 'fixed' }).Count
        rejected     = $rejected
        overridden   = $overridden
        'prevented-by' = $prevented
        signal       = $signal
        reading      = $reading
    })
}

# --- suggestion clusters ----------------------------------------------------
#
# A recurring cluster on one theme is the signal that an invariant is MISSING, which is the
# only signal that argues for addition rather than change.

$clusters = New-Object System.Collections.ArrayList
$byCategory = @{}

foreach ($f in @($corpus | Where-Object { $_.state -eq 'suggested' })) {
    if (-not $byCategory.ContainsKey($f.category)) { $byCategory[$f.category] = 0 }
    $byCategory[$f.category] = $byCategory[$f.category] + 1
}

foreach ($category in @($byCategory.Keys | Sort-Object)) {
    $count = $byCategory[$category]
    if ($count -lt 3) { continue }
    $null = $clusters.Add([pscustomobject]@{
        category = $category
        count    = $count
        reading  = "$count suggestions in this category. A recurring cluster on one theme is the signal that an invariant is MISSING."
    })
}

# --- the unclassified rate --------------------------------------------------
#
# A health metric for the taxonomy itself rather than for any invariant. A high rate means the
# eleven categories are not describing what interrogation actually finds.

$total = $corpus.Count
$unclassified = @($corpus | Where-Object { $_.category -eq 'unclassified' }).Count

$result = [ordered]@{
    'generated-at'      = $null
    'charter'           = ($Charter -replace '\\', '/')
    'corpus-size'       = $total
    'cycles-surveyed'   = @($corpus | ForEach-Object { $_.cycle } | Select-Object -Unique).Count
    'unclassified-rate' = $(if ($total -gt 0) { [math]::Round($unclassified / $total, 3) } else { 0 })
    invariants          = @($rows)
    'suggestion-clusters' = @($clusters)
    # Stated in the output, not only in the help. Whoever reads this is the person who might
    # be tempted to act on it.
    note                = 'A signal is a prompt to investigate, not a verdict. No process may act on one: acting means changing the charter, and that takes an approved charter intent (INV-08). Nothing here is stored — recompute it.'
}

# The timestamp is the one thing not derived from the corpus, and it is left null rather than
# read from a clock: this output is regenerable by construction, so a timestamp would be the
# only part of it that could go stale.
$null = $result.Remove('generated-at')

ConvertTo-Json -InputObject $result -Depth 6
