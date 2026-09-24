#Requires -Version 5.1
<#
.SYNOPSIS
    Gathers the mechanical facts an interrogation needs, once, before any role runs.

.DESCRIPTION
    Roughly 70% of v01's findings were mechanical — unresolved paths, stale counts,
    claims about the tree. Checking them before dispatching an interrogation cut one
    round from 30 tool calls to 13, and what came back was reasoning instead of
    bookkeeping. This is that pre-check.

    It emits named facts as JSON. A finding that contradicts one of them cites it as
    { "kind": "check", "id": "<fact id>" }, which is what makes the finding blocking
    without any judgment: the citation is a fact anyone can re-derive.

    Output is generated, regenerable and never committed. It is not authoritative
    about anything except what was true when it ran, which is why every fact carries
    the commit it was gathered at.

    Windows PowerShell 5.1 compatible by design: no ternary, no null-coalescing, no
    pipeline chain operators, no -AsHashtable.

.PARAMETER Artifact
    Path to the artifact being interrogated.

.PARAMETER Charter
    Path to the charter that governs it. Defaults to .sdlc/CHARTER.md.

.PARAMETER RepoRoot
    Repository root. Defaults to the artifact's enclosing git repository.

.EXAMPLE
    .\Get-SdlcFacts.ps1 -Artifact .sdlc/intents/20260923-4f2a.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Artifact,

    [Parameter()]
    [string] $Charter,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
    param([string] $Start)

    $dir = Split-Path -Parent (Resolve-Path -LiteralPath $Start)
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) { return $dir }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }

    throw "No git repository found above '$Start'."
}

function Get-Frontmatter {
    param([string[]] $Lines)

    $map = [ordered]@{}
    if ($Lines.Count -eq 0 -or $Lines[0].Trim() -ne '---') {
        return [pscustomobject]@{ present = $false; fields = $map }
    }

    for ($i = 1; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($line.Trim() -eq '---') { break }
        # Only top-level scalar "key: value" pairs. Every schema field is one; nesting
        # would need a real YAML parser and nothing here has nesting.
        if ($line -match '^([A-Za-z][A-Za-z0-9_-]*):\s*(.*)$') {
            $value = $Matches[2].Trim()
            if ($value -eq 'null' -or $value -eq '') { $value = $null }
            $map[$Matches[1]] = $value
        }
    }

    return [pscustomobject]@{ present = $true; fields = $map }
}

function Get-MentionedPaths {
    param([string] $Text, [string] $Root)

    $found = [ordered]@{}

    # This heuristic deliberately prefers FALSE NEGATIVES. A fact-gatherer that reports
    # things which are fine teaches everyone to ignore it, and an ignored check is worse
    # than no check — it is gate erosion one layer up. So a token must look
    # unambiguously like a repository-relative path before it is resolved at all:
    #
    #   * it must contain a directory separator  — so `verify.md` and `retro.md`, which
    #     are artifact KIND names used generically, are left alone
    #   * it must not begin with one             — so `/sdlc:init`, a command, is not a path
    #   * it must not contain a colon            — same, plus URLs and drive letters
    #
    # The cost is that a bare filename at the repository root goes unchecked. That is a
    # cheap miss; a noisy check is an expensive one.
    $pattern = '`([^`\r\n]+)`'
    foreach ($m in [regex]::Matches($Text, $pattern)) {
        $token = $m.Groups[1].Value.Trim()
        if ($token -notmatch '[/\\]') { continue }
        if ($token -match '^[/\\]') { continue }
        if ($token -match '[<>*|:]') { continue }
        if ($token -match '\s') { continue }
        $found[$token] = $true
    }

    $results = New-Object System.Collections.ArrayList
    foreach ($token in $found.Keys) {
        $full = Join-Path $Root $token
        $null = $results.Add([pscustomobject]@{
            path   = $token
            exists = (Test-Path -LiteralPath $full)
        })
    }

    return $results
}

