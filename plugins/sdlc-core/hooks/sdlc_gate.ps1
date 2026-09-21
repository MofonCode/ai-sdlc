#Requires -Version 5.1
<#
.SYNOPSIS
    PreToolUse gate: warn when code is edited outside an approved plan.

.DESCRIPTION
    Deliberately advisory by default. A hard block on every edit is infuriating and gets the
    plugin uninstalled; a warning in the transcript is enough to notice the discipline
    slipping. Set SDLC_GATE=block to make it refuse the edit instead.

    Silent in repos that do not use the framework at all, because this plugin loads in every
    project.

    Two distinct states are worth warning about and they are not the same problem:

      no approved plan   nothing has been agreed; this is the gate the README describes.
      unplanned scope    a plan is approved but does not claim this file. That is the
                         verifier's manifest check, moved from after the build to before
                         it, which is where it is cheap.

    Windows PowerShell 5.1 on purpose: it is the only runtime present on every Windows
    machine with no install step, which is the property that matters for something that
    ships to a team and runs on every single edit. It costs ~150ms per edit against ~50ms
    for python; that tax was measured and accepted rather than assumed.
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Artifacts, docs and config are how you get *out* of this state, or are edited while
# setting a repo up. Never gate them.
$SkipExtensions = @('.md', '.txt', '.json', '.yml', '.yaml', '.toml')
$SkipNames      = @('.gitignore', '.editorconfig', '.gitattributes')
$SkipDirs       = @('.git', 'node_modules', 'bin', 'obj', '.venv', 'dist', 'target')
$DefaultRoot    = '.sdlc'

function Get-Frontmatter {
    param([string] $Path)
    try { $text = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop }
    catch { return '' }
    if ($null -eq $text) { return '' }
    if ($text -match '(?s)\A---\r?\n(.*?)\r?\n---(\r?\n|\z)') { return $Matches[1] }
    return ''
}

