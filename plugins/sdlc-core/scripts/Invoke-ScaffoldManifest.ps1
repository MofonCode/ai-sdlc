#Requires -Version 5.1
<#
.SYNOPSIS
    Run a stack pack's scaffold manifest against a target directory.

.DESCRIPTION
    Shipped engine tooling, and deliberately ignorant of what it is scaffolding: every
    directory, command, argument, asset, pattern and token arrives from the manifest the pack
    ships, as data. Charter invariants I1 and I5 are the reason, and a check over shipped
    engine executables holds them - a term from any pack in this file is a bug, not a
    convenience.

    What it guarantees, and what it refuses:

      - It refuses to run at all unless the repository's charter says `status: approved`.
        The approval gate is a check here rather than prose in a skill, because prose a model
        interprets is not a gate.
      - It never writes outside -Target, never overwrites a file that already exists, and
        starts every declared command inside -Target. That bounds where a generator starts;
        it does not bound what the generator can reach once started, and nothing here
        pretends otherwise - a manifest is read by reviewers the way a script is.
      - It is safely re-runnable. A second run over its own output changes no file and exits
        0, which is what makes filling the gaps in a half-scaffolded repository safe.
      - A `rewrite` or a `delete` touches only paths this execution created. An over-broad
        glob in a mature repository therefore has nothing to hit.

    Output contract: the JSON report goes to stdout and nothing else does. Progress and every
    error message go to stderr, so a caller can pipe stdout straight into a parser. The exit
    code is 0 only when the report's `outcome` is `completed`; every halt, refusal and
    rejection still emits the report.

.PARAMETER Manifest
    Path to the pack's scaffold manifest.

.PARAMETER Kind
    Which of the manifest's kinds to execute. Required - it is not declared mandatory only so
    that omitting it can answer with the kinds the manifest declares instead of prompting.

.PARAMETER Target
    The repository directory to scaffold. Everything this script writes lands inside it, and
    its charter is the one that has to be approved.

