#Requires -Version 5.1
<#
.SYNOPSIS
    Compares the backlog file against the work-item board and reports every disagreement.

.DESCRIPTION
    THIS EXISTS BECAUSE THE PROMISE FAILED. FINDINGS.md and the Azure Boards backlog are two
    representations of one list, and on 2026-09-25 they had drifted: ten items existed only in
    the file, and four work items sat in `New` after the work was finished and released. Nobody
    noticed, because closing a work item is not part of any ceremony - `verify` checks the
    build, `land` checks the PR, `retro` reads the findings, and nothing reads the board.

    INV-03 IS THE POINT. No second representation without, in the same change, the check that
    fails when the two disagree. The file and the board were that second representation for
    days with no check, which makes this script the missing half of a change already made
    rather than a new feature.

    IT CANNOT BE A TEST, AND THAT IS THE COMPROMISE. The comparison needs `az` and a
    credentialled connection to the board; CI has neither, so the parity check runs on a
    developer machine and the pure comparison underneath it is what the test suite covers. A
    check that only runs when someone remembers is weaker than one that gates a merge - so this
    reports loudly and returns a non-zero exit code, and `Compare-SdlcBacklogState` is tested
    directly against fixtures.

    THE FILE IS THE AUTHORITY ON WHAT IS TRUE, THE BOARD ON WHAT IS TRACKED. A story is open in
    the file when it has a `#### ` detail section; closed or dissolved items keep a row in the
    resolved tables but lose the section. The board is open when its state is anything but
    `Closed` or `Removed`. Neither wins automatically: a disagreement is something for a person
    to resolve, which is why this script never writes.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.PARAMETER BacklogFile
    The backlog document. Defaults to FINDINGS.md at the repository root.

.PARAMETER Project
    Azure DevOps project holding the work items. Defaults to `ai-sdlc`.

.PARAMETER AsJson
    Emit the comparison as JSON instead of a table. The exit code is unchanged.

.EXAMPLE
    .\Compare-SdlcBacklog.ps1

.EXAMPLE
    .\Compare-SdlcBacklog.ps1 -AsJson | ConvertFrom-Json
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string] $RepoRoot,

    [Parameter()]
    [string] $BacklogFile,

    [Parameter()]
    [string] $Project = 'ai-sdlc',

    [Parameter()]
    [switch] $AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    The pure comparison. Takes two already-gathered lists and returns the disagreements.

.DESCRIPTION
    Separated from the gathering so it can be tested without a board. Every judgement about
    what counts as a mismatch lives here; the caller only collects facts.

    A story absent from the board is a mismatch even when the file calls it closed - the point
    of the board is that a teammate sees the whole list, including what was already done and
    why. A story absent from the FILE is also a mismatch, and the more alarming direction: it
    means work is being tracked that the authority on truth does not know about.
#>
function Compare-SdlcBacklogState {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        # Story id -> $true when the file presents it as open.
        [Parameter(Mandatory)]
        [hashtable] $FileState,

        # Story id -> @{ Id = <int>; State = '<board state>' }.
        [Parameter(Mandatory)]
        [hashtable] $BoardState
    )

    $closedStates = @('Closed', 'Removed', 'Done')
    $rows = New-Object System.Collections.ArrayList
    $mismatches = New-Object System.Collections.ArrayList

    $ids = @($FileState.Keys) + @($BoardState.Keys) |
        Sort-Object -Unique |
        Sort-Object { if ($_ -match '^US-(\d+)$') { [int] $Matches[1] } else { [int]::MaxValue } }

    foreach ($id in $ids) {
        $inFile = $FileState.ContainsKey($id)
        $inBoard = $BoardState.ContainsKey($id)

        $openInFile = $inFile -and $FileState[$id]
        $boardId = if ($inBoard) { $BoardState[$id].Id } else { $null }
        $boardStateName = if ($inBoard) { $BoardState[$id].State } else { $null }
        $openOnBoard = $inBoard -and ($closedStates -notcontains $boardStateName)

        $problem = $null
        if (-not $inBoard) {
            $problem = 'missing from the board'
        }
        elseif (-not $inFile) {
            $problem = 'on the board but not in the backlog file'
        }
        elseif ($openInFile -and -not $openOnBoard) {
            $problem = "open in the file, $boardStateName on the board"
        }
        elseif ($openOnBoard -and -not $openInFile) {
            $problem = "closed in the file, $boardStateName on the board"
        }

        $row = [ordered] @{
            story         = $id
            'board-id'    = $boardId
            'board-state' = $boardStateName
            'open-in-file' = $openInFile
            'open-on-board' = $openOnBoard
            problem       = $problem
        }
        [void] $rows.Add([pscustomobject] $row)
        if ($problem) { [void] $mismatches.Add("$id - $problem") }
    }

    return @{
        rows       = @($rows)
        mismatches = @($mismatches)
        'open-in-file'  = @($rows | Where-Object { $_.'open-in-file' }).Count
        'open-on-board' = @($rows | Where-Object { $_.'open-on-board' }).Count
    }
}

