#Requires -Version 5.1
<#
.SYNOPSIS
    What kind of artifact a path is, and which shipped schema governs it.
    Dot-source this; it defines functions and does nothing on its own.

.DESCRIPTION
    ONE ANSWER TO "WHAT KIND OF ARTIFACT IS THIS". The kind decides which invariants apply,
    which checks run, which row of the gaps table an interrogator reads, and now which schema
    validates it. Two functions answering that question would disagree the first time a new
    kind arrived, and the disagreement would be silent: the gatherer would check a spec while
    the validator checked nothing.

    So it lives here and Get-SdlcFacts.ps1 dot-sources it rather than keeping its own copy,
    which is the same arrangement SdlcGit.ps1 has for the git calls and for the same reason.

    THE SCHEMA IS FOUND BY CONVENTION, NOT BY A MAP. `schemas/<kind>.schema.json` either
    exists or it does not, so there is no table to keep in step with the schemas directory.
    A charter has no schema — its shape is checked by the invariant-form check, not by JSON
    Schema — and that absence is reported rather than treated as a pass.
#>

Set-StrictMode -Version Latest

function Get-SdlcArtifactKind {
    <#
    .SYNOPSIS
        The artifact kind for a repository-relative path.
    .DESCRIPTION
        The vocabulary is the findings schema's artifactKind and nothing else, which is what
        the charter says: this function must never invent a kind that enum does not carry, or
        a finding could name a kind aggregation cannot group by.
    #>
    param([string] $RelativePath)

    $leaf = Split-Path -Leaf $RelativePath
    switch -Regex ($leaf) {
        '^CHARTER\.md$' { return 'charter' }
        '^spec\.md$'    { return 'spec' }
        '^plan\.md$'    { return 'plan' }
        '^verify\.md$'  { return 'verify' }
        '^retro\.md$'   { return 'retro' }
    }

    if ($RelativePath -match '(?i)/intents/') { return 'intent' }
    return 'unknown'
}

function Get-SdlcCycleIdFromPath {
    <#
    .SYNOPSIS
        The cycle an artifact belongs to, read from its path. $null when none is derivable.
    .DESCRIPTION
        Every governed artifact carries its cycle in its path -- an intent IS the id, and
        everything else sits under cycles/<id>/. The exception is a charter, which belongs to the
        charter-review intent proposing to change it and therefore has to be told.

        Here rather than in each caller for the same reason the artifact kind is: the interrogation
        workflow derived this independently in JavaScript, and two answers to "which cycle is this"
        disagree the first time a path convention moves.
    #>
    param([string] $RelativePath)

    $posix = $RelativePath -replace '\\', '/'

    if ($posix -match '(?:^|/)intents/([0-9]{8}-[0-9a-f]{4})\.md$') { return $Matches[1] }
    if ($posix -match '(?:^|/)cycles/([0-9]{8}-[0-9a-f]{4})(?:/|$)') { return $Matches[1] }

    return $null
}

function Get-SdlcSchemaPath {
    <#
    .SYNOPSIS
        The shipped schema for a kind, or $null when none governs it.
    .DESCRIPTION
        Resolved from the PLUGIN rather than the solution, like the pack rules: schemas ship
        with the engine and a solution keeps no copy, so a solution cannot drift from the
        shape its artifacts are validated against.
    #>
    param(
        [string] $Kind,
        [string] $SchemaDir
    )

    if (-not $SchemaDir) {
        $SchemaDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'schemas'
    }

    $candidate = Join-Path $SchemaDir "$Kind.schema.json"
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    return $null
}