function Get-MentionedIds {
    param([string] $Text, [string] $Pattern, [string[]] $Known)

    $found = [ordered]@{}
    foreach ($m in [regex]::Matches($Text, $Pattern)) {
        $found[$m.Value] = $true
    }

    $results = New-Object System.Collections.ArrayList
    foreach ($id in $found.Keys) {
        $null = $results.Add([pscustomobject]@{
            id     = $id
            exists = ($Known -contains $id)
        })
    }

    return $results
}

function Get-FailedCheck {
    param([string] $Id, [string] $Detail)
    return [pscustomobject]@{ id = $Id; passed = $false; detail = $Detail }
}

function Get-ArtifactKind {
    param([string] $RelativePath)

    $leaf = Split-Path -Leaf $RelativePath
    switch -Regex ($leaf) {
        '^CHARTER\.md$' { return 'charter' }
        '^spec\.md$'    { return 'spec' }
        '^plan\.md$'    { return 'plan' }
        '^verify\.md$'  { return 'verify' }
        '^retro\.md$'   { return 'retro' }
    }

    if ($RelativePath -match '(?i)/intents/') { return 'intent' }
    return 'unknown'
}

function Get-AcceptanceCriteria {
    param([string] $Text)

    # A spec's criteria live in a markdown table in the body rather than in
    # frontmatter: they are what a person reads the document for, and a table is
    # parseable without being unreadable. The shape is fixed because this parses it.
    #
    #   | ID | Verifiable | Criterion |
    #   |---|---|---|
    #   | AC-20260924-a1b2-1 | yes | ... |

    $results = New-Object System.Collections.ArrayList
    $pattern = '(?m)^\|\s*(AC-[0-9]{8}-[0-9a-f]{4}-[0-9]+)\s*\|\s*([A-Za-z]+)\s*\|\s*([^|]*)\|'

    foreach ($m in [regex]::Matches($Text, $pattern)) {
        $flag = $m.Groups[2].Value.Trim().ToLowerInvariant()
        $null = $results.Add([pscustomobject]@{
            id         = $m.Groups[1].Value
            verifiable = ($flag -eq 'yes' -or $flag -eq 'true')
            text       = $m.Groups[3].Value.Trim()
        })
    }

    return $results
}

function Get-DeclaredFiles {
    param([string] $Text, [string] $Root)

    # INV-05 requires a plan to enumerate the files it will touch, and the same
    # declaration is what the blast-radius tier reads. One table serves both.
    #
    #   | Path | Change |
    #   |---|---|
    #   | src/Foo.cs | new |

    $results = New-Object System.Collections.ArrayList
    $pattern = '(?m)^\|\s*`?([A-Za-z0-9_./\\-]+\.[A-Za-z0-9]+)`?\s*\|\s*(new|modify|delete)\s*\|'

    foreach ($m in [regex]::Matches($Text, $pattern)) {
        $path = $m.Groups[1].Value
        $change = $m.Groups[2].Value.ToLowerInvariant()
        $full = Join-Path $Root $path
        $exists = Test-Path -LiteralPath $full
        $null = $results.Add([pscustomobject]@{
            path   = $path
            change = $change
            exists = $exists
            # A `new` file that already exists, or a `modify`/`delete` that does not,
            # means the plan was written against a different tree than the one here.
            consistent = $(if ($change -eq 'new') { -not $exists } else { $exists })
        })
    }

    return $results
}

# ---------------------------------------------------------------------------

$artifactPath = (Resolve-Path -LiteralPath $Artifact).Path

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start $artifactPath }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

if (-not $Charter) { $Charter = Join-Path $RepoRoot '.sdlc/CHARTER.md' }

$lines = Get-Content -LiteralPath $artifactPath
$rawText = $lines -join "`n"