.PARAMETER Solution
    The confirmed solution identifier. Wins over derivation from the directory name and over
    recovery from a solution file already on disk.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Invoke-ScaffoldManifest.ps1 `
        -Manifest plugins/<pack>/skills/<pack>/scaffold.json -Kind <kind> -Target C:\repos\thing
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Manifest,

    [string] $Kind,

    [Parameter(Mandatory = $true)]
    [string] $Target,

    [string] $Solution
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$ReportVersion    = 1
$ManifestVersions = @(1)
$DefaultRoot      = '.sdlc'
$SweepSkipDirs    = @('.git')
$ActionKinds      = @('mkdir', 'run', 'copy', 'append', 'rewrite', 'delete')
$PreflightFields  = @('expect_exit', 'expect_match', 'remediation')
$CommandTimeoutMs = 600000

$script:Records      = New-Object System.Collections.Generic.List[object]
$script:Created      = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
$script:ChainBefore  = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
$script:PreExisting  = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
$script:ArtifactRoot = $null
$script:SolutionName = $null
$script:TargetFull   = $null

# --- reporting -------------------------------------------------------------------------

function Write-Note {
    # Everything a human reads goes to stderr. stdout carries the report and nothing else.
    param([string] $Message)
    [Console]::Error.WriteLine($Message)
}

function Add-Record {
    # RecordKind, not Kind: PowerShell looks a variable up the call stack, so a parameter
    # named after the script's own -Kind would be the one a called function sees.
    param(
        [string] $RecordKind,
        [string] $Outcome,
        [string] $Path,
        [string] $Command,
        [string] $Reason
    )

    $record = [ordered]@{ kind = $RecordKind; outcome = $Outcome }
    if (-not [string]::IsNullOrEmpty($Path))    { $record['path']    = $Path }
    if (-not [string]::IsNullOrEmpty($Command)) { $record['command'] = $Command }
    if (-not [string]::IsNullOrEmpty($Reason))  { $record['reason']  = $Reason }
    $script:Records.Add([pscustomobject] $record)
}

function Complete-Run {
    param(
        [string] $Outcome,
        [int] $Code
    )

    $report = [ordered]@{
        report_version = $ReportVersion
        artifact_root  = $script:ArtifactRoot
        target         = $script:TargetFull
        kind           = $script:Kind
        solution       = $script:SolutionName
        outcome        = $Outcome
        actions        = @($script:Records.ToArray())
    }

    [Console]::Out.WriteLine((([pscustomobject] $report) | ConvertTo-Json -Depth 8))
    exit $Code
}

function Stop-Run {
    <#
      Halt: say why on stderr, still emit the report, never exit 0.

      R56 binds "completes OR halts", so a caller that parses the report on failure - which
      is what `init` does when it relays a failure to the owner - always has one to parse.
    #>
    param(
        [string] $Message,
        [string] $RecordKind,
        [string] $Path,
        [string] $Command
    )

    Write-Note $Message
    if (-not [string]::IsNullOrEmpty($RecordKind)) {
        Add-Record -RecordKind $RecordKind -Outcome 'failed' -Path $Path -Command $Command -Reason $Message
    }
    Complete-Run -Outcome 'halted' -Code 1
}

# --- reading the repository's own artifacts ---------------------------------------------

function Get-Frontmatter {
    param([string] $Path)
    try { $text = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop }
    catch { return '' }
    if ($null -eq $text) { return '' }
    if ($text -match '(?s)\A---\r?\n(.*?)\r?\n---(\r?\n|\z)') { return $Matches[1] }
    return ''
}

function Get-ArtifactRoot {
    <#
      Where this repo keeps its chain, or $null if it does not use the framework.

      Lifted from the gate hook, and for the same reason: the charter declares its own root
      in `artifact_root`, so a repo that keeps its backlog at top level is found rather than
      silently ignored. Assuming the default here would mean scaffolding a repo whose
      charter this script never read.
    #>
    param([string] $Cwd)

    $candidates = New-Object System.Collections.Generic.List[string]
    $candidates.Add((Join-Path (Join-Path $Cwd $DefaultRoot) 'CHARTER.md'))

    foreach ($depth in @('*', '*/*')) {
        $glob = Join-Path $Cwd (Join-Path $depth 'CHARTER.md')
        $hits = @(Get-ChildItem -Path $glob -File -ErrorAction SilentlyContinue | Sort-Object FullName)
        foreach ($hit in $hits) {
            $parts = $hit.FullName.Substring($Cwd.Length).Replace('\', '/').Split('/')
            $noisy = $false
            foreach ($part in $parts) { if ($SweepSkipDirs -contains $part) { $noisy = $true } }
            if (-not $noisy) { $candidates.Add($hit.FullName) }
        }
    }

    foreach ($charter in $candidates) {
        if (-not (Test-Path -LiteralPath $charter -PathType Leaf)) { continue }
        $fm = Get-Frontmatter -Path $charter
        if ($fm -match '(?m)^artifact_root:\s*(.+?)\s*$') {
            $declared = $Matches[1].Trim().Trim("'").Trim('"')
            $resolved = Join-Path $Cwd $declared
            if (Test-Path -LiteralPath $resolved -PathType Container) {
                return (Resolve-Path -LiteralPath $resolved).Path
            }
        }
        return (Split-Path -Parent $charter)
    }

    $default = Join-Path $Cwd $DefaultRoot
    if (Test-Path -LiteralPath $default -PathType Container) {
        return (Resolve-Path -LiteralPath $default).Path
    }
    return $null
}

function Get-Field {
    <#
      Read one property, or $null when it is absent.

      Strict mode throws on `$obj.PSObject.Properties.Name` when the object has no
      properties at all, which `{}` produces - so the obvious membership test is exactly
      the one that crashes on the payload shape you did not anticipate. Every optional
      manifest field reaches this path.
    #>
    param($Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-FieldName {
    <#
      Every property name on a parsed object, safely. Same trap as Get-Field: reading
      `.Properties.Name` on an object with none throws under strict mode.
    #>
    param($Object)
    $names = New-Object System.Collections.Generic.List[string]
    if ($null -eq $Object) { return $names }
    foreach ($prop in $Object.PSObject.Properties) { $names.Add($prop.Name) }
    return $names
}

# --- containment -------------------------------------------------------------------------

function Resolve-TargetPath {
    <#
      Full path for a manifest-declared relative path, or $null when it escapes the target.

      Combine rather than Join-Path on purpose: Combine returns the second argument verbatim
      when it is rooted, so an absolute path in a manifest arrives here intact and is caught
      by the containment test below rather than being mangled into something that passes it.
    #>
    param([string] $Relative)

    if ([string]::IsNullOrWhiteSpace($Relative)) { return $null }

    try {
        $full = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($script:TargetFull, $Relative))
    } catch {
        return $null
    }

    if ($full.Equals($script:TargetFull, [StringComparison]::OrdinalIgnoreCase)) { return $full }

    $prefix = $script:TargetFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
              [System.IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    return $full
}

function Test-ContainedGlob {
    <#
      Whether a glob stays inside the target, walked segment by segment.

      GetFullPath cannot be used here: wildcards are illegal path characters to it, so the
      containment test a glob needs is the one the file-system API refuses to perform.
    #>
    param([string] $Glob)

    if ([string]::IsNullOrWhiteSpace($Glob)) { return $false }

    $normalised = $Glob.Replace('\', '/')
    if ($normalised -match '^([A-Za-z]:|/)') { return $false }   # rooted, or a share

    $depth = 0
    foreach ($segment in ($normalised -split '/')) {
        if ($segment -eq '' -or $segment -eq '.') { continue }
        if ($segment -eq '..') {
            $depth--
            if ($depth -lt 0) { return $false }
            continue
        }
        $depth++
    }
    return $true
}

function ConvertTo-GlobRegex {
    <#
      A glob over forward-slashed relative paths as a regex. `**/` spans directories, `*`
      and `?` do not.
    #>
    param([string] $Glob)

    $normalised = $Glob.Replace('\', '/')
    $pattern = '^'
    $i = 0
    while ($i -lt $normalised.Length) {
        $ch = $normalised[$i]
        if ($ch -eq '*') {
            if (($i + 1) -lt $normalised.Length -and $normalised[$i + 1] -eq '*') {
                if (($i + 2) -lt $normalised.Length -and $normalised[$i + 2] -eq '/') {
                    $pattern += '(?:[^/]+/)*'
                    $i += 3
                    continue
                }
                $pattern += '.*'
                $i += 2
                continue
            }
            $pattern += '[^/]*'
            $i++
            continue
        }
        if ($ch -eq '?') { $pattern += '[^/]'; $i++; continue }
        $pattern += [regex]::Escape([string] $ch)
        $i++
    }
    return $pattern + '$'
}

function Get-RelativePath {
    param([string] $Full)
    $prefix = $script:TargetFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
              [System.IO.Path]::DirectorySeparatorChar
    if ($Full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $Full.Substring($prefix.Length).Replace('\', '/')
    }
    return $Full.Replace('\', '/')
}

function Get-ContentHash {
    <#
      A file's content as a hash, or $null if it cannot be read.

      $null rather than a throw or a sentinel: an unreadable file is one this script cannot
      call unchanged, and every caller wants that to mean "sweep it" rather than "trust it".
    #>
    param([string] $Path)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try { return [System.BitConverter]::ToString($sha.ComputeHash($stream)) }
        finally { $stream.Dispose() }
    } catch {
        return $null
    } finally {
        $sha.Dispose()
    }
}

function Test-UnderArtifactRoot {
    <#
      Whether a full path sits inside the chain. One definition, read by the pre-run snapshot
      and by the sweep, so the two cannot disagree about where the chain is.

      Against the root R38 and R40 already resolved, never a literal '.sdlc': a charter
      declares its own, so a hardcoded name would relocate the bug rather than fix it.
    #>
    param([string] $Full)

    if ([string]::IsNullOrEmpty($script:ArtifactRoot)) { return $false }
    $prefix = $script:ArtifactRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
              [System.IO.Path]::DirectorySeparatorChar
    return $Full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-Sweepable {
    <#
      Whether the token sweep is answerable for a file.

      Everything under the target is swept - a token surviving from an earlier run is the
      complaint this whole feature started from. The one exception is the artifact chain,
      whose own documents quote a pack's token while specifying it: a spec that says what the
      token is contains the token, and sweeping those halted on the engine's own paperwork.

      That exception is a question about content, not about mechanism. A chain file is swept
      unless it was there before this execution began AND is unchanged since.

      Four earlier versions asked the other question - how was this file written - and every
      one of them was short a mechanism:

        - two drew the boundary by location, and caught the engine's paperwork or stopped
          catching a pack's mistake;
        - one named `create`, and missed a pack appending a section to the charter;
        - one named `create` and `append`, and missed a `run` action's generator modifying a
          chain file that was already on disk - bytes no registration path records.

      The list of ways bytes reach a file is open, so a rule that enumerates it is always one
      entry behind. "Is it different from before" is closed: a write path added to this script
      next year is covered without anyone remembering to register it.

      The cost is real and deliberate: the pre-run snapshot hashes chain files where it used to
      record paths alone, and the sweep hashes them again. That is the price of a rule that
      cannot be short a mechanism, and it is bounded to the chain.
    #>
    param([string] $Full)

    if (-not (Test-UnderArtifactRoot -Full $Full)) { return $true }

    # Absent from the snapshot means it cannot be unchanged: a chain file this run brought
    # into existence is this run's to answer for.
    if (-not $script:ChainBefore.ContainsKey($Full)) { return $true }

    $now = Get-ContentHash -Path $Full
    if ($null -eq $now) { return $true }
    return ($now -ne $script:ChainBefore[$Full])
}

function Get-TargetFile {
    <#
      Every file under the target, full paths. Used three times: to learn what was already
      there before this run, to notice what a generator created, and to sweep for tokens.
    #>
    $found = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $script:TargetFull -PathType Container)) { return $found }

    $pending = New-Object System.Collections.Generic.Stack[string]
    $pending.Push($script:TargetFull)
    while ($pending.Count -gt 0) {
        $dir = $pending.Pop()
        foreach ($sub in [System.IO.Directory]::GetDirectories($dir)) {
            if ($SweepSkipDirs -contains [System.IO.Path]::GetFileName($sub)) { continue }
            $pending.Push($sub)
        }
        foreach ($file in [System.IO.Directory]::GetFiles($dir)) { $found.Add($file) }
    }
    return $found
}

# --- running a declared command -----------------------------------------------------------

function ConvertTo-ArgumentString {
    <#
      Arguments as one command line, quoted the way CommandLineToArgvW un-quotes it.

      Windows PowerShell has no ArgumentList on ProcessStartInfo - that arrived after this
      runtime - so the quoting is ours to get right, and a pack argument containing a space
      or a quote is not exotic.
    #>
    param([string[]] $Arguments)

    if ($null -eq $Arguments -or $Arguments.Count -eq 0) { return '' }

    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($argument in $Arguments) {
        $value = [string] $argument
        if ($value.Length -gt 0 -and $value -notmatch '[\s"]') {
            $parts.Add($value)
            continue
        }
        $escaped = [regex]::Replace($value, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        $parts.Add('"' + $escaped + '"')
    }
    return ($parts -join ' ')
}

function Invoke-ExternalProcess {
    <#
      Run a pack-declared command and hand back exit code, stdout and stderr.

      Both streams are drained asynchronously: reading one to the end before the other
      deadlocks the moment a generator fills the pipe buffer on the stream nobody is
      reading, which is exactly the shape of bug that only appears on a slow machine.
    #>
    param(
        [string] $FilePath,
        [string[]] $Arguments,
        [string] $WorkingDirectory
    )

    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName               = $FilePath
    $info.Arguments              = ConvertTo-ArgumentString -Arguments $Arguments
    $info.WorkingDirectory       = $WorkingDirectory
    $info.UseShellExecute        = $false
    $info.CreateNoWindow         = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError  = $true

    try {
        $process = [System.Diagnostics.Process]::Start($info)
    } catch {
        return [pscustomobject]@{ ExitCode = -1; StdOut = ''; StdErr = $_.Exception.Message }
    }

    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($CommandTimeoutMs)) {
            try { $process.Kill() } catch { }
            return [pscustomobject]@{
                ExitCode = -1
                StdOut   = ''
                StdErr   = "command did not exit within $([int]($CommandTimeoutMs / 1000))s"
            }
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StdOut   = $stdout.Result
            StdErr   = $stderr.Result
        }
    } finally {
        $process.Dispose()
    }
}

# --- the solution identifier ---------------------------------------------------------------

function Test-Identifier {
    param([string] $Candidate)
    if ([string]::IsNullOrEmpty($Candidate)) { return $false }
    return ($Candidate -cmatch '^[A-Za-z][A-Za-z0-9]*$')
}

function ConvertTo-Identifier {
    <#
      A candidate identifier from a directory name: split on the separators people put in
      repository names, drop everything else, capitalise each piece, join.

      Deliberately no repair. A name nobody chose ends up in every assembly and every
      namespace in the repository, and is unreachable afterwards by a tool that never
      renames anything - so an underivable name is a question for the owner, not a default.
    #>
    param([string] $Name)

    $pieces = $Name -split '[-_. ]'
    $builder = New-Object System.Text.StringBuilder
    foreach ($piece in $pieces) {
        $clean = ($piece -replace '[^A-Za-z0-9]', '')
        if ($clean.Length -eq 0) { continue }
        $null = $builder.Append($clean.Substring(0, 1).ToUpperInvariant())
        if ($clean.Length -gt 1) { $null = $builder.Append($clean.Substring(1)) }
    }
    return $builder.ToString()
}

# --- the actions ------------------------------------------------------------------------------

function Resolve-AssetPath {
    param([string] $PackRoot, [string] $Asset)

    if ([string]::IsNullOrWhiteSpace($Asset)) { return $null }
    try {
        $full = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PackRoot, $Asset))
    } catch {
        return $null
    }
    $prefix = $PackRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
              [System.IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    return $full
}

function Expand-Token {
    <#
      Substitute the tokens an action names. `solution` is the only logical token there is;
      what it is spelled as is the pack's business, and arrives as `solution_token`.
    #>
    param([string] $Text, $Tokens, [string] $SolutionToken, [string] $Label)

    if ($null -eq $Tokens) { return $Text }
    foreach ($token in @($Tokens)) {
        $name = [string] $token
        if ($name -ne 'solution') {
            Stop-Run -Message "$Label declares an unknown token '$name'; the only token this engine substitutes is 'solution'." -RecordKind 'manifest'
        }
        if ([string]::IsNullOrEmpty($SolutionToken)) {
            Stop-Run -Message "$Label asks for the solution token, but the manifest declares no solution_token." -RecordKind 'manifest'
        }
        $Text = $Text.Replace($SolutionToken, $script:SolutionName)
    }
    return $Text
}

function Register-CreatedFile {
    <#
      A path this execution brought into existence. This is R41's set, and the only thing
      `rewrite` and `delete` may reach.

      Nothing else belongs in it, and an `append` destination especially does not. `append`
      writes to a file that was already on disk - that is the case `copy` cannot serve - so
      its existing content is somebody else's. Registering appended paths here would let an
      over-broad `delete` glob remove a mature repository's ignore file because the pack added
      two lines to it, which is the harm R41 exists to prevent. The token sweep asks a
      different question and answers it from content (Test-Sweepable), so it needs nothing
      from this set.
    #>
    param([string] $FullPath)
    $null = $script:Created.Add((Get-RelativePath -Full $FullPath))
}


function Get-GlobMatch {
    <#
      Paths this execution created that match a glob.

      Matching against the created set rather than against the disk is the whole of R41: a
      rewrite or a delete in a mature repository can only reach what this run just made, so
      an over-broad glob has nothing to find.
    #>
    param([string] $Glob)

    $regex = ConvertTo-GlobRegex -Glob $Glob
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($relative in $script:Created) {
        if ([regex]::IsMatch($relative, $regex, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
            $hits.Add($relative)
        }
    }
    $sorted = New-Object System.Collections.Generic.List[string]
    foreach ($hit in ($hits | Sort-Object)) { $sorted.Add($hit) }
    return $sorted
}

function Read-TextFile {
    <#
      Read a file as UTF-8, honouring a byte-order mark if there is one.

      Not `Get-Content -Raw`: on this runtime that reads a file with no byte-order mark in
      the machine's ANSI code page, so an asset with an em dash in it comes back as mojibake
      and is written back out double-encoded. That corrupts the emitted file, and then it
      breaks the re-run: the mangled line no longer matches the asset, so `append` appends it
      again on every single execution. Both halves were found by running the real pack.
    #>
    param([string] $Path)
    return [System.IO.File]::ReadAllText($Path)
}

# --- main ---------------------------------------------------------------------------------------

# --- the target ----------------------------------------------------------------------------------
try {
    $script:TargetFull = [System.IO.Path]::GetFullPath($Target)
} catch {
    Write-Note "target is not a usable path: $Target"
    exit 1
}

if (-not (Test-Path -LiteralPath $script:TargetFull -PathType Container)) {
    # The target is the boundary, not something inside it, so creating it writes nothing the
    # containment rules would refuse. A caller pointing at a file gets the error below.
    if (Test-Path -LiteralPath $script:TargetFull) {
        Write-Note "target is a file, not a directory: $script:TargetFull"
        exit 1
    }
    $null = New-Item -ItemType Directory -Path $script:TargetFull -Force
}
$script:TargetFull = (Resolve-Path -LiteralPath $script:TargetFull).Path

# --- the approval gate -----------------------------------------------------------------------------
# Before the manifest is even read: a scaffold with no approved charter behind it is the state
# the skill's own gate exists to prevent, and a gate that lives only in prose a model reads is
# not a gate. This script is a consumer of an artifact, so it reads `status:` from the file.
$script:ArtifactRoot = Get-ArtifactRoot -Cwd $script:TargetFull
if ($null -eq $script:ArtifactRoot) {
    Stop-Run -Message "no artifact chain under $script:TargetFull - there is no charter to approve this scaffold." -RecordKind 'charter'
}

$charterPath = Join-Path $script:ArtifactRoot 'CHARTER.md'
if (-not (Test-Path -LiteralPath $charterPath -PathType Leaf)) {
    Stop-Run -Message "no charter at $charterPath - nothing has approved this scaffold." -RecordKind 'charter' -Path $charterPath
}

$charterFrontmatter = Get-Frontmatter -Path $charterPath
$charterStatus = '(none)'
if ($charterFrontmatter -match '(?m)^status:\s*(.+?)\s*$') {
    $charterStatus = $Matches[1].Trim().Trim("'").Trim('"')
}
if ($charterStatus -ne 'approved') {
    Stop-Run -Message "charter $charterPath is '$charterStatus', not 'approved' - nothing was written." -RecordKind 'charter' -Path $charterPath
}

# --- the manifest ------------------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $Manifest -PathType Leaf)) {
    Stop-Run -Message "manifest not found: $Manifest" -RecordKind 'manifest' -Path $Manifest
}
$manifestFull = (Resolve-Path -LiteralPath $Manifest).Path
$packRoot = Split-Path -Parent $manifestFull

$manifestText = Get-Content -LiteralPath $manifestFull -Raw
try {
    $document = $manifestText | ConvertFrom-Json
} catch {
    Stop-Run -Message "manifest is not valid JSON: $manifestFull - $($_.Exception.Message)" -RecordKind 'manifest' -Path $manifestFull
}

$manifestVersion = Get-Field -Object $document -Name 'version'
$knownVersion = $false
foreach ($known in $ManifestVersions) {
    # Compared as text: a manifest is hostile input, and casting an arbitrary value to an
    # integer to find out whether it is one throws rather than answering.
    if ([string] $manifestVersion -eq [string] $known) { $knownVersion = $true }
}
if (-not $knownVersion) {
    Stop-Run -Message "manifest version '$manifestVersion' is not one this executor knows ($($ManifestVersions -join ', ')); nothing was written." -RecordKind 'manifest' -Path $manifestFull
}

$solutionToken    = [string] (Get-Field -Object $document -Name 'solution_token')
$solutionFileGlob = [string] (Get-Field -Object $document -Name 'solution_file_glob')

$kinds = Get-Field -Object $document -Name 'kinds'
# @() because a function's return value unrolls: one declared kind would otherwise arrive
# here as a bare string, and .Count on it is a different question than it looks.
$declared = @(Get-FieldName -Object $kinds)
$declaredList = '(none)'
if ($declared.Count -gt 0) { $declaredList = ($declared -join ', ') }

if ([string]::IsNullOrWhiteSpace($Kind)) {
    Stop-Run -Message "no kind supplied. This manifest declares: $declaredList" -RecordKind 'manifest' -Path $manifestFull
}

$selected = Get-Field -Object $kinds -Name $Kind
if ($null -eq $selected) {
    Stop-Run -Message "manifest declares no kind '$Kind'. It declares: $declaredList" -RecordKind 'manifest' -Path $manifestFull
}

$preflights = @()
$rawPreflights = Get-Field -Object $selected -Name 'preflight'
if ($null -ne $rawPreflights) { $preflights = @($rawPreflights) }

$actions = @()
$rawActions = Get-Field -Object $selected -Name 'actions'
if ($null -ne $rawActions) { $actions = @($rawActions) }

# --- the manifest's shape ---------------------------------------------------------------------------
# Validated in full before anything runs, so a manifest error cannot leave a half-scaffolded
# repository behind it.
for ($index = 0; $index -lt $actions.Count; $index++) {
    $action = $actions[$index]
    $label = "action $($index + 1) of kind '$Kind'"
    $actionKind = [string] (Get-Field -Object $action -Name 'kind')

    if ($actionKind -eq 'preflight') {
        Stop-Run -Message "$label is a preflight check declared inside actions; preflight checks belong in the kind's own preflight array." -RecordKind 'manifest' -Path $manifestFull
    }
    foreach ($field in $PreflightFields) {
        if ($null -ne (Get-Field -Object $action -Name $field)) {
            Stop-Run -Message "$label carries the preflight field '$field'; preflight checks belong in the kind's own preflight array." -RecordKind 'manifest' -Path $manifestFull
        }
    }
    if (-not ($ActionKinds -contains $actionKind)) {
        Stop-Run -Message "$label has kind '$actionKind', which is not one of: $($ActionKinds -join ', ')." -RecordKind 'manifest' -Path $manifestFull
    }
}

# --- the solution identifier -------------------------------------------------------------------------
if ($PSBoundParameters.ContainsKey('Solution')) {
    if (-not (Test-Identifier -Candidate $Solution)) {
        Stop-Run -Message "supplied solution identifier '$Solution' is not usable: it must be one or more of A-Za-z0-9 and must not begin with a digit." -RecordKind 'solution'
    }
    $script:SolutionName = $Solution
    Write-Note "solution identifier: $script:SolutionName (supplied)"
} else {
    $recovered = $null
    if (-not [string]::IsNullOrWhiteSpace($solutionFileGlob)) {
        $existing = @(Get-ChildItem -LiteralPath $script:TargetFull -Filter $solutionFileGlob -File -ErrorAction SilentlyContinue |
                      Sort-Object Name)
        if ($existing.Count -gt 1) {
            $names = ($existing | ForEach-Object { $_.Name }) -join ', '
            Stop-Run -Message "more than one solution file matches '$solutionFileGlob' in $script:TargetFull ($names); which identifier is meant is not this script's guess to make." -RecordKind 'solution'
        }
        if ($existing.Count -eq 1) {
            $recovered = [System.IO.Path]::GetFileNameWithoutExtension($existing[0].Name)
        }
    }

    if ($null -ne $recovered) {
        # Recovered, never re-derived: a directory renamed since the first run would other-
        # wise hand this repository a second assembly prefix.
        if (-not (Test-Identifier -Candidate $recovered)) {
            Stop-Run -Message "solution file '$($existing[0].Name)' carries the identifier '$recovered', which is not usable: it must be one or more of A-Za-z0-9 and must not begin with a digit." -RecordKind 'solution'
        }
        $script:SolutionName = $recovered
        Write-Note "solution identifier: $script:SolutionName (recovered from $($existing[0].Name))"
    } else {
        $derived = ConvertTo-Identifier -Name (Split-Path -Leaf $script:TargetFull)
        if (-not (Test-Identifier -Candidate $derived)) {
            Stop-Run -Message "no solution identifier can be derived from the directory name '$(Split-Path -Leaf $script:TargetFull)'; supply one with -Solution rather than accepting a repaired guess." -RecordKind 'solution'
        }
        $script:SolutionName = $derived
        Write-Note "solution identifier: $script:SolutionName (derived from the directory name)"
    }
}

# --- the identifier reaches the manifest ------------------------------------------------------------------
# A manifest names the things it emits, and most of them carry the solution identifier: a
# directory, a generator argument, the `unless_exists` path that makes a second run a no-op.
# So the declared token is substituted through the manifest's own strings, not only through
# the contents of the assets it copies - a `tokens` field says what an asset wants substituted
# inside it, and says nothing about the manifest itself.
#
# Done as text over the document rather than by walking the parsed tree: it is total, it
# cannot miss a field somebody adds later, and an identifier is A-Za-z0-9 by the rule above,
# so it cannot make the JSON invalid.
if (-not [string]::IsNullOrEmpty($solutionToken)) {
    try {
        $document = $manifestText.Replace($solutionToken, $script:SolutionName) | ConvertFrom-Json
    } catch {
        Stop-Run -Message "manifest is not valid JSON once the solution identifier is substituted: $($_.Exception.Message)" -RecordKind 'manifest' -Path $manifestFull
    }

    $selected = Get-Field -Object (Get-Field -Object $document -Name 'kinds') -Name $Kind

    $preflights = @()
    $rawPreflights = Get-Field -Object $selected -Name 'preflight'
    if ($null -ne $rawPreflights) { $preflights = @($rawPreflights) }

    $actions = @()
    $rawActions = Get-Field -Object $selected -Name 'actions'
    if ($null -ne $rawActions) { $actions = @($rawActions) }
}

# --- what was already here ----------------------------------------------------------------------------
foreach ($file in (Get-TargetFile)) {
    $null = $script:PreExisting.Add($file)
    if (Test-UnderArtifactRoot -Full $file) {
        # The hash is what lets the sweep ask whether a chain file is different from before
        # rather than which of this script's write paths touched it. A file that cannot be
        # hashed is simply left out, so the sweep treats it as changed.
        $hash = Get-ContentHash -Path $file
        if ($null -ne $hash) { $script:ChainBefore[$file] = $hash }
    }
}

# --- preflight ------------------------------------------------------------------------------------------
# Before any other action and in its own array, so "before anything is written" is a property of
# the document's shape rather than of this script's care.
for ($index = 0; $index -lt $preflights.Count; $index++) {
    $check = $preflights[$index]
    $command = [string] (Get-Field -Object $check -Name 'command')
    if ([string]::IsNullOrWhiteSpace($command)) {
        Stop-Run -Message "preflight $($index + 1) of kind '$Kind' declares no command." -RecordKind 'manifest' -Path $manifestFull
    }

    $arguments = @()
    $rawArguments = Get-Field -Object $check -Name 'args'
    if ($null -ne $rawArguments) { $arguments = @($rawArguments) }

    $expectExit = Get-Field -Object $check -Name 'expect_exit'
    if ($null -eq $expectExit) { $expectExit = 0 }
    $expectMatch = [string] (Get-Field -Object $check -Name 'expect_match')
    $remediation = [string] (Get-Field -Object $check -Name 'remediation')

    Write-Note "preflight: $command $(ConvertTo-ArgumentString -Arguments $arguments)"
    $result = Invoke-ExternalProcess -FilePath $command -Arguments $arguments -WorkingDirectory $script:TargetFull

    $failure = $null
    if ($result.ExitCode -ne [int] $expectExit) {
        $failure = "exit code $($result.ExitCode), expected $expectExit"
    } elseif (-not [string]::IsNullOrEmpty($expectMatch) -and
              -not [regex]::IsMatch($result.StdOut, $expectMatch)) {
        $failure = "output did not match '$expectMatch'"
    }

    if ($null -ne $failure) {
        if (-not [string]::IsNullOrEmpty($remediation)) { Write-Note $remediation }
        Stop-Run -Message "preflight failed ($failure); nothing was written." -RecordKind 'preflight' -Command $command
    }
}

# --- actions ---------------------------------------------------------------------------------------------
# The loop is wrapped because an unexpected failure inside an action - a locked file, a
# path a manifest points through something that is not a directory - is still a halt, and
# R56 binds "completes OR halts". Without this it escapes as an unhandled terminating
# error: a stack trace on stderr, a non-zero exit, and no report at all, which leaves the
# caller relaying nothing to the owner where R20 and R59 require every failed entry with
# its reason.
$actionKind = 'action'
$label      = 'an action'
try {
    for ($index = 0; $index -lt $actions.Count; $index++) {
        $action = $actions[$index]
        $actionKind = [string] (Get-Field -Object $action -Name 'kind')
        $label = "action $($index + 1) ($actionKind)"

        $unless = [string] (Get-Field -Object $action -Name 'unless_exists')
        if (-not [string]::IsNullOrWhiteSpace($unless)) {
            $unlessFull = Resolve-TargetPath -Relative $unless
            if ($null -eq $unlessFull) {
                Stop-Run -Message "$label declares unless_exists '$unless', which resolves outside the target directory." -RecordKind $actionKind -Path $unless
            }
            if (Test-Path -LiteralPath $unlessFull) {
                Write-Note "$label skipped: $unless is already present"
                Add-Record -RecordKind $actionKind -Outcome 'skipped' -Path $unless -Reason "$unless is already present"
                continue
            }
        }

        switch ($actionKind) {

            'mkdir' {
                $declaredPath = [string] (Get-Field -Object $action -Name 'path')
                $full = Resolve-TargetPath -Relative $declaredPath
                if ($null -eq $full) {
                    Stop-Run -Message "$label would create '$declaredPath', which resolves outside the target directory; nothing was written." -RecordKind $actionKind -Path $declaredPath
                }
                if (Test-Path -LiteralPath $full -PathType Container) {
                    Add-Record -RecordKind $actionKind -Outcome 'skipped' -Path (Get-RelativePath -Full $full) -Reason 'directory already exists'
                    break
                }
                $null = New-Item -ItemType Directory -Path $full -Force
                Write-Note "$label created $declaredPath"
                Add-Record -RecordKind $actionKind -Outcome 'created' -Path (Get-RelativePath -Full $full)
                break
            }

            'run' {
                $command = [string] (Get-Field -Object $action -Name 'command')
                if ([string]::IsNullOrWhiteSpace($command)) {
                    Stop-Run -Message "$label declares no command." -RecordKind $actionKind
                }

                $arguments = @()
                $rawArguments = Get-Field -Object $action -Name 'args'
                if ($null -ne $rawArguments) { $arguments = @($rawArguments) }

                $workingDirectory = $script:TargetFull
                $declaredCwd = [string] (Get-Field -Object $action -Name 'cwd')
                if (-not [string]::IsNullOrWhiteSpace($declaredCwd)) {
                    $workingDirectory = Resolve-TargetPath -Relative $declaredCwd
                    if ($null -eq $workingDirectory) {
                        Stop-Run -Message "$label declares cwd '$declaredCwd', which resolves outside the target directory; nothing was run." -RecordKind $actionKind -Command $command
                    }
                    if (-not (Test-Path -LiteralPath $workingDirectory -PathType Container)) {
                        $null = New-Item -ItemType Directory -Path $workingDirectory -Force
                    }
                }

                Write-Note "$label $command $(ConvertTo-ArgumentString -Arguments $arguments)"
                $result = Invoke-ExternalProcess -FilePath $command -Arguments $arguments -WorkingDirectory $workingDirectory
                if (-not [string]::IsNullOrWhiteSpace($result.StdOut)) { Write-Note $result.StdOut.TrimEnd() }
                if (-not [string]::IsNullOrWhiteSpace($result.StdErr)) { Write-Note $result.StdErr.TrimEnd() }

                # Whatever it produced is this run's to rewrite and delete; whatever was here
                # before it is not.
                foreach ($file in (Get-TargetFile)) {
                    if (-not $script:PreExisting.Contains($file)) { Register-CreatedFile -FullPath $file }
                }

                if ($result.ExitCode -ne 0) {
                    Stop-Run -Message "$label failed with exit code $($result.ExitCode)." -RecordKind $actionKind -Command $command
                }
                Add-Record -RecordKind $actionKind -Outcome 'created' -Command $command
                break
            }

            'copy' {
                $declaredPath = [string] (Get-Field -Object $action -Name 'path')
                $full = Resolve-TargetPath -Relative $declaredPath
                if ($null -eq $full) {
                    Stop-Run -Message "$label would write '$declaredPath', which resolves outside the target directory; nothing was written." -RecordKind $actionKind -Path $declaredPath
                }

                $assetName = [string] (Get-Field -Object $action -Name 'asset')
                $assetPath = Resolve-AssetPath -PackRoot $packRoot -Asset $assetName
                if ($null -eq $assetPath -or -not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
                    Stop-Run -Message "$label names asset '$assetName', which the pack does not ship beside its manifest." -RecordKind $actionKind -Path $declaredPath
                }

                if (Test-Path -LiteralPath $full) {
                    # Never overwrite. A file that is already there was put there by somebody,
                    # and this script is not the one to decide it was wrong.
                    Add-Record -RecordKind $actionKind -Outcome 'skipped' -Path (Get-RelativePath -Full $full) -Reason 'destination already exists'
                    break
                }

                $parent = Split-Path -Parent $full
                if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
                    $null = New-Item -ItemType Directory -Path $parent -Force
                }

                $tokens = Get-Field -Object $action -Name 'tokens'
                if ($null -eq $tokens) {
                    # Byte-for-byte when there is nothing to substitute, so an asset that is not
                    # text survives the trip.
                    Copy-Item -LiteralPath $assetPath -Destination $full -Force
                } else {
                    $text = Read-TextFile -Path $assetPath
                    $text = Expand-Token -Text $text -Tokens $tokens -SolutionToken $solutionToken -Label $label
                    [System.IO.File]::WriteAllText($full, $text)
                }

                Write-Note "$label created $declaredPath"
                Register-CreatedFile -FullPath $full
                Add-Record -RecordKind $actionKind -Outcome 'created' -Path (Get-RelativePath -Full $full)
                break
            }

            'append' {
                $declaredPath = [string] (Get-Field -Object $action -Name 'path')
                $full = Resolve-TargetPath -Relative $declaredPath
                if ($null -eq $full) {
                    Stop-Run -Message "$label would write '$declaredPath', which resolves outside the target directory; nothing was written." -RecordKind $actionKind -Path $declaredPath
                }

                $assetName = [string] (Get-Field -Object $action -Name 'asset')
                $assetPath = Resolve-AssetPath -PackRoot $packRoot -Asset $assetName
                if ($null -eq $assetPath -or -not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
                    Stop-Run -Message "$label names asset '$assetName', which the pack does not ship beside its manifest." -RecordKind $actionKind -Path $declaredPath
                }

                $assetText = Read-TextFile -Path $assetPath
                $assetText = Expand-Token -Text $assetText -Tokens (Get-Field -Object $action -Name 'tokens') `
                                          -SolutionToken $solutionToken -Label $label

                if (-not (Test-Path -LiteralPath $full)) {
                    $parent = Split-Path -Parent $full
                    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
                        $null = New-Item -ItemType Directory -Path $parent -Force
                    }
                    [System.IO.File]::WriteAllText($full, $assetText)
                    Write-Note "$label created $declaredPath"
                    Register-CreatedFile -FullPath $full
                    Add-Record -RecordKind $actionKind -Outcome 'created' -Path (Get-RelativePath -Full $full)
                    break
                }

                # The file exists - a generator got here first, which is the case `copy` cannot
                # serve: it would skip, exit 0, and leave the declared lines silently absent.
                $existingText = Read-TextFile -Path $full
                $present = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::Ordinal)
                foreach ($line in ($existingText -split "`r?`n")) { $null = $present.Add($line.Trim()) }

                $missing = New-Object System.Collections.Generic.List[string]
                foreach ($line in ($assetText -split "`r?`n")) {
                    $trimmed = $line.Trim()
                    if ($trimmed.Length -eq 0) { continue }          # a blank line is never missing
                    if ($present.Contains($trimmed)) { continue }
                    if ($missing -contains $line) { continue }
                    $missing.Add($line)
                }

                if ($missing.Count -eq 0) {
                    Add-Record -RecordKind $actionKind -Outcome 'skipped' -Path (Get-RelativePath -Full $full) -Reason 'every line is already present'
                    break
                }

                $addition = ($missing -join "`r`n") + "`r`n"
                if ($existingText.Length -gt 0 -and -not $existingText.EndsWith("`n")) {
                    $addition = "`r`n" + $addition
                }
                [System.IO.File]::AppendAllText($full, $addition)
                Write-Note "$label appended $($missing.Count) line(s) to $declaredPath"
                Add-Record -RecordKind $actionKind -Outcome 'mutated' -Path (Get-RelativePath -Full $full) -Reason "appended $($missing.Count) line(s)"
                break
            }

            'rewrite' {
                $glob = [string] (Get-Field -Object $action -Name 'glob')
                if (-not (Test-ContainedGlob -Glob $glob)) {
                    Stop-Run -Message "$label declares glob '$glob', which resolves outside the target directory; nothing was changed." -RecordKind $actionKind -Path $glob
                }

                $pattern = [string] (Get-Field -Object $action -Name 'pattern')
                if ([string]::IsNullOrEmpty($pattern)) {
                    Stop-Run -Message "$label declares no pattern." -RecordKind $actionKind -Path $glob
                }
                $replacement = [string] (Get-Field -Object $action -Name 'replacement')
                $assertAfter = [string] (Get-Field -Object $action -Name 'assert_after')

                # @() because a function's return value unrolls: an empty result arrives as $null
                # and a single hit as a bare string, and .Count on either is a different question
                # than it looks - the second one silently, which is worse.
                $matched = @(Get-GlobMatch -Glob $glob)
                $mutated = 0
                foreach ($relative in $matched) {
                    $full = Resolve-TargetPath -Relative $relative
                    if ($null -eq $full -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }

                    $before = Read-TextFile -Path $full
                    $after = [regex]::Replace($before, $pattern, $replacement)
                    if ($after -cne $before) {
                        [System.IO.File]::WriteAllText($full, $after)
                        Write-Note "$label rewrote $relative"
                        Add-Record -RecordKind $actionKind -Outcome 'mutated' -Path $relative
                        $mutated++
                    }
                }

                if ($mutated -eq 0) {
                    # R56 records every action, and an action that did nothing is the one the
                    # report most needs to carry: a pack's glob that quietly stops matching -
                    # because an SDK renamed the file it names - leaves an exit 0 and a silent
                    # report, and the thing the rewrite existed to change ships unchanged.
                    # Not a failure, so `skipped` with a reason rather than a sixth outcome.
                    $reason = "no file this run created matched '$glob'"
                    if ($matched.Count -gt 0) {
                        $reason = "$($matched.Count) file(s) matched '$glob'; none needed the rewrite"
                    }
                    Add-Record -RecordKind $actionKind -Outcome 'skipped' -Path $glob -Reason $reason
                }

                if (-not [string]::IsNullOrEmpty($assertAfter)) {
                    # Asserting the end state rather than that the regex fired: a rewrite that
                    # had nothing to do is fine, and a template that changed shape is not.
                    foreach ($relative in $matched) {
                        $full = Resolve-TargetPath -Relative $relative
                        if ($null -eq $full -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
                        if (-not [regex]::IsMatch((Read-TextFile -Path $full), $assertAfter)) {
                            Stop-Run -Message "$label left $relative not matching assert_after '$assertAfter'." -RecordKind $actionKind -Path $relative
                        }
                    }
                }
                break
            }

            'delete' {
                $glob = [string] (Get-Field -Object $action -Name 'glob')
                if (-not (Test-ContainedGlob -Glob $glob)) {
                    Stop-Run -Message "$label declares glob '$glob', which resolves outside the target directory; nothing was removed." -RecordKind $actionKind -Path $glob
                }

                $matched = @(Get-GlobMatch -Glob $glob)
                $removed = 0
                foreach ($relative in $matched) {
                    $full = Resolve-TargetPath -Relative $relative
                    if ($null -eq $full -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
                    Remove-Item -LiteralPath $full -Force
                    $null = $script:Created.Remove($relative)
                    Write-Note "$label removed $relative"
                    Add-Record -RecordKind $actionKind -Outcome 'removed' -Path $relative
                    $removed++
                }

                if ($removed -eq 0) {
                    # Same reason as the rewrite above, and the sharper half of it: a `delete`
                    # that strips generator sample content and silently matches nothing leaves
                    # that sample content in every repository scaffolded from then on, with an
                    # exit 0 and an empty report behind it.
                    $reason = "no file this run created matched '$glob'"
                    if ($matched.Count -gt 0) {
                        $reason = "$($matched.Count) file(s) matched '$glob'; none was still on disk to remove"
                    }
                    Add-Record -RecordKind $actionKind -Outcome 'skipped' -Path $glob -Reason $reason
                }
                break
            }
        }
    }
} catch {
    # Not swallowed: the exception's own message is the reason, so the owner reads what
    # actually went wrong rather than that something did. Stop-Run records it against the
    # action that was in flight, emits the report, and exits non-zero - the same three
    # things a declared failure produces.
    $detail = ''
    if ($null -ne $_.Exception) { $detail = [string] $_.Exception.Message }
    if ([string]::IsNullOrWhiteSpace($detail)) { $detail = [string] $_ }
    Stop-Run -Message "$label failed unexpectedly: $detail" -RecordKind $actionKind
}

