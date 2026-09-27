# Refuses a targeted edit to an artifact whose whole value is being derived.
#
# A verify document is assembled from evidence that already exists. The moment someone can tweak
# one, nothing downstream can tell an audit from an assertion - so `escalated` could become `pass`
# with a two-word edit and the record would still look like a record.
#
# This is one layer, not the whole guarantee. It stops the realistic path: a targeted Edit that
# changes an outcome or deletes a finding. A full rewrite through Write is still possible, because
# re-deriving IS a full rewrite and a hook cannot tell the two apart from the call alone. The
# durable check is in CI, where every verify document is validated against verify.schema.json and
# its state machine - see tests/Sdlc.Tests. Layered deliberately: this one is immediate and
# narrow, that one is complete and slower.
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

$path = $null
if ($event.tool_input) { $path = $event.tool_input.file_path }
if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }

$normalized = $path.Replace('\', '/')

if ($normalized -match '/\.sdlc/cycles/[^/]+/verify\.md$') {
    Deny (
        'A verify document is derived, never edited. Change one of its inputs - the code, the ' +
        'spec, or the waivers - and run /sdlc:verify again so a fresh document supersedes this ' +
        'one. If the budget is spent, the four ways out belong to a person: fix by hand, amend ' +
        'the spec and re-approve it, waive the finding with a reason, or abandon the cycle.'
    )
}

if ($normalized -match '/\.sdlc/(cycles/[^/]+|intents)/findings/') {
    Deny (
        'Findings are the record of what an interrogation concluded. Re-run ' +
        '/sdlc:interrogate to produce a new round; rounds are numbered and kept, because the ' +
        'sequence is the evidence of whether a repair worked.'
    )
}

if ($normalized -match '/\.sdlc/cycles/[^/]+/evidence/') {
    Deny (
        'Build and test evidence is written by /sdlc:build and read by /sdlc:verify. Editing it ' +
        'makes every verify claim that cites it unfalsifiable.'
    )
}

exit 0
