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

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install. An em-dash then leaves this script as a CP1252 best-fit
# substitution, and U+201D best-fits to a plain double quote — which does not merely look
# wrong, it TERMINATES the JSON string and the caller receives a structurally invalid
# document. Every governed artifact in this repository contains em-dashes, so this is the
# normal case rather than an edge one. UTF8Encoding($false) rather than
# [System.Text.Encoding]::UTF8 because the latter prepends a BOM to the output stream,
# which is not valid at the start of a JSON document.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

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

function Get-BlastRadius {
    param([object[]] $Files, [int] $SizeThreshold = 10)

    # A real blast radius needs a dependency graph and infra topology. This is a PROXY:
    # cheap, computable from paths alone, and directionally right. Every flag below
    # marks a change whose failure CANNOT be corrected by reverting the commit — that
    # is the selection rule, and it is what lets the list grow without guesswork.
    #
    # Heuristics, deliberately. A stack pack can extend them; none is authoritative.
    $flagPatterns = [ordered]@{
        # A failure here is a security incident, silent and exploitable while live.
        'auth'          = '(?i)(^|/)(auth|authn|authz|identity|login|oauth|token)'
        # git revert does not un-migrate. Transformed data stays transformed.
        'migrations'    = '(?i)(^|/)migrations?/'
        # Breaks other people's code, and data already written in the old shape.
        'contracts'     = '(?i)(^|/)(contracts?|dtos?)/|\.(proto|avsc)$|(?i)openapi'
        # You cannot un-leak a secret; rotation is the only remedy.
        'secrets'       = '(?i)(appsettings|\.env|secrets?|credential)'
        # Can disable the gates that catch everything else. Self-referential risk.
        'ci'            = '(?i)(^|/)(\.github/workflows|\.azuredevops|azure-pipelines)'
        # Already in someone''s lock file. NuGet does not recall.
        'public-api'    = '(?i)PublicAPI\.(Shipped|Unshipped)\.txt$'
    }

    $flags = New-Object System.Collections.ArrayList
    foreach ($name in $flagPatterns.Keys) {
        $pattern = $flagPatterns[$name]
        $hit = @($Files | Where-Object { $_.path -match $pattern })
        if ($hit.Count -gt 0) {
            $null = $flags.Add([pscustomobject]@{
                flag  = $name
                paths = @($hit | ForEach-Object { $_.path })
            })
        }
    }

    # Built with foreach rather than a pipeline: under Set-StrictMode -Version Latest,
    # .Count on the result of an empty pipeline throws, and the zero-flag case — a
    # tier-1 plan — is the common one.
    $names = @()
    foreach ($f in $flags) { $names += $f.flag }

    $fileCount = @($Files).Count

    # Tier 3 is what can never be unwound. Tier 2 is what a reviewer will skim if it
    # is large. Size is a SEPARATE dimension from irreversibility: a 40-file diff gets
    # skimmed however careful the reviewer.
    $tier = 1
    if ($fileCount -gt $SizeThreshold -or $names.Count -gt 0) { $tier = 2 }
    if (($names -contains 'auth') -or ($names -contains 'migrations') -or ($names -contains 'public-api')) {
        $tier = 3
    }

    return [pscustomobject]@{
        tier          = $tier
        flags         = @($flags)
        fileCount     = $fileCount
        sizeThreshold = $SizeThreshold
    }
}

function Get-TestAnnotations {
    param([string] $Root, [string[]] $TrackedFiles)

    # Traceability is GENERATED from test metadata, never hand-maintained. A hand-written
    # table beside the real tests drifts the moment anything is refactored, and nothing
    # detects it — which is why the annotation is the source and this only reads it.
    $covered = [ordered]@{}

    foreach ($rel in $TrackedFiles) {
        if ($rel -notmatch '\.cs$') { continue }
        $full = Join-Path $Root $rel
        if (-not (Test-Path -LiteralPath $full)) { continue }

        $content = Get-Content -LiteralPath $full -Raw -Encoding UTF8
        foreach ($m in [regex]::Matches($content, 'TestProperty\s*\(\s*"AC"\s*,\s*"([^"]+)"\s*\)')) {
            $id = $m.Groups[1].Value
            if (-not $covered.Contains($id)) { $covered[$id] = New-Object System.Collections.ArrayList }
            $null = $covered[$id].Add($rel)
        }
    }

    $results = New-Object System.Collections.ArrayList
    foreach ($id in $covered.Keys) {
        $null = $results.Add([pscustomobject]@{
            criterion = $id
            tests     = @($covered[$id] | Select-Object -Unique)
        })
    }

    return $results
}