# --- the token sweep ----------------------------------------------------------------------------------------
# Every file under the target, not only the ones this run wrote. A token left behind by an
# earlier run is the complaint this whole feature started from, and a gap-filling run that
# writes nothing is exactly when nobody would look.
#
# Under the artifact root, only what changed since this execution began (R72). See
# Test-Sweepable for why that question is asked of content rather than of write mechanism.
if (-not [string]::IsNullOrEmpty($solutionToken)) {
    $offenders = New-Object System.Collections.Generic.List[string]
    foreach ($file in (Get-TargetFile)) {
        if (-not (Test-Sweepable -Full $file)) { continue }
        $relative = Get-RelativePath -Full $file
        if ($relative.IndexOf($solutionToken, [StringComparison]::Ordinal) -ge 0) {
            $offenders.Add("$relative (in its name)")
            continue
        }
        try { $text = Read-TextFile -Path $file } catch { continue }
        if ($text.IndexOf($solutionToken, [StringComparison]::Ordinal) -ge 0) {
            $offenders.Add($relative)
        }
    }

    if ($offenders.Count -gt 0) {
        foreach ($offender in $offenders) {
            Add-Record -RecordKind 'token' -Outcome 'failed' -Path $offender -Reason "token $solutionToken was not substituted"
        }
        Stop-Run -Message "token $solutionToken survives in: $($offenders -join ', ')"
    }
}

Write-Note "scaffold completed for kind '$Kind' in $script:TargetFull"
Complete-Run -Outcome 'completed' -Code 0
