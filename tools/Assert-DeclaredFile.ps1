# Refuses a change to a file no approved plan declared.
#
# Two rules, in order, and both only on a cycle branch (`cycle/<id>`). Off one this hook stays out of the
# way, so ordinary work in a governed repository is unaffected.
#
#   1. No source change until a person has approved the cycle's plan. The plan gate is the last point
#      before anything becomes irreversible.
#   2. No change to a file the plan did not declare. The declared list is what a reviewer approved; a
#      file outside it arrives carrying an approval it never had.
#
# THIS IS THE FAST LAYER, NOT THE COMPLETE ONE. It inspects an action, so it can only recognise ways of
# writing a file that it knows about - and a `python - <<PY` heredoc that writes one is opaque. Anything
# it misses is caught at verification by tools/Compare-DiffToPlan.ps1, which reads git and therefore sees
# every change however it arrived. This layer exists so a mistake is reported at the edit rather than an
# hour later, not because it is airtight.
#
# It also cannot see creep INSIDE a declared file - an unrelated refactor in a file the plan legitimately
# names. That needs reading comprehension, so it is CON_07's job at verification.
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

function Remove-LiteralSpan([string]$Command) {
    <#
    .SYNOPSIS
    The command with heredoc bodies, here-strings and quoted spans blanked out.

    .DESCRIPTION
    A redirect is shell syntax. A `>` inside text the shell never interprets is not one, and the first
    version of this scanned the raw command string, so every `>` anywhere looked like a redirect.

    Two real refusals from a trial run, both of them wrong:

      git commit -m @'...<noreply@anthropic.com>...'@   ->  refused a file called "@"
      cat > f <<'EOF' ... <LangVersion>latest</...      ->  refused a file called "latest"

    The first fires on any commit carrying an attribution line. The second fires on any .csproj, .props
    or .razor written through a heredoc, which is how every markup file in a cycle gets written. Between
    them the gate refused twelve times during one build, and the caller learned to route around it.

    Blanking the literals rather than parsing them can lose a genuine target - `> "a file.md"` becomes
    `> ` and is skipped. That direction is safe: anything missed here is caught at verification by
    Compare-DiffToPlan.ps1, which reads git. A false refusal is not safe, because it teaches a caller
    that the gate is noise.
    #>

    $text = $Command

    # Heredoc bodies first: they can contain quotes of their own. `<<EOF`, `<<'EOF'`, `<<-"EOF"`.
    $text = [regex]::Replace(
        $text,
        '(?s)<<-?\s*(?<q>["'']?)(?<w>[A-Za-z_][A-Za-z0-9_]*)\k<q>.*?(?:\r?\n|\A)\k<w>',
        ' ')

    # PowerShell here-strings.
    $text = [regex]::Replace($text, "(?s)@'.*?'@", ' ')
    $text = [regex]::Replace($text, '(?s)@".*?"@', ' ')

    # Ordinary quoted spans.
    $text = [regex]::Replace($text, "'[^']*'", ' ')
    $text = [regex]::Replace($text, '"[^"]*"', ' ')

    return $text
}

function Get-RedirectionTarget([string]$Command) {
    # The shell forms that create or replace a file. Deliberately short: the complete guarantee is the
    # diff comparison at verification, so this only has to catch the common shapes.
    #
    #   > path      >> path      N> path      tee path      tee -a path
    #
    # Not counted: `2>&1` and friends, which redirect a descriptor and name no file, and the null device.
    # Not seen at all: anything inside a literal - see Remove-LiteralSpan.
    $targets = New-Object System.Collections.Generic.List[string]

    $shell = Remove-LiteralSpan -Command $Command

    foreach ($m in [regex]::Matches($shell, '(?<![0-9&])[0-9]?>>?\s*(?<t>[^\s|;&<>()]+)')) {
        $t = $m.Groups['t'].Value.Trim().Trim('"').Trim("'")
        if (-not $t) { continue }
        if ($t -match '^&') { continue }
        if ($t -eq '/dev/null' -or $t -eq 'NUL' -or $t -eq '$null') { continue }
        if (-not $targets.Contains($t)) { $targets.Add($t) }
    }

    foreach ($m in [regex]::Matches($shell, '\btee\b\s+(?:-a\s+)?(?<t>[^\s|;&<>()-][^\s|;&<>()]*)')) {
        $t = $m.Groups['t'].Value.Trim().Trim('"').Trim("'")
        if ($t -and -not $targets.Contains($t)) { $targets.Add($t) }
    }

    return $targets.ToArray()
}

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $event = $raw | ConvertFrom-Json } catch { exit 0 }

