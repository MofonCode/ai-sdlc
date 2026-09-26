#Requires -Version 5.1
<#
.SYNOPSIS
    What the declared stack pack says: its rules, and its gate commands.
    Dot-source this; it defines functions and does nothing on its own.

.DESCRIPTION
    THE PACK IS THE AUTHORITY ON TECH, and the charter delegates to it precisely so that tool names
    stay out of the invariants. That delegation only works if something reads the pack, and two
    halves of it were declared and never read:

      THE RULES were exposed to an interrogation as bare IDS. A role handed `PR-03` and nothing else
      cannot show that the forbidden thing happened -- and it cannot go looking, because the pack
      lives in the plugin rather than in the solution. So one invariant made every pack rule
      blockable in principle and none of them citable in practice.

      THE GATE COMMANDS were declared in pack.yaml and hardcoded in two scripts. A solution on a
      pack that builds with something other than `dotnet` would have had its gates run against a
      toolchain it does not use, and the pack's own declaration would have sat there being correct
      and ignored.

    RESOLVED FROM THE PLUGIN, NOT THE SOLUTION. A pack is a shared versioned artifact and only its
    CHARTER is copied into a solution at init time, so a solution consuming dotnet-library@v1 has no
    local pack.rules.yaml. Reading it from beside this script is what makes a pack rule resolvable at
    all.

    PARSED BY PATTERN, NOT BY A YAML READER. Windows PowerShell 5.1 ships none, and every other
    extractor in this framework works the same way. The parsing prefers false negatives: a rule whose
    body cannot be read arrives with a null title rather than failing the run, because a check that
    reports a healthy pack as broken is a check that gets switched off.
#>

Set-StrictMode -Version Latest

function Get-SdlcPackRoot {
    <#
    .SYNOPSIS
        The directory holding a pack, or $null when that pack does not ship.
    #>
    param([string] $Name)

    if (-not $Name -or $Name -eq 'none') { return $null }

    $candidate = Join-Path (Split-Path -Parent $PSScriptRoot) "packs/$Name"
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    return $null
}

function Get-SdlcPackRules {
    <#
    .SYNOPSIS
        Every rule the pack declares, with the text a finding needs to show what was broken.
    .DESCRIPTION
        A rule has the same shape as an invariant on purpose: an id assigned once, a non-empty
        `forbids`, and evidence. What a finding does with it is NOT to cite the rule directly --
        `cites` accepts an invariant, a failed check, a DEC id or a location pair, and a pack rule is
        none of those. It cites the invariant that delegates to the pack and NAMES the rule, which is
        why the wording matters enough to carry.
    #>
    param([string] $Name)

    $rules = New-Object System.Collections.ArrayList

    $root = Get-SdlcPackRoot -Name $Name
    if (-not $root) { return $rules }

    $path = Join-Path $root 'pack.rules.yaml'
    if (-not (Test-Path -LiteralPath $path)) { return $rules }

    $current = $null
    $folding = $null

    foreach ($line in (Get-Content -LiteralPath $path -Encoding UTF8)) {
        # A new rule. Ends whatever folded block was being collected.
        if ($line -match '^\s*-\s+id:\s*([A-Z]{2}-[0-9]{2})\s*$') {
            if ($null -ne $current) { $null = $rules.Add($current) }
            $current = [ordered]@{ id = $Matches[1]; title = $null; forbids = $null }
            $folding = $null
            continue
        }

        if ($null -eq $current) { continue }

        # `forbids: >` folds the following indented lines into one string. Collected rather than
        # skipped, because that text IS what a finding quotes.
        if ($line -match '^\s*(title|forbids|evidence|unlocks|since):\s*(.*)$') {
            $key = $Matches[1]
            $value = $Matches[2].Trim()

            if ($value -eq '>' -or $value -eq '|') {
                $folding = $key
                $current[$key] = ''
                continue
            }

            $folding = $null
            if ($key -eq 'title' -or $key -eq 'forbids') { $current[$key] = $value }
            continue
        }

        if ($folding) {
            $text = $line.Trim()
            if (-not $text) { continue }
            if ($current[$folding]) { $current[$folding] = $current[$folding] + ' ' + $text }
            else { $current[$folding] = $text }
        }
    }

    if ($null -ne $current) { $null = $rules.Add($current) }

    $results = New-Object System.Collections.ArrayList
    foreach ($rule in $rules) {
        $null = $results.Add([pscustomobject]@{
            id      = $rule['id']
            title   = $rule['title']
            forbids = $rule['forbids']
        })
    }

    return $results
}

function Get-SdlcPackGate {
    <#
    .SYNOPSIS
        One gate command the pack declares -- `build` or `test` -- or $null when it declares none.
    .DESCRIPTION
        The charter excludes tool names, INV-04 delegates them here, and the outcome scripts take
        them as parameters. This is the middle step that was missing: the pack's declaration reaching
        the script that runs the gate.
    #>
    param([string] $Name, [string] $Gate)

    $root = Get-SdlcPackRoot -Name $Name
    if (-not $root) { return $null }

    $path = Join-Path $root 'pack.yaml'
    if (-not (Test-Path -LiteralPath $path)) { return $null }

    $inGates = $false

    foreach ($line in (Get-Content -LiteralPath $path -Encoding UTF8)) {
        if ($line -match '^gates:\s*$') { $inGates = $true; continue }

        # Any other top-level key ends the block. Checked before the value match so a `build:` key
        # elsewhere in the document cannot be read as a gate.
        if ($inGates -and $line -match '^[A-Za-z]') { $inGates = $false }
        if (-not $inGates) { continue }

        if ($line -match ('^\s+' + [regex]::Escape($Gate) + ':\s*(\S.*)$')) {
            return $Matches[1].Trim()
        }
    }

    return $null
}

function Get-SdlcDeclaredPack {
    <#
    .SYNOPSIS
        The pack a charter declares, or $null for a solution that consumes none.
    .DESCRIPTION
        Solution zero consumes no pack -- it builds the framework -- so `none` and a missing charter
        both mean the same thing here and neither is an error.
    #>
    param([string] $CharterPath)

    if (-not $CharterPath -or -not (Test-Path -LiteralPath $CharterPath)) { return $null }

    $lines = @(Get-Content -LiteralPath $CharterPath -Encoding UTF8)
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $null }

    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match '^pack:\s*(\S+)\s*$') {
            $value = $Matches[1].Trim()
            if ($value -eq 'none' -or $value -eq 'null') { return $null }
            return $value
        }
    }

    return $null
}
