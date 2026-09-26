#Requires -Version 5.1
<#
.SYNOPSIS
    Validates one artifact against its shipped schema. Exits non-zero when it does not pass.

.DESCRIPTION
    THE SCHEMAS WERE EXERCISED ONLY BY TEST FIXTURES. Six of them ship, the test suite proves
    each accepts what it should and rejects what it should — and nothing looked at a real
    artifact at the moment one was written. So a spec with no charter version, a plan with a
    malformed cycle id, or a findings document carrying a blocking finding with no citation
    could be written by any stage and nothing would say so.

    That last one matters most. The findings schema is where the anti-inflation rules live:
    blocking requires a citation, `unclassified` can never block, a mechanical finding never
    sits at `open`, a location pair is only for a contradiction, and a dismissal states its
    reason. The interrogation workflow enforces three of those in its own code. Two were
    enforced nowhere until this ran.

    IT IS A WRAPPER, NOT A VALIDATOR. Sdlc.Validate.cs does the validating, because the schema
    files are the authority for these rules and a PowerShell reimplementation of them would be
    a second representation with nothing binding the two. This script's whole job is deciding
    WHICH schema applies and reporting the answer in one shape.

    A KIND WITH NO SCHEMA IS REPORTED, NOT PASSED. The charter has no JSON Schema — its shape
    is checked by the invariant-form check instead — and code and pack files have none either.
    Reporting `skipped` with the reason keeps a caller from reading silence as approval.

.PARAMETER Path
    The artifact to validate. A .md file is validated on its frontmatter; anything else is
    read as JSON.

.PARAMETER Schema
    The bare schema name to use — `findings`, `intent`, `plan` — overriding kind detection.
    Required for a findings document, which is not an artifact kind and has no path convention
    the kind detector is allowed to know about.

.PARAMETER RepoRoot
    Repository root. Defaults to the enclosing git repository.

.EXAMPLE
    .\Test-SdlcArtifact.ps1 -Path .sdlc/cycles/20260925-a1b2/plan.md

.EXAMPLE
    .\Test-SdlcArtifact.ps1 -Path findings.json -Schema findings
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [Parameter()]
    [string] $Schema,

    [Parameter()]
    [string] $RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 encodes stdout using the console's codepage, which is ANSI on a
# default Windows install. An em-dash then leaves this script as a CP1252 best-fit
# substitution, and U+201D best-fits to a plain double quote — which does not merely look
# wrong, it TERMINATES the JSON string and the caller receives a structurally invalid
# document. Schema complaints quote the property they are about, so this is the normal case.
$OutputEncoding = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $OutputEncoding

. (Join-Path $PSScriptRoot 'SdlcArtifact.ps1')

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

function Write-Result {
    param(
        [bool] $Ok,
        [bool] $Skipped,
        [string] $SchemaPath,
        [string[]] $Errors,
        [int] $Exit
    )

    $payload = [ordered]@{
        ok      = $Ok
        skipped = $Skipped
        path    = ($script:relative -replace '\\', '/')
        schema  = $(if ($SchemaPath) { (Split-Path -Leaf $SchemaPath) } else { $null })
        errors  = @($Errors)
    }

    ConvertTo-Json -InputObject $payload -Depth 6
    exit $Exit
}

# Checked before resolving. Resolve-Path throws under `Stop`, and a validator that dies with a
# PowerShell error instead of a verdict is one a caller cannot read — so the absent-input case
# has to come out in the same shape as every other answer.
$script:relative = $Path
if (-not (Test-Path -LiteralPath $Path)) {
    Write-Result -Ok $false -Skipped $false -SchemaPath $null -Exit 2 -Errors @(
        "no artifact at '$Path', so nothing was validated")
}

$artifactPath = (Resolve-Path -LiteralPath $Path).Path

if (-not $RepoRoot) { $RepoRoot = Resolve-RepoRoot -Start $artifactPath }
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$relative = $artifactPath.Substring($RepoRoot.Length).TrimStart('\', '/')

# --- which schema ------------------------------------------------------------

$kind = $null
if ($Schema) {
    $kind = $Schema
}
else {
    $kind = Get-SdlcArtifactKind -RelativePath ($relative -replace '\\', '/')
}

$schemaPath = Get-SdlcSchemaPath -Kind $kind

if (-not $schemaPath) {
    # Named, not swallowed. A caller that cannot tell "nothing governs this" from "this
    # passed" will eventually report the first as the second.
    Write-Result -Ok $true -Skipped $true -SchemaPath $null -Exit 0 -Errors @(
        "no schema ships for kind '$kind', so nothing here was validated")
}

# --- run the validator -------------------------------------------------------

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    # Fails closed. A validator that cannot run has not approved anything.
    Write-Result -Ok $false -Skipped $false -SchemaPath $schemaPath -Exit 2 -Errors @(
        'dotnet is not on PATH, so the schema could not be checked at all')
}

$validator = Join-Path $PSScriptRoot 'Sdlc.Validate.cs'
if (-not (Test-Path -LiteralPath $validator)) {
    Write-Result -Ok $false -Skipped $false -SchemaPath $schemaPath -Exit 2 -Errors @(
        "the validator is missing at $validator")
}

$arguments = @('run', '--verbosity', 'quiet', $validator, '--',
    '--schema', $schemaPath, '--input', $artifactPath)

if ($artifactPath -match '\.md$') { $arguments += '--frontmatter' }

# stderr goes to a file rather than into the success stream: `2>&1` on a native command wraps
# each line in an ErrorRecord and sets $? to false even at exit code 0, which under `Stop`
# terminates on success. See SdlcGit.ps1 for the full account.
$errFile = [System.IO.Path]::GetTempFileName()
try {
    $stdout = @(& $dotnet.Source $arguments 2>$errFile)
    $code = $LASTEXITCODE
    $stderr = ''
    if (Test-Path -LiteralPath $errFile) {
        $stderr = [string] (Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue)
    }
}
finally {
    Remove-Item -LiteralPath $errFile -Force -ErrorAction SilentlyContinue
}

# A first run compiles the validator, and a build line ahead of the JSON would make the
# document unparseable for every caller. Start reading at the first line that opens an object.
$start = -1
for ($i = 0; $i -lt $stdout.Count; $i++) {
    if ($stdout[$i].TrimStart().StartsWith('{')) { $start = $i; break }
}

if ($start -lt 0) {
    $detail = (@($stdout) + @($stderr)) -join ' '
    Write-Result -Ok $false -Skipped $false -SchemaPath $schemaPath -Exit 2 -Errors @(
        "the validator produced no result: $detail")
}

$json = ($stdout[$start..($stdout.Count - 1)]) -join "`n"
$parsed = ConvertFrom-Json $json

$errors = @()
if ($parsed.PSObject.Properties.Name -contains 'errors') {
    $errors = @($parsed.errors | Where-Object { $_ })
}

Write-Result -Ok ([bool] $parsed.ok) -Skipped $false -SchemaPath $schemaPath -Exit $code -Errors $errors