$cwd = $event.cwd
if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = (Get-Location).Path }

# What is about to be written, whichever tool is writing it.
$targets = @()
if ($event.tool_input) {
    $fields = $event.tool_input.PSObject.Properties.Name

    if ($fields -contains 'file_path') {
        if (-not [string]::IsNullOrWhiteSpace($event.tool_input.file_path)) {
            $targets = @("$($event.tool_input.file_path)")
        }
    }
    elseif ($fields -contains 'command') {
        $targets = Get-RedirectionTarget ("$($event.tool_input.command)")
    }
}

if ($targets.Count -eq 0) { exit 0 }

$branch = Get-SdlcBranchState -WorkingDirectory $cwd

if (-not $branch.IsRepository) {
    # Silence is right for a directory nobody governs: this hook runs on every edit everywhere, and
    # refusing in an ordinary folder gets it switched off. A directory holding .sdlc/ is governed, and
    # without git nothing works - no diff for build to record, no evidence for verify to derive from,
    # no branch for this hook to key on. Permitting everything there is the worst outcome available.
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

$cycleId = $branch.CycleId
if ($null -eq $cycleId) { exit 0 }

$planPath = Join-Path $cwd ".sdlc/cycles/$cycleId/plan.md"
$cwdNormalized = $cwd.Replace('\', '/').TrimEnd('/')

foreach ($target in $targets) {
    $normalized = "$target".Replace('\', '/')

    # The framework's own artifacts are always writable: a plan cannot be authored under a rule that
    # requires a plan, and the findings that judge it are written while it is being decided.
    if ($normalized -match '(^|/)\.sdlc/') { continue }
    if ($normalized -match '(^|/)\.claude/') { continue }

    if (-not (Test-Path -LiteralPath $planPath)) {
        Deny (
            "Branch cycle/$cycleId has no plan at .sdlc/cycles/$cycleId/plan.md. Run /sdlc:plan " +
            "$cycleId, have a person approve it, then build."
        )
    }

    $plan = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8

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

    $relative = $normalized
    if ($relative.StartsWith($cwdNormalized, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relative = $relative.Substring($cwdNormalized.Length).TrimStart('/')
    }
    elseif ([System.IO.Path]::IsPathRooted($normalized)) {
        # Absolute, and not under this repository. A plan declares what a change does to its own
        # repository and says nothing about anywhere else, so this is not ours to refuse.
        #
        # It used to be refused, and during a trial run that blocked `gh pr create --body-file` writing
        # to the session scratchpad - a path with no relationship to the plan at all. The caller worked
        # around it by piping the body over stdin, which is what an over-refusing gate always produces.
        continue
    }

    $declared = Get-SdlcDeclaredFile -PlanPath $planPath

    if ($declared.Count -eq 0) {
        Deny (
            "The plan for $cycleId declares no files. Its ## Files table is missing or malformed, so " +
            'there is nothing to check this change against. Fix the table and have the plan re-approved.'
        )
    }

    if (-not (Test-SdlcPathDeclared -Declared $declared -RelativePath $relative)) {
        Deny (
            "The plan for $cycleId does not declare $relative. Either the plan is incomplete - stop, " +
            'say which file and why, and have it amended and re-approved - or this change is scope the ' +
            'plan never described. Both are decisions for a person, not an edit.'
        )
    }
}

exit 0
