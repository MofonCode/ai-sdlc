#Requires -Version 5.1
<#
.SYNOPSIS
    Renders a findings document to markdown, for a person.

.DESCRIPTION
    ONE RENDERER, TWO AUDIENCES, and that is deliberate. This produces both outputs the
    concept asks for, because two templates over one schema is the INV-03 duplication this
    framework exists to catch:

      -State suggested   the CYCLE'S SUGGESTIONS FILE, committed, one per cycle. Intake:
                         a person triages it on their own schedule, and a promoted
                         suggestion becomes an ordinary intent with `source: suggestion`.

      (no -State)        a REGENERABLE VIEW of everything, not committed. `*.findings.md`
                         is gitignored for exactly this, the same pattern as `facts.md`.

    THE JSON IS AUTHORITATIVE. Findings exist in two representations, and without a stated
    authority that is a duplication. The JSON is what interrogation emits and what
    aggregation reads; this markdown is rendered from it, never edited, and never read back.
    Nothing in this framework parses the output of this script.

    ONE FILE PER CYCLE, NEVER A SHARED BACKLOG. A single suggestions file that every cycle
    appends to conflicts exactly the way `plugin.json` does under concurrent cycles: a
    guaranteed collision rather than a possible one.

.PARAMETER Path
    The findings JSON to render. Defaults to the cycle's findings.json.

.PARAMETER CycleId
    Render this cycle's findings. Ignored when -Path is given.

.PARAMETER State
    Render only findings in this state. Use `suggested` for the suggestions file.

.PARAMETER OutFile
    Write here instead of to stdout.

.PARAMETER For
    `human` renders the reading view. `retro` renders THE table a retro carries into the corpus:
    one row per finding with its category, attributed stage, detection, citation and disposition,
    in a fixed shape that Get-SdlcSignals.ps1 parses back out.

    That shape had to be declared somewhere. A retro is what TRAVELS between repositories — no
    spec, plan or findings document follows it — so the amendment signals are computed from its
    body, and nothing said what the body had to look like. An agent transcribing findings into a
    table of its own design produces a retro no query can read, which is the whole loop failing
    quietly at the last step.

    So the retro's table is GENERATED from the findings documents rather than transcribed, for the
    same reason the traceability table is generated from test annotations. The copy in the retro is
    not a second list in the INV-03 sense: a landed retro is immutable, so the two cannot diverge.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Format-SdlcFindings.ps1 -CycleId 20260924-c4a1 -State suggested -OutFile .sdlc/suggestions/20260924-c4a1.md

.EXAMPLE
    .\Format-SdlcFindings.ps1 -CycleId 20260924-c4a1 -OutFile cycle.findings.md
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $Path,

    [Parameter()]
    [string] $CycleId,

    [Parameter()]
    [ValidateSet('open', 'blocking', 'suggested')]
    [string] $State,

    [Parameter()]
    [string] $OutFile,

    [Parameter()]
    [ValidateSet('human', 'retro')]
    [string] $For = 'human',

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install. An em-dash then leaves this script as a CP1252 best-fit
# substitution, and U+201D best-fits to a plain double quote. Findings summaries are prose
# written by agents and are full of such characters, so without this the rendered markdown
# is mojibake even though the JSON it came from was correct.
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

function Get-Field {
    param($Object, [string] $Name, $Default = '')

    if (Test-Field $Object $Name) {
        $v = $Object.$Name
        if ($null -ne $v) { return $v }
    }
    return $Default
}

# A pipe inside a summary would split the table cell it sits in, which silently drops the
# rest of the sentence. Escaped rather than stripped: the character is part of what the
# agent wrote, and a finding whose text is quietly truncated is worse than an ugly one.
function Format-Cell {
    param([string] $Text)

    if (-not $Text) { return '' }
    $t = $Text -replace '\r?\n', ' '
    $t = $t -replace '\|', '\|'
    return ($t -replace '\s+', ' ').Trim()
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

# A CYCLE HAS SEVERAL DOCUMENTS, ONE PER INTERROGATION. The schema carries a single target and
# round per document, which is what one interrogation is; a cycle interrogates its intent, its
# spec, its plan and its verification, and repairs add rounds. So -CycleId renders every document
# the cycle has filed, and -Path renders exactly the one named.
#
# That matters most for the suggestions file. It is written once at landing and is the cycle's
# intake, so rendering only the last interrogation would drop the suggestions from every earlier
# stage — the evidence a missing invariant is inferred from.
$sources = New-Object System.Collections.ArrayList

if ($Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "No findings document at '$Path'."
    }
    $null = $sources.Add((Resolve-Path -LiteralPath $Path).Path)
}
else {
    if (-not $CycleId) {
        throw 'Give -Path or -CycleId.'
    }

    $findingsDir = Join-Path $RepoRoot ".sdlc/cycles/$CycleId/findings"
    if (-not (Test-Path -LiteralPath $findingsDir)) {
        throw "No findings have been filed for '$CycleId'."
    }

    foreach ($file in @(Get-ChildItem -LiteralPath $findingsDir -Filter '*.json' -File | Sort-Object Name)) {
        $null = $sources.Add($file.FullName)
    }

    if ($sources.Count -eq 0) {
        throw "No findings documents under '$findingsDir'."
    }
}

