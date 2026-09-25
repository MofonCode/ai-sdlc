#Requires -Version 5.1
<#
.SYNOPSIS
    Scaffolds a .NET class library solution on the dotnet-library pack.

.DESCRIPTION
    `scaffold` IS THE ONE VERB THAT IS CODE RATHER THAN DATA. Every other verb the engine
    owns takes data from a pack: `plan` reads what a vertical slice means, `interrogate`
    reads checks, `retro` takes nothing because the taxonomy is engine-owned. This one
    creates files and runs `dotnet new`, which is execution, and no manifest format can
    describe it honestly.

    Nothing else gets code until it proves it needs it. Code-per-verb-per-stack is five lists
    beside one source of truth wearing a new costume.

    THIS SCRIPT DOES NOT WRITE THE CHARTER. `init` copies `CHARTER.md` from the pack and
    stamps its `Origin:`, because how the copy was made is what tells every later
    interrogation how much to trust the document — and a scaffolder that also decided
    provenance could quietly claim a scaffolded origin for an inferred one.

    IDEMPOTENT. Re-running skips what exists rather than overwriting it. A scaffolder that
    clobbers is one keystroke away from destroying work, and `init` is allowed to be re-run.

.PARAMETER SolutionName
    The solution and root project name, for example MofonCode.Core.

.PARAMETER Root
    Where to scaffold. Defaults to the current directory.

.PARAMETER WhatIfOnly
    Report what would be created and create nothing.

.EXAMPLE
    .\scaffold.ps1 -SolutionName MofonCode.Core
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $SolutionName,

    [Parameter()]
    [string] $Root,

    [Parameter()]
    [switch] $WhatIfOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install, and an em-dash then best-fits to a plain double quote that
# terminates a JSON string.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

if ($SolutionName -notmatch '^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$') {
    throw "'$SolutionName' is not a .NET-shaped name. Expected something like MofonCode.Core."
}

if (-not $Root) { $Root = (Get-Location).Path }
$Root = (Resolve-Path -LiteralPath $Root).Path

$created = New-Object System.Collections.ArrayList
$skipped = New-Object System.Collections.ArrayList

function Write-File {
    param([string] $RelativePath, [string] $Content)

    $full = Join-Path $Root $RelativePath

    if (Test-Path -LiteralPath $full) {
        $null = $script:skipped.Add($RelativePath)
        return
    }

    if ($WhatIfOnly -or -not $PSCmdlet.ShouldProcess($RelativePath, 'create')) {
        $null = $script:created.Add($RelativePath)
        return
    }

    $dir = Split-Path -Parent $full
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        $null = New-Item -ItemType Directory -Path $dir -Force
    }

    # UTF8 without a BOM for everything except .ps1: the charter commits shipped PowerShell
    # to 5.1, which reads a BOM-less file as ANSI, but nothing else here is run by it.
    $encoding = New-Object System.Text.UTF8Encoding($RelativePath.EndsWith('.ps1'))
    [System.IO.File]::WriteAllText($full, $Content, $encoding)
    $null = $script:created.Add($RelativePath)
}

# --- the facts this pack declares -------------------------------------------
#
# Read from pack.yaml rather than repeated here. Hardcoding the framework would put the
# same fact in two files with nothing binding them, which is the INV-03 duplication this
# pack's own PR-03 exists to catch one layer down.

$packPath = Join-Path $PSScriptRoot 'pack.yaml'
if (-not (Test-Path -LiteralPath $packPath)) {
    throw "Missing $packPath. The scaffolder takes its versions from the pack, not from itself."
}

# A minimal reader, not a YAML parser: 5.1 ships none, and the gatherer's other extractors
# work the same way. It understands exactly what this pack uses — top-level scalars and one
# level of nesting, stored as `parent.child`.
#
# AN EARLIER VERSION MATCHED ONLY COLUMN ZERO. Every version under the indented `packages:`
# block therefore read as empty, the scaffolded Directory.Packages.props pinned nothing, and
# restore failed with NU1604 on a solution this script had just reported as created. Found by
# building the output, which is the only way it could have been found.
$pack = @{}
$parent = ''
foreach ($line in (Get-Content -LiteralPath $packPath -Encoding UTF8)) {
    if ($line -match '^\s*#' -or -not $line.Trim()) { continue }

    if ($line -match '^([a-z][a-z0-9-]*):\s*(.*)$') {
        $key = $Matches[1]
        $value = $Matches[2].Trim()
        $parent = $key
        if ($value) { $pack[$key] = $value }
        continue
    }

    if ($parent -and $line -match '^\s+([a-z][a-z0-9-]*):\s*(\S.*)$') {
        $pack[($parent + '.' + $Matches[1])] = $Matches[2].Trim()
    }
}

