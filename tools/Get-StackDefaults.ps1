# Prints the stack defaults so a skill can inject them at load time.
#
# Same reason the constraints are injected: a session working in a governed solution is sandboxed to that
# solution and cannot read anything at the plugin root. A plan choosing project shapes had no access to
# the defaults at all until this existed - nobody noticed, because no plan had yet needed them.
#
# Only the marked block of docs/techstack.md is emitted. The rest of that file is about how this
# repository is built, which is none of a solution's business.
#
# If the markers are missing this throws, which abandons the skill invocation rather than letting a plan
# proceed with no defaults. That is the behaviour we want: choosing project shapes silently, against
# nothing, is worse than stopping.
#
# Windows PowerShell 5.1. -Encoding UTF8 is required on the READ as well as the write: 5.1's
# Get-Content assumes the system codepage for a file with no BOM, so an em dash round-trips as
# mojibake and a rule arrives corrupted. UTF-8 is written explicitly too: the default encoding here mangles the dashes the
# tables use.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root 'docs/techstack.md'

if (-not (Test-Path -LiteralPath $path)) {
    throw "The stack defaults are missing from $path."
}

$text = Get-Content -LiteralPath $path -Raw -Encoding UTF8

$begin = '<!-- stack-defaults:begin -->'
$end = '<!-- stack-defaults:end -->'

$from = $text.IndexOf($begin)
$to = $text.IndexOf($end)

if ($from -lt 0 -or $to -lt 0 -or $to -le $from) {
    throw "Could not find the stack-defaults markers in $path."
}

$from = $from + $begin.Length
$block = $text.Substring($from, $to - $from).Trim()

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::Out.Write($block)
[Console]::Out.Write("`r`n")