$findings = @()
$runs = @()
$cycle = $CycleId
$charterVersion = 'unknown'

foreach ($source in $sources) {
    $doc = ConvertFrom-Json ((Get-Content -LiteralPath $source -Raw -Encoding UTF8))

    if (Test-Field $doc 'findings') { $findings += @($doc.findings) }

    $docCycle = Get-Field $doc 'cycle-id' ''
    if ($docCycle) { $cycle = $docCycle }

    $docVersion = Get-Field $doc 'charter-version' ''
    if ($docVersion) { $charterVersion = $docVersion }

    $runs += ((Get-Field $doc 'target' 'artifact') + ' r' + (Get-Field $doc 'round' 1))
}

if ($State) {
    $findings = @($findings | Where-Object { (Get-Field $_ 'state') -eq $State })
}

$target = ($runs -join ', ')
$round = $runs.Count

# --- the retro's table, which is a CONTRACT rather than a presentation ------
#
# THE COLUMNS ARE PARSED BACK OUT by Get-SdlcSignals.ps1, so this shape is as fixed as a spec's
# criteria table or a plan's file table. Every column is here because a signal is computed from it:
#
#   Category     the unclassified rate, which is a health metric for the taxonomy itself
#   Stage        where the process leaks, attributed upstream — the high-value signal
#   Detection    whether fact-gathering is absorbing more of the load over time
#   Cites        the per-invariant cite count, and with it the override rate
#   Disposition  every signal the charter amends on. `rejected` marks a noisy ROLE;
#                `accepted-wont-fix` marks an invariant that may be too broad. Never merged
#   Role         which lens produced it, so a lens can be dropped on evidence
#
# One row per finding, every state included. A suggestion that was never dispositioned still
# belongs here: the count of those IS the evidence that suggestions go untriaged.
if ($For -eq 'retro') {
    $rows = New-Object System.Collections.ArrayList
    $null = $rows.Add('| Finding | State | Category | Stage | Detection | Role | Cites | Disposition | Why |')
    $null = $rows.Add('|---|---|---|---|---|---|---|---|---|')

    foreach ($f in $findings) {
        $cites = @()
        if (Test-Field $f 'cites') {
            foreach ($c in @($f.cites)) {
                $kind = Get-Field $c 'kind' '?'
                if ($kind -eq 'locations') { $cites += 'locations' }
                else { $cites += ($kind + ' ' + (Get-Field $c 'id')) }
            }
        }

        $citeText = '—'
        if ($cites.Count -gt 0) { $citeText = ($cites -join '; ') }

        $null = $rows.Add(
            '| `' + (Get-Field $f 'id') + '` | ' +
            (Get-Field $f 'state') + ' | ' +
            (Get-Field $f 'category') + ' | ' +
            (Get-Field $f 'stage') + ' | ' +
            (Get-Field $f 'detection') + ' | ' +
            (Get-Field $f 'role' '—') + ' | ' +
            (Format-Cell $citeText) + ' | ' +
            (Get-Field $f 'disposition' '—') + ' | ' +
            (Format-Cell (Get-Field $f 'disposition-reason' '')) + ' |')
    }

    $table = ($rows -join "`n")

    if ($OutFile) {
        $full = $OutFile
        if (-not [System.IO.Path]::IsPathRooted($full)) { $full = Join-Path $RepoRoot $OutFile }
        [System.IO.File]::WriteAllText($full, $table, (New-Object System.Text.UTF8Encoding $false))
        Write-Host "wrote $OutFile ($($findings.Count) finding(s))"
    }
    else {
        $table
    }

    return
}