foreach ($required in @('target-framework', 'sdk', 'language-version', 'nullable',
                         'packages.test-framework-version', 'packages.coverage-version')) {
    if (-not $pack.ContainsKey($required)) {
        throw "pack.yaml declares no '$required'. The scaffolder cannot invent one."
    }
}

$tfm = $pack['target-framework']
$sdk = $pack['sdk']
$langVersion = $pack['language-version']
$nullable = $pack['nullable']

# --- what a solution on this pack looks like --------------------------------

Write-File "global.json" @"
{
  "sdk": {
    "version": "$sdk",
    "rollForward": "latestFeature"
  }
}
"@

Write-File "Directory.Build.props" @"
<Project>

  <!-- PR-01 and PR-02. These are not preferences: the pack rules make each of them
       citable, so a project that loosens one is a blocking finding rather than a
       style disagreement. -->
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <LangVersion>$langVersion</LangVersion>
    <Nullable>$nullable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- PR-01: warnings are errors, and the exceptions list stays EMPTY. A non-empty
         one turns a binary signal into a judgment call, and it only ever grows. -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors></WarningsNotAsErrors>

    <!-- PR-02: the strictest level the SDK offers. -->
    <AnalysisLevel>latest-all</AnalysisLevel>
    <AnalysisModeSecurity>All</AnalysisModeSecurity>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>

    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

</Project>
"@

Write-File "Directory.Packages.props" @"
<Project>

  <!-- PR-03: central and exact. A version in a project file is a second list; a
       floating version means the build is not reproducible, and NU1011 makes that
       an error rather than a surprise. -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="MSTest" Version="$($pack['packages.test-framework-version'])" />
    <PackageVersion Include="coverlet.collector" Version="$($pack['packages.coverage-version'])" />
  </ItemGroup>

</Project>
"@

Write-File "src/$SolutionName/$SolutionName.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">

  <!-- PR-05: a library does not depend on a host. No ASP.NET Core, no Blazor, no
       hosting package, and no configuration read from a host-owned source. A host
       dependency is invisible until the second consumer arrives. -->
  <PropertyGroup>
    <RootNamespace>$SolutionName</RootNamespace>
  </PropertyGroup>

</Project>
"@

Write-File "tests/$SolutionName.Tests/$SolutionName.Tests.csproj" @"
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>

    <!-- A test project is not a documented public API. Inheriting
         GenerateDocumentationFile from Directory.Build.props makes CS1591 an error on
         every test class and method, so no test can compile at all. -->
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <!-- JUSTIFIED SUPPRESSION (INV-06, PR-04).
       CA1515 wants types in a non-library assembly to be internal. MSTest requires test
       classes and methods to be public (MSTEST0002, MSTEST0003), and the alternative -
       internal plus [DiscoverInternals] - trades CA1515 for CA1812, which fires because
       MSTest instantiates test classes by reflection and the analyzer cannot see it.
       One of the two must be suppressed; this is the narrower of the pair and it is
       scoped to the test project alone.

       This is not a shortcut the scaffolder took to make the build pass. It is an
       unavoidable conflict between two tools, and PR-04 requires the reason to sit
       beside the suppression rather than in a commit message. Remove it if MSTest ever
       supports internal discovery without the CA1812 consequence. -->
  <PropertyGroup>
    <NoWarn>CA1515</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\$SolutionName\$SolutionName.csproj" />
  </ItemGroup>

</Project>
"@

Write-File "$SolutionName.slnx" @"
<Solution>
  <Project Path="src/$SolutionName/$SolutionName.csproj" />
  <Project Path="tests/$SolutionName.Tests/$SolutionName.Tests.csproj" />
</Solution>
"@

Write-File ".gitignore" @"
bin/
obj/
[Tt]est[Rr]esults/

# Generated, regenerable, never authoritative.
facts.md
*.findings.md
"@

# The governed directories. Empty, because an intent is authored rather than scaffolded,
# and a placeholder artifact would be a real artifact as far as interrogation is concerned.
Write-File ".sdlc/.gitkeep" ''
Write-File ".sdlc/intents/.gitkeep" ''
Write-File ".sdlc/cycles/.gitkeep" ''

$result = [ordered]@{
    'solution'        = $SolutionName
    pack              = 'dotnet-library'
    'pack-version'    = 1
    'target-framework' = $tfm
    created           = @($created)
    skipped           = @($skipped)
    # Stated rather than left implicit: the charter is `init`'s to copy, and a scaffolder
    # that claimed to have handled it would leave a solution with no authority at all.
    'charter'         = 'NOT written here - init copies CHARTER.md from the pack and stamps its Origin'
}

ConvertTo-Json -InputObject $result -Depth 6