function Get-Suppressions {
    param([string] $Root, [string[]] $TrackedFiles)

    # A zero-warning build is a strong signal BECAUSE it is binary, and the failure mode
    # is not warnings appearing — it is suppressions accumulating until the zero means
    # nothing. So the count is tracked, and an increase is itself a finding. Whether a
    # suppression carries a justification is judged by proximity to a comment, which is a
    # heuristic that prefers false negatives: it would rather miss an unjustified
    # suppression than flag a justified one and get the whole check switched off.
    $results = New-Object System.Collections.ArrayList

    foreach ($rel in $TrackedFiles) {
        if ($rel -notmatch '\.(cs|csproj|props|targets)$') { continue }
        $full = Join-Path $Root $rel
        if (-not (Test-Path -LiteralPath $full)) { continue }

        $lines = @(Get-Content -LiteralPath $full -Encoding UTF8)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $isSuppression = ($line -match '#pragma\s+warning\s+disable') -or
                             ($line -match '<NoWarn>') -or
                             ($line -match 'SuppressMessage')
            if (-not $isSuppression) { continue }

            # A justification is a comment on the line itself, or immediately above it.
            #
            # "Immediately above" means the nearest NON-BLANK preceding lines, and the
            # test is whether they look like comment CONTENT rather than a comment start.
            # A multi-line XML justification ends with `-->` and its interior lines begin
            # with neither `<!--` nor `//`, so looking only for a comment opener reports a
            # carefully justified suppression as unjustified — which was happening to this
            # repository's own, and is exactly the false positive that gets a check
            # switched off.
            $justified = ($line -match '//' -or $line -match '<!--')

            $checked = 0
            for ($j = $i - 1; $j -ge 0 -and $checked -lt 3; $j--) {
                if ($lines[$j].Trim() -eq '') { continue }
                $checked++
                if ($lines[$j] -match '-->' -or $lines[$j] -match '^\s*(//|\*|<!--)') {
                    $justified = $true
                    break
                }
            }

            $null = $results.Add([pscustomobject]@{
                file      = $rel
                line      = $i + 1
                text      = $line.Trim()
                justified = $justified
            })
        }
    }

    return $results
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

$lines = Get-Content -LiteralPath $artifactPath -Encoding UTF8
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
    $charterLines = Get-Content -LiteralPath $Charter -Encoding UTF8
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

# --- the form rule, which the charter asserts twice and nothing performed --------
#
# "A missing or empty `Forbids:` fails the form check." "An empty `Applies-to:` governs
# nothing and fails the form check for the same reason." The charter says this about itself
# in two places, and there was no form check - so the one rule governing the shape of every
# invariant could be violated with nothing to cite. That is the defect `r1` and `US-13`
# found independently: not a missing feature, a claim the document makes and does not keep.
#
# `untestable` is the category, and the charter's own definition settles it: "marked
# verifiable, but stated so that no test could verify it". An invariant compatible with
# every possible implementation is exactly that - and Coverage already gives `untestable` a
# failed-check route, so building this check creates the citation route without touching the
# charter. INV-08 is not involved: the check makes the charter true rather than changing it.
#
# Read from the ARTIFACT rather than from $Charter. When a charter is interrogated they are
# the same file, but the subject of a finding is the thing under interrogation - and a
# proposed charter on a branch is precisely when this needs to fire.
function Get-SdlcInvariantForm {
    param([string] $Text)

    $rows = @()
    $matches = [regex]::Matches($Text, '(?m)^#{2,4}\s+((?:INV|LOC)-[0-9]{2})\s')

    for ($i = 0; $i -lt $matches.Count; $i++) {
        $start = $matches[$i].Index
        $end = if ($i + 1 -lt $matches.Count) { $matches[$i + 1].Index } else { $Text.Length }
        $body = $Text.Substring($start, $end - $start)

        # Non-empty means something after the colon ON THE SAME LINE, and the character class
        # is `[ \t]` rather than `\s` for exactly that reason: in .NET `\s` matches a newline,
        # so `\s*(\S.*)` walked past the line break and matched the NEXT clause. An invariant
        # whose `Applies-to:` was empty then read as satisfied because `Forbids:` sat under it,
        # which is the precise case the charter calls out - "an empty `Applies-to:` governs
        # nothing and fails the form check for the same reason an empty `Forbids:` does".
        $forbids = [regex]::Match($body, '(?m)^[ \t]*Forbids:[ \t]*(\S.*)$')
        $appliesTo = [regex]::Match($body, '(?m)^[ \t]*Applies-to:[ \t]*(\S.*)$')

        $rows += [pscustomobject]@{
            id            = $matches[$i].Groups[1].Value
            'has-forbids' = $forbids.Success
            'has-applies-to' = $appliesTo.Success
        }
    }

    return $rows
}

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