$relativeSources = @()
foreach ($source in $sources) {
    $relativeSources += ((($source.Substring($RepoRoot.Length)).TrimStart('\', '/')) -replace '\\', '/')
}

$out = New-Object System.Collections.ArrayList
$null = $out.Add('<!-- GENERATED from ' + ($relativeSources -join ', ') + ' -->')
$null = $out.Add('<!-- The JSON is authoritative. Edits here are lost on the next render. -->')
$null = $out.Add('')

if ($State -eq 'suggested') {
    $null = $out.Add("# Suggestions from cycle $cycle")
    $null = $out.Add('')
    $null = $out.Add("$round interrogation(s) at charter v$charterVersion, on: $target.")
    $null = $out.Add('')
    $null = $out.Add('These did not halt the cycle. They accumulate as evidence: a recurring cluster on')
    $null = $out.Add('one theme is the signal that an invariant is missing. Triage on your own schedule,')
    $null = $out.Add('and promote one by filing an ordinary intent with `source: suggestion`.')
}
else {
    $null = $out.Add("# Findings for cycle $cycle")
    $null = $out.Add('')
    $null = $out.Add("$round interrogation(s) at charter v$charterVersion, on: $target.")
}

$null = $out.Add('')

if ($findings.Count -eq 0) {
    $what = 'findings'
    if ($State) { $what = "``$State`` findings" }
    $null = $out.Add("No $what. That is a normal and useful result.")
}
else {
    # Blocking first, then open, then suggested. A reader wants what halts the cycle at the
    # top, and within a state the emission order is the merge's order, which is meaningful.
    $order = @{ 'blocking' = 0; 'open' = 1; 'suggested' = 2 }
    $sorted = @($findings | Sort-Object -Property @{ Expression = {
        $s = Get-Field $_ 'state' 'suggested'
        if ($order.ContainsKey($s)) { $order[$s] } else { 3 }
    } }, @{ Expression = { Get-Field $_ 'id' } })

    $null = $out.Add('| ID | State | Category | Stage | Role | Cites | Defect |')
    $null = $out.Add('|---|---|---|---|---|---|---|')

    foreach ($f in $sorted) {
        $cites = @()
        if (Test-Field $f 'cites') {
            foreach ($c in @($f.cites)) {
                $kind = Get-Field $c 'kind' '?'
                if ($kind -eq 'locations') { $cites += 'two locations' }
                else { $cites += ($kind + ' ' + (Get-Field $c 'id')) }
            }
        }
        $citeText = '—'
        if ($cites.Count -gt 0) { $citeText = ($cites -join '; ') }

        $row = '| `' + (Get-Field $f 'id') + '` | ' +
               (Get-Field $f 'state') + ' | ' +
               (Get-Field $f 'category') + ' | ' +
               (Get-Field $f 'stage') + ' | ' +
               (Get-Field $f 'role' '—') + ' | ' +
               (Format-Cell $citeText) + ' | ' +
               (Format-Cell (Get-Field $f 'summary')) + ' |'
        $null = $out.Add($row)
    }

    # Repairs are prose and do not belong in a table cell, but dropping them would lose the
    # most actionable part of a finding.
    $withRepair = @($sorted | Where-Object { (Get-Field $_ 'repair') })
    if ($withRepair.Count -gt 0) {
        $null = $out.Add('')
        $null = $out.Add('## Suggested repairs')
        foreach ($f in $withRepair) {
            $null = $out.Add('')
            $null = $out.Add('### `' + (Get-Field $f 'id') + '` · ' + (Format-Cell (Get-Field $f 'summary')))
            $null = $out.Add('')
            $null = $out.Add((Get-Field $f 'repair'))
        }
    }
}

$null = $out.Add('')
$text = ($out -join "`n")

if ($OutFile) {
    $full = $OutFile
    if (-not [System.IO.Path]::IsPathRooted($full)) { $full = Join-Path $RepoRoot $OutFile }
    $dir = Split-Path -Parent $full
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        $null = New-Item -ItemType Directory -Path $dir -Force
    }

    # UTF8 WITHOUT a BOM. Set-Content -Encoding UTF8 in 5.1 writes one, and a BOM in the
    # middle of a markdown corpus renders as a stray character in some viewers.
    [System.IO.File]::WriteAllText($full, $text, (New-Object System.Text.UTF8Encoding $false))
    Write-Host "wrote $OutFile ($($findings.Count) finding(s))"
}
else {
    $text
}
