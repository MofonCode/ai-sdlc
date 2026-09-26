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

    THE SHELL ROUTE, AND WHY IT IS THE SAME GATE. This gate once watched four file-writing tools,
    so `sed -i` on the charter through a shell walked straight past it - the rule was enforced
    against the tools that announce a path and not against the one that hides it in a string. Two
    layers had to change together: the hook matcher, which decides whether this script runs at
    all, and the path extraction, which decides whether it has an opinion. Either alone is worse
    than neither: a matcher that admits shell commands while the extraction only reads `file_path`
    turns every shell command into "a recognised tool that yielded no recognised path", which this
    gate denies. That is a total block on the shell.

    THE LICENCE ANSWER LIVES IN ONE PLACE, LOADED INSIDE THE TRY/CATCH. `SdlcProtectedPaths.ps1`
    holds what is protected, what licenses a write to each path, and whether a command string
    writes one - shared with the detection half in `Get-SdlcFacts.ps1`, because a prevention and a
    detection that disagree are worse than either alone. It is dot-sourced INSIDE the try below
    and not above it: a load failure above the try is an error this script never sees, which Claude
    Code reads as no opinion, which is a fail-open in the one component that must fail closed.

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

# The tools that can change a file. The first four announce a path in a field; the last two carry
# a command string that may name one. Both groups are checked, by different routes, against the
# same licence answer.
$fileWritingTools = @('Edit', 'Write', 'MultiEdit', 'NotebookEdit')

# `Bash` is a name an official plugin already matches. `PowerShell` is included because it can
# write files and is this stack's preferred shell - whether the harness delivers PreToolUse for it
# is unverified, and a gate that is ready for a payload it may never receive costs nothing.
$shellTools = @('Bash', 'PowerShell')

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

function Get-CommandString {
    param($ToolInput)

    if ($null -eq $ToolInput) { return $null }
    $names = @($ToolInput.PSObject.Properties.Name)
    if ($names -notcontains 'command') { return $null }

    $value = $ToolInput.command
    if ($null -eq $value) { return $null }
    return [string] $value
}

