# Prints the global constraints so a skill can inject them at load time.
#
# A session working in a governed solution is sandboxed to that solution, so the model cannot read
# anything at the plugin root - not this file's neighbour, not the plugin manifest. A load-time
# injection runs with the harness's privileges instead, and the text is in the skill body before the
# model is asked to do anything with it. If this script fails the skill invocation is abandoned
# rather than proceeding with no rules, which is the behaviour we want.
#
# Windows PowerShell 5.1. -Encoding UTF8 is required on the READ as well as the write: 5.1's
# Get-Content assumes the system codepage for a file with no BOM, so an em dash round-trips as
# mojibake and a rule arrives corrupted. UTF-8 is written explicitly too: the default encoding here mangles the dashes
# and arrows the constraints use, and a corrupted rule is worse than a missing one.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root 'constraints.md'

if (-not (Test-Path -LiteralPath $path)) {
    throw "The global constraints are missing from $path."
}

$version = 'unknown'
$manifest = Join-Path $root '.claude-plugin/plugin.json'
if (Test-Path -LiteralPath $manifest) {
    $version = (Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json).version
}

$text = Get-Content -LiteralPath $path -Raw -Encoding UTF8

# The wall-clock time is injected for the same reason the rules are: a model cannot read the clock,
# and left to itself it writes a plausible-looking midnight. A timestamp nobody can trust is worse
# than none, because rounds are ordered by it.
$now = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::Out.Write("Framework version: $version`r`n")
[Console]::Out.Write("Now (UTC): $now`r`n`r`n")
[Console]::Out.Write($text)
