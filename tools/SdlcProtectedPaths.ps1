#Requires -Version 5.1
<#
.SYNOPSIS
    The one licence answer for INV-08: which paths are protected, what licenses a write to each,
    and whether a shell command string writes one.

.DESCRIPTION
    DOT-SOURCED BY BOTH MECHANISMS, because two copies of this answer is the failure it exists to
    prevent. `Assert-SdlcCharterGate.ps1` prevents a write; `Get-SdlcFacts.ps1` detects one that
    already happened. A prevention and a detection that disagree about what is protected is worse
    than either alone: the gate looks stricter than it is and the check reports paths nobody
    guards.

    FAILS CLOSED, EVERYWHERE. Every function returns "no licence" rather than "no opinion" when it
    cannot positively establish one. The gate reads a thrown error above its own try/catch as
    silence, so this file must not throw for an ordinary condition - a missing intent, an
    unreadable file, a branch that parses into nothing are all answers, not exceptions.

    THE LICENCE IS PATH-BOUND. INV-08 names three things and two intent sources, and the sources
    are not interchangeable: a pack-review intent has no business rewriting a solution's charter,
    and a charter-review intent has none rewriting the rules every future solution inherits.
    Before this file the gate held one flat list of permitted sources, so either source licensed
    every protected path.

    CREATION IS NOT MODIFICATION, AND IT IS A QUESTION ABOUT THE FILESYSTEM. A write to a
    protected path that does not exist yet needs no licence - you cannot loosen a rule that has
    not been written. Existence is tested on disk rather than against a git baseline because
    `init` writes a first charter into a repository with no commit, which is exactly where a
    baseline cannot be consulted.
#>

Set-StrictMode -Version Latest

# The four protected paths, each with the intent source that licenses a write to it.
#
# The pack charter TEMPLATE is a judgement worth recording: INV-08's wording is "this document,
# the stack pack, or the pack rules", and a template governs nothing until a solution copies it,
# so a narrow reading would leave it out. It is in scope because it is the most consequential file
# in a pack - every solution created from that pack inherits it, and an ungated edit changes the
# rules every future solution is judged by.
$script:SdlcProtectedPathRules = @(
    [pscustomobject]@{
        pattern = '^\.sdlc/CHARTER\.md$'
        source  = 'charter-review'
        what    = 'the solution charter'
    }
    [pscustomobject]@{
        pattern = '^packs/[^/]+/pack\.yaml$'
        source  = 'pack-review'
        what    = 'a pack manifest'
    }
    [pscustomobject]@{
        pattern = '^packs/[^/]+/pack\.rules\.yaml$'
        source  = 'pack-review'
        what    = 'a pack rule set'
    }
    [pscustomobject]@{
        pattern = '^packs/[^/]+/CHARTER\.md$'
        source  = 'pack-review'
        what    = 'a pack charter template'
    }
)

function Get-SdlcProtectedPathRules {
    <#
    .SYNOPSIS
        Every protected-path rule. One source of truth for both mechanisms and for their tests.
    #>
    return @($script:SdlcProtectedPathRules)
}

function Get-SdlcProtectedPathRule {
    <#
    .SYNOPSIS
        The rule governing a repository-relative POSIX path, or $null when it is not protected.
    #>
    param([string] $RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $null }

    foreach ($rule in $script:SdlcProtectedPathRules) {
        if ($RelativePath -match $rule.pattern) { return $rule }
    }

    return $null
}

