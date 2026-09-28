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

# Windows PowerShell 5.1 turns a native command's stderr into an ErrorRecord, and with
# $ErrorActionPreference = 'Stop' that terminates the script. Outside a repository git writes to
# stderr, so without this the hook CRASHES instead of deciding - and a crashed hook is a hook that
# enforces nothing. It stayed latent until a test ran the script outside a repository.
. (Join-Path $PSScriptRoot 'SdlcPlan.ps1')

$state = Get-SdlcBranchState -WorkingDirectory $cwd
$gitFailed = -not $state.IsRepository

if ($gitFailed) {
    # Staying quiet here is correct for a directory that is not governed: this hook runs on every
    # edit everywhere, and refusing in an ordinary folder would get it switched off. But a directory
    # holding .sdlc/ IS governed, and without git nothing works - no diff for build to record, no
    # evidence for verify to derive from, and no branch for this hook to key on. Silently permitting
    # everything there is the worst outcome available: the process looks enforced and is not.
    if (Test-Path -LiteralPath (Join-Path $cwd '.sdlc')) {
        Deny (
            'This directory has a .sdlc/ directory but is not a git repository, so nothing here is ' +
            'enforced: build cannot record a diff, verify has no evidence to derive from, and the ' +
            'gates key on a cycle/<id> branch that cannot exist. Run git init and commit the current ' +
            'state before starting a cycle.'
        )
    }

    exit 0
}

$cycleId = $state.CycleId
if ($null -eq $cycleId) { exit 0 }

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