# A struck-through token is a record of absence, not a reference to something that
# should exist — the charter uses ~~DEC-05~~ to record a removed decision. Strip those
# spans before extracting references, or every gap reads as an unresolved id.
$text = [regex]::Replace($rawText, '~~[^~\r\n]*~~', '')

# --- the charter's own ids, so a citation can be resolved rather than trusted ---

$charterPresent = Test-Path -LiteralPath $Charter
$invariantIds = @()
$decIds = @()
$charterVersion = $null

if ($charterPresent) {
    $charterLines = Get-Content -LiteralPath $Charter
    $charterText = $charterLines -join "`n"

    # An invariant is declared by a heading; a mere mention elsewhere is not a
    # declaration, which is the distinction that makes this check meaningful.
    foreach ($m in [regex]::Matches($charterText, '(?m)^#{2,4}\s+((?:INV|LOC)-[0-9]{2})\s')) {
        $invariantIds += $m.Groups[1].Value
    }

    # A deferred decision is declared by a row in the register table. A struck-through
    # id is a record of a removed decision, not a declaration, so the leading cell must
    # not be wrapped in ~~.
    foreach ($m in [regex]::Matches($charterText, '(?m)^\|\s*(DEC-[0-9]{2})\s*\|')) {
        $decIds += $m.Groups[1].Value
    }

    # The charter's metadata is ordinary frontmatter, read by the same parser as any
    # other artifact's. It used to be an indented block read by a bespoke regex, which
    # was a second parser for the same data.
    $charterFm = Get-Frontmatter -Lines $charterLines
    if ($charterFm.present -and $charterFm.fields.Contains('version')) {
        $charterVersion = [int] $charterFm.fields['version']
    }
}

$invariantIds = $invariantIds | Select-Object -Unique
$decIds = $decIds | Select-Object -Unique

# --- tree truth ---

Push-Location $RepoRoot
try {
    $trackedFiles = @(git ls-files)
    $headSha = (git rev-parse --short HEAD).Trim()
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
}
finally {
    Pop-Location
}

# --- references in the artifact ---

$frontmatter = Get-Frontmatter -Lines $lines
$paths = Get-MentionedPaths -Text $text -Root $RepoRoot
$invRefs = Get-MentionedIds -Text $text -Pattern '\b(?:INV|LOC)-[0-9]{2}\b' -Known $invariantIds
$decRefs = Get-MentionedIds -Text $text -Pattern '\bDEC-[0-9]{2}\b' -Known $decIds

# --- named checks, each citable by id ---
#
# Checks apply only to GOVERNED artifacts — those under .sdlc/. Everything else is
# prose, and gets facts without verdicts.
#
# This was learned by running the gatherer against the design record, which produced
# eleven unresolved paths and five unresolved ids, every one of them correct-as-written:
# a design document talks ABOUT directories that do not exist yet, cites LOC-01 as an
# illustration, and carries a frozen appendix referencing decisions since removed.
# Scraping prose cannot distinguish a reference from a description, and a check that
# reports healthy artifacts as broken gets switched off — which is the INV-06 failure
# arriving one layer up. So the scope is narrowed rather than the heuristic sharpened.

