# Refuses to write a governed artifact whose top-level shape contradicts its schema.
#
# The trial run produced a verify document with twelve schema errors - `id` where `cycle` belongs,
# `passes_spent` outside `remediation`, an unpermitted `criteria` key - and nothing noticed, because
# schema validation lived only in the framework's own test project, against fixtures. A document that
# reads authoritatively and cannot be parsed by its consumer is worse than one that is obviously
# broken: the state machine it is supposed to encode silently stops being enforced.
#
# WHAT THIS CHECKS: the top-level property names of the artifact, against the `properties` and
# `required` lists of the real schema file. Nothing is duplicated here - the schema is read at run
# time, so there is no second definition to drift.
#
# WHAT IT DOES NOT: values, nested objects, list items, and the conditional rules (a `pass` over an
# open blocking finding, an `escalated` with budget left). Those need a real JSON Schema evaluator,
# which Windows PowerShell 5.1 has no way to be - `Test-Json` does not exist here. They stay in the
# test project, which validates with JsonSchema.Net. This layer is immediate and shallow; that one is
# complete and slower. Between them the realistic defect classes are covered.
#
# An Edit is not checked: it carries only the fragment being replaced, so the resulting document
# cannot be assembled from the call. Writes are where a whole artifact lands.
#
# Windows PowerShell 5.1. No &&, no ??, no ternary; ConvertFrom-Json returns PSCustomObject.

$ErrorActionPreference = 'Stop'

function Deny([string]$Reason) {
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

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $event = $raw | ConvertFrom-Json } catch { exit 0 }

if ("$($event.tool_name)" -ne 'Write') { exit 0 }

$path = $null
$content = $null
if ($event.tool_input) {
    $path = $event.tool_input.file_path
    $content = $event.tool_input.content
}
if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }
if ($null -eq $content) { exit 0 }

$normalized = "$path".Replace('\', '/')

# Which schema governs this path. Anything not listed is not a governed artifact.
$schemaName = $null
if ($normalized -match '/\.sdlc/solution\.md$')                 { $schemaName = 'solution.schema.json' }
if ($normalized -match '/\.sdlc/cycles/[^/]+/intent\.md$')       { $schemaName = 'intent.schema.json' }
if ($normalized -match '/\.sdlc/cycles/[^/]+/spec\.md$')         { $schemaName = 'spec.schema.json' }
if ($normalized -match '/\.sdlc/cycles/[^/]+/plan\.md$')         { $schemaName = 'plan.schema.json' }
if ($normalized -match '/\.sdlc/cycles/[^/]+/verify\.md$')       { $schemaName = 'verify.schema.json' }
if ($normalized -match '/\.sdlc/cycles/[^/]+/findings/[^/]+\.json$') { $schemaName = 'findings.schema.json' }

if ($null -eq $schemaName) { exit 0 }

$schemaPath = Join-Path (Split-Path -Parent $PSScriptRoot) "schemas/$schemaName"
if (-not (Test-Path -LiteralPath $schemaPath)) { exit 0 }

$schema = Get-Content -LiteralPath $schemaPath -Raw -Encoding UTF8 | ConvertFrom-Json

$allowed = @()
if ($schema.properties) { $allowed = $schema.properties.PSObject.Properties.Name }

$required = @()
if ($schema.required) { $required = @($schema.required) }

# Collect the artifact's top-level keys. A findings document is JSON; the rest are markdown carrying
# YAML frontmatter, where a top-level key is a name at column zero inside the block.
$keys = @()

if ($schemaName -eq 'findings.schema.json') {
    try { $doc = $content | ConvertFrom-Json } catch {
        Deny "That findings document is not valid JSON, so nothing downstream can read it: $($_.Exception.Message)"
    }
    $keys = $doc.PSObject.Properties.Name
}
else {
    $text = "$content"
    if (-not $text.StartsWith('---')) {
        Deny (
            "A governed artifact needs YAML frontmatter and this one has none. $schemaName requires " +
            ($required -join ', ') + '.'
        )
    }

    $end = $text.IndexOf("`n---", 3)
    if ($end -lt 0) {
        Deny 'The frontmatter block is not terminated by a line containing only ---.'
    }

    foreach ($line in $text.Substring(3, $end - 3).Split("`n")) {
        $m = [regex]::Match($line, '^(?<k>[A-Za-z_][A-Za-z0-9_]*):')
        if ($m.Success) { $keys += $m.Groups['k'].Value }
    }
}

$unknown = @($keys | Where-Object { $allowed -notcontains $_ })
$missing = @($required | Where-Object { $keys -notcontains $_ })

if ($unknown.Count -gt 0 -or $missing.Count -gt 0) {
    $parts = @()
    if ($missing.Count -gt 0) { $parts += 'missing required: ' + ($missing -join ', ') }
    if ($unknown.Count -gt 0) { $parts += 'not permitted: ' + ($unknown -join ', ') }

    Deny (
        "That artifact's top-level keys do not match $schemaName - " + ($parts -join '; ') + '. ' +
        'The schema is the interface every later phase reads through, so a document using different ' +
        'names is unreadable rather than untidy. Allowed keys are: ' + ($allowed -join ', ') + '.'
    )
}

exit 0
