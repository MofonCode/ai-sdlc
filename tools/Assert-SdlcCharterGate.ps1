#Requires -Version 5.1
<#
.SYNOPSIS
    PreToolUse gate: refuses an edit to the charter or a pack outside a charter-review branch.

.DESCRIPTION
    INV-08 WAS THE ONLY INVARIANT ENFORCED BY ASKING NICELY. Every other rule is enforced by
    being citable: a finding points at it, earns a citation, and the cycle halts. INV-08 forbids
    amending the charter, the stack pack or the pack rules outside an approved charter or pack
    intent - and nothing checked it. The rule was repeated in three skills as prose, and prose
    is what an agent is asked to follow rather than what it is prevented from breaking.

    That is backwards for the reason the charter itself gives: this is the rule with the most to
    lose. Without it, a cycle that edits its way around an invariant leaves no finding behind.

    FAIL CLOSED, IN EVERY CASE. Decided deliberately: a protection that opens when it is
    confused is the INV-06 erosion failure wearing a different hat - it stops working and
    nothing announces that it has. Unparseable input, an undeterminable branch, a missing intent
    file, an unreadable one, or an error in this script all deny.

    IT NEVER ANSWERS "allow", AND THAT IS NOT AN OVERSIGHT. A PreToolUse hook that returns
    permissionDecision 'allow' AUTO-APPROVES the tool call and skips the checks that would
    otherwise apply. This gate has an opinion about one thing only, so when it has no objection
    it prints nothing and exits 0, leaving every other rule to decide as usual. Granting blanket
    approval to every Write in the repository, as a side effect of protecting one file, would be
    a far larger hole than the one being closed.

    THE ESCAPE HATCH IS A PERSON, AND THAT IS THE DESIGN. A hook can be switched off, and doing
    so is loud and deliberate. INV-08 says a cycle "can request; only a person can grant" - a
    hook a person can disable and a process cannot is that sentence made mechanical.

    WHAT IT DOES NOT PROTECT AGAINST, AND MUST NOT CLAIM TO. A hook sees Claude Code tool calls
    and nothing else. A person editing the charter in an editor, or running git directly, is
    untouched. That is not a gap to close: it constrains the automated process, which is what
    INV-08 is about.

.PARAMETER PayloadPath
    Read the hook payload from this file instead of stdin. For tests only - a hook is always
    invoked with the payload on stdin.

.EXAMPLE
    powershell -NoProfile -File Assert-SdlcCharterGate.ps1   # payload arrives on stdin
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $PayloadPath
)

Set-StrictMode -Version Latest

# NOT 'Stop'. A terminating error must become a DENY with an explanation rather than a crash
# whose exit code Claude Code reads as "no opinion" - which would turn every unexpected
# condition into silent permission. Everything below runs inside one try/catch for that reason.
$ErrorActionPreference = 'Continue'

$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

# The four protected paths. The last is a judgement worth recording: INV-08's wording is "this
# document, the stack pack, or the pack rules", and a pack's charter TEMPLATE governs nothing
# until a solution copies it - so a narrow reading would leave it out. It is in scope because it
# is the most consequential file in a pack: every solution created from that pack inherits it,
# and an ungated edit changes the rules every future solution is judged by.
$protectedPatterns = @(
    '^\.sdlc/CHARTER\.md$',
    '^packs/[^/]+/pack\.yaml$',
    '^packs/[^/]+/pack\.rules\.yaml$',
    '^packs/[^/]+/CHARTER\.md$'
)

# The sources that may amend them. Anything else on the branch is not an approved charter intent.
$permittedSources = @('charter-review', 'pack-review')

function Write-Deny {
    param([string] $Reason)

    # The refusal explains itself at the moment of refusal, which is the whole advantage it has
    # over prose in a skill.
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

function Write-NoOpinion {
    # Nothing printed, exit 0. See the note above on why this is not `allow`.
    exit 0
}

function Get-RelativePosixPath {
    param([string] $Path, [string] $Root)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }

    $candidate = $Path
    if (-not [System.IO.Path]::IsPathRooted($candidate)) {
        $candidate = Join-Path $Root $candidate
    }

    # GetFullPath rather than Resolve-Path: the target of a Write may not exist yet, and
    # Resolve-Path fails on a path that is not there - which would deny every new file.
    try { $full = [System.IO.Path]::GetFullPath($candidate) } catch { return $null }
    try { $rootFull = [System.IO.Path]::GetFullPath($Root) } catch { return $null }

    $rootFull = $rootFull.TrimEnd('\', '/')
    if (-not $full.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        # Outside the project. Not this gate's business.
        return $null
    }

    $relative = $full.Substring($rootFull.Length).TrimStart('\', '/')
    return ($relative -replace '\\', '/')
}

function Get-TargetPaths {
    param($ToolInput)

    $paths = @()
    if ($null -eq $ToolInput) { return $paths }

    $names = @($ToolInput.PSObject.Properties.Name)

    # Edit, Write and MultiEdit all carry `file_path`; NotebookEdit carries `notebook_path`.
    # Both are read rather than assumed, because a tool this gate does not know about must not
    # slip through on a field name it never looked at.
    foreach ($field in @('file_path', 'notebook_path')) {
        if ($names -contains $field) {
            $value = $ToolInput.$field
            if ($value) { $paths += [string] $value }
        }
    }

    # A batch edit may carry its own list. Same reasoning: read it rather than trust the
    # top-level field to cover everything.
    if ($names -contains 'edits') {
        foreach ($edit in @($ToolInput.edits)) {
            if ($null -eq $edit) { continue }
            if (@($edit.PSObject.Properties.Name) -contains 'file_path' -and $edit.file_path) {
                $paths += [string] $edit.file_path
            }
        }
    }

    return $paths
}

function Get-FrontmatterSource {
    param([string] $Path)

    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8 -ErrorAction Stop)
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $null }

    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match '^source:\s*(\S+)') { return $Matches[1].Trim() }
    }

    return $null
}

