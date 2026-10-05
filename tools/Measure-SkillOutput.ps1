<#
.SYNOPSIS
    Measures how much prose each skill invocation returned, from a Claude Code transcript.

.DESCRIPTION
    The skills cap what a stage may say when it hands back. A cap is a claim about output, so it is
    checkable -- but only against a real run, because no unit test can judge what a model will write.
    This reads a session transcript and counts the assistant's own text per skill invocation: how many
    separate blocks it wrote while working, how many words in total, and how many in the final
    hand-back, which is the one the caps govern.

    Run it after a trial cycle, against the trial repository. Compare the LastWords column to the cap
    the skill states. A stage over its cap is the finding; a stage far under it usually means the
    licence moved somewhere the cap does not reach, which is worth looking at too.

    Mid-stage blocks are counted but uncapped by design -- one line per step is the rule, and a build
    that touches thirty files legitimately says thirty things. The number to watch there is words per
    block, not the block count.

.PARAMETER Repo
    Path to the repository whose session is being measured, e.g. C:\Repos\dice-roll-rev-5. The newest
    transcript for that repository is read. Ignored when -Transcript is given.

.PARAMETER Transcript
    Path to a .jsonl transcript, when a specific session is wanted rather than the newest.

.PARAMETER All
    Also list every text block with its word count, newest invocation last. Use it to find which block
    in an over-cap stage is the one carrying the weight.

.EXAMPLE
    .\tools\Measure-SkillOutput.ps1 -Repo C:\Repos\dice-roll-rev-5

.EXAMPLE
    .\tools\Measure-SkillOutput.ps1 -Repo C:\Repos\dice-roll-rev-5 -All

.NOTES
    Targets Windows PowerShell 5.1, like everything else shipped here.

    Attribution: a block belongs to the most recent /sdlc:<skill> invocation before it. Plain
    conversation after a stage is therefore counted against that stage, so read a tail of follow-up
    chat as what it is. The transcript format is Claude Code's and is not a contract -- if a future
    version moves the fields, this reports zero invocations rather than wrong numbers.
#>
[CmdletBinding(DefaultParameterSetName = 'Repo')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Repo', Position = 0)]
    [string] $Repo,

    [Parameter(Mandatory, ParameterSetName = 'Transcript')]
    [string] $Transcript,

    [switch] $All
)

$ErrorActionPreference = 'Stop'

function Resolve-Transcript {
    param([string] $RepoPath)

    # Claude Code names a project directory after the repository path, with every separator and colon
    # replaced by a dash. C:\Repos\dice-roll-rev-5 becomes C--Repos-dice-roll-rev-5.
    $full = [System.IO.Path]::GetFullPath($RepoPath).TrimEnd('\')
    $slug = $full -replace '[:\\/]', '-'
    $dir = Join-Path $env:USERPROFILE ".claude\projects\$slug"

    if (-not (Test-Path -LiteralPath $dir)) {
        throw "No transcripts for $full. Looked in $dir. A repository with no session has nothing to measure."
    }

    $candidates = Get-ChildItem -LiteralPath $dir -Filter '*.jsonl' | Sort-Object LastWriteTime -Descending

    if ($candidates.Count -eq 0) {
        throw "No .jsonl transcript in $dir."
    }

    # The newest transcript that ran a skill, not simply the newest. A repository accumulates sessions
    # that never invoked one -- a question, a look around -- and picking by date alone measures those
    # and reports nothing, which reads like a broken script rather than the wrong file.
    foreach ($candidate in $candidates) {
        if (Select-String -LiteralPath $candidate.FullName -Pattern '<command-name>/?sdlc:' -SimpleMatch:$false -Quiet) {
            Write-Verbose "Reading $($candidate.FullName)"
            return $candidate.FullName
        }
    }

    throw "No session in $dir invoked a /sdlc: skill. Run a cycle first, or pass -Transcript."
}

if ($PSCmdlet.ParameterSetName -eq 'Repo') {
    $Transcript = Resolve-Transcript -RepoPath $Repo
}

if (-not (Test-Path -LiteralPath $Transcript)) {
    throw "No transcript at $Transcript."
}

$invocations = New-Object System.Collections.ArrayList
$current = $null

foreach ($line in [System.IO.File]::ReadLines($Transcript)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }

    try { $entry = $line | ConvertFrom-Json } catch { continue }
    if ($null -eq $entry.message) { continue }

    $content = $entry.message.content
    if ($null -eq $content) { continue }

    if ($entry.type -eq 'user') {
        # A slash command arrives as a user turn carrying <command-name>. Anything else -- a tool
        # result, a hook message, a typed sentence -- leaves the current stage in place.
        if ($content -is [string]) {
            $text = $content
        }
        else {
            $parts = $content | Where-Object { $_.type -eq 'text' } | ForEach-Object { $_.text }
            $text = $parts -join ' '
        }

        if ($text -match '<command-name>/?(sdlc:[a-z\-]+)</command-name>') {
            $current = [pscustomobject]@{
                Invocation = $Matches[1]
                Blocks     = 0
                Words      = 0
                LastWords  = 0
                Texts      = (New-Object System.Collections.ArrayList)
            }
            [void] $invocations.Add($current)
        }

        continue
    }

    if ($entry.type -ne 'assistant' -or $null -eq $current) { continue }

    foreach ($block in $content) {
        if ($block.type -ne 'text') { continue }
        if ([string]::IsNullOrWhiteSpace($block.text)) { continue }

        $words = ($block.text -split '\s+' | Where-Object { $_ -ne '' }).Count

        $current.Blocks++
        $current.Words += $words
        $current.LastWords = $words
        [void] $current.Texts.Add([pscustomobject]@{ Words = $words; Text = $block.text })
    }
}

if ($invocations.Count -eq 0) {
    Write-Warning "No /sdlc: invocations in $Transcript. Nothing to measure."
    return
}

$invocations |
    Select-Object Invocation, Blocks, Words, LastWords,
        @{ Name = 'WordsPerBlock'; Expression = {
            if ($_.Blocks -eq 0) { 0 } else { [math]::Round($_.Words / $_.Blocks) } } } |
    Format-Table -AutoSize

$total = ($invocations | Measure-Object -Sum Words).Sum
$blocks = ($invocations | Measure-Object -Sum Blocks).Sum
Write-Host ""
Write-Host "  $($invocations.Count) invocations, $blocks blocks, $total words of the assistant's own prose."
Write-Host "  LastWords is the hand-back the caps govern. Blocks and WordsPerBlock are the mid-stage rule."

if (-not $All) { return }

foreach ($invocation in $invocations) {
    Write-Host ""
    Write-Host "=== $($invocation.Invocation) ===" -ForegroundColor Cyan

    foreach ($text in $invocation.Texts) {
        Write-Host ""
        Write-Host "  [$($text.Words)w]" -ForegroundColor DarkGray
        Write-Host $text.Text
    }
}