function Get-SdlcRelativePosixPath {
    <#
    .SYNOPSIS
        A path as repository-relative POSIX, or $null when it lies outside the repository.

    .DESCRIPTION
        GetFullPath rather than Resolve-Path: the target of a write may not exist yet, and
        Resolve-Path fails on a path that is not there - which would make every new file
        unresolvable and, in the gate, deny it.
    #>
    param([string] $Path, [string] $Root)

    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    if ([string]::IsNullOrWhiteSpace($Root)) { return $null }

    $candidate = $Path
    if (-not [System.IO.Path]::IsPathRooted($candidate)) {
        $candidate = Join-Path $Root $candidate
    }

    try { $full = [System.IO.Path]::GetFullPath($candidate) } catch { return $null }
    try { $rootFull = [System.IO.Path]::GetFullPath($Root) } catch { return $null }

    $rootFull = $rootFull.TrimEnd('\', '/')
    if (-not $full.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }

    $relative = $full.Substring($rootFull.Length).TrimStart('\', '/')
    return ($relative -replace '\\', '/')
}

function Get-SdlcProtectedPathsInText {
    <#
    .SYNOPSIS
        Every protected path named literally in a string, as repository-relative POSIX.

    .DESCRIPTION
        LITERAL ONLY, AND THAT LIMIT IS THE ACCEPTED LEAK. A path assembled at runtime - from a
        variable, a substitution, a decode - is not found here. What that costs is bounded by the
        shape classifier: a command naming no protected path is permitted, but a command that
        names one and then does something this file cannot read statically is denied, so the
        evasion has to hide the path AND be a write.

        Tokenised rather than regex-scanned across the whole string, because a protected pattern
        is anchored at both ends and an unanchored version of it would match
        `notes/.sdlc/CHARTER.md.bak`.
    #>
    param([string] $Text, [string] $Root)

    $found = New-Object System.Collections.ArrayList
    if ([string]::IsNullOrWhiteSpace($Text)) { return @() }

    # Shell punctuation, not path punctuation. Backslash is absent deliberately: it separates
    # directories on Windows and splitting on it would destroy every absolute path.
    $tokens = $Text -split '[\s''"`;|&<>,()\[\]{}=]+'

    foreach ($token in $tokens) {
        if ([string]::IsNullOrWhiteSpace($token)) { continue }

        # Trailing shell noise a split leaves behind, and a leading ./ which is the same path.
        $candidate = $token.Trim().TrimEnd(':', '.', '!', '?')
        $candidate = $candidate -replace '^\./', ''
        if (-not $candidate) { continue }

        $relative = Get-SdlcRelativePosixPath -Path $candidate -Root $Root
        if (-not $relative) { continue }

        if (Get-SdlcProtectedPathRule -RelativePath $relative) {
            if ($found -notcontains $relative) { $null = $found.Add($relative) }
        }
    }

    return @($found)
}

function Test-SdlcProtectedPathExists {
    <#
    .SYNOPSIS
        Does this protected path exist on disk right now.

    .DESCRIPTION
        On disk, not in git. See the header: `init` writes a first charter where there is no
        commit to compare against, and a creation test that needs a baseline denies it.
    #>
    param([string] $Root, [string] $RelativePath)

    if ([string]::IsNullOrWhiteSpace($Root)) { return $false }
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }

    $full = Join-Path $Root ($RelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    return [bool] (Test-Path -LiteralPath $full -PathType Leaf)
}

function Get-SdlcBranchLicence {
    <#
    .SYNOPSIS
        The intent source this branch carries, or a reason there is none.

    .OUTPUTS
        ok      - $true only when an intent file was found and declared a source
        source  - the declared source, or $null
        intent  - the cycle id parsed from the branch, or $null
        reason  - why there is no licence, phrased for a refusal
    #>
    param([string] $Root, [string] $Branch)

    $answer = [pscustomobject]@{ ok = $false; source = $null; intent = $null; reason = $null }

    if ([string]::IsNullOrWhiteSpace($Branch)) {
        $answer.reason = 'the current branch could not be determined'
        return $answer
    }

    # A detached HEAD reports the literal string HEAD.
    if ($Branch -eq 'HEAD') {
        $answer.reason = 'HEAD is detached, so no intent governs this write'
        return $answer
    }

    if ($Branch -notmatch '^(?:intent|cycle)-(.+)$') {
        $answer.reason = "'$Branch' is not an intent or cycle branch, so it carries no intent"
        return $answer
    }

    $intentId = $Matches[1]
    $answer.intent = $intentId

    $intentRel = ".sdlc/intents/$intentId.md"
    $intentPath = Join-Path $Root ($intentRel -replace '/', [System.IO.Path]::DirectorySeparatorChar)

    if (-not (Test-Path -LiteralPath $intentPath -PathType Leaf)) {
        $answer.reason = "no intent file was found at $intentRel for branch '$Branch'"
        return $answer
    }

    $lines = $null
    try { $lines = @(Get-Content -LiteralPath $intentPath -Encoding UTF8 -ErrorAction Stop) }
    catch {
        $answer.reason = "the intent at $intentRel could not be read"
        return $answer
    }

    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') {
        $answer.reason = "the intent at $intentRel carries no frontmatter"
        return $answer
    }

    $source = $null
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match '^source:\s*(\S+)') { $source = $Matches[1].Trim(); break }
    }

    if (-not $source) {
        $answer.reason = "the intent at $intentRel declares no source"
        return $answer
    }

    $answer.ok = $true
    $answer.source = $source
    return $answer
}