# Every extractor is wrapped in @(). PowerShell UNWRAPS a single-element collection on
# return, so a one-item result arrives as a bare object and `.Count` on it throws under
# Set-StrictMode. That failure only appears when a result has exactly one element — the
# commonest case in practice, and the one every earlier fixture happened to avoid.
$paths = @(Get-MentionedPaths -Text $text -Root $RepoRoot)
$invRefs = @(Get-MentionedIds -Text $text -Pattern '\b(?:INV|LOC)-[0-9]{2}\b' -Known $invariantIds)
$decRefs = @(Get-MentionedIds -Text $text -Pattern '\bDEC-[0-9]{2}\b' -Known $decIds)

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
$criteria = @(Get-AcceptanceCriteria -Text $text)

# A verify document's criteria come from its sibling spec: verify reports on the spec's
# criteria, so reading them from anywhere else would be a second list.
if ($kind -eq 'verify' -and $criteria.Count -eq 0) {
    $siblingSpec = Join-Path (Split-Path -Parent $artifactPath) 'spec.md'
    if (Test-Path -LiteralPath $siblingSpec) {
        $criteria = @(Get-AcceptanceCriteria -Text ((Get-Content -LiteralPath $siblingSpec -Encoding UTF8) -join "`n"))
    }
}

$annotations = @()
$suppressions = @()
$traceability = $null

if ($kind -eq 'spec' -or $kind -eq 'verify') {
    $annotations = @(Get-TestAnnotations -Root $RepoRoot -TrackedFiles $trackedFiles)
    $suppressions = @(Get-Suppressions -Root $RepoRoot -TrackedFiles $trackedFiles)

    $coveredIds = @()
    foreach ($a in $annotations) { $coveredIds += $a.criterion }

    $rows = New-Object System.Collections.ArrayList
    foreach ($c in $criteria) {
        $tests = @()
        foreach ($a in $annotations) { if ($a.criterion -eq $c.id) { $tests = @($a.tests) } }
        $null = $rows.Add([pscustomobject]@{
            criterion  = $c.id
            verifiable = $c.verifiable
            # An unverifiable criterion is dispositioned by a person at the deploy gate;
            # it is not covered and is not expected to be.
            covered    = $(if ($c.verifiable) { $coveredIds -contains $c.id } else { $null })
            tests      = $tests
        })
    }

    $unmapped = @()
    foreach ($r in $rows) { if ($r.verifiable -and -not $r.covered) { $unmapped += $r.criterion } }

    # An annotation naming a criterion the spec does not declare points at a renumbering
    # or a typo, and it silently reports coverage for nothing.
    $criterionIds = @()
    foreach ($c in $criteria) { $criterionIds += $c.id }
    $orphanAnnotations = @()
    foreach ($id in $coveredIds) { if ($criterionIds -notcontains $id) { $orphanAnnotations += $id } }

    $traceability = [ordered]@{
        rows              = @($rows)
        'unmapped'        = @($unmapped)
        'orphan-annotations' = @($orphanAnnotations)
        'suppression-count' = @($suppressions).Count
        'unjustified-suppressions' = @($suppressions | Where-Object { -not $_.justified } | ForEach-Object { $_.file + ':' + $_.line })
    }
}
# --- the interrogator roles, read from the charter ------------------------
#
# THE CHARTER OWNS THE ROLES. The interrogator used to hardcode them, and the two disagreed
# within a day: charter v1's `form` question omitted `Applies-to:` while the shipped role
# asked for both clauses. That is INV-03, so the roles are parsed here and the interrogator
# reads them from the attached facts.
#
# SCOPED TO THE ROLES SECTION. A first version matched any table row whose first cell was a
# backticked lowercase word, which swept up the coverage table, the taxonomy, the state table
# and the disposition table - twenty-five "roles", most of them finding categories. The
# document is full of tables that look alike; only position distinguishes them.
$roles = New-Object System.Collections.ArrayList
$gapsByArtifact = [ordered]@{}