function Get-ArtifactRoot {
    <#
      Where this repo keeps its chain, or $null if it does not use the framework.

      The charter declares its own root in `artifact_root`, so a repo that keeps its backlog
      visible at top level (intent/, specs/, docs/sdlc/) is found rather than silently
      ignored - a gate that goes quiet on a non-default root is worse than no gate, because
      silence reads as approval.

      Two levels deep, because docs/sdlc/ is a documented root and is two levels.
    #>
    param([string] $Cwd)

    $candidates = New-Object System.Collections.Generic.List[string]
    $candidates.Add((Join-Path (Join-Path $Cwd $DefaultRoot) 'CHARTER.md'))

    foreach ($depth in @('*', '*/*')) {
        $glob = Join-Path $Cwd (Join-Path $depth 'CHARTER.md')
        $hits = @(Get-ChildItem -Path $glob -File -ErrorAction SilentlyContinue | Sort-Object FullName)
        foreach ($hit in $hits) {
            $parts = $hit.FullName.Substring($Cwd.Length).Replace('\', '/').Split('/')
            $noisy = $false
            foreach ($part in $parts) { if ($SkipDirs -contains $part) { $noisy = $true } }
            if (-not $noisy) { $candidates.Add($hit.FullName) }
        }
    }

    foreach ($charter in $candidates) {
        if (-not (Test-Path -LiteralPath $charter -PathType Leaf)) { continue }
        $fm = Get-Frontmatter -Path $charter
        if ($fm -match '(?m)^artifact_root:\s*(.+?)\s*$') {
            $declared = $Matches[1].Trim().Trim("'").Trim('"')
            $resolved = Join-Path $Cwd $declared
            if (Test-Path -LiteralPath $resolved -PathType Container) {
                return (Resolve-Path -LiteralPath $resolved).Path
            }
        }
        return (Split-Path -Parent $charter)
    }

    # A repo mid-flight can have a chain before it has a charter.
    $default = Join-Path $Cwd $DefaultRoot
    if (Test-Path -LiteralPath $default -PathType Container) {
        return (Resolve-Path -LiteralPath $default).Path
    }
    return $null
}

function Get-Field {
    <#
      Read one property, or $null when it is absent.

      Strict mode throws on `$obj.PSObject.Properties.Name` when the object has no
      properties at all, which `{}` produces - so the obvious membership test is exactly
      the one that crashes on the payload shape you did not anticipate.
    #>
    param($Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-ClaimedFile {
    <#
      Every path in a plan's slice manifest, normalised to forward slashes.

      Deliberately a regex over the `files:` lines rather than a YAML parser: PowerShell 5.1
      has no YAML support in the box, and taking a module dependency on something that runs
      on every single edit is not worth it for a shape the schema already mandates.
    #>
    param([string] $Frontmatter)

    $files = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in ($Frontmatter -split "`n")) {
        if ($line -notmatch 'files:') { continue }
        foreach ($m in [regex]::Matches($line, '["'']([^"'']+)["'']')) {
            $null = $files.Add($m.Groups[1].Value.Replace('\', '/').TrimStart('.', '/'))
        }
    }
    return $files
}

# --- main -----------------------------------------------------------------------------
function Invoke-Gate {

try { $raw = [Console]::In.ReadToEnd() } catch { return 0 }

try { $payload = $raw | ConvertFrom-Json } catch { return 0 }  # malformed payload

$path = [string] (Get-Field -Object (Get-Field -Object $payload -Name 'tool_input') -Name 'file_path')
if ([string]::IsNullOrWhiteSpace($path)) { return 0 }

$leaf = [System.IO.Path]::GetFileName($path)
$ext  = [System.IO.Path]::GetExtension($path)
if ($SkipExtensions -contains $ext.ToLowerInvariant()) { return 0 }
if ($SkipNames -contains $leaf.ToLowerInvariant()) { return 0 }

$cwd  = (Get-Location).Path
$root = Get-ArtifactRoot -Cwd $cwd
if ($null -eq $root) { return 0 }   # repo does not use the framework

$rootLeaf = Split-Path -Leaf $root
if ($path.Replace('\', '/').Split('/') -contains $rootLeaf) { return 0 }   # the chain itself

$plans = @(Get-ChildItem -Path (Join-Path $root '*/plan.md') -File -ErrorAction SilentlyContinue |
           Sort-Object FullName)

$approved = @()
foreach ($plan in $plans) {
    $fm = Get-Frontmatter -Path $plan.FullName
    if ($fm -match '(?m)^status:\s*approved\s*$') {
        $approved += [pscustomobject]@{
            Slug        = (Split-Path -Leaf $plan.DirectoryName)
            Frontmatter = $fm
        }
    }
}

if ($approved.Count -eq 0) {
    if ($plans.Count -gt 0) {
        $slugs  = ($plans | ForEach-Object { Split-Path -Leaf $_.DirectoryName }) -join ', '
        $detail = "$($plans.Count) plan.md found under $rootLeaf/ but none approved ($slugs)."
    } else {
        $detail = "No plan.md exists under $rootLeaf/."
    }
    $action = 'Run /sdlc-core:plan first, or state the tier if this is a trivial/task change.'
} else {
    $full = $path
    if (-not [System.IO.Path]::IsPathRooted($full)) { $full = Join-Path $cwd $path }
    $target = [System.IO.Path]::GetFullPath($full)
    if ($target.StartsWith($cwd, [StringComparison]::OrdinalIgnoreCase)) {
        $target = $target.Substring($cwd.Length).TrimStart('\', '/')
    }
    $target = $target.Replace('\', '/')

    foreach ($a in $approved) {
        $claimed = Get-ClaimedFile -Frontmatter $a.Frontmatter
        if ($claimed.Contains($target)) { return 0 }  # claimed by an approved plan: the point of the gate
    }

    $slugs  = ($approved | ForEach-Object { $_.Slug }) -join ', '
    $detail = "$target is claimed by no slice of the approved plan(s): $slugs."
    $action = 'This is unplanned scope. Add the file to a slice manifest and re-approve, or say why it is out-of-chain work.'
}

[Console]::Error.WriteLine("[sdlc-gate] Editing $leaf. $detail $action")

if ($env:SDLC_GATE -eq 'block') { return 2 }   # PreToolUse: exit 2 blocks the tool call
    return 0
}

# This hook runs on every edit in every repo, so an unhandled error here is a stack trace
# in the transcript on every keystroke. Whatever goes wrong, stay out of the way: the gate
# is advisory, and a broken advisor is worse than a silent one.
try {
    # A PowerShell function returns everything it emits, not just its `return` value, so
    # one stray uncaptured expression would turn this into an array and `exit` would throw.
    # Take the last value and coerce; anything unexpected means "stay out of the way".
    $result = @(Invoke-Gate)
    $code = 0
    if ($result.Count -gt 0) { $null = [int]::TryParse([string] $result[-1], [ref] $code) }
    if ($code -ne 2) { $code = 0 }
    exit $code
} catch {
    exit 0
}
