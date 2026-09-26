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

# Dot-sourced, not duplicated: the artifact kind decides which invariants apply, which checks
# run, which row of the gaps table a role reads and which schema validates the document. Two
# functions answering that would disagree the first time a kind was added, silently.
. (Join-Path $PSScriptRoot 'SdlcArtifact.ps1')

# And one home for what the declared pack says, for the same reason. The charter delegates tool
# names to the pack, so anything that needs them -- the rules an interrogation can name, the gate
# commands a verification runs -- asks here.
. (Join-Path $PSScriptRoot 'SdlcPack.ps1')

# And one home for the INV-08 licence answer, shared with the PreToolUse gate. THE PREVENTION AND
# THE DETECTION MUST AGREE ABOUT WHAT IS PROTECTED. If they each kept their own list, the gate
# would look stricter than it is and this check would report paths nobody guards -- and the
# disagreement would surface as a false accusation rather than as a missing guard.
. (Join-Path $PSScriptRoot 'SdlcProtectedPaths.ps1')

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

function Get-ProtectedPathStatus {
    <#
    .SYNOPSIS
        Which protected paths differ from the merge-target baseline without a licence for them.

    .DESCRIPTION
        THE DETECTION HALF OF INV-08. The PreToolUse gate prevents a write it can see; this reports
        one that already happened - a person editing in an editor, a tool the matcher does not
        cover, a hook switched off. The gate's own header says it constrains the automated process
        and nothing else, so without this the rule has no answer for anything outside that.

        IT EMITS BOTH A CHECK AND A FACT FIELD, AND THAT IS NOT REDUNDANT. The check makes the
        finding citable in an interrogation. The FACT FIELD is what gates verification, because the
        verification outcome consumes no checks at all - it reads specific fact fields, which is how
        unjustified suppressions already block. A detection that emitted only a check would report
        and gate nothing.

        THE BASELINE IS THE MERGE TARGET, DEGRADING EXPLICITLY. The remote-tracking ref for master
        when it resolves, else a local master when HEAD is not on it, else HEAD. Each fallback is
        named in the output, because which comparison was actually made changes what the result
        means: against a remote-tracking ref this sees a protected path committed directly to
        master, while against HEAD it sees only an uncommitted change.

        WHAT IT CANNOT SEE, stated rather than implied. Once an amendment is merged AND pushed the
        baseline contains it, so the difference disappears and the check goes quiet - which is
        correct. In the window where an amendment is merged locally and not yet pushed, a difference
        against the remote-tracking ref is indistinguishable from an unlicensed direct commit, and
        this reports it. The detail names the baseline so a reader can tell which case they are
        looking at; narrowing it further needs per-commit licence attribution, which is a larger
        mechanism than this cycle has room for.

        SKIPPING AND FAILING ARE DIFFERENT ANSWERS. No baseline resolves at all - a repository with
        no commit - is a skip, because no comparison was owed. Git failing when a baseline DID
        resolve is a failure, because a comparison was owed and could not be made.
    #>
    param([string] $Root, [string] $Branch, [string[]] $TrackedFiles)

    $status = [ordered]@{
        baseline        = $null
        'baseline-kind' = 'none'
        skipped         = $false
        'could-not-run' = $false
        reason          = $null
        unlicensed      = @()
    }

    # --- which protected paths exist here ------------------------------------
    #
    # From the tracked list and from disk both: a path that is protected and untracked is exactly
    # the case a detection should not miss.
    $candidates = New-Object System.Collections.ArrayList
    foreach ($rel in $TrackedFiles) {
        $normal = ($rel -replace '\\', '/')
        if (Get-SdlcProtectedPathRule -RelativePath $normal) {
            if ($candidates -notcontains $normal) { $null = $candidates.Add($normal) }
        }
    }

    $onDisk = @('.sdlc/CHARTER.md')
    $packsDir = Join-Path $Root 'packs'
    if (Test-Path -LiteralPath $packsDir) {
        foreach ($packDir in @(Get-ChildItem -LiteralPath $packsDir -Directory -ErrorAction SilentlyContinue)) {
            foreach ($leaf in @('pack.yaml', 'pack.rules.yaml', 'CHARTER.md')) {
                $onDisk += "packs/$($packDir.Name)/$leaf"
            }
        }
    }
    foreach ($rel in $onDisk) {
        if (Test-SdlcProtectedPathExists -Root $Root -RelativePath $rel) {
            if ($candidates -notcontains $rel) { $null = $candidates.Add($rel) }
        }
    }

    if ($candidates.Count -eq 0) {
        # NOT a skip. There is nothing to compare and nothing to violate, and skipping would imply
        # a check was owed and not made.
        $status.reason = 'this repository carries no protected paths'
        return [pscustomobject] $status
    }

    # --- resolve the baseline, now that there is something to compare --------
    #
    # AFTER the candidate enumeration, and the order is the answer to a contradiction the spec
    # carried: it says a repository with no protected paths PASSES and does not skip, and also that
    # no resolvable baseline SKIPS. A fresh ungoverned repository is both at once, and resolving the
    # baseline first made it skip - reporting that a comparison was owed and not made, when none was
    # owed at all. Nothing to protect is decided before anything to compare against.
    $baseline = $null
    $kind = 'none'

    $null = git -C $Root rev-parse --verify --quiet 'refs/remotes/origin/master'
    if ($LASTEXITCODE -eq 0) {
        $baseline = 'refs/remotes/origin/master'
        $kind = 'remote-tracking'
    }
    else {
        $null = git -C $Root rev-parse --verify --quiet 'refs/heads/master'
        if ($LASTEXITCODE -eq 0 -and $Branch -ne 'master') {
            $baseline = 'refs/heads/master'
            $kind = 'local-master'
        }
        else {
            $null = git -C $Root rev-parse --verify --quiet 'HEAD'
            if ($LASTEXITCODE -eq 0) {
                $baseline = 'HEAD'
                $kind = 'head'
            }
        }
    }

    if (-not $baseline) {
        $status.skipped = $true
        $status.reason = 'no baseline resolves: the repository has no remote-tracking master, no local master and no commit'
        return [pscustomobject] $status
    }

    $status.baseline = $baseline
    $status.'baseline-kind' = $kind

    # --- compare, then ask whether the branch may have done it ---------------
    $unlicensed = New-Object System.Collections.ArrayList

    foreach ($rel in $candidates) {
        $null = git -C $Root diff --quiet $baseline '--' $rel
        $code = $LASTEXITCODE

        if ($code -eq 0) { continue }

        if ($code -ne 1) {
            # A comparison was owed and could not be made. Distinct from skipping, and a failure.
            $status.reason = "git could not compare $rel against $baseline (exit $code)"
            $status.'could-not-run' = $true
            $status.unlicensed = @()
            return [pscustomobject] $status
        }

        $decision = Test-SdlcWriteLicensed -Root $Root -Branch $Branch -RelativePath $rel
        if (-not $decision.allowed) {
            $null = $unlicensed.Add([pscustomobject]@{
                path   = $rel
                reason = $decision.reason
            })
        }
    }

    $status.unlicensed = @($unlicensed)
    return [pscustomobject] $status
}