if ($charterPresent) {
    $section = ''
    $subsection = ''

    foreach ($cLine in $charterLines) {
        if ($cLine -match '^##\s+(.+)$') {
            $section = $Matches[1].Trim()
            $subsection = ''
            continue
        }
        if ($cLine -match '^###\s+(.+)$') {
            $subsection = $Matches[1].Trim()
            continue
        }

        if ($section -ne 'Interrogator roles') { continue }
        if ($cLine -notmatch '^\|\s*`([a-z]+)`\s*\|\s*(.+?)\s*\|\s*$') {
            # Four-column role rows end with a trailing pipe too, so both shapes are handled
            # by splitting rather than by two regexes.
            if ($cLine -notmatch '^\|\s*`([a-z]+)`\s*\|') { continue }
        }

        $cells = @($cLine.Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
        if ($cells.Count -lt 2) { continue }
        if ($cells[0] -notmatch '^`([a-z]+)`$') { continue }
        $name = $Matches[1]

        if ($subsection -match 'gaps.*by artifact') {
            $gapsByArtifact[$name] = $cells[1]
            continue
        }

        $null = $roles.Add([pscustomobject]@{
            name       = $name
            question   = $cells[1]
            evidence   = $(if ($cells.Count -ge 3) { $cells[2] } else { $null })
            'fails-by' = $(if ($cells.Count -ge 4) { $cells[3] } else { $null })
        })
    }
}

# --- the declared stack pack, and the rule ids INV-04 makes citable --------
#
# Resolved from the PLUGIN, not the solution. A pack is a shared versioned artifact
# reachable at ../packs/ from this script; only the CHARTER is copied into a solution at
# init time. So a solution consuming dotnet-library@v1 has no local pack.rules.yaml, and
# without this the id in `cites: PR-04` could never be checked against anything.
#
# Rule ids are extracted by pattern rather than parsed: 5.1 ships no YAML reader, and every
# other extractor in this script works the same way.
$packName = $null
$packRuleIds = @()
$packPresent = $false

if ($null -ne $charterFm -and $charterFm.fields.Contains('pack')) {
    $packName = [string] $charterFm.fields['pack']
}

if ($packName -and $packName -ne 'none') {
    $packRulesPath = Join-Path (Split-Path -Parent $PSScriptRoot) "packs/$packName/pack.rules.yaml"
    if (Test-Path -LiteralPath $packRulesPath) {
        $packPresent = $true
        foreach ($packLine in (Get-Content -LiteralPath $packRulesPath -Encoding UTF8)) {
            if ($packLine -match '^\s*-\s+id:\s*([A-Z]{2}-[0-9]{2})\s*$') { $packRuleIds += $Matches[1] }
        }
    }
}

$declaredFiles = @(Get-DeclaredFiles -Text $text -Root $RepoRoot)
$blastRadius = $null
$actualRadius = $null
$adherence = $null

if ($kind -eq 'plan' -and $declaredFiles.Count -gt 0) {
    $blastRadius = Get-BlastRadius -Files $declaredFiles

    # The same tier function applied to the real diff. Predicted gates the build;
    # actual is checked at verify, and actual exceeding predicted halts rather than
    # merges. One function, two inputs — so the two numbers are comparable by
    # construction rather than by convention.
    Push-Location $RepoRoot
    try {
        $changed = @(git diff --name-only HEAD) + @(git ls-files --others --exclude-standard)
    }
    finally {
        Pop-Location
    }

    $changed = @($changed | Where-Object { $_ } | Select-Object -Unique)

    if ($changed.Count -gt 0) {
        $actualFiles = @()
        foreach ($c in $changed) {
            $actualFiles += [pscustomobject]@{ path = $c; change = 'modify'; exists = $true; consistent = $true }
        }

        $actualRadius = Get-BlastRadius -Files $actualFiles

        $declaredPaths = @()
        foreach ($d in $declaredFiles) { $declaredPaths += ($d.path -replace '\\', '/') }

        $undeclared = @()
        foreach ($c in $changed) {
            if ($declaredPaths -notcontains ($c -replace '\\', '/')) { $undeclared += $c }
        }

        $untouched = @()
        foreach ($d in $declaredPaths) {
            if ($changed -notcontains $d) { $untouched += $d }
        }

        $adherence = [ordered]@{
            'changed-files'   = @($changed)
            'undeclared'      = @($undeclared)
            'declared-untouched' = @($untouched)
            'tier-escalated'  = ($actualRadius.tier -gt $blastRadius.tier)
        }
    }
}

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

    if ($kind -eq 'charter') {
        # Parsed from $rawText, not $text: stripping struck-through spans is right for
        # reference extraction and wrong here, because a struck-through invariant heading is
        # a record of a vacated slot rather than a declaration that must carry clauses.
        $formRows = @(Get-SdlcInvariantForm -Text $rawText)
        $malformed = @($formRows | Where-Object { -not $_.'has-forbids' -or -not $_.'has-applies-to' })

        if ($formRows.Count -eq 0) {
            Add-Check 'invariant-form' $false 'no invariant headings found in a charter'
        }
        elseif ($malformed.Count -gt 0) {
            $detail = ($malformed | ForEach-Object {
                $missing = @()
                if (-not $_.'has-forbids') { $missing += 'Forbids:' }
                if (-not $_.'has-applies-to') { $missing += 'Applies-to:' }
                "$($_.id) missing $($missing -join ' and ')"
            }) -join '; '
            Add-Check 'invariant-form' $false $detail
        }
        else {
            Add-Check 'invariant-form' $true $null
        }
    }

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

    if ($null -ne $traceability) {
        if ($traceability['unmapped'].Count -gt 0) {
            Add-Check 'traceability-complete' $false (
                "verifiable criteria with no annotated test: " + ($traceability['unmapped'] -join ', '))
        }
        else {
            Add-Check 'traceability-complete' $true $null
        }

        if ($traceability['orphan-annotations'].Count -gt 0) {
            Add-Check 'annotations-resolve' $false (
                "annotated criteria the spec does not declare: " + ($traceability['orphan-annotations'] -join ', '))
        }
        else {
            Add-Check 'annotations-resolve' $true $null
        }

        if ($traceability['unjustified-suppressions'].Count -gt 0) {
            Add-Check 'suppressions-justified' $false (
                "no written justification: " + ($traceability['unjustified-suppressions'] -join ', '))
        }
        else {
            Add-Check 'suppressions-justified' $true $null
        }
    }

    if ($kind -eq 'plan') {
        if ($declaredFiles.Count -eq 0) {
            Add-Check 'plan-declares-files' $false 'no parseable file table — INV-05 requires one'
        }
        else {
            Add-Check 'plan-declares-files' $true $null
        }

        if ($null -ne $adherence) {
            # Scope drift, mechanically. An undeclared file is a finding rather than a
            # footnote, which is the whole reason INV-05 exists.
            if ($adherence['undeclared'].Count -gt 0) {
                Add-Check 'plan-adherence' $false (
                    "changed but not declared: " + ($adherence['undeclared'] -join ', '))
            }
            else {
                Add-Check 'plan-adherence' $true $null
            }

            # A tier escalation is the one condition that halts a build rather than
            # merely reporting: the work turned out more dangerous than it was approved
            # to be, so a person decides.
            Add-Check 'tier-not-escalated' (-not $adherence['tier-escalated']) $(
                if ($adherence['tier-escalated']) {
                    "predicted tier $($blastRadius.tier), actual $($actualRadius.tier)"
                } else { $null })
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
    'traceability'        = $traceability
    'test-annotations'    = @($annotations)
    'suppressions'        = @($suppressions)
    'blast-radius'        = $blastRadius
    'actual-blast-radius' = $actualRadius
    'plan-adherence'      = $adherence
    'charter'          = [ordered]@{
        path          = ($Charter -replace '\\', '/')
        present       = $charterPresent
        version       = $charterVersion
        'invariant-ids' = @($invariantIds)
        'invariant-form' = @(if ($kind -eq 'charter') { Get-SdlcInvariantForm -Text $rawText } else { @() })
        'dec-ids'     = @($decIds)
    }
    roles              = @($roles)
    'gaps-by-artifact' = $gapsByArtifact
    pack               = [ordered]@{
        name       = $packName
        present    = $packPresent
        'rule-ids' = @($packRuleIds)
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
