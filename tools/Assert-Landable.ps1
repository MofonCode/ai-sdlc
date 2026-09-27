# Refuses to open a pull request for a cycle that has not verified clean.
#
# `/sdlc:land` refuses this too, in words. This hook is the reason the refusal holds: a skill body
# is instructions a model can reason around, and landing is the one outward-facing step where being
# talked into an exception has consequences for other people.
#
# An escalated cycle is refused with its history attached - three remediation passes were spent -
# because a reviewer needs to tell work that resisted three attempts from work nobody tried to fix.
#
# Windows PowerShell 5.1. No &&, no ??, no ternary; ConvertFrom-Json returns PSCustomObject.

$ErrorActionPreference = 'Stop'

function Deny([string]$Reason) {
    $payload = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $Reason
        }
    }
    $payload | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $event = $raw | ConvertFrom-Json } catch { exit 0 }

$command = $null
if ($event.tool_input) { $command = $event.tool_input.command }
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

# Only the call that creates a pull request. Reading, listing and checking out are all fine.
if ($command -notmatch 'gh\s+pr\s+create') { exit 0 }

$cwd = $event.cwd
if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = (Get-Location).Path }

Push-Location $cwd
try {
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) { exit 0 }
    $branch = "$branch".Trim()
}
finally {
    Pop-Location
}

if ($branch -notmatch '^cycle/(?<id>[0-9]{8}-[0-9a-f]{4})$') { exit 0 }
$cycleId = $Matches['id']

$verifyPath = Join-Path $cwd (".sdlc/cycles/$cycleId/verify.md")

if (-not (Test-Path -LiteralPath $verifyPath)) {
    Deny (
        "Cycle $cycleId has no verify document. An unverified cycle has no claim to land - run " +
        "/sdlc:verify $cycleId first."
    )
}

$verify = Get-Content -LiteralPath $verifyPath -Raw

$outcomeMatch = [regex]::Match($verify, '(?m)^outcome:\s*(?<v>pass|blocked|escalated)\s*$')
if (-not $outcomeMatch.Success) {
    Deny (
        "Cannot read an outcome from .sdlc/cycles/$cycleId/verify.md. A verify document with no " +
        'outcome is not a verification.'
    )
}

$outcome = $outcomeMatch.Groups['v'].Value

if ($outcome -eq 'pass') { exit 0 }

$spentMatch = [regex]::Match($verify, '(?m)^\s*passes_spent:\s*(?<v>[0-3])\s*$')
$spent = '?'
if ($spentMatch.Success) { $spent = $spentMatch.Groups['v'].Value }

if ($outcome -eq 'blocked') {
    $remaining = '?'
    if ($spentMatch.Success) { $remaining = 3 - [int]$spentMatch.Groups['v'].Value }

    Deny (
        "Cycle $cycleId verified as blocked, with $remaining of 3 remediation passes remaining. " +
        "Run /sdlc:remediate $cycleId."
    )
}

Deny (
    "Cycle $cycleId is escalated: verification is still failing after $spent remediation passes, " +
    'and it belongs to a person now. Landing is refused. The four ways forward are a fix by hand, ' +
    'an amended and re-approved spec, a waiver with a recorded reason, or abandoning the cycle - ' +
    'each of them changes an input, and /sdlc:verify then supersedes this document.'
)