function Hide-CSharpStringLiterals {
    <#
    .SYNOPSIS
        The same C# source with the INTERIOR of every string and char literal blanked.

    .DESCRIPTION
        A MATCH INSIDE A STRING LITERAL IS DATA, NOT CODE. That one sentence is the whole
        diagnosis behind two mechanical checks that report false positives on this repository, and
        both false positives have to be answered here rather than in either scanner, because they
        are the same defect in two shapes:

          `tests/Sdlc.Tests/PackTests.cs` asserts that a pack carries no suppression spellings, and
          therefore NAMES those spellings inside quoted strings. The suppression scan matched the
          text, found no comment beside it, and called the assertion an unjustified suppression.

          `PackTests.cs` and `VerifyOutcomeTests.cs` hold annotated test methods inside C# RAW
          string literals, as the source of generated fixture projects. The annotation scan read
          those ids as real traceability and reported them as criteria no spec declares.

        LENGTH AND LINE STRUCTURE ARE PRESERVED EXACTLY. Interiors are replaced character for
        character with spaces and newlines are left alone, so every line number and column the
        callers report still refers to the real file. A masking pass that collapsed literals would
        silently renumber every suppression this repository has.

        COMMENTS ARE LEFT INTACT, deliberately. The suppression scan decides whether a suppression
        is justified by looking for a comment beside it, so blanking comments would convert one
        false positive into another. They are still TRACKED, because a quote inside a comment must
        not open a string and a `//` inside a string must not open a comment.

        IT IS NOT A C# PARSER, AND MUST NOT BE MISTAKEN FOR ONE. It handles regular, verbatim,
        interpolated and raw strings and char literals. An unterminated literal ends at the line
        break rather than consuming the rest of the file, which keeps a malformed source from
        blanking everything after it.
    #>
    param([string] $Text)

    if ([string]::IsNullOrEmpty($Text)) { return $Text }

    $chars = $Text.ToCharArray()
    $n = $chars.Length
    $i = 0

    # Blanks one position unless it carries the line structure.
    function Set-Blank {
        param($Buffer, [int] $At)
        if ($Buffer[$At] -ne "`n" -and $Buffer[$At] -ne "`r") { $Buffer[$At] = ' ' }
    }

    while ($i -lt $n) {
        $c = $chars[$i]

        # --- comments: tracked so they cannot open a literal, but not blanked ---
        if ($c -eq '/' -and ($i + 1) -lt $n -and $chars[$i + 1] -eq '/') {
            while ($i -lt $n -and $chars[$i] -ne "`n") { $i++ }
            continue
        }
        if ($c -eq '/' -and ($i + 1) -lt $n -and $chars[$i + 1] -eq '*') {
            $i += 2
            while (($i + 1) -lt $n -and -not ($chars[$i] -eq '*' -and $chars[$i + 1] -eq '/')) { $i++ }
            $i += 2
            continue
        }

        # --- verbatim string: @"..." with "" as the escape ---
        if ($c -eq '@' -and ($i + 1) -lt $n -and $chars[$i + 1] -eq '"') {
            $i += 2
            while ($i -lt $n) {
                if ($chars[$i] -eq '"') {
                    if (($i + 1) -lt $n -and $chars[$i + 1] -eq '"') {
                        Set-Blank $chars $i
                        Set-Blank $chars ($i + 1)
                        $i += 2
                        continue
                    }
                    break
                }
                Set-Blank $chars $i
                $i++
            }
            $i++
            continue
        }

        if ($c -eq '"') {
            # How many quotes open this literal. Three or more is a raw string, whose interior may
            # span lines and whose closing run has to match the opening run.
            $quotes = 0
            while (($i + $quotes) -lt $n -and $chars[$i + $quotes] -eq '"') { $quotes++ }

            if ($quotes -ge 3) {
                $i += $quotes
                $closed = $false
                while ($i -lt $n) {
                    if ($chars[$i] -eq '"') {
                        $run = 0
                        while (($i + $run) -lt $n -and $chars[$i + $run] -eq '"') { $run++ }
                        if ($run -ge $quotes) { $i += $run; $closed = $true; break }
                        for ($k = 0; $k -lt $run; $k++) { Set-Blank $chars ($i + $k) }
                        $i += $run
                        continue
                    }
                    Set-Blank $chars $i
                    $i++
                }
                if (-not $closed) { break }
                continue
            }

            # An empty string carries no interior to blank.
            if ($quotes -eq 2) { $i += 2; continue }

            $i++
            while ($i -lt $n) {
                if ($chars[$i] -eq "`n") { break }
                if ($chars[$i] -eq '\' -and ($i + 1) -lt $n) {
                    Set-Blank $chars $i
                    Set-Blank $chars ($i + 1)
                    $i += 2
                    continue
                }
                if ($chars[$i] -eq '"') { break }
                Set-Blank $chars $i
                $i++
            }
            $i++
            continue
        }

        if ($c -eq "'") {
            $i++
            while ($i -lt $n) {
                if ($chars[$i] -eq "`n") { break }
                if ($chars[$i] -eq '\' -and ($i + 1) -lt $n) {
                    Set-Blank $chars $i
                    Set-Blank $chars ($i + 1)
                    $i += 2
                    continue
                }
                if ($chars[$i] -eq "'") { break }
                Set-Blank $chars $i
                $i++
            }
            $i++
            continue
        }

        $i++
    }

    return (-join $chars)
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

        # An annotated test method inside a raw string literal is the SOURCE of a generated fixture
        # project, not traceability for this repository. Read literally it makes every fixture id an
        # orphan annotation, which is what blocked verification here.
        #
        # POSITIONAL, NOT SUBSTITUTED. The criterion id is itself the interior of a literal, so
        # matching against masked text would find nothing at all - the mask would blank the very ids
        # this is looking for. So the match is made on the real source and its POSITION is tested
        # against the mask: an annotation written in code keeps its first character, and one inside
        # a literal has been blanked to a space.
        $masked = Hide-CSharpStringLiterals -Text $content

        foreach ($m in [regex]::Matches($content, 'TestProperty\s*\(\s*"AC"\s*,\s*"([^"]+)"\s*\)')) {
            if ($m.Index -lt $masked.Length -and $masked[$m.Index] -ne $content[$m.Index]) { continue }

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

        # A SUPPRESSION SPELLING INSIDE A QUOTED STRING IS AN ASSERTION ABOUT SUPPRESSIONS, NOT A
        # SUPPRESSION. This repository's own tests assert that a pack carries no such spellings, and
        # therefore name them - so reading the source literally reported the assertions as
        # unjustified suppressions and blocked verification for every cycle here.
        #
        # Masking is the right instrument for this scan, where it was the wrong one for annotations:
        # a real `#pragma` or `SuppressMessage` sits OUTSIDE any literal and survives the mask
        # untouched, so what disappears is exactly the false positives.
        #
        # C# only. The project and property files this scan also reads are XML, where `<NoWarn>` is
        # element text and quoting means something else entirely.
        $matchLines = $lines
        if ($rel -match '\.cs$') {
            $masked = Hide-CSharpStringLiterals -Text ((Get-Content -LiteralPath $full -Raw -Encoding UTF8))
            $matchLines = @($masked -split '\r?\n')
        }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]

            # Matched against the masked line, reported from the real one.
            $probe = if ($i -lt $matchLines.Count) { $matchLines[$i] } else { $line }

            $isSuppression = ($probe -match '#pragma\s+warning\s+disable') -or
                             ($probe -match '<NoWarn>') -or
                             ($probe -match 'SuppressMessage')
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

function Get-SdlcInterrogationProgress {
    <#
    .SYNOPSIS
        The rounds already filed for one artifact, the next round's number, and whether the repair
        loop is still making progress.
    .DESCRIPTION
        STOP ON NO-PROGRESS, NEVER "UNTIL CLEAN". That was measured rather than assumed: the
        finding rate does not decay -- round 2 found three of five blocking items inside round 1's
        repairs, round 3 found four of five inside round 2's, and this project replicated it at
        three of four. "Until clean" therefore never fires, and a fixed round cap ships
        known-broken artifacts wearing a clean label.

        So the rule is two consecutive rounds with no net reduction. NOTHING COULD APPLY IT: the
        workflow took a round number nobody incremented, so every interrogation was round 1, every
        document overwrote the last, and the one rule that makes the loop terminate had no state to
        measure. A rule an agent is asked to remember across rounds is a rule it can talk itself
        past, which is the exact failure mode the no-progress detector exists to replace.

        The MEASUREMENT is mechanical and lives here. The RESPONSE is an escalation to a person,
        which is why this reports rather than refuses -- once someone is looking, they decide
        whether the next round is worth running.
    #>
    param([string] $Root, [string] $CycleId, [string] $Target)

    $result = [ordered]@{
        rounds       = @()
        'next-round' = 1
        progress     = 'first-round'
        detail       = 'nothing has been interrogated for this artifact yet'
    }

    if (-not $CycleId -or -not $Target) { return $result }

    $dir = Join-Path $Root ".sdlc/cycles/$CycleId/findings"
    if (-not (Test-Path -LiteralPath $dir)) { return $result }

    $rounds = New-Object System.Collections.ArrayList

    foreach ($file in @(Get-ChildItem -LiteralPath $dir -Filter "$Target.r*.json" -File)) {
        if ($file.Name -notmatch "^$([regex]::Escape($Target))\.r([0-9]+)\.json$") { continue }
        $round = [int] $Matches[1]

        $raw = [string] (Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8)
        if (-not $raw.Trim()) { continue }

        $doc = ConvertFrom-Json $raw
        $blocking = 0
        $unresolved = 0

        if ($doc.PSObject.Properties.Name -contains 'findings') {
            foreach ($f in @($doc.findings)) {
                $state = ''
                if ($f.PSObject.Properties.Name -contains 'state') { $state = [string] $f.state }
                if ($state -ne 'blocking') { continue }
                $blocking++

                $hasDisposition = $false
                if ($f.PSObject.Properties.Name -contains 'disposition') {
                    $hasDisposition = [bool] $f.disposition
                }
                if (-not $hasDisposition) { $unresolved++ }
            }
        }

        $null = $rounds.Add([pscustomobject]@{
            round      = $round
            blocking   = $blocking
            unresolved = $unresolved
        })
    }

    $ordered = @($rounds | Sort-Object -Property round)
    $result['rounds'] = $ordered

    if ($ordered.Count -eq 0) { return $result }

    $result['next-round'] = ([int] $ordered[$ordered.Count - 1].round) + 1

    if ([int] $ordered[$ordered.Count - 1].blocking -eq 0) {
        $result['progress'] = 'converged'
        $result['detail'] = "round $($ordered[$ordered.Count - 1].round) found nothing blocking"
        return $result
    }

    if ($ordered.Count -eq 1) {
        $result['progress'] = 'first-round'
        $result['detail'] = "round 1 found $($ordered[0].blocking) blocking; there is nothing to compare against yet"
        return $result
    }

    # Counted from the END, because the rule is about CONSECUTIVE rounds. A loop that stalled, then
    # reduced, then stalled again has not stalled twice in a row and is still making progress.
    $stalled = 0
    for ($i = $ordered.Count - 1; $i -ge 1; $i--) {
        $delta = [int] $ordered[$i].blocking - [int] $ordered[$i - 1].blocking
        if ($delta -ge 0) { $stalled++ } else { break }
    }

    if ($stalled -ge 2) {
        $result['progress'] = 'halted-no-progress'
        $result['detail'] = "$stalled consecutive rounds with no net reduction in blocking findings: " +
            (($ordered | ForEach-Object { "r$($_.round)=$($_.blocking)" }) -join ' ')
    }
    elseif ($stalled -eq 1) {
        $result['progress'] = 'stalled-once'
        $result['detail'] = 'one round with no net reduction. Another and the loop halts for a person'
    }
    else {
        $result['progress'] = 'reducing'
        $result['detail'] = 'blocking findings are still falling round over round'
    }

    return $result
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

# DECLARED BEFORE THE BRANCH, because an ungoverned repository is a case this has to survive
# rather than a broken one. A repository with no charter is exactly the state the precondition
# rule exists for -- "does a charter exist? no: offer /sdlc:init" -- and under strict mode reading
# a variable that was only ever assigned inside `if ($charterPresent)` throws. So the gatherer
# died with "The variable '$charterFm' cannot be retrieved" on what may be a person's first use,
# naming an internal variable instead of the missing charter it actually found.
$charterFm = $null
$charterLines = @()

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

    # A REPOSITORY WITH NO COMMITS IS A REAL CASE, not a broken one: `init` scaffolds a solution
    # and the first intent can be written before anything is committed. `git rev-parse HEAD` fails
    # there with "Needed a single revision", and taking .Trim() of the nothing it returns died with
    # "You cannot call a method on a null-valued expression" — an error naming neither the cause nor
    # the artifact, on what may be a person's first ever use of this.
    $headSha = $null
    $rawSha = (git rev-parse --short HEAD)
    if ($LASTEXITCODE -eq 0 -and $rawSha) { $headSha = ([string] $rawSha).Trim() }

    $branch = $null
    $rawBranch = (git rev-parse --abbrev-ref HEAD)
    if ($LASTEXITCODE -eq 0 -and $rawBranch) { $branch = ([string] $rawBranch).Trim() }
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
$kind = Get-SdlcArtifactKind -RelativePath $relative

# The cycle this artifact belongs to, derived here so there is ONE answer to it. The interrogation
# workflow derived it independently in JavaScript, which is two implementations of one path
# convention; it reads this instead.
$cycleId = Get-SdlcCycleIdFromPath -RelativePath $relative

# Which round the next interrogation of this artifact is, and whether the repair loop is still
# getting anywhere. Both are facts about the tree, which is why they are gathered here rather than
# remembered by an agent across rounds.
$interrogation = Get-SdlcInterrogationProgress -Root $RepoRoot -CycleId $cycleId -Target $kind

$criteria = @(Get-AcceptanceCriteria -Text $text)

# A verify document's criteria come from its sibling spec: verify reports on the spec's
# criteria, so reading them from anywhere else would be a second list.
if ($kind -eq 'verify' -and $criteria.Count -eq 0) {
    $siblingSpec = Join-Path (Split-Path -Parent $artifactPath) 'spec.md'
    if (Test-Path -LiteralPath $siblingSpec) {
        $criteria = @(Get-AcceptanceCriteria -Text ((Get-Content -LiteralPath $siblingSpec -Encoding UTF8) -join "`n"))
    }
}

# UNCONDITIONALLY, for every artifact kind. INV-08 does not become relevant at one stage: an
# unlicensed charter change is as much a fact during a spec interrogation as during a verification,
# and gating it only where it happened to be convenient is how a protection acquires a hole.
$protectedPaths = Get-ProtectedPathStatus -Root $RepoRoot -Branch $branch -TrackedFiles $trackedFiles

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
    #
    # EACH ANNOTATION IS JUDGED AGAINST ITS OWN CYCLE'S SPEC, not against whichever spec happens to
    # be under interrogation. Comparing every annotation in the suite to one spec's criteria was
    # correct only while the suite held one cycle's annotations: the moment a second cycle annotated
    # a test, each cycle's annotations read as orphans of the other's spec and the check blocked
    # every verification from then on. It did exactly that on the first cycle after the one that
    # introduced it, which is how this was found.
    #
    # A criterion id carries its cycle - `AC-<yyyymmdd>-<hhhh>-<n>` - so the spec that governs it is
    # named by the annotation itself. An id naming a cycle with no spec on disk IS an orphan: that is
    # the renumbering-and-typo case the check exists for, and it stays caught.
    $criterionIds = @()
    foreach ($c in $criteria) { $criterionIds += $c.id }

    $criteriaByCycle = @{}
    $orphanAnnotations = @()
    foreach ($id in $coveredIds) {
        $governingIds = $criterionIds

        if ($id -match '^AC-(\d{8}-[0-9a-f]{4})-') {
            $annotatedCycle = $Matches[1]

            if ($annotatedCycle -ne $cycleId) {
                if (-not $criteriaByCycle.ContainsKey($annotatedCycle)) {
                    $otherIds = @()
                    $otherSpec = Join-Path $RepoRoot ".sdlc/cycles/$annotatedCycle/spec.md"
                    if (Test-Path -LiteralPath $otherSpec) {
                        $otherText = (Get-Content -LiteralPath $otherSpec -Encoding UTF8) -join "`n"
                        foreach ($c in @(Get-AcceptanceCriteria -Text $otherText)) { $otherIds += $c.id }
                    }
                    $criteriaByCycle[$annotatedCycle] = $otherIds
                }

                $governingIds = $criteriaByCycle[$annotatedCycle]
            }
        }

        if ($governingIds -notcontains $id) { $orphanAnnotations += $id }
    }

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

# --- the declared stack pack, and the rules INV-04 makes blockable ----------
#
# THE RULES ARRIVE WITH THEIR TEXT, not as bare ids. A role handed `PR-03` and nothing else cannot
# show that the forbidden thing happened, and it cannot go looking either: the pack lives in the
# plugin rather than in the solution, so the role has no path to read. One invariant made every
# pack rule blockable in principle and none of them citable in practice.
#
# Resolved from the PLUGIN for that same reason -- only the pack's CHARTER is copied into a solution
# at init time, so a solution consuming dotnet-library@v1 has no local pack.rules.yaml.
$packName = $null
if ($null -ne $charterFm -and $charterFm.fields.Contains('pack')) {
    $packName = [string] $charterFm.fields['pack']
    if ($packName -eq 'none') { $packName = $null }
}

$packRules = @(Get-SdlcPackRules -Name $packName)
$packPresent = ($packRules.Count -gt 0)

$packRuleIds = @()
foreach ($rule in $packRules) { $packRuleIds += $rule.id }

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
    # --- INV-08, detected rather than only prevented -------------------------
    #
    # Three outcomes, kept distinct on purpose. Skipping says no comparison was owed; failing to run
    # says one was owed and could not be made; and a clean pass says one was made and found nothing.
    # Collapsing the first two would let a broken git report as a clean charter.
    if ($protectedPaths.skipped) {
        Add-Check 'protected-paths-licensed' $true "skipped: $($protectedPaths.reason)"
    }
    elseif ($protectedPaths.'could-not-run') {
        Add-Check 'protected-paths-licensed' $false "could not run: $($protectedPaths.reason)"
    }
    elseif (@($protectedPaths.unlicensed).Count -gt 0) {
        Add-Check 'protected-paths-licensed' $false (
            "changed against $($protectedPaths.baseline) with no licence on this branch: " +
            ((@($protectedPaths.unlicensed) | ForEach-Object { $_.path }) -join ', '))
    }
    else {
        Add-Check 'protected-paths-licensed' $true $null
    }

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

    # THE SCHEMAS WERE EXERCISED ONLY BY TEST FIXTURES until this check existed, so a real
    # artifact's frontmatter was never validated at the moment it was written. Run through
    # Test-SdlcArtifact.ps1 rather than reimplemented here: the schema files are the authority
    # for these rules, and restating them in PowerShell would be the duplication INV-03 forbids.
    #
    # A kind with no schema reports `skipped`, which is a pass here — the charter's shape is
    # checked by the invariant-form check below instead, and treating "nothing governs this" as
    # a failure would make the check fire on the one document it cannot judge.
    #
    # FAILS CLOSED WHEN IT CANNOT RUN, and the condition is deliberately only "is there
    # frontmatter to check". Guarding on the validator's existence instead would mean a renamed
    # or missing validator makes the check DISAPPEAR rather than fail — the same silent-removal
    # shape the hook test calls out, where protection stops existing and everything still looks
    # correct. A missing validator is a broken installation, not a clean artifact.
    $validator = Join-Path $PSScriptRoot 'Test-SdlcArtifact.ps1'
    if ($frontmatter.present) {
        $verdict = $null
        try {
            if (Test-Path -LiteralPath $validator) {
                $verdictJson = (& $validator -Path $artifactPath -RepoRoot $RepoRoot) -join "`n"
                if ($verdictJson.Trim()) { $verdict = ConvertFrom-Json $verdictJson }
            }
        }
        catch {
            $verdict = $null
        }

        if ($null -eq $verdict) {
            # Fails closed, and says which way. An unavailable validator is not an approval,
            # but neither is it the artifact's fault, so the detail distinguishes them.
            Add-Check 'frontmatter-valid' $false 'the schema validator could not be run'
        }
        elseif ($verdict.skipped) {
            Add-Check 'frontmatter-valid' $true "no schema governs a '$kind'"
        }
        else {
            Add-Check 'frontmatter-valid' ([bool] $verdict.ok) $(
                if ($verdict.ok) { $null } else { ($verdict.errors -join '; ') })
        }
    }

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
        'cycle-id'  = $cycleId
        governed    = $governed
        frontmatter = $frontmatter.fields
    }
    'interrogation'       = $interrogation
    'acceptance-criteria' = @($criteria)
    'declared-files'      = @($declaredFiles)
    'traceability'        = $traceability
    'test-annotations'    = @($annotations)
    'suppressions'        = @($suppressions)
    'protected-paths'     = $protectedPaths
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
        rules      = @($packRules)
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
