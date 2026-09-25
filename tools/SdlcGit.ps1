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
        Remove-Item -LiteralPath $errFile -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-SdlcGit {
    <#
    .SYNOPSIS
        Runs git. Returns an object; never throws on a non-zero exit code.
    .EXAMPLE
        $r = Invoke-SdlcGit checkout -b $branch
        if (-not $r.ok) { ... $r.stderr ... }
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    return Invoke-SdlcNative -Command 'git' -Arguments @($Arguments)
}
