#Requires -Version 5.1
<#
.SYNOPSIS
    Behaviour tests for plugins/sdlc-core/hooks/sdlc_gate.ps1.

.DESCRIPTION
    The gate is the one piece of this framework that runs on every single edit in every
    repo, so its failure modes are asymmetric: a false silence reads as approval, and a
    false alarm gets the plugin uninstalled. Both are pinned here.

        powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-SdlcGate.ps1
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Hook = Join-Path (Split-Path -Parent $PSScriptRoot) 'plugins\sdlc-core\hooks\sdlc_gate.ps1'
if (-not (Test-Path -LiteralPath $Hook)) { throw "hook not found: $Hook" }

$script:Failures = @()

$PlanTemplate = @'
---
slug: audit-log
status: {0}
slices:
  - id: storage
    files: ["src/Storage/Writer.cs", "tests/Storage/WriterTests.cs"]
    depends_on: []
---
# body
'@

$CharterTemplate = @'
---
solution: Test
artifact_root: {0}
status: approved
---
'@

function New-TempDir {
    $p = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    $null = New-Item -ItemType Directory -Path $p
    return $p
}

function Invoke-Gate {
    <# Returns @{ Code; Stderr }. Start-Process keeps the exit code and stderr honest;
       an inline 2>&1 in 5.1 wraps native stderr in ErrorRecords and corrupts $?. #>
    param([string] $Cwd, [string] $FilePath, [switch] $Block, [string] $RawInput)

    $inFile  = [System.IO.Path]::GetTempFileName()
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()

    if ($PSBoundParameters.ContainsKey('RawInput')) { $payload = $RawInput }
    else { $payload = (@{ tool_input = @{ file_path = $FilePath } } | ConvertTo-Json -Compress) }
    [System.IO.File]::WriteAllText($inFile, $payload)

    $previous = $env:SDLC_GATE
    if ($Block) { $env:SDLC_GATE = 'block' } else { Remove-Item Env:\SDLC_GATE -ErrorAction SilentlyContinue }

    try {
        $proc = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Hook) `
            -WorkingDirectory $Cwd `
            -RedirectStandardInput $inFile -RedirectStandardOutput $outFile -RedirectStandardError $errFile `
            -NoNewWindow -Wait -PassThru
        $stderr = (Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue)
        if ($null -eq $stderr) { $stderr = '' }
        return @{ Code = $proc.ExitCode; Stderr = $stderr.Trim() }
    } finally {
        if ($null -ne $previous) { $env:SDLC_GATE = $previous }
        else { Remove-Item Env:\SDLC_GATE -ErrorAction SilentlyContinue }
        Remove-Item $inFile, $outFile, $errFile -Force -ErrorAction SilentlyContinue
    }
}

function Assert-That {
    param([string] $Name, $Got, $Want)
    if ($Got -eq $Want) {
        Write-Host ("  ok      {0}" -f $Name)
    } else {
        Write-Host ("  FAILED  {0}: expected {1}, got {2}" -f $Name, $Want, $Got)
        $script:Failures += $Name
    }
}

function New-Scenario {
    param([string] $RootDir, [string] $Status = 'approved', [string] $CharterRoot)
    $tmp  = New-TempDir
    $root = Join-Path $tmp $RootDir
    $null = New-Item -ItemType Directory -Path (Join-Path $root 'audit-log') -Force
    Set-Content -LiteralPath (Join-Path $root 'audit-log\plan.md') `
        -Value ($PlanTemplate -f $Status) -Encoding utf8
    if ($PSBoundParameters.ContainsKey('CharterRoot')) {
        Set-Content -LiteralPath (Join-Path $root 'CHARTER.md') `
            -Value ($CharterTemplate -f $CharterRoot) -Encoding utf8
    }
    $null = New-Item -ItemType Directory -Path (Join-Path $tmp 'src\Storage') -Force
    return $tmp
}

Write-Host "`nsdlc_gate.ps1`n"

# --- silence where silence is correct -------------------------------------------------
$empty = New-TempDir
Assert-That 'no artifact root -> silent' (Invoke-Gate -Cwd $empty -FilePath 'src/Thing.cs').Code 0

$t = New-Scenario -RootDir '.sdlc'
Assert-That 'claimed by approved plan -> silent' (Invoke-Gate -Cwd $t -FilePath 'src/Storage/Writer.cs').Code 0
Assert-That 'markdown never gated'              (Invoke-Gate -Cwd $t -FilePath 'README.md').Code 0
Assert-That 'dotfile config never gated'        (Invoke-Gate -Cwd $t -FilePath '.gitignore').Code 0
Assert-That 'the chain itself never gated'      (Invoke-Gate -Cwd $t -FilePath '.sdlc/audit-log/notes.cs').Code 0

# --- warnings where the discipline has slipped ----------------------------------------
$r = Invoke-Gate -Cwd $t -FilePath 'src/Storage/Rogue.cs'
Assert-That 'unplanned scope -> warns' ($r.Code -eq 0 -and $r.Stderr -match 'unplanned scope') $true

$draft = New-Scenario -RootDir '.sdlc' -Status 'draft'
$r = Invoke-Gate -Cwd $draft -FilePath 'src/Storage/Writer.cs'
Assert-That 'draft plan -> warns' ($r.Code -eq 0 -and $r.Stderr -match 'none approved') $true

$noplan = New-TempDir
$null = New-Item -ItemType Directory -Path (Join-Path $noplan '.sdlc')
$r = Invoke-Gate -Cwd $noplan -FilePath 'src/Thing.cs'
Assert-That 'no plan at all -> warns' ($r.Code -eq 0 -and $r.Stderr -match 'No plan\.md exists') $true

# --- the half-finished refactor these tests exist to prevent --------------------------
$custom = New-Scenario -RootDir 'docs\sdlc' -CharterRoot 'docs/sdlc/'
Assert-That 'non-default root, claimed -> silent' (Invoke-Gate -Cwd $custom -FilePath 'src/Storage/Writer.cs').Code 0
$r = Invoke-Gate -Cwd $custom -FilePath 'src/Storage/Rogue.cs'
Assert-That 'non-default root, unplanned -> warns' ($r.Code -eq 0 -and $r.Stderr -match 'unplanned scope') $true

# --- block mode and robustness --------------------------------------------------------
Assert-That 'block mode refuses the edit'            (Invoke-Gate -Cwd $t -FilePath 'src/Storage/Rogue.cs' -Block).Code 2
Assert-That 'block mode still allows a claimed file' (Invoke-Gate -Cwd $t -FilePath 'src/Storage/Writer.cs' -Block).Code 0
Assert-That 'malformed payload never breaks the session' (Invoke-Gate -Cwd $t -RawInput 'not json').Code 0
Assert-That 'payload with no file_path'                  (Invoke-Gate -Cwd $t -RawInput '{}').Code 0
Assert-That 'empty payload'                              (Invoke-Gate -Cwd $t -RawInput '').Code 0

Write-Host ''
if ($script:Failures.Count -gt 0) {
    Write-Host ("{0} failure(s): {1}" -f $script:Failures.Count, ($script:Failures -join ', '))
    exit 1
}
Write-Host 'Test-SdlcGate: all passed'
exit 0