try {
    if ($PayloadPath) {
        $raw = Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8
    }
    else {
        $raw = [Console]::In.ReadToEnd()
    }

    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Deny 'INV-08 gate: the hook payload was empty, so the target could not be identified. Failing closed.'
    }

    $payload = $raw | ConvertFrom-Json

    $payloadNames = @($payload.PSObject.Properties.Name)
    $toolName = if ($payloadNames -contains 'tool_name') { [string] $payload.tool_name } else { '' }
    $toolInput = if ($payloadNames -contains 'tool_input') { $payload.tool_input } else { $null }
    $cwd = if ($payloadNames -contains 'cwd') { [string] $payload.cwd } else { '' }

    # Only file-writing tools are in scope. The matcher should already have narrowed this, but a
    # gate that depends on its own configuration being right is a gate with a second failure
    # mode nobody tests.
    if ($toolName -notmatch '^(Edit|Write|MultiEdit|NotebookEdit)$') {
        Write-NoOpinion
    }

    if ([string]::IsNullOrWhiteSpace($cwd)) {
        Write-Deny 'INV-08 gate: the payload carried no working directory, so no repository could be established. Failing closed.'
    }

    $targets = @(Get-TargetPaths -ToolInput $toolInput)
    if ($targets.Count -eq 0) {
        # A writing tool with no path this gate recognises. It cannot show the edit is safe, so
        # it does not pretend to.
        Write-Deny "INV-08 gate: $toolName gave no file path this gate recognises, so it could not be checked against the protected paths. Failing closed."
    }

    $hits = @()
    foreach ($target in $targets) {
        $relative = Get-RelativePosixPath -Path $target -Root $cwd
        if (-not $relative) { continue }

        foreach ($pattern in $protectedPatterns) {
            if ($relative -match $pattern) {
                $hits += $relative
                break
            }
        }
    }

    if ($hits.Count -eq 0) {
        Write-NoOpinion
    }

    $protectedList = ($hits | Select-Object -Unique) -join ', '

    # --- the edit is to a protected path, so the branch has to earn it -------

    # No `2>&1`: redirecting a native command's stderr in PowerShell wraps each line in an
    # ErrorRecord, which has already turned successful git calls into failures in this
    # repository. stderr is left where it is.
    $branch = & git -C $cwd rev-parse --abbrev-ref HEAD
    if ($LASTEXITCODE -ne 0) {
        Write-Deny "INV-08 gate: $protectedList is protected, and the current branch could not be determined (git failed). Failing closed."
    }

    $branch = ([string] ($branch | Out-String)).Trim()

    # A detached HEAD reports the literal string HEAD.
    if ([string]::IsNullOrWhiteSpace($branch) -or $branch -eq 'HEAD') {
        Write-Deny "INV-08 gate: $protectedList is protected, and HEAD is detached, so no intent governs this edit. Failing closed."
    }

    if ($branch -notmatch '^(?:intent|cycle)-(.+)$') {
        Write-Deny "INV-08 gate: $protectedList may only be amended on a charter-review or pack-review intent branch. You are on '$branch'. File a charter intent and work on its branch."
    }

    $intentId = $Matches[1]
    $intentPath = Join-Path $cwd ".sdlc/intents/$intentId.md"

    if (-not (Test-Path -LiteralPath $intentPath)) {
        Write-Deny "INV-08 gate: $protectedList is protected, and no intent file was found at .sdlc/intents/$intentId.md for branch '$branch'. Failing closed."
    }

    $source = $null
    try { $source = Get-FrontmatterSource -Path $intentPath }
    catch {
        Write-Deny "INV-08 gate: $protectedList is protected, and the intent at .sdlc/intents/$intentId.md could not be read. Failing closed."
    }

    if (-not $source) {
        Write-Deny "INV-08 gate: $protectedList is protected, and the intent at .sdlc/intents/$intentId.md declares no source. Failing closed."
    }

    if ($permittedSources -notcontains $source) {
        Write-Deny "INV-08 gate: $protectedList may only be amended by a charter-review or pack-review intent. Intent $intentId declares source '$source'."
    }

    # An approved charter intent, on its own branch. INV-08 is satisfied.
    Write-NoOpinion
}
catch {
    # THE LAST AND MOST IMPORTANT DENY. If this script is broken, the protection it provides is
    # gone - and a broken protection that permits everything is worse than none, because the
    # charter still claims to be protected.
    $message = 'INV-08 gate: the gate itself failed, so it cannot confirm this edit is permitted. Failing closed. ' + $_.Exception.Message
    $out = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $message
        }
    }
    $out | ConvertTo-Json -Depth 5 -Compress
    exit 0
}
