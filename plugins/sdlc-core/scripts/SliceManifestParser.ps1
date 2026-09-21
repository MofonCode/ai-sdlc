#Requires -Version 5.1
<#
.SYNOPSIS
    The one reader of a plan's slice manifest. Dot-sourced by the gate hook and by
    Assert-SliceManifest.ps1.

.DESCRIPTION
    Before this file existed the manifest was parsed in three places and the two PowerShell
    readers disagreed in three ways - where a `files:` line had to start, whether it had to sit
    inside a slice, and whether comparison was case-sensitive - while the hook also disagreed
    with *itself*, normalising the edited path differently from the claims it compared it to.
    A fifth defect they shared: TrimStart('.', '/') mapped `.sdlc/CHARTER.md` onto a string
    naming nothing on disk.

    The property this file exists for is not "no divergence" but the thing underneath it:
    **one question about one file should have one answer.** Two readers differing violates it,
    and one reader disagreeing with itself violates it equally.

    A library. No top-level side effects, nothing written to any stream, and no terminating
    error for a malformed plan - a caller that must never throw cannot use a parser that does.
    Refusals come back as data for the caller to report or discard.
#>

# Deliberately no Set-StrictMode here. This file is dot-sourced, so anything it sets at file
# scope it sets in its caller's session - which is a top-level side effect, and the one kind of
# side effect a library dot-sourced by a hook that runs on every edit must not have. Both
# callers set their own.

$script:SdlcDefaultRoot = '.sdlc'

# Directories a charter is never looked for under. Moved here with the resolution that uses it;
# it has never applied to an edited path and does not now.
$script:SdlcNoisyDirs = @('.git', 'node_modules', 'bin', 'obj', '.venv', 'dist', 'target')

$script:SdlcStatusValues = @('draft', 'approved', 'superseded')


function Get-SdlcFrontmatter {
    <#
    .SYNOPSIS
        The frontmatter block of a file, or $null when it has none as its first line.
    #>
    param([Parameter(Mandatory = $true)][string] $Path)

    try { $text = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop }
    catch { return $null }

    if ($null -eq $text) { return $null }
    if ($text -match '(?s)\A---\r?\n(.*?)\r?\n---(\r?\n|\z)') { return $Matches[1] }
    return $null
}


function ConvertTo-SdlcPathKey {
    <#
    .SYNOPSIS
        The comparison key for one path, relative to a base directory.

    .DESCRIPTION
        Textual and segment-wise, deliberately short of the filesystem. It folds exactly the
        spellings R2 enumerates and nothing else: a leading `./`, separator direction, case,
        repeated separators, a trailing separator, `.` and `..` segments that cancel, surrounding
        whitespace, and an absolute path inside the base against its relative form.

        `.sdlc/CHARTER.md` survives intact, because `.sdlc` is an ordinary segment. That is the
        whole difference from the TrimStart('.', '/') both readers used to make.

        Not [System.IO.Path]::GetFullPath. The overload taking an explicit base does not exist
        on the runtime this ships to, so it would silently anchor to the process working
        directory, which is not where Get-Location points. It would also drag in trailing dots,
        reserved device names and long-path handling, all of which are out of scope.

        (Stated without naming a runtime on purpose: this is a shipped engine executable, and
        the invariant against the engine naming a stack has no carve-out for comments. That
        check caught this line when it did name one.)
    #>
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Path,
        [string] $BaseDirectory
    )

    $p = $Path.Trim()
    if ([string]::IsNullOrEmpty($p)) { return '' }

    $p = $p.Replace('\', '/')

    if ($BaseDirectory) {
        $base = $BaseDirectory.Trim().Replace('\', '/').TrimEnd('/')
        if ($base -and $p.Length -gt $base.Length -and
            $p.Substring(0, $base.Length).Equals($base, [StringComparison]::OrdinalIgnoreCase) -and
            $p[$base.Length] -eq '/') {
            $p = $p.Substring($base.Length + 1)
        }
    }

    $out = New-Object System.Collections.Generic.List[string]
    foreach ($segment in $p.Split('/')) {
        if ($segment -eq '' -or $segment -eq '.') { continue }
        if ($segment -eq '..') {
            if ($out.Count -gt 0 -and $out[$out.Count - 1] -ne '..') { $out.RemoveAt($out.Count - 1) }
            else { $out.Add('..') }
            continue
        }
        $out.Add($segment)
    }

    return ($out -join '/').ToLowerInvariant()
}


function Test-SdlcPathUnderRoot {
    <#
    .SYNOPSIS
        Whether a claim's path is under the artifact root: containment, which is the charter's
        own wording for the carve-out.

    .DESCRIPTION
        Not the gate hook's edit-path skip, which is segment equality on the root's leaf and
        which the intent freezes as it stands. Different objects, different tests: that skip
        decides what the hook *inspects*, this decides which *claims* leave a comparison.
        Making both segment equality would exempt implementation files the disjointness
        invariant binds - under a root of `docs/sdlc`, `src/sdlc/Thing.cs` is not under the
        artifact root at all.
    #>
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $PathKey,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $RootKey
    )

    if ([string]::IsNullOrEmpty($RootKey)) { return $false }
    if ($PathKey -eq $RootKey) { return $true }
    return $PathKey.StartsWith($RootKey + '/', [StringComparison]::Ordinal)
}