$relative = ($artifactPath.Substring($RepoRoot.Length).TrimStart('\', '/') -replace '\\', '/')
$governed = $relative.StartsWith('.sdlc/', [System.StringComparison]::Ordinal)
$kind = Get-ArtifactKind -RelativePath $relative
$criteria = Get-AcceptanceCriteria -Text $text
$declaredFiles = Get-DeclaredFiles -Text $text -Root $RepoRoot

$checks = New-Object System.Collections.ArrayList

function Add-Check {
    param([string] $Id, [bool] $Passed, [string] $Detail)
    $null = $script:checks.Add([pscustomobject]@{ id = $Id; passed = $Passed; detail = $Detail })
}

if ($governed) {
    $badPaths = @($paths | Where-Object { -not $_.exists })
    if ($badPaths.Count -gt 0) {
        Add-Check 'paths-resolve' $false (
            "does not exist: " + (($badPaths | ForEach-Object { $_.path }) -join ', '))
    }
    else {
        Add-Check 'paths-resolve' $true $null
    }

    $badInv = @($invRefs | Where-Object { -not $_.exists })
    if ($badInv.Count -gt 0) {
        Add-Check 'invariant-ids-resolve' $false (
            "not declared in the charter: " + (($badInv | ForEach-Object { $_.id }) -join ', '))
    }
    else {
        Add-Check 'invariant-ids-resolve' $true $null
    }

    $badDec = @($decRefs | Where-Object { -not $_.exists })
    if ($badDec.Count -gt 0) {
        Add-Check 'dec-ids-resolve' $false (
            "not in the register: " + (($badDec | ForEach-Object { $_.id }) -join ', '))
    }
    else {
        Add-Check 'dec-ids-resolve' $true $null
    }

    Add-Check 'charter-present' $charterPresent $(
        if ($charterPresent) { $null } else { "no charter at '$Charter'" })

    # The charter's own metadata is frontmatter like anything else; an artifact under
    # .sdlc/ without it cannot be schema-validated at all.
    Add-Check 'frontmatter-present' $frontmatter.present $(
        if ($frontmatter.present) { $null } else { 'no YAML frontmatter block' })

    # --- kind-specific checks ---------------------------------------------

    if ($kind -eq 'spec') {
        if ($criteria.Count -eq 0) {
            Add-Check 'acceptance-criteria-present' $false 'no parseable acceptance-criteria table'
        }
        else {
            Add-Check 'acceptance-criteria-present' $true $null
        }

        # Ids end up in test annotations, so a duplicate is not cosmetic: two criteria
        # sharing an id make traceability report one as covered when the other is not.
        $dupes = @($criteria | Group-Object -Property id | Where-Object { $_.Count -gt 1 })
        if ($dupes.Count -gt 0) {
            Add-Check 'acceptance-criteria-unique' $false (
                "duplicated: " + (($dupes | ForEach-Object { $_.Name }) -join ', '))
        }
        else {
            Add-Check 'acceptance-criteria-unique' $true $null
        }
    }

    if ($kind -eq 'plan') {
        if ($declaredFiles.Count -eq 0) {
            Add-Check 'plan-declares-files' $false 'no parseable file table — INV-05 requires one'
        }
        else {
            Add-Check 'plan-declares-files' $true $null
        }

        $inconsistent = @($declaredFiles | Where-Object { -not $_.consistent })
        if ($inconsistent.Count -gt 0) {
            Add-Check 'plan-files-consistent' $false (
                "declared state does not match the tree: " +
                (($inconsistent | ForEach-Object { $_.path + ' (' + $_.change + ')' }) -join ', '))
        }
        else {
            Add-Check 'plan-files-consistent' $true $null
        }
    }
}

# --- output ---

$facts = [ordered]@{
    'generated-at-sha' = $headSha
    'branch'           = $branch
    'artifact'         = [ordered]@{
        path        = $relative
        kind        = $kind
        governed    = $governed
        frontmatter = $frontmatter.fields
    }
    'acceptance-criteria' = @($criteria)
    'declared-files'      = @($declaredFiles)
    'charter'          = [ordered]@{
        path          = ($Charter -replace '\\', '/')
        present       = $charterPresent
        version       = $charterVersion
        'invariant-ids' = @($invariantIds)
        'dec-ids'     = @($decIds)
    }
    'tree'             = [ordered]@{
        'tracked-file-count' = $trackedFiles.Count
        'tracked-files'      = $trackedFiles
    }
    'references'       = [ordered]@{
        paths      = @($paths)
        invariants = @($invRefs)
        deferred   = @($decRefs)
    }
    'checks'           = @($checks)
    'failed-checks'    = @($checks | Where-Object { -not $_.passed } | ForEach-Object { $_.id })
}

ConvertTo-Json -InputObject $facts -Depth 8