function Test-SdlcWriteLicensed {
    <#
    .SYNOPSIS
        May this branch write this protected path.

    .DESCRIPTION
        THE EXISTENCE TEST RUNS FIRST. Creating a protected path needs no licence, and asking for
        one before asking whether the file is even there would deny `init` in a fresh repository.

    .OUTPUTS
        allowed - $true when the write is permitted
        why     - 'not-protected', 'creation', 'licensed', or 'refused'
        reason  - a sentence naming the path and what would permit the write, when refused
    #>
    param([string] $Root, [string] $Branch, [string] $RelativePath)

    $rule = Get-SdlcProtectedPathRule -RelativePath $RelativePath
    if (-not $rule) {
        return [pscustomobject]@{ allowed = $true; why = 'not-protected'; reason = $null }
    }

    if (-not (Test-SdlcProtectedPathExists -Root $Root -RelativePath $RelativePath)) {
        return [pscustomobject]@{
            allowed = $true
            why     = 'creation'
            reason  = "$RelativePath does not exist yet, so this creates it rather than amending it"
        }
    }

    $licence = Get-SdlcBranchLicence -Root $Root -Branch $Branch
    if (-not $licence.ok) {
        return [pscustomobject]@{
            allowed = $false
            why     = 'refused'
            reason  = ("$RelativePath is $($rule.what) and is protected by INV-08: " +
                       "$($licence.reason). It may be amended only on the branch of an approved " +
                       "intent whose source is '$($rule.source)'.")
        }
    }

    if ($licence.source -ne $rule.source) {
        return [pscustomobject]@{
            allowed = $false
            why     = 'refused'
            reason  = ("$RelativePath is $($rule.what) and is protected by INV-08: it may be " +
                       "amended only by an intent whose source is '$($rule.source)'. Intent " +
                       "$($licence.intent) declares source '$($licence.source)'.")
        }
    }

    return [pscustomobject]@{ allowed = $true; why = 'licensed'; reason = $null }
}

