#Requires -Version 5.1
<#
.SYNOPSIS
    Behaviour tests for plugins/sdlc-core/scripts/Assert-SliceManifest.ps1.

.DESCRIPTION
    This check is the only thing standing between an overlapping manifest and two agents
    editing the same file at the same time, so a false PASS is the expensive direction.
    Every rejection case is pinned here, not just the happy path.

        powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-AssertSliceManifest.ps1
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Script = Join-Path (Split-Path -Parent $PSScriptRoot) 'plugins\sdlc-core\scripts\Assert-SliceManifest.ps1'
if (-not (Test-Path -LiteralPath $Script)) { throw "script not found: $Script" }

$script:Failures = @()
$Work = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
$null = New-Item -ItemType Directory -Path $Work

function Test-Plan {
    <# Start-Process keeps exit code and stderr honest; an inline 2>&1 in 5.1 wraps native
       stderr in ErrorRecords and reports failure even on a clean exit. #>
    param([string] $Name, [string] $Body, [int] $Expect, [string] $Expect_Output)

    $plan    = Join-Path $Work 'plan.md'
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    Set-Content -LiteralPath $plan -Value $Body -Encoding utf8

    try {
        $proc = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $Script, '-Plan', $plan) `
            -RedirectStandardOutput $outFile -RedirectStandardError $errFile `
            -NoNewWindow -Wait -PassThru

        $stdout = [string] (Get-Content -LiteralPath $outFile -Raw -ErrorAction SilentlyContinue)
        $stderr = [string] (Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue)
        $both   = "$stdout`n$stderr"

        $ok = ($proc.ExitCode -eq $Expect)
        if ($ok -and $PSBoundParameters.ContainsKey('Expect_Output')) {
            $ok = ($both -match [regex]::Escape($Expect_Output))
        }

        if ($ok) {
            Write-Host ("  ok      {0}" -f $Name)
        } else {
            Write-Host ("  FAILED  {0}: exit {1} (wanted {2})" -f $Name, $proc.ExitCode, $Expect)
            if ($both.Trim()) { Write-Host ("            {0}" -f $both.Trim()) }
            $script:Failures += $Name
        }
    } finally {
        Remove-Item $outFile, $errFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`nAssert-SliceManifest.ps1`n"

Test-Plan 'disjoint plan passes' @'
---
slug: audit-log
status: approved
slices:
  - id: storage
    files: ["src/Storage/Writer.cs", "tests/Storage/WriterTests.cs"]
    depends_on: []
  - id: api
    files: ["src/Api/Endpoint.cs"]
    depends_on: [storage]
---
'@ 0 'manifests disjoint'

Test-Plan 'parallel waves are reported' @'
---
slug: x
status: approved
slices:
  - id: a
    files: ["src/A.cs"]
    depends_on: []
  - id: b
    files: ["src/B.cs"]
    depends_on: []
  - id: c
    files: ["src/C.cs"]
    depends_on: [a, b]
---
'@ 0 'wave 1: a, b'

Test-Plan 'a sequential chain says so' @'
---
slug: x
status: approved
slices:
  - id: a
    files: ["src/A.cs"]
    depends_on: []
  - id: b
    files: ["src/B.cs"]
    depends_on: [a]
  - id: c
    files: ["src/C.cs"]
    depends_on: [b]
---
'@ 0 'this plan is sequential'

Test-Plan 'overlapping manifests are rejected' @'
---
slug: audit-log
status: approved
slices:
  - id: storage
    files: ["src/Storage/Writer.cs", "src/Storage/IStore.cs"]
    depends_on: []
  - id: api
    files: ["src/Api/Endpoint.cs", "src/Storage/IStore.cs"]
    depends_on: []
---
'@ 1 'OVERLAP storage <-> api: src/Storage/IStore.cs'

Test-Plan 'an empty manifest is rejected' @'
---
slug: x
status: approved
slices:
  - id: a
    files: ["src/A.cs"]
    depends_on: []
  - id: b
    files: []
    depends_on: []
---
'@ 1 'empty manifest'

Test-Plan 'a dependency cycle is rejected' @'
---
slug: x
status: approved
slices:
  - id: a
    files: ["src/A.cs"]
    depends_on: [b]
  - id: b
    files: ["src/B.cs"]
    depends_on: [a]
---
'@ 1 'dependency cycle'

Test-Plan 'an unknown depends_on is rejected' @'
---
slug: x
status: approved
slices:
  - id: a
    files: ["src/A.cs"]
    depends_on: [ghost]
---
'@ 1 'is not a slice in this plan'

Test-Plan 'a plan with no frontmatter is rejected' "# just a heading" 1 'no frontmatter'

Test-Plan 'frontmatter with no slices is rejected' @'
---
slug: x
status: approved
---
'@ 1 'no slices found'

Test-Plan 'paths are normalised before comparison' @'
---
slug: x
status: approved
slices:
  - id: a
    files: ["./src/A.cs"]
    depends_on: []
  - id: b
    files: ["src/A.cs"]
    depends_on: []
---
'@ 1 'OVERLAP'

Remove-Item $Work -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($script:Failures.Count -gt 0) {
    Write-Host ("{0} failure(s): {1}" -f $script:Failures.Count, ($script:Failures -join ', '))
    exit 1
}
Write-Host 'Test-AssertSliceManifest: all passed'
exit 0
