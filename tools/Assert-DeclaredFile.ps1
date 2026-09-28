# Refuses a source edit that no approved plan declared.
#
# Two rules, in order. Both matter only on a cycle branch (`cycle/<id>`); off one, this hook stays
# out of the way so ordinary work in a repository is unaffected.
#
#   1. No source edit at all until a person has approved the cycle's plan. The plan gate is the
#      last point before anything becomes irreversible, and a build that starts before it has
#      skipped the only review that happens while the change is still a paragraph.
#   2. No edit to a file the plan did not declare. The declared list is what a reviewer approved;
#      a file outside it arrives with an approval it never had.
#
# What this hook cannot see is creep INSIDE a declared file - an unrelated refactor in a file the
# plan legitimately names. That needs reading comprehension, so it is CON-07's job at verification
# rather than a hook's.
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

$cwd = $event.cwd
if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = (Get-Location).Path }

$normalized = $path.Replace('\', '/')

# The framework's own artifacts are always writable: a plan cannot be authored under a rule that
# requires a plan.
if ($normalized -match '/\.sdlc/') { exit 0 }
if ($normalized -match '/\.claude/') { exit 0 }

# Windows PowerShell 5.1 turns a native command's stderr into an ErrorRecord, and with
# $ErrorActionPreference = 'Stop' that terminates the script. Outside a repository git writes to
# stderr, so without this the hook CRASHES instead of deciding - and a crashed hook is a hook that
# enforces nothing. It stayed latent until a test ran the script outside a repository.
Push-Location $cwd
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
    $gitFailed = $LASTEXITCODE -ne 0
    $branch = "$branch".Trim()
}
finally {
    $ErrorActionPreference = $previousPreference
    Pop-Location
}

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

if ($branch -notmatch '^cycle/(?<id>[0-9]{8}-[0-9a-f]{4})$') { exit 0 }
$cycleId = $Matches['id']

$planPath = Join-Path $cwd (".sdlc/cycles/$cycleId/plan.md")

if (-not (Test-Path -LiteralPath $planPath)) {
    Deny (
        "Branch cycle/$cycleId has no plan at .sdlc/cycles/$cycleId/plan.md. Run /sdlc:plan " +
        '$cycleId, have a person approve it, then build.'
    )
}

$plan = Get-Content -LiteralPath $planPath -Raw

# Approval is recorded in the plan's frontmatter by the person who gave it. Both fields, because a
# name with no date says nothing about which version of the plan was approved.
$approvedBy = [regex]::Match($plan, '(?m)^approved_by:\s*(?<v>\S.*)$')
$approvedAt = [regex]::Match($plan, '(?m)^approved_at:\s*(?<v>\S.*)$')

if (-not ($approvedBy.Success -and $approvedAt.Success)) {
    Deny (
        "The plan for $cycleId is not approved. A person adds approved_by and approved_at to its " +
        'frontmatter; until then nothing in this cycle may touch source. This is the gate the ' +
        'whole process rests on, not a formality to work around.'
    )
}

# Declared files appear in the plan's Files table as `backticked/paths`.
$declared = @()
foreach ($m in [regex]::Matches($plan, '`(?<p>[^`]+)`')) {
    $candidate = $m.Groups['p'].Value.Replace('\', '/').Trim()
    if ($candidate -match '[/.]') { $declared += $candidate }
}

$relative = $normalized
$cwdNormalized = $cwd.Replace('\', '/').TrimEnd('/')
if ($relative.StartsWith($cwdNormalized, [System.StringComparison]::OrdinalIgnoreCase)) {
    $relative = $relative.Substring($cwdNormalized.Length).TrimStart('/')
}

$isDeclared = $false
foreach ($d in $declared) {
    if ($relative.Equals($d, [System.StringComparison]::OrdinalIgnoreCase)) { $isDeclared = $true }
    if ($relative.EndsWith('/' + $d, [System.StringComparison]::OrdinalIgnoreCase)) { $isDeclared = $true }
}

if (-not $isDeclared) {
    Deny (
        "The plan for $cycleId does not declare $relative. Either the plan is incomplete - stop, " +
        'say which file and why, and have it amended and re-approved - or this change is scope ' +
        'the plan never described. Both are decisions for a person, not an edit.'
    )
}

exit 0
