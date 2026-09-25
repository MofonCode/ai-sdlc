#Requires -Version 5.1
<#
.SYNOPSIS
    Finds an existing charter intent targeting a rule, so a cycle links instead of filing.

.DESCRIPTION
    WITHOUT THIS, A BATCH OF EIGHT FILES EIGHT IDENTICAL REQUESTS. Cycles are independent
    by design: separate worktrees, no shared state, so one halting affects nothing else. But
    independence cuts both ways — if cycle three halts on a bad invariant and cycle six hits
    the same one, nothing stops them both filing a charter intent about it.

    So charter intents deduplicate on the rule they TARGET, and there are three answers:

      pending    link to it, block, wait. The question is already asked.
      abandoned  block, and SURFACE ITS REASON. This is the useful one.
      none       file one.

    THE MIDDLE ROW IS WHY THIS SCRIPT IS WORTH HAVING. A cycle blocked on a question
    already answered is a different signal from one blocked on an open question: it says the
    WORK needs rethinking rather than the charter, and it shows the reason already given
    instead of making someone re-derive it. A genuinely different case may still file a new
    charter intent, but it has to state what differs.

    REPEATED REJECTIONS ARE A CLARIFICATION SIGNAL, NOT A DELETION SIGNAL. Three abandoned
    charter intents against one invariant means agents keep bumping into it and a person
    keeps saying no, which points at the rule being UNCLEAR rather than wrong. The count is
    reported for that reason, and it is one of the charter's amendment signals.

.PARAMETER Target
    The rule a cycle wants changed: an invariant id, a DEC id, or a pack rule id.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Find-SdlcCharterIntent.ps1 -Target INV-06
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Target,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that
# terminates a JSON string. An abandoned intent's `reason:` is prose and often has one.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

function Resolve-RepoRoot {
    param([string] $Start)

    $dir = (Resolve-Path -LiteralPath $Start).Path
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) { return $dir }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }

    throw "No git repository found at or above '$Start'."
}

function Get-Frontmatter {
    param([string] $Path)

    $map = @{}
    if (-not (Test-Path -LiteralPath $Path)) { return $map }

    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $map }

    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match '^([A-Za-z][A-Za-z0-9-]*):\s*(.*)$') {
            $map[$Matches[1]] = $Matches[2].Trim()
        }
    }

    return $map
}

# `targets` is a list. YAML admits several spellings and the intent skill is prose, so all
# the plausible ones are read rather than one being declared correct and the rest silently
# ignored: `[INV-06, INV-07]`, `INV-06`, or `INV-06, INV-07`.
function Get-Targets {
    param($Frontmatter)

    if (-not $Frontmatter.ContainsKey('targets')) { return @() }

    $raw = [string] $Frontmatter['targets']
    $raw = $raw.Trim().TrimStart('[').TrimEnd(']')
    $out = @()
    foreach ($part in ($raw -split ',')) {
        $t = $part.Trim().Trim("'", '"')
        if ($t) { $out += $t }
    }

    return @($out)
}

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start (Get-Location).Path }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$intentDir = Join-Path $RepoRoot '.sdlc/intents'
$abandonedDir = Join-Path $intentDir 'abandoned'

$pending = New-Object System.Collections.ArrayList
$abandoned = New-Object System.Collections.ArrayList

function Read-Dir {
    param([string] $Dir, [bool] $IsAbandoned)

    if (-not (Test-Path -LiteralPath $Dir)) { return }

    foreach ($file in @(Get-ChildItem -LiteralPath $Dir -Filter '*.md' -File)) {
        $fm = Get-Frontmatter -Path $file.FullName
        if (-not $fm.ContainsKey('source')) { continue }
        if ($fm['source'] -ne 'charter-review' -and $fm['source'] -ne 'pack-review') { continue }

        $targets = Get-Targets -Frontmatter $fm
        if ($targets -notcontains $Target) { continue }

        $title = ''
        foreach ($line in (Get-Content -LiteralPath $file.FullName -Encoding UTF8)) {
            if ($line -match '^#\s+(.+)$') { $title = $Matches[1].Trim(); break }
        }

        $entry = [pscustomobject]@{
            'cycle-id' = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
            source     = $fm['source']
            title      = $title
            targets    = @($targets)
            reason     = $(if ($fm.ContainsKey('reason')) { $fm['reason'] } else { $null })
        }

        if ($IsAbandoned) { $null = $script:abandoned.Add($entry) }
        else { $null = $script:pending.Add($entry) }
    }
}

Read-Dir -Dir $intentDir -IsAbandoned $false
Read-Dir -Dir $abandonedDir -IsAbandoned $true

# Order matters: a pending request is the live one to join. An abandoned request only
# becomes the answer once there is no pending one, because a rejected question that has
# since been re-asked is being reconsidered.
if ($pending.Count -gt 0) {
    $verdict = 'link-and-block'
    $advice = "Link to $($pending[0].'cycle-id') and block. The question is already asked and waiting."
}
elseif ($abandoned.Count -gt 0) {
    $verdict = 'blocked-already-answered'
    $reasons = @($abandoned | ForEach-Object { $_.reason } | Where-Object { $_ })
    $because = 'no reason was recorded'
    if ($reasons.Count -gt 0) { $because = $reasons -join ' / ' }
    $advice = "This was already refused: $because. The WORK needs rethinking, not the charter. " +
              'Filing again requires stating what differs from the refused request.'
}
else {
    $verdict = 'file-one'
    $advice = "Nothing targets $Target. File a charter intent recording it in ``targets``."
}

$result = [ordered]@{
    target                = $Target
    verdict               = $verdict
    advice                = $advice
    'pending-count'       = $pending.Count
    'abandoned-count'     = $abandoned.Count
    # Three refusals against one rule says the rule is UNCLEAR rather than wrong, which is a
    # clarification signal and not a deletion one.
    'clarification-signal' = ($abandoned.Count -ge 3)
    pending               = @($pending)
    abandoned             = @($abandoned)
}

ConvertTo-Json -InputObject $result -Depth 6