function New-SdlcRefusal {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('claim', 'plan')][string] $Kind,
        [Parameter(Mandatory = $true)][string] $Rule,
        [Parameter(Mandatory = $true)][string] $Message
    )

    [pscustomobject]@{ Kind = $Kind; Rule = $Rule; Message = $Message }
}


function Read-SdlcPlan {
    <#
    .SYNOPSIS
        One plan: its lifecycle fields, its slices, its claims, and its refusals.

    .DESCRIPTION
        Line-oriented on purpose - the engine ships with no YAML dependency - and narrower than
        YAML by the five rules the charter freezes. Every form those rules reject was already
        rejected in silence by both readers; what changes is that the silence becomes a refusal
        a caller can report.

        Two kinds of refusal, and the distinction decides whether a plan still counts:

          claim-level  a `files:` value or entry is malformed. Those claims are dropped and the
                       rest of the plan stands.
          plan-level   the lifecycle lines themselves are malformed or duplicated. The plan is
                       not live and contributes nothing. A refusal that dropped only its claims
                       would leave such a plan live, because its claims are not what is wrong
                       with it - and `status: draft` followed by `status: approved` reading as
                       live is the quietest route there is to work produced from a draft.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [string] $BaseDirectory
    )

    $refusals = New-Object System.Collections.Generic.List[object]
    $slices   = New-Object System.Collections.Generic.List[object]
    $claims   = New-Object System.Collections.Generic.List[object]

    $frontmatter = Get-SdlcFrontmatter -Path $Path
    if ($null -eq $frontmatter) {
        $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'frontmatter' -Message 'no frontmatter as the first line'))
        return [pscustomobject]@{
            Path = $Path; Slug = (Split-Path -Leaf (Split-Path -Parent $Path))
            Status = $null; Finished = $null; Slices = @(); Claims = @(); Refusals = $refusals.ToArray()
        }
    }

    $lines = $frontmatter -split "`n" | ForEach-Object { $_.TrimEnd("`r") }

    # --- lifecycle lines, top level only (R60) -------------------------------------------
    $statusLines   = @($lines | Where-Object { $_ -match '^status:' })
    $finishedLines = @($lines | Where-Object { $_ -match '^finished:' })
    $nestedFinished = @($lines | Where-Object { $_ -match '^\s+finished:' })

    if ($statusLines.Count -ne 1) {
        $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'one status line' `
            -Message ("expected exactly one 'status:' line, found {0}" -f $statusLines.Count)))
    }
    if ($finishedLines.Count -gt 1) {
        $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'one finished line' `
            -Message ("expected at most one 'finished:' line, found {0}" -f $finishedLines.Count)))
    }
    if ($nestedFinished.Count -gt 0) {
        # Ambiguity on a one-way field fails loudly. A column-anchored reader cannot see this
        # line and a looser one reads it as the plan's; if the looser one wins on `true`, the
        # plan releases every file it claims and there is no way back.
        $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'finished at top level' `
            -Message 'a "finished:" line is nested inside the slices block'))
    }

    $status = $null
    if ($statusLines.Count -ge 1) {
        if ($statusLines[0] -match '^status:\s*(.*)$') {
            $raw = $Matches[1].Trim()
            if ($raw -ne $raw.Trim('"').Trim("'") -or $script:SdlcStatusValues -notcontains $raw) {
                $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'status value' `
                    -Message ("'status: {0}' is quoted or not one of the declared values" -f $raw)))
            }
            $status = $raw
        }
    }

    $finished = $null
    if ($finishedLines.Count -ge 1) {
        if ($finishedLines[0] -match '^finished:\s*(.*)$') {
            $raw = $Matches[1].Trim()
            if ($raw -ne 'true' -and $raw -ne 'false') {
                $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'finished value' `
                    -Message ("'finished: {0}' is not exactly true or false, unquoted, alone on its line" -f $raw)))
            }
            elseif ($raw -eq 'true' -and $status -ne 'approved') {
                $refusals.Add((New-SdlcRefusal -Kind 'plan' -Rule 'finished requires approved' `
                    -Message "'finished: true' without 'status: approved'"))
            }
            $finished = $raw
        }
    }

    # --- slices and claims ----------------------------------------------------------------
    $current = $null
    foreach ($line in $lines) {
        if ($line -match '^\s*-\s+id:\s*(.+?)\s*$') {
            $current = [pscustomobject]@{
                Id     = $Matches[1].Trim().Trim('"').Trim("'")
                Claims = (New-Object System.Collections.Generic.List[object])
            }
            $slices.Add($current)
            continue
        }

        # R4: only a line whose first non-whitespace text is `files:`, and only inside a slice.
        # R57: a `files:` line R4 does not collect from contributes nothing and is not refused -
        # it is structurally meaningless rather than ambiguous. Nothing ever closes a slice, so
        # what is dropped is a `files:` line before the first `- id:` and only that.
        if ($line -notmatch '^\s*files:\s*(.*)$') { continue }
        if ($null -eq $current) { continue }

        $value = $Matches[1]
        $quoted = [regex]::Matches($value, '["'']([^"'']+)["'']')

        if ($quoted.Count -eq 0) {
            # `files: []` is well-formed and empty, not malformed. The caller's own "a slice
            # that owns no files is not a slice" is the better diagnosis and the one a reader
            # can act on; refusing it here would replace a precise message with a vaguer one.
            if ($value.Trim() -eq '[]') { continue }

            $refusals.Add((New-SdlcRefusal -Kind 'claim' -Rule 'files entry is a quoted string' `
                -Message ("slice '{0}': a 'files:' line carries no quoted entry" -f $current.Id)))
            continue
        }

        foreach ($m in $quoted) {
            $spelling = $m.Groups[1].Value

            if ($spelling -match '[*?]' -or $spelling -match '[\\/]\s*$') {
                $refusals.Add((New-SdlcRefusal -Kind 'claim' -Rule 'files entry is a file path' `
                    -Message ("slice '{0}': '{1}' is a directory or glob entry" -f $current.Id, $spelling)))
                continue
            }

            $claim = [pscustomobject]@{
                Spelling = $spelling
                Key      = (ConvertTo-SdlcPathKey -Path $spelling -BaseDirectory $BaseDirectory)
                SliceId  = $current.Id
            }
            $current.Claims.Add($claim)
            $claims.Add($claim)
        }
    }

    [pscustomobject]@{
        Path     = $Path
        Slug     = (Split-Path -Leaf (Split-Path -Parent $Path))
        Status   = $status
        Finished = $finished
        Slices   = $slices.ToArray()
        Claims   = $claims.ToArray()
        Refusals = $refusals.ToArray()
    }
}


