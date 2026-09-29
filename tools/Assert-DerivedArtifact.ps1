# Refuses the ways a derived artifact gets quietly rewritten, without blocking the thing that
# produces it.
#
# The first version of this hook denied every write under .sdlc/**/findings/ and stopped
# `/sdlc:interrogate` writing its own findings document. That is the failure mode to design against:
# a hook cannot tell a legitimate producer from a person with an opinion by looking at the call, so
# it must key on something that distinguishes them.
#
# Two signals do:
#
#   * Edit versus Write. Re-deriving an artifact replaces it whole; changing an outcome or deleting a
#     finding is a targeted edit. So Edit on a verify document is refused and Write is not.
#   * Creation versus overwrite. Interrogation rounds and build evidence are written once and kept -
#     round 2 is a new file, never a rewrite of round 1 - so creating one is allowed and overwriting
#     an existing one is refused.
#
# What is left uncovered is a full rewrite of a verify document through Write, which is
# indistinguishable from re-derivation at the call. CI closes that: every verify document is
# validated against verify.schema.json and its state machine, so a hand-made `pass` over an open
# blocking finding fails the build. Layered deliberately - this layer is immediate and narrow, that
# one is complete and slower.
#
# Windows PowerShell 5.1. No &&, no ??, no ternary; ConvertFrom-Json returns PSCustomObject.

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SdlcPlan.ps1')

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

$tool = "$($event.tool_name)"
$normalized = $path.Replace('\', '/')
$exists = Test-Path -LiteralPath $path

if ($normalized -match '/\.sdlc/cycles/[^/]+/verify\.md$') {
    # A whole-file Write is how re-derivation lands, so only a targeted edit is refused here.
    if ($tool -eq 'Edit' -or $tool -eq 'NotebookEdit') {
        Deny (
            'A verify document is derived, never edited. Change one of its inputs - the code, the ' +
            'spec, or the waivers - and run /sdlc:verify again so a fresh document supersedes this ' +
            'one. If the budget is spent, the four ways out belong to a person: fix by hand, amend ' +
            'the spec and re-approve it, waive the finding with a reason, or abandon the cycle.'
        )
    }

    exit 0
}

if ($normalized -match '/\.sdlc/cycles/(?<cycle>[^/]+)/findings/(?<kind>intent|spec|plan)\.r[0-9]+\.json$') {
    if ($exists) {
        Deny (
            'That findings document already exists. Interrogation rounds are numbered and kept, ' +
            'because the sequence is the evidence of whether a repair worked - write the next round ' +
            'instead of replacing this one.'
        )
    }

    # An artifact a person has approved is settled, and a finding against it has no consumer: the
    # phase that would have acted on it is behind us, and the artifact is already the basis of
    # everything downstream. Writing one anyway is how the review loop restarts on work that was
    # finished. The legitimate case - an amended spec - clears the approval first, which is a person's
    # act and not a skill's.
    #
    # One word for the gate, on every artifact. An intent, a spec and a plan are all approved_by a
    # person; there is no second vocabulary for the same act.
    $cycle = $Matches['cycle']
    $kind = $Matches['kind']

    $artifact = Join-Path (Split-Path -Parent (Split-Path -Parent $path)) "$kind.md"

    if (Test-Path -LiteralPath $artifact) {
        # -Encoding UTF8: 5.1 assumes the system codepage for a file with no BOM, and a mangled read
        # is a frontmatter field silently not matching.
        $frontmatter = Get-Content -LiteralPath $artifact -Raw -Encoding UTF8

        $settledBy = Get-SdlcApprovalField -Text $frontmatter -Field 'approved_by'

        if ($null -ne $settledBy) {

            Deny (
                "The $kind for $cycle was approved by $settledBy, so it is settled and a finding " +
                'against it has nothing to act on it. If it has since been amended, the approval is ' +
                'stale: remove those frontmatter fields, interrogate, and have a person approve it ' +
                'again. Otherwise the cycle has moved on - after an approved spec comes /sdlc:plan, ' +
                'after an approved plan comes /sdlc:build.'
            )
        }
    }

    exit 0
}

if ($normalized -match '/\.sdlc/cycles/[^/]+/evidence/') {
    if ($exists) {
        Deny (
            'That evidence file already exists. Build writes evidence once and verify derives every ' +
            'claim from it; replacing it makes every claim that cited it unfalsifiable. If the build ' +
            'ran again, it is a new cycle or a new round, not an overwrite.'
        )
    }

    exit 0
}

exit 0