<#
.SYNOPSIS
    Reads the backlog file and reports which story ids it presents as open.

.DESCRIPTION
    A `#### ` heading is the file's own marker for "this still has work" - the detail sections
    under *The detail*. Closing an item in this file means deleting its section, so the heading
    is the state rather than a duplicate of it.

    Read with an explicit encoding: a BOM-less UTF-8 file read as ANSI has already gutted one
    check in this repository, and every id here is ASCII so the failure would be silent.
#>
function Get-SdlcBacklogFileState {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $state = @{}

    # Every id the document mentions at all, so a story tracked nowhere else still appears.
    foreach ($m in [regex]::Matches($text, '`(US-\d+)`')) {
        $state[$m.Groups[1].Value] = $false
    }

    # A detail heading means open.
    foreach ($m in [regex]::Matches($text, '(?m)^####\s+`(US-\d+)`')) {
        $state[$m.Groups[1].Value] = $true
    }

    return $state
}

<#
.SYNOPSIS
    Reads the user stories from the board.

.DESCRIPTION
    The story id is parsed from the title rather than held in a field, because the title is
    what a person reads on the board and a field beside it would be a third representation of
    the same id.
#>
function Get-SdlcBacklogBoardState {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string] $Project
    )

    $wiql = "SELECT [System.Id], [System.Title], [System.State] FROM WorkItems " +
        "WHERE [System.TeamProject] = '$Project' AND [System.WorkItemType] = 'User Story' " +
        "ORDER BY [System.Id]"

    # Note the absence of `2>&1`: redirecting a native command's stderr in PowerShell wraps
    # each line in an ErrorRecord and trips $ErrorActionPreference, which is how eight git
    # calls in this repository became silent failures.
    $raw = & az boards query --wiql $wiql --project $Project --output json
    if ($LASTEXITCODE -ne 0) {
        throw "az boards query failed with exit code $LASTEXITCODE. Run 'az login' and " +
            "'az devops configure --defaults organization=<url>' if this is a credential problem."
    }

    $items = ($raw | Out-String) | ConvertFrom-Json
    $state = @{}
    foreach ($item in $items) {
        $title = [string] $item.fields.'System.Title'
        if ($title -match '^\s*(US-\d+)') {
            $state[$Matches[1]] = @{
                Id    = [int] $item.id
                State = [string] $item.fields.'System.State'
            }
        }
    }

    return $state
}

# --- gather -----------------------------------------------------------------

if (-not $RepoRoot) {
    $RepoRoot = (& git rev-parse --show-toplevel)
    if ($LASTEXITCODE -ne 0) { throw 'Not inside a git repository, and -RepoRoot was not given.' }
    $RepoRoot = ([string] ($RepoRoot | Out-String)).Trim()
}

if (-not $BacklogFile) { $BacklogFile = Join-Path $RepoRoot 'FINDINGS.md' }
if (-not (Test-Path -LiteralPath $BacklogFile)) {
    throw "Backlog file not found: $BacklogFile"
}

# NO BACKTICK LINE CONTINUATION IN SHIPPED SCRIPTS. It survives reformatting badly: a blank
# line, or a single trailing space, after the backtick silently ends the statement, so the call
# runs with no arguments. It PARSES CLEANLY and fails only when executed, which is how this
# script shipped broken in the same commit that introduced it.
$fileState = Get-SdlcBacklogFileState -Path $BacklogFile
$boardState = Get-SdlcBacklogBoardState -Project $Project
$comparison = Compare-SdlcBacklogState -FileState $fileState -BoardState $boardState

# --- report -----------------------------------------------------------------

if ($AsJson) {
    $comparison | ConvertTo-Json -Depth 6
}
else {
    $columns = @('story', 'board-id', 'board-state', 'open-in-file', 'open-on-board', 'problem')
    $comparison.rows | Format-Table -AutoSize -Property $columns | Out-String | Write-Host

    Write-Host ("open in file: {0}    open on board: {1}    mismatches: {2}" -f
        $comparison.'open-in-file', $comparison.'open-on-board', $comparison.mismatches.Count)

    foreach ($m in $comparison.mismatches) { Write-Host "  MISMATCH  $m" }
}

# The exit code is the whole point: a check that only prints is a check that gets skimmed.
if ($comparison.mismatches.Count -gt 0) { exit 1 }
exit 0