function Get-SdlcCommandShape {
    <#
    .SYNOPSIS
        Does this command string write, destroy, or merely read one particular protected path -
        or can it not be read at all.

    .DESCRIPTION
        THREE KINDS OF ANSWER, AND THE THIRD IS THE POINT. "Evaluated it and found no write" is a
        determination, and permitting it is the accepted leak. "Could not evaluate it" is no
        determination at all, so it denies - the same reason a thrown gate denies.

        DESTRUCTION IS A WRITE SHAPE, NOT THE ABSENCE OF ONE. Removing, renaming, moving or
        truncating a protected path is the largest change available to it. A classifier that
        recognised only writes producing content would permit `rm .sdlc/CHARTER.md` on any branch.

        BOUND TO THE PATH, NOT MERELY PRESENT IN THE COMMAND. The first version of this function
        asked whether a write construct appeared anywhere in the string, which made
        `grep x .sdlc/CHARTER.md > notes.txt` a write to the charter - a read, denied. So the
        command is split into segments and only the segments naming this path are classified, and
        a redirection counts only when the path is its target.

        THE DYNAMIC-CONSTRUCT TEST IS WHOLE-COMMAND, AND CONDITIONAL. Scoping it per segment would
        permit `X=.sdlc/CHARTER.md; echo hi > $X`, where the path and the write live in different
        segments. Applying it unconditionally would deny a command that reads the path and then
        mentions a variable for an unrelated reason,
        which is a read. So it fires only when the command contains a dynamic construct AND some
        write-shaped construct somewhere: a command with no way to write is not made dangerous by
        a variable.

    .OUTPUTS
        verdict - 'write', 'destroy', 'read', 'none' or 'unevaluable'
        detail  - the construct that decided it, for the refusal to quote
    #>
    param([string] $Command, [string] $RelativePath, [string] $Root)

    if ([string]::IsNullOrWhiteSpace($Command)) {
        return [pscustomobject]@{ verdict = 'unevaluable'; detail = 'the command was empty' }
    }

    # --- unparseable outright -------------------------------------------------
    #
    # Unbalanced quoting means any tokenisation of this string is guesswork, so every conclusion
    # drawn from it is guesswork too.
    $doubles = ([regex]::Matches($Command, '(?<!\\)"')).Count
    $singles = ([regex]::Matches($Command, "(?<!\\)'")).Count
    if (($doubles % 2) -ne 0 -or ($singles % 2) -ne 0) {
        return [pscustomobject]@{ verdict = 'unevaluable'; detail = 'its quoting does not balance' }
    }

    $destroyers = @(
        @{ p = '(?<![-\w])rm(?![-\w])'; d = 'rm' }
        @{ p = '(?<![-\w])unlink(?![-\w])'; d = 'unlink' }
        @{ p = '(?<![-\w])Remove-Item(?![-\w])'; d = 'Remove-Item' }
        @{ p = '(?<![-\w])del(?![-\w])'; d = 'del' }
        @{ p = '(?<![-\w])erase(?![-\w])'; d = 'erase' }
        @{ p = '(?<![-\w])mv(?![-\w])'; d = 'mv' }
        @{ p = '(?<![-\w])move(?![-\w])'; d = 'move' }
        @{ p = '(?<![-\w])Move-Item(?![-\w])'; d = 'Move-Item' }
        @{ p = '(?<![-\w])Rename-Item(?![-\w])'; d = 'Rename-Item' }
        @{ p = '(?<![-\w])truncate(?![-\w])'; d = 'truncate' }
        @{ p = '(?<![-\w])Clear-Content(?![-\w])'; d = 'Clear-Content' }
        @{ p = '(?<![-\w])shred(?![-\w])'; d = 'shred' }
    )

    $writers = @(
        @{ p = '(?<![-\w])tee(?![-\w])'; d = 'tee' }
        @{ p = '(?<![-\w])sed(?![-\w])[^|;]*\s-i'; d = 'sed in place' }
        @{ p = '(?<![-\w])perl(?![-\w])[^|;]*\s-i'; d = 'perl in place' }
        @{ p = '(?<![-\w])cp(?![-\w])'; d = 'cp' }
        @{ p = '(?<![-\w])copy(?![-\w])'; d = 'copy' }
        @{ p = '(?<![-\w])Copy-Item(?![-\w])'; d = 'Copy-Item' }
        @{ p = '(?<![-\w])Set-Content(?![-\w])'; d = 'Set-Content' }
        @{ p = '(?<![-\w])Add-Content(?![-\w])'; d = 'Add-Content' }
        @{ p = '(?<![-\w])Out-File(?![-\w])'; d = 'Out-File' }
        @{ p = '(?<![-\w])New-Item(?![-\w])'; d = 'New-Item' }
        @{ p = '(?<![-\w])dd(?![-\w])[^|;]*of='; d = 'dd' }
        @{ p = '(?<![-\w])patch(?![-\w])'; d = 'patch' }
        @{ p = 'git\s+apply(?![-\w])'; d = 'git apply' }
        @{ p = 'git\s+checkout(?![-\w])'; d = 'git checkout' }
        @{ p = 'git\s+restore(?![-\w])'; d = 'git restore' }
        @{ p = '(?<![-\w])touch(?![-\w])'; d = 'touch' }
    )

    $readers = @(
        'cat', 'head', 'tail', 'less', 'more', 'grep', 'rg', 'wc', 'diff', 'sed', 'awk', 'sort',
        'Get-Content', 'Select-String', 'Get-Item', 'Get-ChildItem', 'Test-Path', 'file', 'stat'
    )

    # --- does anything here write at all -------------------------------------
    #
    # Used only by the dynamic-construct test below, so it is deliberately generous: a `>`
    # anywhere counts, because the question is whether this command has any means of writing.
    $hasAnyWriteMeans = $false
    if ($Command -match '(?<![0-9&])>') { $hasAnyWriteMeans = $true }
    if (-not $hasAnyWriteMeans) {
        foreach ($c in ($destroyers + $writers)) {
            if ($Command -match $c.p) { $hasAnyWriteMeans = $true; break }
        }
    }

    $dynamic = @(
        @{ p = '\$\('; d = 'a command substitution' }
        @{ p = '`'; d = 'a backtick substitution' }
        @{ p = '(?<![-\w])eval(?![-\w])'; d = 'eval' }
        @{ p = '(?<![-\w])Invoke-Expression(?![-\w])'; d = 'Invoke-Expression' }
        @{ p = '(?<![-\w])iex(?![-\w])'; d = 'iex' }
        @{ p = '(?<![-\w])xargs(?![-\w])'; d = 'xargs' }
        @{ p = '(?<![-\w])base64(?![-\w])'; d = 'a base64 decode' }
        @{ p = '\|\s*(?:ba)?sh(?![-\w])'; d = 'a pipe into a shell' }
        @{ p = '\|\s*pwsh(?![-\w])'; d = 'a pipe into a shell' }
        @{ p = '\|\s*powershell(?![-\w])'; d = 'a pipe into a shell' }
        @{ p = '--%'; d = 'the stop-parsing token' }
        @{ p = '\$\{'; d = 'a variable expansion' }
        @{ p = '\$(?:env:)?[A-Za-z_]\w*'; d = 'a variable reference' }
    )

    if ($hasAnyWriteMeans) {
        foreach ($construct in $dynamic) {
            if ($Command -match $construct.p) {
                return [pscustomobject]@{
                    verdict = 'unevaluable'
                    detail  = ("it contains $($construct.d) alongside a write, so what it writes " +
                               'cannot be determined by reading it')
                }
            }
        }
    }

    # --- classify only the segments that name this path ----------------------
    # `&{2}` and `\|{2}` rather than the literal operators: a shipped-script check forbids a doubled
    # ampersand anywhere in a 5.1 script, because a pipeline chain operator there is a parse error.
    # The check reads comments too and is deliberately blunt, and this is only a regex, so the regex
    # is spelled to suit it rather than the check weakened to admit it.
    $segments = @($Command -split '(?:&{2}|\|{2}|;|\||\r?\n)')

    $sawRead = $false
    $sawSegment = $false

    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }

        $named = @(Get-SdlcProtectedPathsInText -Text $segment -Root $Root)
        $isRedirectTarget = $false

        # A redirection counts only when this path is its target. `2>&1` yields no token, which is
        # why `&` is excluded from the target character class.
        foreach ($m in [regex]::Matches($segment, '(?<![0-9&])>>?\s*(?:"([^"]+)"|''([^'']+)''|([^\s"''|;&<>]+))')) {
            $token = $m.Groups[1].Value
            if (-not $token) { $token = $m.Groups[2].Value }
            if (-not $token) { $token = $m.Groups[3].Value }
            if (-not $token) { continue }

            $resolved = Get-SdlcRelativePosixPath -Path ($token -replace '^\./', '') -Root $Root
            if ($resolved -and $resolved -eq $RelativePath) { $isRedirectTarget = $true; break }
        }

        if ($isRedirectTarget) {
            return [pscustomobject]@{ verdict = 'write'; detail = 'a redirection into it' }
        }

        if ($named -notcontains $RelativePath) { continue }
        $sawSegment = $true

        foreach ($construct in $destroyers) {
            if ($segment -match $construct.p) {
                return [pscustomobject]@{ verdict = 'destroy'; detail = $construct.d }
            }
        }

        foreach ($construct in $writers) {
            if ($segment -match $construct.p) {
                return [pscustomobject]@{ verdict = 'write'; detail = $construct.d }
            }
        }

        foreach ($reader in $readers) {
            if ($segment -match ('(?<![-\w])' + [regex]::Escape($reader) + '(?![-\w])')) {
                $sawRead = $true
                break
            }
        }
    }

    if ($sawRead) {
        return [pscustomobject]@{ verdict = 'read'; detail = 'it only reads the path' }
    }

    if (-not $sawSegment) {
        return [pscustomobject]@{ verdict = 'none'; detail = 'no segment of it names the path' }
    }

    # Parsed, the path is named, and no write shape was found. A determination, so it is
    # permitted - and it is the leak the intent records rather than a gap nobody noticed.
    return [pscustomobject]@{ verdict = 'none'; detail = 'no write shape was recognised' }
}