try {
    # INSIDE the try, deliberately. See the header: a load failure above it is an error this
    # script never sees, which Claude Code reads as no opinion.
    . (Join-Path $PSScriptRoot 'SdlcProtectedPaths.ps1')

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

    # Only tools that can change a file are in scope. The matcher should already have narrowed
    # this, but a gate that depends on its own configuration being right is a gate with a second
    # failure mode nobody tests.
    $isFileTool = $fileWritingTools -contains $toolName
    $isShellTool = $shellTools -contains $toolName
    if (-not $isFileTool -and -not $isShellTool) {
        Write-NoOpinion
    }

    if ([string]::IsNullOrWhiteSpace($cwd)) {
        Write-Deny 'INV-08 gate: the payload carried no working directory, so no repository could be established. Failing closed.'
    }

    # --- which protected paths does this call touch, and how ------------------
    #
    # The two routes differ only here. A file tool names its target in a field, so a protected hit
    # is a write by definition. A shell tool carries a string, so a hit has to be classified: a
    # read is permitted, a write or a destruction has to earn a licence, and a command that cannot
    # be read statically is denied.
    #
    # THE EXISTENCE TEST RUNS HERE, BEFORE THE BRANCH IS EVER RESOLVED, and the ordering is the
    # whole point rather than an optimisation. Creating a protected path needs no licence - you
    # cannot loosen a rule that has not been written - and `init` writes a first charter into a
    # repository with NO COMMIT, where `git rev-parse` fails and this gate would deny on "the
    # current branch could not be determined". Asking about the branch first denies the one case
    # that must always work. Found by testing it.

    # Each entry: path, and the construct that makes it a write, for the refusal to quote.
    $writes = @()

    if ($isFileTool) {
        $targets = @(Get-TargetPaths -ToolInput $toolInput)
        if ($targets.Count -eq 0) {
            # A writing tool with no path this gate recognises. It cannot show the edit is safe,
            # so it does not pretend to.
            Write-Deny "INV-08 gate: $toolName gave no file path this gate recognises, so it could not be checked against the protected paths. Failing closed."
        }

        foreach ($target in $targets) {
            $relative = Get-SdlcRelativePosixPath -Path $target -Root $cwd
            if (-not $relative) { continue }
            if (-not (Get-SdlcProtectedPathRule -RelativePath $relative)) { continue }
            if (-not (Test-SdlcProtectedPathExists -Root $cwd -RelativePath $relative)) { continue }
            $writes += [pscustomobject]@{ path = $relative; how = "an edit by $toolName" }
        }
    }
    else {
        $command = Get-CommandString -ToolInput $toolInput
        if ([string]::IsNullOrWhiteSpace($command)) {
            Write-Deny "INV-08 gate: $toolName carried no command string, so it could not be checked against the protected paths. Failing closed."
        }

        $named = @(Get-SdlcProtectedPathsInText -Text $command -Root $cwd)

        # A command naming no protected path is not this gate's business, whatever else it does.
        if ($named.Count -eq 0) {
            Write-NoOpinion
        }

        foreach ($path in $named) {
            if (-not (Test-SdlcProtectedPathExists -Root $cwd -RelativePath $path)) { continue }

            $shape = Get-SdlcCommandShape -Command $command -RelativePath $path -Root $cwd

            # UNEVALUABLE IS TREATED AS A WRITE, NOT AS AN IMMEDIATE REFUSAL, and the distinction
            # matters on exactly one branch: the licensed one. Fail-closed means denying when
            # permission cannot be ESTABLISHED - but on a charter-review branch it has been, for
            # this path, so refusing an unreadable command there is friction with no safety behind
            # it. The author could make the same edit with Edit and the gate would permit it.
            #
            # So it joins the write list and the licence decides, which keeps the deny for every
            # branch that has no licence and drops it for the one that does.
            if ($shape.verdict -eq 'unevaluable') {
                $writes += [pscustomobject]@{
                    path = $path
                    how  = "a command the gate could not evaluate - $($shape.detail)"
                }
                continue
            }

            # A read is not a change, and a parsed command with no write shape is a determination
            # rather than a guess. Both pass; the second is the accepted leak the intent records.
            if ($shape.verdict -eq 'read' -or $shape.verdict -eq 'none') { continue }

            $writes += [pscustomobject]@{ path = $path; how = $shape.detail }
        }
    }

    if ($writes.Count -eq 0) {
        Write-NoOpinion
    }

    # --- the call writes a protected path, so the branch has to earn it -------

    # No `2>&1`: redirecting a native command's stderr in PowerShell wraps each line in an
    # ErrorRecord, which has already turned successful git calls into failures in this
    # repository. stderr is left where it is.
    $protectedList = (@($writes | ForEach-Object { $_.path }) | Select-Object -Unique) -join ', '

    $branch = & git -C $cwd rev-parse --abbrev-ref HEAD
    if ($LASTEXITCODE -ne 0) {
        Write-Deny "INV-08 gate: $protectedList is protected, and the current branch could not be determined (git failed). Failing closed."
    }

    $branch = ([string] ($branch | Out-String)).Trim()
    if ([string]::IsNullOrWhiteSpace($branch)) {
        Write-Deny "INV-08 gate: $protectedList is protected, and the current branch could not be determined. Failing closed."
    }

    # PATH BY PATH, not once for the branch. The licence is bound to the path: a pack-review intent
    # does not license the solution charter and a charter-review intent does not license the rules
    # every future solution inherits.
    foreach ($write in $writes) {
        $decision = Test-SdlcWriteLicensed -Root $cwd -Branch $branch -RelativePath $write.path
        if (-not $decision.allowed) {
            Write-Deny ("INV-08 gate: $($decision.reason) This call would change it by " +
                "$($write.how). Use Edit or Write, which name their target explicitly.")
        }
    }

    # Every protected path this call writes is licensed by the branch's intent. INV-08 is satisfied.
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
