#Requires -Version 5.1
<#
.SYNOPSIS
    Runs git without Windows PowerShell turning a successful call into a failure.
    Dot-source this; it defines functions and does nothing on its own.

.DESCRIPTION
    THE BUG THIS EXISTS TO PREVENT, because it is subtle and it bites on success rather
    than on failure.

    In Windows PowerShell, redirecting a native command's stderr with `2>&1` wraps each
    stderr line in an ErrorRecord (NativeCommandError) and sets `$?` to `$false` — even when
    the executable returned exit code 0. Under `$ErrorActionPreference = 'Stop'`, which every
    script here sets, that becomes a TERMINATING error.

    And git writes ordinary progress to stderr. `git checkout -b x` says "Switched to a new
    branch 'x'" on stderr when it succeeds. So:

        git checkout -b $branch 2>&1 | Out-Null     # dies on success

    Fourteen calls across three scripts were written that way, eight of them in the landing
    script, which had never been run. The first landing would have failed at its first git
    command with an error describing a successful operation.

    THE FIX is to redirect stderr to a FILE rather than into the success stream. `2>$path`
    on a native command writes the bytes and creates no ErrorRecord, so exit code stays the
    only signal of failure — which is what it should have been all along. Callers get both
    streams as text and branch on `.ok`.

    Kept in one file and dot-sourced rather than copied into each script: three copies of a
    rule is how a rule starts drifting, and PowerShellScriptTests binds every shipped script
    to it by forbidding the `2>&1` form outright.
#>

Set-StrictMode -Version Latest

function Invoke-SdlcNative {
    <#
    .SYNOPSIS
        Runs a native command and reports its exit code, stdout and stderr as data.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Command,

        [Parameter()]
        [string[]] $Arguments = @()
    )

    $errFile = [System.IO.Path]::GetTempFileName()
    $previous = $ErrorActionPreference

    # -WhatIf MUST NOT reach in here. A caller running with -WhatIf is previewing ITS OWN
    # actions; reading git and capturing its output is how the caller decides what it would
    # do, so suppressing that leaves the preview blind. Observed: under -WhatIf the stderr
    # redirect below was itself previewed, so nothing was captured and temp files were left
    # behind on every call.
    $WhatIfPreference = $false

    # Belt and braces. The file redirect below is what actually prevents the ErrorRecord,
    # but a native command can still raise one on a path this function does not control,
    # and a helper whose whole job is not to throw should not throw.
    $ErrorActionPreference = 'Continue'

    try {
        $stdout = & $Command @Arguments 2>$errFile
        $code = $LASTEXITCODE

        $stderr = ''
        if (Test-Path -LiteralPath $errFile) {
            $stderr = [string] (Get-Content -LiteralPath $errFile -Raw -Encoding UTF8)
        }

        # CAST BEFORE CALLING A METHOD. A command that writes nothing to stdout leaves
        # $stdout null, `$null | Out-String` yields null, and .TrimEnd() on null throws
        # under Set-StrictMode. `git checkout -b` is exactly that case: everything it says
        # goes to stderr, so stdout is always empty and the helper died on every success.
        # Same family as the single-element unwrap: strict mode turns a silent null into a
        # failure, and the fix is to be explicit rather than to relax the mode.
        $outText = [string] ($stdout | Out-String)
        return [pscustomobject]@{
            ok     = ($code -eq 0)
            code   = $code
            stdout = $outText.TrimEnd()
            stderr = ([string] $stderr).TrimEnd()
        }
    }
    finally {
        $ErrorActionPreference = $previous
        Remove-Item -LiteralPath $errFile -Force -ErrorAction SilentlyContinue -WhatIf:$false
    }
}

function Invoke-SdlcGit {
    <#
    .SYNOPSIS
        Runs git. Returns an object; never throws on a non-zero exit code.
    .DESCRIPTION
        NO PARAMETER BLOCK, AND THAT IS THE WHOLE POINT. It had `[CmdletBinding()]` with a
        ValueFromRemainingArguments parameter, which makes this an ADVANCED function -- and an
        advanced function gets PowerShell's common parameters, which then compete with git's own
        flags for every token passed.

        `Invoke-SdlcGit branch -D $branch` was the one that surfaced it: `-D` is an unambiguous
        abbreviation of `-Debug`, so binding consumed it and git received `branch <name>`, which
        CREATES a branch. The observed failure was "fatal: a branch named 'cycle-...' already
        exists" from a call whose only job was to delete it -- a delete that silently became a
        create.

        Probed rather than reasoned about, because the rule is not obvious: `-b`, `-u`, `-1`,
        `--force` and `--cached` all arrive intact, since none of them abbreviates a parameter name.
        Only flags that prefix a common parameter are stolen, which means the hazard is invisible
        until one of them happens to be the flag you need. git uses `-v` (Verbose), `-o`
        (OutVariable), `-e` (ErrorAction) and `-D` among others.

        A simple function has no parameters to bind against, so every token lands in `$args`
        verbatim. That removes the class rather than the instance -- the alternative was quoting
        every flag at every call site and remembering to keep doing it.
    .EXAMPLE
        $r = Invoke-SdlcGit checkout -b $branch
        if (-not $r.ok) { ... $r.stderr ... }
    #>

    return Invoke-SdlcNative -Command 'git' -Arguments @($args)
}