function Test-SdlcPlanIsLive {
    <#
    .SYNOPSIS
        Whether a read plan is live: approved, not finished, and carrying no plan-level refusal.

    .DESCRIPTION
        The refusal clause comes first. Without it this and the plan-level refusal rule give
        opposite answers for a plan whose lifecycle lines are duplicated, which is precisely the
        plan charter version 21 was amended to catch.
    #>
    param([Parameter(Mandatory = $true)] $Plan)

    foreach ($r in $Plan.Refusals) { if ($r.Kind -eq 'plan') { return $false } }
    if ($Plan.Status -ne 'approved') { return $false }
    if ($Plan.Finished -eq 'true') { return $false }
    return $true
}


function Get-SdlcArtifactRoot {
    <#
    .SYNOPSIS
        Where this repository keeps its chain, or $null when it does not use the framework.

    .DESCRIPTION
        Moved out of the gate hook unchanged - the same candidate order, the same two-level
        search, the same fallbacks, and the same wrong edges, including returning from the first
        candidate charter found. Repairing it here would change which plans every existing
        repository sees, which is a different change.
    #>
    param([Parameter(Mandatory = $true)][string] $Cwd)

    $candidates = New-Object System.Collections.Generic.List[string]
    $candidates.Add((Join-Path (Join-Path $Cwd $script:SdlcDefaultRoot) 'CHARTER.md'))

    foreach ($depth in @('*', '*/*')) {
        $glob = Join-Path $Cwd (Join-Path $depth 'CHARTER.md')
        $hits = @(Get-ChildItem -Path $glob -File -ErrorAction SilentlyContinue | Sort-Object FullName)
        foreach ($hit in $hits) {
            $parts = $hit.FullName.Substring($Cwd.Length).Replace('\', '/').Split('/')
            $noisy = $false
            foreach ($part in $parts) { if ($script:SdlcNoisyDirs -contains $part) { $noisy = $true } }
            if (-not $noisy) { $candidates.Add($hit.FullName) }
        }
    }

    foreach ($charter in $candidates) {
        if (-not (Test-Path -LiteralPath $charter -PathType Leaf)) { continue }
        $fm = Get-SdlcFrontmatter -Path $charter
        if ($null -ne $fm -and $fm -match '(?m)^artifact_root:\s*(.+?)\s*$') {
            $declared = $Matches[1].Trim().Trim("'").Trim('"')
            $resolved = Join-Path $Cwd $declared
            if (Test-Path -LiteralPath $resolved -PathType Container) {
                return (Resolve-Path -LiteralPath $resolved).Path
            }
        }
        return (Split-Path -Parent $charter)
    }

    # A repo mid-flight can have a chain before it has a charter.
    $default = Join-Path $Cwd $script:SdlcDefaultRoot
    if (Test-Path -LiteralPath $default -PathType Container) {
        return (Resolve-Path -LiteralPath $default).Path
    }
    return $null
}


function Get-SdlcPlanPath {
    <#
    .SYNOPSIS
        Every `<root>/*/plan.md`, in a stable order.
    #>
    param([Parameter(Mandatory = $true)][string] $Root)

    @(Get-ChildItem -Path (Join-Path $Root '*/plan.md') -File -ErrorAction SilentlyContinue |
        Sort-Object FullName | ForEach-Object { $_.FullName })
}


function Get-SdlcLivePlan {
    <#
    .SYNOPSIS
        Every live plan under a root, each already read.

    .DESCRIPTION
        Each plan is read inside its own try, so one malformed plan cannot take the caller down
        with it. A plan that raises is excluded and the rest are compared - the gate hook must
        never throw, and a comparison that stops at the first bad file is worse than one that
        names it and carries on.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [string] $BaseDirectory,
        [string] $ExcludePath
    )

    $excludeKey = $null
    if ($ExcludePath) {
        try { $excludeKey = (Resolve-Path -LiteralPath $ExcludePath -ErrorAction Stop).Path }
        catch { $excludeKey = $ExcludePath }
    }

    $out = New-Object System.Collections.Generic.List[object]
    foreach ($path in (Get-SdlcPlanPath -Root $Root)) {
        if ($excludeKey) {
            $thisPath = $path
            try { $thisPath = (Resolve-Path -LiteralPath $path -ErrorAction Stop).Path } catch { }
            if ($thisPath.Equals($excludeKey, [StringComparison]::OrdinalIgnoreCase)) { continue }
        }
        try {
            $plan = Read-SdlcPlan -Path $path -BaseDirectory $BaseDirectory
            if (Test-SdlcPlanIsLive -Plan $plan) { $out.Add($plan) }
        }
        catch { continue }
    }
    $out.ToArray()
}
