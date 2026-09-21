#Requires -Version 5.1
<#
.SYNOPSIS
    PreToolUse gate: warn when code is edited outside an approved plan, or claimed by two.

.DESCRIPTION
    Deliberately advisory by default. A hard block on every edit is infuriating and gets the
    plugin uninstalled; a warning in the transcript is enough to notice the discipline
    slipping. Set SDLC_GATE=block to make it refuse the edit instead.

    Silent in repos that do not use the framework at all, because this plugin loads in every
    project.

    Three distinct states are worth warning about and they are not the same problem:

      no approved plan   nothing has been agreed; this is the gate the README describes.
      unplanned scope    a plan is approved but does not claim this file. That is the
                         verifier's manifest check, moved from after the build to before
                         it, which is where it is cheap.
      contested file     two live plans claim it. Unlike the other two, the person editing
                         cannot fix this alone: the remedy is re-planning a chain that may
                         not be theirs.

    Those three labels are a contract. They are written here, in the README's gate-hook
    section, and in the description shipped in hooks.json, and a check compares all three.

    Windows PowerShell 5.1 on purpose: it is the only runtime present on every Windows
    machine with no install step, which is the property that matters for something that
    ships to a team and runs on every single edit.

    The manifest is read by scripts/SliceManifestParser.ps1, which is the only thing in the
    engine that knows how to read one. This file used to carry its own copy, and the two
    disagreed.
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Artifacts, docs and config are how you get *out* of this state, or are edited while
# setting a repo up. Never gate them.
$SkipExtensions = @('.md', '.txt', '.json', '.yml', '.yaml', '.toml')
$SkipNames      = @('.gitignore', '.editorconfig', '.gitattributes')

function Get-Field {
    param($Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

# --- main -----------------------------------------------------------------------------
function Invoke-Gate {

# The parser is loaded here rather than at file scope, so a missing or unreadable one is
# caught by the try around this call and leaves the gate silent instead of printing a stack
# trace on every edit an agent makes. A dot-source at file scope would be outside that guard.
. (Join-Path $PSScriptRoot '..' | Join-Path -ChildPath 'scripts' | Join-Path -ChildPath 'SliceManifestParser.ps1')

try { $raw = [Console]::In.ReadToEnd() } catch { return 0 }

try { $payload = $raw | ConvertFrom-Json } catch { return 0 }  # malformed payload

$path = [string] (Get-Field -Object (Get-Field -Object $payload -Name 'tool_input') -Name 'file_path')
if ([string]::IsNullOrWhiteSpace($path)) { return 0 }

$leaf = [System.IO.Path]::GetFileName($path)
$ext  = [System.IO.Path]::GetExtension($path)
if ($SkipExtensions -contains $ext.ToLowerInvariant()) { return 0 }
if ($SkipNames -contains $leaf.ToLowerInvariant()) { return 0 }

$cwd  = (Get-Location).Path
$root = Get-SdlcArtifactRoot -Cwd $cwd
if ($null -eq $root) { return 0 }   # repo does not use the framework

# Segment equality on the root's leaf, unchanged, wrong edges included. This decides what
# the hook *inspects*; whether a claim is under the artifact root is a different question
# with a different answer, and the parser holds that one.
$rootLeaf = Split-Path -Leaf $root
if ($path.Replace('\', '/').Split('/') -contains $rootLeaf) { return 0 }   # the chain itself

$planPaths = @(Get-SdlcPlanPath -Root $root)

$live     = @()
$refusals = @()
foreach ($planPath in $planPaths) {
    # Each plan in its own try: one malformed file must not disable the gate for the whole
    # repository, which is what a single failure outside this loop would do.
    try {
        $plan = Read-SdlcPlan -Path $planPath -BaseDirectory $cwd
        foreach ($r in $plan.Refusals) {
            if ($r.Kind -eq 'plan') { $refusals += [pscustomobject]@{ Slug = $plan.Slug; Rule = $r.Rule } }
        }
        if (Test-SdlcPlanIsLive -Plan $plan) { $live += $plan }
    }
    catch { continue }
}

if ($live.Count -eq 0) {
    $detail = "no approved plan: "
    if ($planPaths.Count -gt 0) {
        $slugs = ($planPaths | ForEach-Object { Split-Path -Leaf (Split-Path -Parent $_) }) -join ', '
        $detail += "$($planPaths.Count) plan.md found under $rootLeaf/, none live ($slugs)."
    } else {
        $detail += "no plan.md exists under $rootLeaf/."
    }
    # A plan refused for its lifecycle lines is not live, so its absence from the live set is
    # the whole reason there is nothing to compare against. Saying so beats a warning that
    # blames the repository for a typo in one file.
    if ($refusals.Count -gt 0) {
        $named = ($refusals | ForEach-Object { "$($_.Slug) ($($_.Rule))" }) -join ', '
        $detail += " Refused: $named."
    }
    $action = 'Run /sdlc-core:plan first, or state the tier if this is a trivial/task change.'
} else {
    $targetKey = ConvertTo-SdlcPathKey -Path $path -BaseDirectory $cwd

    # Collect every claimant rather than returning at the first. Returning early is what made
    # a second claim structurally unobservable.
    $claimants = @()
    foreach ($plan in $live) {
        foreach ($claim in $plan.Claims) {
            if ($claim.Key -eq $targetKey) { $claimants += $plan.Slug; break }
        }
    }

    if ($claimants.Count -eq 1) { return 0 }   # claimed by one live plan: the point of the gate

    if ($claimants.Count -gt 1) {
        $spelling = $path.Replace('\', '/')
        $detail = "contested file: $spelling is claimed by $($claimants.Count) live plans ($($claimants -join ', '))."
        $action = 'Settle which chain owns it with whoever owns the other one and re-plan there; this is not something this edit can fix.'
    } else {
        $slugs  = ($live | ForEach-Object { $_.Slug }) -join ', '
        $detail = "unplanned scope: $($path.Replace('\', '/')) is claimed by no slice of the live plan(s): $slugs."
        $action = 'Add the file to a slice manifest and re-approve, or say why it is out-of-chain work.'
    }
}

[Console]::Error.WriteLine("[sdlc-gate] Editing $leaf. $detail $action")

if ($env:SDLC_GATE -eq 'block') { return 2 }   # PreToolUse: exit 2 blocks the tool call
    return 0
}

# This hook runs on every edit in every repo, so an unhandled error here is a stack trace
# in the transcript on every edit. Whatever goes wrong, stay out of the way: the gate
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
