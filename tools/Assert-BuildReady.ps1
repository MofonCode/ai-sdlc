# Whether a cycle is in a state where build may begin.
#
# Build is the first phase that writes source, and every later claim is measured from the commit it
# pins as its base. Three things have to be true at that moment or the measurement means nothing:
#
#   on the cycle's own branch  - the gates key on cycle/<id>. Off it they stay silent, and a build that
#                                appears governed is not.
#   the plan approved          - a person decided what this change may touch. Build implements that
#                                decision; it does not get to start before it exists.
#   no uncommitted source      - anything already dirty would be attributed to this cycle. The carve-out
#                                is .sdlc/, which build commits itself as its first act.
#
# This refuses; it never fixes. Each failure is a decision for a person.
#
# Windows PowerShell 5.1.
#
#   .\Assert-BuildReady.ps1 -CycleId 20260928_a43a [-RepositoryRoot <path>]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CycleId,
    [string]$RepositoryRoot = '.'
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'SdlcPlan.ps1')

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$problems = New-Object System.Collections.Generic.List[string]

# ---------------------------------------------------------------- a repository, on the right branch
$branch = Get-SdlcBranchState -WorkingDirectory $root

if (-not $branch.IsRepository) {
    Write-Output "NOT READY - $root is not a git repository."
    Write-Output ''
    Write-Output '  Build records the diff that is the only evidence verify derives its claims from,'
    Write-Output '  and every gate keys on a cycle/<id> branch that cannot otherwise exist.'
    exit 1
}

if ($null -eq $branch.CycleId) {
    $problems.Add(
        "Not on a cycle branch. Build runs on cycle/$CycleId - off it the write gates stay silent and " +
        'nothing is enforced. Run: git checkout -b cycle/' + $CycleId)
}
elseif ($branch.CycleId -ne $CycleId) {
    $problems.Add(
        "On cycle/$($branch.CycleId) but asked to build $CycleId. One branch per cycle; building one " +
        "cycle's plan on another's branch mixes two changes into one diff.")
}

# ---------------------------------------------------------------- the plan, and its approval
$planPath = Join-Path $root ".sdlc/cycles/$CycleId/plan.md"

if (-not (Test-Path -LiteralPath $planPath)) {
    $problems.Add("No plan at .sdlc/cycles/$CycleId/plan.md. Run /sdlc:plan $CycleId first.")
}
else {
    $plan = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8

    $approvedBy = [regex]::Match($plan, '(?m)^approved_by:\s*(?<v>\S.*)$')
    $approvedAt = [regex]::Match($plan, '(?m)^approved_at:\s*(?<v>\S.*)$')

    if (-not ($approvedBy.Success -and $approvedAt.Success)) {
        $problems.Add(
            "The plan for $CycleId is not approved. A person adds approved_by and approved_at to its " +
            'frontmatter. This is the gate the whole process rests on.')
    }

    $declared = Get-SdlcDeclaredFile -PlanPath $planPath

    if ($declared.Count -eq 0) {
        $problems.Add(
            "The plan for $CycleId declares no files. Its ## Files table is missing or malformed, so " +
            'there is nothing to measure the change against.')
    }
}

# ---------------------------------------------------------------- nothing dirty outside .sdlc/
Push-Location $root
$previous = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $status = (& git status --porcelain 2>$null)
    $failed = $LASTEXITCODE -ne 0
}
finally {
    $ErrorActionPreference = $previous
    Pop-Location
}

if ($failed) {
    Write-Output "NOT READY - could not read git status in $root."
    exit 1
}

$dirty = New-Object System.Collections.Generic.List[string]
foreach ($line in @($status)) {
    $text = "$line"
    if ($text.Length -lt 4) { continue }

    # Porcelain v1: two status characters, a space, then the path. A rename carries "old -> new"; the
    # destination is what now exists on disk.
    $path = $text.Substring(3).Trim().Replace('\', '/')
    if ($path -match ' -> ') { $path = ($path -split ' -> ')[-1] }
    $path = $path.Trim('"')

    if ($path -like '.sdlc/*') { continue }
    if ($path -like '.claude/*') { continue }
    if (-not $dirty.Contains($path)) { $dirty.Add($path) }
}

if ($dirty.Count -gt 0) {
    $problems.Add(
        "$($dirty.Count) uncommitted change(s) outside .sdlc/: " + ($dirty -join ', ') + '. Commit or ' +
        'stash them first - anything already dirty is attributed to this cycle and shows up as scope ' +
        'the plan never declared.')
}

# ---------------------------------------------------------------- the verdict
if ($problems.Count -gt 0) {
    Write-Output "NOT READY - $($problems.Count) thing(s) to resolve before building $CycleId."
    Write-Output ''
    foreach ($p in $problems) { Write-Output "  - $p" }
    Write-Output ''
    exit 1
}

Write-Output "READY - $CycleId may build."
Write-Output "  branch: cycle/$CycleId"
Write-Output '  plan: approved'
Write-Output '  working tree: clean outside .sdlc/'
exit 0
